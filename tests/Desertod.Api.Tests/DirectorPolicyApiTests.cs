using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Director;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DirectorPolicyApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private GameService Game=>factory.Services.GetRequiredService<GameService>();
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private static async Task<AlmightyPolicyChange> Set(FoundedVillage v,string kind,bool enabled,long version,Guid? id=null)
    {
        var response=await v.Tsar.PutAsJsonAsync(v.Path+"/director/category/"+kind,new{requestId=id??Guid.NewGuid(),enabled,expectedPolicyVersion=version});
        response.EnsureSuccessStatusCode();return (await response.Content.ReadFromJsonAsync<AlmightyPolicyChange>())!;
    }

    [Fact]
    public async Task Only_owner_may_change_categories_and_incomplete_unknown_or_stale_requests_leave_no_receipt()
    {
        var v=await players.FoundVillageAsync("Director policy access");var member=await players.JoinAsync(v,"Viewer");
        var body=new{requestId=Guid.NewGuid(),enabled=false,expectedPolicyVersion=0};
        Assert.Equal(HttpStatusCode.Forbidden,(await member.Client.PutAsJsonAsync(v.Path+"/director/category/raid",body)).StatusCode);
        foreach(var kind in new[]{"observe","wallet_credit","RAID"})
            Assert.Equal(HttpStatusCode.BadRequest,(await v.Tsar.PutAsJsonAsync(v.Path+"/director/category/"+kind,body)).StatusCode);
        foreach(var invalid in new object[]{new{enabled=false,expectedPolicyVersion=0},new{requestId=Guid.NewGuid(),expectedPolicyVersion=0},
            new{requestId=Guid.NewGuid(),enabled=false},new{requestId=Guid.NewGuid(),enabled=false,expectedPolicyVersion=-1},
            new{requestId=Guid.NewGuid(),enabled=false,expectedPolicyVersion=1}})
            Assert.Equal(HttpStatusCode.BadRequest,(await v.Tsar.PutAsJsonAsync(v.Path+"/director/category/raid",invalid)).StatusCode);
        var view=(await v.Tsar.GetFromJsonAsync<AlmightyView>(v.Path+"/director"))!;
        Assert.Empty(view.PolicyChanges!);Assert.Empty(view.State.DisabledKinds);Assert.Equal(0,view.State.PolicyVersion);
    }

    [Fact]
    public async Task Concurrent_retries_record_once_and_old_retry_cannot_reverse_a_later_reenable()
    {
        var v=await players.FoundVillageAsync("Director policy retry");var id=Guid.NewGuid();
        var results=await Task.WhenAll(Set(v,"raid",false,0,id),Set(v,"raid",false,0,id));
        Assert.All(results,r=>{Assert.Equal(1,r.PolicyVersion);Assert.True(r.Changed);Assert.Equal(["raid"],r.DisabledKinds);});
        var enabled=await Set(v,"raid",true,1);Assert.Equal(2,enabled.PolicyVersion);
        var replay=await Set(v,"raid",false,0,id);Assert.Equal(1,replay.PolicyVersion);Assert.Equal(results[0].At,replay.At);
        Assert.Equal(HttpStatusCode.BadRequest,(await v.Tsar.PutAsJsonAsync(v.Path+"/director/category/raid",new{requestId=id,enabled=true,expectedPolicyVersion=0})).StatusCode);
        var view=(await v.Tsar.GetFromJsonAsync<AlmightyView>(v.Path+"/director"))!;
        Assert.Equal(2,view.State.PolicyVersion);Assert.Empty(view.State.DisabledKinds);Assert.Equal(2,view.PolicyChanges!.Count);
    }

    [Fact]
    public async Task Concurrent_different_requests_with_same_expected_version_cannot_overwrite_each_other()
    {
        var v=await players.FoundVillageAsync("Director policy conflict");
        var results=await Task.WhenAll(new[]{"raid","dungeon"}.Select(kind=>v.Tsar.PutAsJsonAsync(v.Path+"/director/category/"+kind,
            new{requestId=Guid.NewGuid(),enabled=false,expectedPolicyVersion=0})));
        Assert.Single(results,r=>r.IsSuccessStatusCode);Assert.Single(results,r=>r.StatusCode==HttpStatusCode.BadRequest);
        var view=(await v.Tsar.GetFromJsonAsync<AlmightyView>(v.Path+"/director"))!;
        Assert.Single(view.State.DisabledKinds);Assert.Single(view.PolicyChanges!);Assert.Equal(1,view.State.PolicyVersion);
    }

    [Fact]
    public async Task Noop_is_receipted_without_cancelling_inflight_review_or_spending_another_model_call()
    {
        using var frozen=factory.Clock.Freeze();var v=await players.FoundVillageAsync("Director policy noop");
        await using var db=await Db();var world=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);
        var review=(await Game.BeginAlmightyReviewAsync(world.Id,default))!;
        var id=Guid.NewGuid();var ack=await Set(v,"raid",true,0,id);Assert.False(ack.Changed);Assert.Equal(0,ack.PolicyVersion);
        await Set(v,"raid",true,0,id);
        Assert.True(await Game.CompleteAlmightyReviewAsync(review,new("economy_report","Observe economy","Wait"),default));
        var state=await db.AlmightyStates.AsNoTracking().SingleAsync(s=>s.VillageId==world.Id);Assert.Equal(1,state.CallsToday);
        Assert.Single(await db.AlmightyPolicyChanges.Where(p=>p.VillageId==world.Id).ToArrayAsync());
    }

    [Fact]
    public async Task Stop_invalidates_old_analysis_and_new_snapshot_has_no_disabled_options_or_raid_fallback()
    {
        using var frozen=factory.Clock.Freeze();var v=await players.FoundVillageAsync("Director policy inflight");
        await using var db=await Db();var world=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);
        var review=(await Game.BeginAlmightyReviewAsync(world.Id,default))!;
        await Set(v,"raid",false,0);await Set(v,"vigor",false,1);await Set(v,"advice",false,2);
        Assert.False(await Game.CompleteAlmightyReviewAsync(review,new("vigor_stronger","Stale answer","Tune"),default));
        var fresh=(await Game.BeginAlmightyReviewAsync(world.Id,default))!;
        Assert.DoesNotContain(fresh.Options,o=>o.Enemy is not null||o.Id.StartsWith("vigor_")||o.Id=="economy_report");
        Assert.Contains(fresh.Options,o=>o.Id=="none");using var json=JsonDocument.Parse(fresh.Snapshot);
        var pressure=json.RootElement.GetProperty("raidPressure");Assert.False(pressure.GetProperty("proposalDue").GetBoolean());
        Assert.False(pressure.GetProperty("eligible").GetBoolean());Assert.Equal(JsonValueKind.Null,pressure.GetProperty("fallbackOption").ValueKind);
        Assert.Contains("director_raid_category_disabled",pressure.GetProperty("blockedBy").EnumerateArray().Select(x=>x.GetString()));
        Assert.True(await Game.CompleteAlmightyReviewAsync(fresh,new("none","Observe","Wait"),default));
        Assert.Empty(await db.AlmightyProposals.Where(p=>p.VillageId==world.Id).ToArrayAsync());
    }

    [Fact]
    public async Task Pending_proposal_is_superseded_and_enabling_does_not_revive_it_or_erase_an_applied_effect()
    {
        using var frozen=factory.Clock.Freeze();var v=await players.FoundVillageAsync("Director policy accepted");
        await using var db=await Db();var world=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);
        var review=(await Game.BeginAlmightyReviewAsync(world.Id,default))!;
        await Game.CompleteAlmightyReviewAsync(review,new("vigor_stronger","More energy","Tune"),default);
        var proposal=await db.AlmightyProposals.SingleAsync(p=>p.VillageId==world.Id);
        var stop=await Set(v,"vigor",false,0);Assert.Equal([proposal.Id],stop.SupersededProposals);
        await v.Tsar.PostAsJsonAsync(v.Path+$"/director/{proposal.Id}/vote",new{accept=true});
        await Set(v,"vigor",true,1);
        await v.Tsar.PostAsJsonAsync(v.Path+$"/director/{proposal.Id}/vote",new{accept=true});
        Assert.Equal("Superseded",(await db.AlmightyProposals.AsNoTracking().SingleAsync(p=>p.Id==proposal.Id)).Status);
        Assert.Equal(8,(await db.Villages.AsNoTracking().SingleAsync(w=>w.Id==world.Id)).VigorEnergy);
        // A separate accepted proposal models the opposite serialization order: vote before stop.
        var accepted=new AlmightyProposal{VillageId=world.Id,ReviewId=Guid.NewGuid(),Option="vigor_stronger",Snapshot=proposal.Snapshot,
            CreatedAt=factory.Clock.GetUtcNow(),ExpiresAt=factory.Clock.GetUtcNow().AddMinutes(20)};
        db.AlmightyProposals.Add(accepted);await db.SaveChangesAsync();
        (await v.Tsar.PostAsJsonAsync(v.Path+$"/director/{accepted.Id}/vote",new{accept=true})).EnsureSuccessStatusCode();
        await Set(v,"vigor",false,2);
        Assert.Equal("Applied",(await db.AlmightyProposals.AsNoTracking().SingleAsync(p=>p.Id==accepted.Id)).Status);
        Assert.Equal(10,(await db.Villages.AsNoTracking().SingleAsync(w=>w.Id==world.Id)).VigorEnergy);
    }

    [Fact]
    public async Task Mode_changes_advance_policy_version_but_keep_category_stops()
    {
        var v=await players.FoundVillageAsync("Director policy modes");await Set(v,"dungeon",false,0);
        foreach(var mode in new[]{"Off","Propose"})(await v.Tsar.PutAsJsonAsync(v.Path+"/director/mode",new{mode})).EnsureSuccessStatusCode();
        var view=(await v.Tsar.GetFromJsonAsync<AlmightyView>(v.Path+"/director"))!;
        Assert.Equal(3,view.State.PolicyVersion);Assert.Equal(["dungeon"],view.State.DisabledKinds);
        Assert.Equal(HttpStatusCode.BadRequest,(await v.Tsar.PutAsJsonAsync(v.Path+"/director/category/dungeon",new{requestId=Guid.NewGuid(),enabled=true,expectedPolicyVersion=1})).StatusCode);
    }

    [Fact]
    public async Task Racing_stop_and_owner_vote_have_one_serial_result_and_never_reapply_the_effect()
    {
        using var frozen=factory.Clock.Freeze();var v=await players.FoundVillageAsync("Director policy race");
        await using var db=await Db();var world=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);
        var review=(await Game.BeginAlmightyReviewAsync(world.Id,default))!;
        await Game.CompleteAlmightyReviewAsync(review,new("vigor_stronger","Tune energy","Tune"),default);
        var p=await db.AlmightyProposals.SingleAsync(p=>p.VillageId==world.Id);
        var stopTask=Set(v,"vigor",false,0);var voteTask=v.Tsar.PostAsJsonAsync(v.Path+$"/director/{p.Id}/vote",new{accept=true});
        await Task.WhenAll(stopTask,voteTask);var stop=await stopTask;(await voteTask).EnsureSuccessStatusCode();
        var after=await db.AlmightyProposals.AsNoTracking().SingleAsync(x=>x.Id==p.Id);
        Assert.Contains(after.Status,new[]{"Applied","Superseded"});
        var energy=(await db.Villages.AsNoTracking().SingleAsync(w=>w.Id==world.Id)).VigorEnergy;
        Assert.Equal(after.Status=="Applied"?10:8,energy);
        Assert.Equal(after.Status=="Superseded",stop.SupersededProposals.Contains(p.Id));
        (await v.Tsar.PostAsJsonAsync(v.Path+$"/director/{p.Id}/vote",new{accept=true})).EnsureSuccessStatusCode();
        Assert.Equal(energy,(await db.Villages.AsNoTracking().SingleAsync(w=>w.Id==world.Id)).VigorEnergy);
    }

    [Fact]
    public async Task Category_stops_leave_announced_wave_and_accepted_dungeon_byte_for_byte_unchanged()
    {
        using var frozen=factory.Clock.Freeze();var v=await players.FoundVillageAsync("Director accepted events");
        await using var db=await Db();var world=await db.Villages.SingleAsync(w=>w.Slug==v.Slug);var now=factory.Clock.GetUtcNow();
        RaidRules.CallUndead(world,EnemyKind.Skeleton,RaidSize.Small,3,now);
        db.DungeonRuns.Add(DungeonRules.Open(world,DungeonCatalog.Dungeons.First().Id,"Accepted ruler",123,now,[]));
        await db.SaveChangesAsync();
        async Task<string[]> Rows()=>await db.Database.SqlQuery<string>($"""
            SELECT to_jsonb(v)::text AS "Value" FROM "Villages" v WHERE "Id"={world.Id}
            UNION ALL SELECT to_jsonb(r)::text FROM "DungeonRuns" r WHERE "VillageId"={world.Id}
            ORDER BY "Value"
            """).ToArrayAsync();
        var before=await Rows();Assert.Equal(2,before.Length);
        await Set(v,"raid",false,0);await Set(v,"dungeon",false,1);await Set(v,"rest",false,2);
        Assert.Equal(before,await Rows());
    }
}
