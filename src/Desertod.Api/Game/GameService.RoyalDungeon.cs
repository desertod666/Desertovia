using Desertod.Domain;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed partial class GameService
{
    public async Task<IReadOnlyList<HeroClassOfferDto>> GetRoyalHeroClassesAsync(string slug,Guid actor,CancellationToken ct)
    {
        await using var db=await contextFactory.CreateDbContextAsync(ct);
        var village=await db.Villages.AsNoTracking().SingleAsync(v=>v.Slug==slug,ct);
        if(village.OwnerAccountId!=actor)throw new UnauthorizedAccessException();
        var run=await db.DungeonRuns.AsNoTracking().SingleOrDefaultAsync(r=>r.VillageId==village.Id&&r.Kind==DungeonRunKind.Group&&r.Phase==DungeonPhase.Gathering,ct);
        if(run?.CombatRulesVersion!=CombatantRules.CurrentVersion)return [];
        var residents=await db.Citizens.AsNoTracking().Where(c=>c.VillageId==village.Id&&c.AccountId==actor).ToListAsync(ct);
        var body=DungeonRules.RoyalBody(run,village,residents,time.GetUtcNow());
        return body is null?[]:ClassOffers(body.Citizen,village,run.Tsar);
    }

    public async Task<VillageStateDto> PickRoyalHeroClassAsync(string slug,Guid actor,HeroClass heroClass,CancellationToken ct)=>
        await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>
        {
            var village=await LoadVillageAsync(db,slug,ct);
            if(village.OwnerAccountId!=actor)throw new UnauthorizedAccessException();
            var run=await RequireGatheringAsync(db,village.Id,ct);
            var residents=await db.Citizens.Where(c=>c.VillageId==village.Id&&c.AccountId==actor).ToListAsync(ct);
            await SyncHospitalAsync(db,village.Id,ct);
            await RequireNoRaidActorAsync(db,village.Id,actor,residents.FirstOrDefault()?.Id,ct);
            var now=time.GetUtcNow();DungeonRules.PickRoyalClass(run,village,residents,heroClass,now);
            await db.SaveChangesAsync(ct);changes.Mark(village.Id);
            foreach(var resident in residents)await notifier.CitizenUpdated(slug,resident.ToSummaryDto(now));
            return await BuildStateAsync(db,village,now,ct);
        },ct);

    private static IReadOnlyList<HeroClassOfferDto> ClassOffers(Citizen citizen,Village village,DungeonHero? hero)
    {
        var leather=village.Stock(ResourceKind.LeatherArmor)+citizen.Carried(ResourceKind.LeatherArmor)>0||citizen.Armor==ResourceKind.LeatherArmor;
        return DungeonRules.AvailableClasses(citizen,village,hero).Select(HeroClassCatalog.Get)
            .Select(d=>new HeroClassOfferDto(d.Class,Math.Round(DungeonRules.Health(citizen,d),1),Math.Round(DungeonRules.Damage(citizen,d),1),d.Range,d.Weapon,d.Armor??(leather?ResourceKind.LeatherArmor:null))).ToList();
    }
}
