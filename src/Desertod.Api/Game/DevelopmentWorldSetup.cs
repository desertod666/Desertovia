using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

/// <summary>Interactive experiments can keep their worlds; disposable CI demos retain the original reset behavior.</summary>
public static class DevelopmentWorldSetup
{
    public static async Task EnsureAsync(DesertodDbContext db,TimeProvider clock,bool preserveExisting,CancellationToken ct)
    {
        if(!preserveExisting || !await db.Villages.AnyAsync(v=>v.Slug==DemoVillage.Slug,ct))
            await DemoVillage.SeedAsync(db,clock,ct);
        if(!preserveExisting || !await db.Villages.AnyAsync(v=>v.Slug==BuiltVillage.Slug,ct))
            await BuiltVillage.SeedAsync(db,clock,ct);
    }
}
