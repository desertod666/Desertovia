using System.Security.Cryptography;
using System.Text.Json;
using Desertod.Domain.Combat;

namespace Desertod.Domain.Director;

/// <summary>Describes existing owner-approved handlers; it never grants autonomous authority.</summary>
public sealed record AlmightyPermission(string OptionId,string Kind,bool RequiresOwnerApproval,bool Autonomous,
    EnemyKind? Enemy=null,string? Sector=null,string? Composition=null);

public sealed record AlmightyCapability(string Kind,bool RequiresOwnerApproval,bool Autonomous);

public sealed record AlmightyObservedWorld(AlmightyFacts Facts,RaidReadiness Readiness,
    IReadOnlyDictionary<string,int> Stock,string Mode,int VigorEnergy,int VigorCooldownMinutes);

public static class AlmightyPermissions
{
    public const int SnapshotVersion=1;
    public static IReadOnlyList<string> ControllableKinds { get; } = Array.AsReadOnly<string>(["advice","notice","rest","raid","dungeon","vigor"]);
    public static readonly string[] RevisionScope=["facts","stock","readiness","mode","vigor","offeredPermissions"];

    public static string? KindForOptionId(string id) => id switch
    {
        "none"=>"observe", "economy_report"=>"advice", "work_notice"=>"notice", "rest"=>"rest", "dungeon"=>"dungeon",
        "vigor_stronger" or "vigor_weaker" or "vigor_frequent" or "vigor_rare"=>"vigor",
        _=>AlmightyPolicy.RaidOptionIds().Contains(id,StringComparer.Ordinal)?"raid":null,
    };

    public static bool IsEnabled(string optionId,IReadOnlyCollection<string> disabledKinds) =>
        KindForOptionId(optionId) is {} kind && (kind=="observe" || !disabledKinds.Contains(kind,StringComparer.Ordinal));

    public static List<AlmightyOption> Filter(IReadOnlyList<AlmightyOption> options,IReadOnlyCollection<string> disabledKinds)
    {
        Describe(options); // A category switch never turns unknown parameters into an admitted action.
        return options.Where(o=>IsEnabled(o.Id,disabledKinds)).ToList();
    }

    // Parameters already live in options; repeating every raid profile would waste bridge payload.
    public static IReadOnlyList<AlmightyCapability> Catalog(IReadOnlyList<AlmightyOption> options)=>
        Describe(options).Select(p=>new AlmightyCapability(p.Kind,p.RequiresOwnerApproval,p.Autonomous))
            .Distinct().OrderBy(p=>p.Kind,StringComparer.Ordinal).ToArray();

    public static IReadOnlyList<AlmightyPermission> Describe(IReadOnlyList<AlmightyOption> options)
    {
        if(options.Any(o=>o is null||string.IsNullOrWhiteSpace(o.Id)))
            throw new ArgumentException("Missing director option identifier.",nameof(options));
        if(options.Select(o=>o.Id).Distinct(StringComparer.Ordinal).Count()!=options.Count)
            throw new ArgumentException("Ambiguous director option identifiers.",nameof(options));
        return options.Select(Describe).ToArray();
    }

    private static AlmightyPermission Describe(AlmightyOption option)
    {
        string kind;
        if(option.Enemy is {} enemy)
        {
            if(!Enum.IsDefined(enemy)||!EnemyCatalog.Get(enemy).Undead||EnemyCatalog.Get(enemy).DungeonOnly)
                throw new ArgumentException("Unknown director raid enemy.",nameof(option));
            var prefix=enemy.ToString().ToLowerInvariant()+"s";
            var valid=option.Sector is null
                ?option.Id==prefix+"_small"&&option.Composition is null
                :RaidTactics.Sectors.Contains(option.Sector)&&option.Composition is {} composition
                    &&RaidBalance.Compositions.Contains(composition)
                    &&option.Id==prefix+"_"+option.Sector+(composition=="mixed"?"":"_"+composition);
            if(!valid)throw new ArgumentException("Unrecognised director raid parameters.",nameof(option));
            kind="raid";
        }
        else
        {
            if(option.Sector is not null||option.Composition is not null)
                throw new ArgumentException("Non-raid options cannot carry raid parameters.",nameof(option));
            kind=option.Id switch
            {
                "none"=>"observe",
                "economy_report"=>"advice",
                "work_notice"=>"notice",
                "rest"=>"rest",
                "dungeon"=>"dungeon",
                "vigor_stronger" or "vigor_weaker" or "vigor_frequent" or "vigor_rare"=>"vigor",
                _=>throw new ArgumentException("Unknown director permission.",nameof(option)),
            };
        }
        return new(option.Id,kind,option.Id!="none",false,option.Enemy,option.Sector,option.Composition);
    }

    /// <summary>A digest of named observed evidence, not a revision of every row in the world.</summary>
    public static string Revision(AlmightyObservedWorld world,IReadOnlyList<AlmightyOption> options)
    {
        var canonical=new
        {
            world.Facts,world.Readiness,
            stock=world.Stock.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new{resource=p.Key,amount=p.Value}),
            world.Mode,world.VigorEnergy,world.VigorCooldownMinutes,
            permissions=Describe(options).OrderBy(p=>p.OptionId,StringComparer.Ordinal),
        };
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical)));
    }
}
