using System.Text.Json;
using System.Text.Json.Nodes;
using Desertod.Api.Game;
using Desertod.Domain.Director;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DirectorContractApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private GameService Game=>factory.Services.GetRequiredService<GameService>();
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Missing_lease_deadline_or_the_exact_expiry_boundary_cannot_complete_a_review(bool missing)
    {
        using var frozen=factory.Clock.Freeze();var village=await players.FoundVillageAsync("Director lease deadline "+missing);
        await using var db=await Db();var world=await db.Villages.SingleAsync(w=>w.Slug==village.Slug);
        var review=(await Game.BeginAlmightyReviewAsync(world.Id,default))!;
        var state=await db.AlmightyStates.SingleAsync(s=>s.VillageId==world.Id);
        state.LeaseUntil=missing?null:factory.Clock.GetUtcNow();await db.SaveChangesAsync();
        Assert.False(await Game.CompleteAlmightyReviewAsync(review,new("economy_report","Too late","Observe"),default));
        Assert.Empty(await db.AlmightyProposals.Where(p=>p.VillageId==world.Id).ToListAsync());
    }

    [Fact]
    public async Task Review_is_versioned_and_an_in_memory_allowlist_cannot_grant_new_permissions()
    {
        using var frozen=factory.Clock.Freeze();var village=await players.FoundVillageAsync("Director contract");
        await using var db=await Db();var world=await db.Villages.SingleAsync(w=>w.Slug==village.Slug);
        var review=(await Game.BeginAlmightyReviewAsync(world.Id,default))!;
        using var snapshot=JsonDocument.Parse(review.Snapshot);var contract=snapshot.RootElement.GetProperty("contract");
        Assert.Equal(1,contract.GetProperty("version").GetInt32());Assert.Equal(world.Id,contract.GetProperty("worldId").GetGuid());
        Assert.Equal(review.Lease,contract.GetProperty("observationId").GetGuid());Assert.Equal(64,contract.GetProperty("revision").GetString()!.Length);
        var permissions=snapshot.RootElement.GetProperty("permissions");Assert.All(permissions.EnumerateArray(),p=>Assert.False(p.GetProperty("autonomous").GetBoolean()));
        var forged=review with{Options=review.Options.Append(new("give_real_candles","Money","Mint money")).ToArray()};
        Assert.True(await Game.CompleteAlmightyReviewAsync(forged,new("give_real_candles","Forged decision","Mint"),default));
        Assert.Empty(await db.AlmightyProposals.Where(p=>p.VillageId==world.Id).ToListAsync());
        Assert.NotEmpty((await db.AlmightyStates.SingleAsync(s=>s.VillageId==world.Id)).LastError);
        Assert.False(await Game.CompleteAlmightyReviewAsync(review,new("economy_report","Retry","Watch"),default));
    }

    [Fact]
    public async Task Completion_uses_the_persisted_snapshot_and_options_instead_of_mutated_caller_metadata()
    {
        using var frozen=factory.Clock.Freeze();var village=await players.FoundVillageAsync("Stored director review");
        await using var db=await Db();var world=await db.Villages.SingleAsync(w=>w.Slug==village.Slug);
        var review=(await Game.BeginAlmightyReviewAsync(world.Id,default))!;
        var forged=review with{Snapshot="not json",Options=[]};
        Assert.True(await Game.CompleteAlmightyReviewAsync(forged,new("economy_report","Use saved evidence","Observe"),default));
        var proposal=await db.AlmightyProposals.SingleAsync(p=>p.VillageId==world.Id);
        Assert.Equal("economy_report",proposal.Option);Assert.Equal(review.Snapshot,proposal.Snapshot);Assert.Equal("Pending",proposal.Status);
        Assert.False(await Game.CompleteAlmightyReviewAsync(review,new("economy_report","Repeat","Observe"),default));
    }

    [Theory]
    [InlineData("future_version")]
    [InlineData("world")]
    [InlineData("observation")]
    [InlineData("revision")]
    [InlineData("permission")]
    [InlineData("json")]
    public async Task Incompatible_or_damaged_persisted_evidence_cannot_create_a_proposal(string damage)
    {
        using var frozen=factory.Clock.Freeze();var village=await players.FoundVillageAsync("Invalid director contract "+damage);
        await using var db=await Db();var world=await db.Villages.SingleAsync(w=>w.Slug==village.Slug);
        var review=(await Game.BeginAlmightyReviewAsync(world.Id,default))!;
        var stored=await db.AlmightyObservations.SingleAsync(o=>o.Id==review.Lease);var json=JsonNode.Parse(stored.Snapshot)!;
        switch(damage)
        {
            case "future_version":json["contract"]!["version"]=999;break;
            case "world":json["contract"]!["worldId"]=Guid.NewGuid();break;
            case "observation":json["contract"]!["observationId"]=Guid.NewGuid();break;
            case "revision":json["stock"]!["Food"]=999;break;
            case "permission":json["permissions"]![0]!["autonomous"]=true;break;
        }
        stored.Snapshot=damage=="json"?"{broken":json.ToJsonString();await db.SaveChangesAsync();
        Assert.False(await Game.CompleteAlmightyReviewAsync(review,new("economy_report","Invalid evidence","Observe"),default));
        Assert.Empty(await db.AlmightyProposals.Where(p=>p.VillageId==world.Id).ToListAsync());
        Assert.Null((await db.AlmightyStates.SingleAsync(s=>s.VillageId==world.Id)).Lease);
        Assert.Contains("повреждён",(await db.AlmightyStates.SingleAsync(s=>s.VillageId==world.Id)).LastError);
    }

    [Fact]
    public async Task Legacy_in_flight_review_keeps_its_stored_admission_without_becoming_autonomous()
    {
        using var frozen=factory.Clock.Freeze();var village=await players.FoundVillageAsync("Legacy director review");
        await using var db=await Db();var world=await db.Villages.SingleAsync(w=>w.Slug==village.Slug);
        var review=(await Game.BeginAlmightyReviewAsync(world.Id,default))!;
        var stored=await db.AlmightyObservations.SingleAsync(o=>o.Id==review.Lease);var json=JsonNode.Parse(stored.Snapshot)!.AsObject();
        json.Remove("contract");json.Remove("permissions");stored.Snapshot=json.ToJsonString();await db.SaveChangesAsync();
        Assert.True(await Game.CompleteAlmightyReviewAsync(review,new("economy_report","Old admitted review","Observe"),default));
        Assert.Equal("Pending",(await db.AlmightyProposals.SingleAsync(p=>p.VillageId==world.Id)).Status);
        Assert.Equal("Propose",(await db.AlmightyStates.SingleAsync(s=>s.VillageId==world.Id)).Mode);
    }
}
