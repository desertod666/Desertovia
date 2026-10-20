using Desertod.Api.Game;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DevelopmentWorldPersistenceTests(DesertodApiFactory factory)
{
    [Fact]
    public async Task Interactive_preservation_keeps_world_citizen_appointments_and_crop_identity()
    {
        var dbFactory=factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>();
        await using var db=await dbFactory.CreateDbContextAsync();
        // Roll back the fixture edits so other cases keep their original demo state.
        await using var transaction=await db.Database.BeginTransactionAsync();
        var world=await db.Villages.SingleAsync(v=>v.Slug==BuiltVillage.Slug);
        var citizen=await db.Citizens.FirstAsync(c=>c.VillageId==world.Id);
        citizen.IsFarmer=true;world.FarmerAdmissionEnabled=true;world.Description="Persistent interactive farm";
        await db.SaveChangesAsync();var id=world.Id;var person=citizen.Id;
        var fields=world.Fields.Select(b=>(b.PlotId,b.Index,b.Crop,b.GenerationId)).ToArray();
        await DevelopmentWorldSetup.EnsureAsync(db,factory.Clock,true,default);
        db.ChangeTracker.Clear();
        var saved=await db.Villages.SingleAsync(v=>v.Slug==BuiltVillage.Slug);
        Assert.Equal(id,saved.Id);Assert.True(saved.FarmerAdmissionEnabled);Assert.Equal("Persistent interactive farm",saved.Description);
        Assert.True((await db.Citizens.SingleAsync(c=>c.Id==person)).IsFarmer);
        Assert.Equal(fields,saved.Fields.Select(b=>(b.PlotId,b.Index,b.Crop,b.GenerationId)).ToArray());
        await transaction.RollbackAsync();
    }
}
