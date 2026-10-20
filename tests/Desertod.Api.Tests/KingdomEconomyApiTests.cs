using System.Net;
using System.Net.Http.Json;
using Desertod.Domain;
using Desertod.Api.Game;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Economy;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class KingdomEconomyApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db() => factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private static async Task<EconomyView> View(HttpClient client,string path)
    {
        var response=await client.GetAsync(path+"/economy");
        await Players.OkJsonAsync(response);
        return (await response.Content.ReadFromJsonAsync<EconomyView>(JsonOptions))!;
    }
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions=new(System.Text.Json.JsonSerializerDefaults.Web)
    { Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()} };

    private async Task SeedWallet(string slug,Guid citizenId,long coins)
    {
        await using var db=await Db();var c=await db.Citizens.SingleAsync(x=>x.Id==citizenId);
        var wallet=await db.GameWallets.SingleAsync(x=>x.VillageId==c.VillageId&&x.AccountId==c.AccountId);
        wallet.Coins=coins;await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Currency_identity_projection_preserves_legacy_balances_and_material_gold()
    {
        var v = await players.FoundVillageAsync("Separate currency identities");
        var a = await players.JoinAsync(v, "Account holder");
        await View(a.Client, v.Path);
        await players.ArrangeVillageAsync(v.Slug, (city, now) =>
        {
            var storage = StorageBuilding.Create(StorageKind.TownHall, "3,3", now);
            storage.Put(ResourceKind.Gold, 7);
            city.Storages.Add(storage);
        });
        await using (var db = await Db())
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == a.Id);
            var wallet = await db.GameWallets.SingleAsync(w => w.AccountId == citizen.AccountId && w.VillageId == citizen.VillageId);
            wallet.Coins = 1234;
            wallet.TestCandles = 9;
            await db.SaveChangesAsync();
        }
        for (var repeat = 0; repeat < 2; repeat++)
        {
            using var response = await a.Client.GetAsync(v.Path + "/economy");
            await Players.OkJsonAsync(response);
            using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var view = json.RootElement;
            Assert.Equal(1234, view.GetProperty("coins").GetInt64());
            Assert.Equal(9, view.GetProperty("testCandles").GetInt64());
            var currencies = view.GetProperty("currencies");
            Assert.Equal("economic-gold", currencies.GetProperty("coins").GetProperty("id").GetString());
            Assert.Equal("DZK", currencies.GetProperty("coins").GetProperty("ledgerAsset").GetString());
            Assert.Equal("test-candle", currencies.GetProperty("testCandles").GetProperty("id").GetString());
            Assert.Equal("TEST_CANDLE", currencies.GetProperty("testCandles").GetProperty("ledgerAsset").GetString());
        }
        await using var check = await Db();
        var city = await check.Villages.SingleAsync(x => x.Slug == v.Slug);
        Assert.Equal(7, city.Stock(ResourceKind.Gold));
        Assert.Empty(await check.EconomyReceipts.Where(x => x.VillageId == city.Id).ToArrayAsync());
    }

    [Fact]
    public async Task Candle_energy_purchase_burns_only_test_candles_and_preserves_coins_and_character_progress()
    {
        var v=await players.FoundVillageAsync("Candle energy");var a=await players.JoinAsync(v,"Buyer");await View(a.Client,v.Path);
        await using(var db=await Db())
        {
            var c=await db.Citizens.SingleAsync(x=>x.Id==a.Id);c.StoredEnergy=0;c.Renown=53;
            var wallet=await db.GameWallets.SingleAsync(x=>x.AccountId==c.AccountId&&x.VillageId==c.VillageId);wallet.TestCandles=4;wallet.Coins=500;
            await db.SaveChangesAsync();
        }
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="energy-drink-candles"}));
        var view=await View(a.Client,v.Path);Assert.Equal(1,view.TestCandles);Assert.Equal(500,view.Coins);
        await using var check=await Db();var saved=await check.Citizens.AsNoTracking().SingleAsync(c=>c.Id==a.Id);
        Assert.Equal(saved.MaximumEnergy,saved.StoredEnergy);Assert.Equal(53,saved.Renown);
        Assert.Equal(-3,Assert.Single(view.Journal,x=>x.Operation=="energy-drink").Delta);
    }

    [Fact]
    public async Task Full_energy_purchase_is_atomic_idempotent_and_cannot_be_bought_twice_by_changing_currency()
    {
        var v=await players.FoundVillageAsync("Energy shop");var a=await players.JoinAsync(v,"Buyer");
        await View(a.Client,v.Path);await SeedWallet(v.Slug,a.Id,1000);
        await players.ArrangeCitizenAsync(a.Id,(c,_)=>c.StoredEnergy=0);
        var request=new{requestId=Guid.NewGuid(),action="energy-drink-coins"};
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        var view=await View(a.Client,v.Path);Assert.Equal(700,view.Coins);Assert.NotNull(view.EnergyDrinkReadyAt);
        await players.ArrangeCitizenAsync(a.Id,(c,_)=>c.StoredEnergy=0);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.Client.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="energy-drink-candles"})).StatusCode);
        Assert.Equal(700,(await View(a.Client,v.Path)).Coins);
        Assert.Equal(1,view.Journal.Count(x=>x.Operation=="energy-drink"));
        await using var db=await Db();var citizen=await db.Citizens.AsNoTracking().SingleAsync(c=>c.Id==a.Id);
        Assert.Equal(0,citizen.StoredEnergy);
    }

    [Fact]
    public async Task Unaffordable_energy_drink_does_not_refill_energy_or_start_a_cooldown()
    {
        var v=await players.FoundVillageAsync("No free energy");var a=await players.JoinAsync(v,"Buyer");
        await View(a.Client,v.Path);
        await players.ArrangeCitizenAsync(a.Id,(c,_)=>c.StoredEnergy=0);
        foreach(var action in new[]{"energy-drink-coins","energy-drink-candles"})
            Assert.Equal(HttpStatusCode.BadRequest,(await a.Client.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action})).StatusCode);
        await using var db=await Db();var saved=await db.Citizens.AsNoTracking().SingleAsync(c=>c.Id==a.Id);
        Assert.Equal(0,saved.StoredEnergy);Assert.Null(saved.LastEnergyDrinkAt);
        Assert.Empty(await db.EconomyReceipts.Where(x=>x.AccountId==saved.AccountId).ToArrayAsync());
    }

    [Fact]
    public async Task Common_purchase_is_once_only_and_a_reused_id_cannot_change_the_amount()
    {
        var v=await players.FoundVillageAsync("Stock purchase");var a=await players.JoinAsync(v,"Buyer");
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>{var s=StorageBuilding.Create(StorageKind.TownHall,"3,3",now);s.Put(ResourceKind.Wood,10);city.Storages.Add(s);});
        await View(a.Client,v.Path);await SeedWallet(v.Slug,a.Id,1000);
        var request=new{requestId=Guid.NewGuid(),action="buy-stock",resource="Wood",amount=3,price=2};
        var replies=await Task.WhenAll(a.Client.PostAsJsonAsync(v.Path+"/economy",request),a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        foreach(var reply in replies)await Players.OkJsonAsync(reply);
        var after=await View(a.Client,v.Path);Assert.Equal(994,after.Coins);Assert.Equal(6,after.Treasury);Assert.Equal(3,Assert.Single(after.Inventory).Amount);
        var bad=await a.Client.PostAsJsonAsync(v.Path+"/economy",new{request.requestId,action="buy-stock",resource="Wood",amount=4,price=2});
        await Players.AssertRuleBrokenAsync(bad,"economy.reusedRequest");
        await using var db=await Db();var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);Assert.Equal(7,city.Stock(ResourceKind.Wood));
        Assert.Equal(1,await db.EconomyReceipts.CountAsync(x=>x.VillageId==city.Id&&x.RequestId==request.requestId));
        Assert.Contains(after.Journal,x=>x.Asset=="Wood"&&x.Delta==-3);Assert.Contains(after.Journal,x=>x.Asset=="Wood"&&x.Delta==3);
    }

    [Fact]
    public async Task Independent_service_instances_share_receipts_and_conserve_money_and_goods()
    {
        var v = await players.FoundVillageAsync("Independent economic workers");
        var a = await players.JoinAsync(v, "Buyer");
        await players.ArrangeVillageAsync(v.Slug, (city, now) =>
        {
            var storage = StorageBuilding.Create(StorageKind.TownHall, "3,3", now);
            storage.Put(ResourceKind.Wood, 10);
            city.Storages.Add(storage);
        });
        await View(a.Client, v.Path);
        await SeedWallet(v.Slug, a.Id, 1000);
        Guid account;
        await using (var db = await Db()) account = (await db.Citizens.SingleAsync(c => c.Id == a.Id)).AccountId;

        // Each GameService owns a separate in-process VillageLocks instance. Only PostgreSQL
        // transactions/advisory locks and durable receipts can serialize these two commands.
        var first = ActivatorUtilities.CreateInstance<GameService>(factory.Services);
        var second = ActivatorUtilities.CreateInstance<GameService>(factory.Services);
        var request = new EconomyRequest(Guid.NewGuid(), "buy-stock", ResourceKind.Wood, 3, 2);
        var results = await Task.WhenAll(
            first.ChangeEconomyAsync(v.Slug, account, request, default),
            second.ChangeEconomyAsync(v.Slug, account, request, default));
        foreach (var result in results)
        {
            Assert.Equal(994, result.Coins);
            Assert.Equal(6, result.Treasury);
            Assert.Equal(3, Assert.Single(result.Inventory).Amount);
        }
        var conflict = await Assert.ThrowsAsync<GameRuleException>(() =>
            second.ChangeEconomyAsync(v.Slug, account, request with { Amount = 4 }, default));
        Assert.Equal("economy.reusedRequest", conflict.Code);
        await using var check = await Db();
        var city = await check.Villages.SingleAsync(x => x.Slug == v.Slug);
        var wallet = await check.GameWallets.SingleAsync(x => x.VillageId == city.Id && x.AccountId == account);
        var treasury = await check.KingdomEconomies.SingleAsync(x => x.VillageId == city.Id);
        Assert.Equal(1000, wallet.Coins + treasury.TreasuryCoins);
        Assert.Equal(7, city.Stock(ResourceKind.Wood));
        var citizen = await check.Citizens.SingleAsync(x => x.Id == a.Id);
        Assert.Equal(3, PersonalInventory.Amount(citizen, ResourceKind.Wood));
        Assert.Equal(6, citizen.PersonalGoods.Sum(x => x.CostBasis));
        Assert.Equal(1, await check.EconomyReceipts.CountAsync(x => x.VillageId == city.Id && x.RequestId == request.RequestId));
    }

    [Fact]
    public async Task Purchased_drink_is_usable_through_existing_action_without_charging_or_consuming_twice()
    {
        var v=await players.FoundVillageAsync("Personal drink");var a=await players.JoinAsync(v,"Buyer");
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>{
            var storage=StorageBuilding.Create(StorageKind.TownHall,"3,3",now);
            storage.Put(ResourceKind.VigorDrink,2);city.Storages.Add(storage);
        });
        await View(a.Client,v.Path);await SeedWallet(v.Slug,a.Id,100);
        await players.ArrangeCitizenAsync(a.Id,(p,now)=>{p.StoredEnergy=10;p.EnergyUpdatedAt=now;});
        var request=new{requestId=Guid.NewGuid(),action="buy-stock",resource="VigorDrink",amount=1};
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        await Players.OkJsonAsync(await a.Client.PostAsync(v.Path+"/me/drink",null));
        await Players.AssertRuleBrokenAsync(await a.Client.PostAsync(v.Path+"/me/drink",null),"drink.cooldown");
        var view=await View(a.Client,v.Path);Assert.Empty(view.Inventory);Assert.Equal(70,view.Coins);
        var citizen=await players.LoadCitizenAsync(a.Id);Assert.NotNull(citizen.LastDrinkAt);
        await using var db=await Db();var city=await db.Villages.AsNoTracking().SingleAsync(x=>x.Slug==v.Slug);
        Assert.Equal(1,city.Stock(ResourceKind.VigorDrink));
        Assert.Contains(view.Journal,x=>x.Asset=="VigorDrink"&&x.Delta==-1&&x.Location.StartsWith("personal:"));
    }

    [Fact]
    public async Task Two_buyers_cannot_buy_the_same_lot_and_money_and_basis_are_conserved()
    {
        var v=await players.FoundVillageAsync("Market race");var a=await players.JoinAsync(v,"Seller");var b=await players.JoinAsync(v,"First");var c=await players.JoinAsync(v,"Second");
        foreach(var resident in new[]{a,b,c}){await View(resident.Client,v.Path);await SeedWallet(v.Slug,resident.Id,1000);}
        await players.ArrangeCitizenAsync(a.Id,(p,_)=>PersonalInventory.Add(p,ResourceKind.Planks,4,20));
        var lotId=Guid.NewGuid();await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",new{requestId=lotId,action="list",resource="Planks",amount=4,price=80}));
        var rb=new{requestId=Guid.NewGuid(),action="buy-lot",target=lotId};var rc=new{requestId=Guid.NewGuid(),action="buy-lot",target=lotId};
        var replies=await Task.WhenAll(b.Client.PostAsJsonAsync(v.Path+"/economy",rb),c.Client.PostAsJsonAsync(v.Path+"/economy",rc));
        Assert.Single(replies,x=>x.IsSuccessStatusCode);Assert.Single(replies,x=>x.StatusCode==HttpStatusCode.BadRequest);
        var winner=replies[0].IsSuccessStatusCode?b:c;
        if(replies[0].IsSuccessStatusCode)await Players.OkJsonAsync(await winner.Client.PostAsJsonAsync(v.Path+"/economy",rb));
        else await Players.OkJsonAsync(await winner.Client.PostAsJsonAsync(v.Path+"/economy",rc));
        var seller=await View(a.Client,v.Path);var first=await View(b.Client,v.Path);var second=await View(c.Client,v.Path);
        Assert.Equal(3000,seller.Coins+first.Coins+second.Coins);Assert.Equal(1080,seller.Coins);Assert.Equal(60,seller.ProfitToday);
        Assert.Equal(4,first.Inventory.Sum(x=>x.Amount)+second.Inventory.Sum(x=>x.Amount));
        Assert.Equal(80,first.Inventory.Sum(x=>x.CostBasis)+second.Inventory.Sum(x=>x.CostBasis));
        Assert.False(seller.TaxEnabled);Assert.Equal(0,seller.TaxPreview);
        await using var db=await Db();var city=await db.Villages.AsNoTracking().SingleAsync(x=>x.Slug==v.Slug);
        var moves=await db.LedgerEntries.Where(x=>x.VillageId==city.Id&&x.Asset=="Planks"&&
            (x.Operation=="list"||x.Operation=="buy-lot")).ToListAsync();
        Assert.Contains(moves,x=>x.Location=="market:"+lotId&&x.Delta==4);
        Assert.Contains(moves,x=>x.Location=="market:"+lotId&&x.Delta==-4);
        Assert.All(moves.GroupBy(x=>x.Operation),group=>Assert.Equal(0,group.Sum(x=>x.Delta)));
    }

    [Fact]
    public async Task Exchange_is_test_only_daily_limited_and_idempotent()
    {
        var v=await players.FoundVillageAsync("Test exchange");var a=await players.JoinAsync(v,"Saver");
        await View(a.Client,v.Path);await SeedWallet(v.Slug,a.Id,2000);
        var request=new{requestId=Guid.NewGuid(),action="exchange",amount=5};
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        var view=await View(a.Client,v.Path);Assert.Equal(1500,view.Coins);Assert.Equal(5,view.TestCandles);Assert.Equal(5,view.ExchangedToday);Assert.Equal(45,view.KingdomCandleRoom);
        await Players.AssertRuleBrokenAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="exchange",amount=1}),"exchange.limit");
        await using var db=await Db();var person=await db.Citizens.SingleAsync(x=>x.Id==a.Id);
        var wallet=await db.GameWallets.SingleAsync(x=>x.VillageId==person.VillageId&&x.AccountId==person.AccountId);wallet.ExchangeDay=DateOnly.FromDateTime(factory.Clock.GetUtcNow().UtcDateTime.AddDays(-1));
        await db.SaveChangesAsync();
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="exchange",amount=1}));
        Assert.Equal(1,(await View(a.Client,v.Path)).ExchangedToday);
    }

    [Fact]
    public async Task Useful_cycle_receives_one_wage_and_repeat_settlement_does_not_pay_again()
    {
        var v=await players.FoundVillageAsync("Useful wage");var a=await players.JoinAsync(v,"Worker");
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>city.Storages.Add(StorageBuilding.Create(StorageKind.TownHall,"3,3",now)));
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="chop-wood"}));
        await players.ArrangeCitizenAsync(a.Id,(p,now)=>{p.CycleStartedAt=now.AddMinutes(-5);p.CycleEndsAt=now.AddSeconds(-1);});
        var game=factory.Services.GetRequiredService<GameService>();await using var db=await Db();var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
        await game.SettleVillageAsync(city.Id,[a.Id],default);await game.SettleVillageAsync(city.Id,[a.Id],default);
        var view=await View(a.Client,v.Path);Assert.Equal(view.WorkPay,view.Coins);Assert.Single(view.Journal,x=>x.Operation=="wage");
        Assert.Single((await View(v.Tsar,v.Path)).Journal,x=>x.Operation=="wage");
        var before=(await players.LoadCitizenAsync(a.Id)).Skills.Sum(x=>x.Experience);
        await game.SettleVillageAsync(city.Id,[a.Id],default);Assert.Equal(before,(await players.LoadCitizenAsync(a.Id)).Skills.Sum(x=>x.Experience));
    }

    [Fact]
    public async Task Primary_world_service_and_http_delete_preserve_progress_while_logout_still_works()
    {
        var v=await players.FoundVillageAsync("Protected realm");var a=await players.JoinAsync(v,"Resident");
        await players.ArrangeCitizenAsync(a.Id,(p,_)=>p.Renown=73);
        await using var db=await Db();var person=await db.Citizens.AsNoTracking().SingleAsync(x=>x.Id==a.Id);
        var login=await db.ExternalLogins.Where(x=>x.AccountId==person.AccountId).Select(x=>x.ProviderUserId).FirstAsync();
        await using var mounted=factory.WithWebHostBuilder(builder=>builder.UseSetting("Hosting:PrimaryVillage",v.Slug));
        var service=mounted.Services.GetRequiredService<GameService>();
        await Assert.ThrowsAsync<Desertod.Domain.GameRuleException>(()=>service.LeaveVillageAsync(v.Slug,person.AccountId,default));
        await Assert.ThrowsAsync<Desertod.Domain.GameRuleException>(()=>service.DeleteVillageAsync(v.Slug,person.AccountId,default));
        using var client=mounted.CreateClient();await client.PostAsJsonAsync("/auth/dev-login",new{name=login});
        await Players.AssertRuleBrokenAsync(await client.DeleteAsync(v.Path+"/me"),"village.protectedWorld");
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsync("/auth/logout",null)).StatusCode);
        var saved=await players.LoadCitizenAsync(a.Id);Assert.Equal(person.Id,saved.Id);Assert.Equal(73,saved.Renown);
        Assert.True(await db.Villages.AnyAsync(x=>x.Slug==v.Slug));
    }

    [Fact]
    public async Task Directed_raid_path_survives_reload_and_retired_defense_sectors_cannot_be_reassigned()
    {
        var v=await players.FoundVillageAsync("Directed raid");var residents=new[]{await players.JoinAsync(v,"Guard"),await players.JoinAsync(v,"One"),await players.JoinAsync(v,"Two")};
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>{city.CreatedAt=now.AddDays(-10);city.RaidsPlannedPeriod=long.MaxValue;});
        var guardTask=GameCatalog.Tasks.First(t=>t.Effect==TaskEffect.Guard);
        await players.ArrangeCitizenAsync(residents[0].Id,(p,now)=>{p.CurrentTaskId=guardTask.Id;p.CycleSettled=false;p.CycleStartedAt=now;p.CycleEndsAt=now.AddHours(1);});
        foreach(var p in residents)factory.Services.GetRequiredService<CitizenPresence>().Touch(p.Id,v.Slug);
        await using var db=await Db();var city=await db.Villages.AsNoTracking().SingleAsync(x=>x.Slug==v.Slug);var service=factory.Services.GetRequiredService<GameService>();
        var review=await service.BeginAlmightyReviewAsync(city.Id,default);Assert.NotNull(review);
        var option=review.Options.First(x=>x.Enemy==EnemyKind.Skeleton&&x.Sector is not null);
        await service.CompleteAlmightyReviewAsync(review,new(option.Id,"Проверенный подход","Не обходить оборону"),default);
        var proposal=await db.AlmightyProposals.AsNoTracking().SingleAsync(x=>x.VillageId==city.Id);
        await Players.OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+$"/director/{proposal.Id}/vote",new{accept=true}));
        await Players.OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+$"/director/{proposal.Id}/vote",new{accept=true}));
        var loaded=await db.Villages.AsNoTracking().SingleAsync(x=>x.Id==city.Id);var raid=Assert.Single(loaded.Raids);
        Assert.Equal(option.Sector,raid.EntrySector);Assert.Equal(2,raid.EngineVersion);Assert.Empty(raid.ApproachPath);
        var battle=await db.RaidBattles.AsNoTracking().SingleAsync(b=>b.Id==raid.Id);
        Assert.Equal(option.Sector,battle.Sector);Assert.Equal(option.Composition,battle.Composition);
        Assert.Equal(raid.Count,battle.Units.Count);Assert.NotEmpty(battle.Units);
        await Players.AssertRuleBrokenAsync(await residents[0].Client.PutAsJsonAsync(v.Path+"/me/defense",new{mode=option.Sector}),"defense.removed");
        var resident=await players.LoadCitizenAsync(residents[0].Id);Assert.Null(resident.DefenseSector);Assert.Null(resident.DefenseReadyAt);
    }

    [Fact]
    public async Task Personal_transfer_keeps_basis_and_journal_pages_do_not_expose_other_private_goods()
    {
        var v=await players.FoundVillageAsync("Owned inventory");var a=await players.JoinAsync(v,"Donor");var b=await players.JoinAsync(v,"Recipient");
        await View(a.Client,v.Path);await players.ArrangeCitizenAsync(a.Id,(p,_)=>PersonalInventory.Add(p,ResourceKind.Stone,9,99));
        var recipient=await players.LoadCitizenAsync(b.Id);
        var request=new{requestId=Guid.NewGuid(),action="transfer",resource="Stone",amount=3,target=recipient.AccountId};
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        var av=await View(a.Client,v.Path);var bv=await View(b.Client,v.Path);
        Assert.Equal(6,Assert.Single(av.Inventory).Amount);Assert.Equal(3,Assert.Single(bv.Inventory).Amount);
        Assert.Equal(99,av.Inventory.Sum(x=>x.CostBasis)+bv.Inventory.Sum(x=>x.CostBasis));
        Assert.DoesNotContain(av.Journal,x=>x.Location=="personal:"+recipient.AccountId);
        await using var db=await Db();var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);var now=factory.Clock.GetUtcNow();
        for(var i=0;i<55;i++)db.LedgerEntries.Add(new LedgerEntry{VillageId=city.Id,OperationId=Guid.NewGuid(),Operation="test",Asset="Wood",Location="storage:3,3",Delta=1,At=now});
        await db.SaveChangesAsync();var first=await View(a.Client,v.Path);Assert.NotNull(first.NextJournalBefore);Assert.Equal(50,first.Journal.Count);
        var page=await a.Client.GetFromJsonAsync<EconomyView>(v.Path+"/economy?before="+first.NextJournalBefore,JsonOptions);
        Assert.NotNull(page);Assert.DoesNotContain(page.Journal,x=>first.Journal.Any(p=>p.Id==x.Id));
    }

    [Fact]
    public async Task Mentions_use_same_village_citizen_ids_and_remain_plain_text()
    {
        var v=await players.FoundVillageAsync("Mentions");var a=await players.JoinAsync(v,"Writer");var b=await players.JoinAsync(v,"Reader");
        var sent=await a.Client.PostAsJsonAsync(v.Path+"/chat",new{text="@Reader привет <b>без HTML</b>",mentions=new[]{b.Id}});
        var data=await Players.OkJsonAsync(sent);Assert.Equal(b.Id,data.GetProperty("mentionCitizenIds")[0].GetGuid());
        Assert.Contains("<b>",data.GetProperty("text").GetString());
        await Players.AssertRuleBrokenAsync(await a.Client.PostAsJsonAsync(v.Path+"/chat",new{text="Не тот адресат",mentions=new[]{Guid.NewGuid()}}),"chat.mentions");
        var history=await a.Client.GetFromJsonAsync<System.Text.Json.JsonElement>(v.Path+"/chat");
        Assert.Single(history.EnumerateArray());
    }

    [Fact]
    public async Task External_blueprint_is_owned_and_cannot_draw_common_material_or_other_workers()
    {
        var v=await players.FoundVillageAsync("Private land");var a=await players.JoinAsync(v,"Owner");var b=await players.JoinAsync(v,"Other");
        var definition=BuildingCatalog.Get(BuildingKind.Hut);
        var stage=Enumerable.Range(0,definition.Stages.Count).First(i=>definition.Stages[i].Jobs.Any(j=>BuildingCatalog.FindJob(j.JobId)?.Kind==JobKind.Haul));
        var job=definition.Stages[stage].Jobs.First(j=>BuildingCatalog.FindJob(j.JobId)?.Kind==JobKind.Haul);
        var resource=BuildingCatalog.FindJob(job.JobId)!.Resource!.Value;
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>{
            Array.Fill(city.Explored,(byte)255);city.Wilds!.Ravine.Clear();city.Wilds.Obstacles.Clear();
            city.Structures.Add(new Structure{Kind=BuildingKind.LeanTo,PlotId="4,18",BuiltAt=now});
            var s=StorageBuilding.Create(StorageKind.TownHall,"3,3",now);s.Put(resource,10);city.Storages.Add(s);
        });
        await Players.AssertRuleBrokenAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="private-build",building="Hut",x=-1,y=10}),"building.publicApproval");
        var request=new{requestId=Guid.NewGuid(),action="private-build",building="Hut",x=-8,y=10};
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        await Players.AssertRuleBrokenAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/job",new{plotId="-8,10",stage,jobId=job.JobId}),"site.noMaterial");
        await Players.AssertRuleBrokenAsync(await b.Client.PostAsJsonAsync(v.Path+"/me/job",new{plotId="-8,10",stage,jobId=job.JobId}),"building.private");
        await using var db=await Db();var city=await db.Villages.AsNoTracking().SingleAsync(x=>x.Slug==v.Slug);var owner=await players.LoadCitizenAsync(a.Id);
        Assert.Equal(owner.AccountId,Assert.Single(city.Sites).OwnerAccountId);Assert.Equal(10,city.Stock(resource));
        await Players.AssertRuleBrokenAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="private-build",building="Hut",x=40,y=10}),"bridge.closed");
        Assert.Single((await db.Villages.AsNoTracking().SingleAsync(x=>x.Slug==v.Slug)).Sites);
    }
}
