using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;

namespace Desertod.Domain.Combat;

/// <summary>The ruler's ordinary, initially untrained body when no resident profile exists. No political power bonus.</summary>
public sealed class RoyalCombatState
{
    public StatBlock Stats { get; set; } = new();
    public List<SkillProgress> Skills { get; set; } = [];
    public List<CarriedGood> Load { get; set; } = [];
    public ResourceKind? Weapon { get; set; }
    public ResourceKind? Armor { get; set; }
    public int WeaponSharpen { get; set; }
    public int ArrowSharpen { get; set; }
    public int Arrows { get; set; }
    public int Renown { get; set; }

    internal Citizen Bind(Village village, DateTimeOffset now) => new()
    {
        Id=village.OwnerAccountId, AccountId=village.OwnerAccountId, VillageId=village.Id, Name="Ruler",
        Stats=Stats, Skills=Skills, Load=Load, Weapon=Weapon, Armor=Armor, WeaponSharpen=WeaponSharpen,
        ArrowSharpen=ArrowSharpen, Arrows=Arrows, Renown=Renown, CreatedAt=village.CreatedAt,
        CycleSettled=true, RestState=village.TsarRestState, InjuredUntil=village.TsarInjuredUntil,
        HospitalizedAt=village.TsarHospitalizedAt, WoundedAt=village.TsarWoundedAt,
        StoredEnergy=Energy.Maximum(Stats.Endurance), EnergyUpdatedAt=now,
    };

    internal void Capture(Citizen body)
    {
        Stats=body.Stats;Skills=body.Skills;Load=body.Load;
        Weapon=body.Weapon;Armor=body.Armor;WeaponSharpen=body.WeaponSharpen;
        ArrowSharpen=body.ArrowSharpen;Arrows=body.Arrows;Renown=body.Renown;
    }
}

/// <summary>Reuses all existing resident equipment/training rules; never inserts a synthetic citizen into the database.</summary>
public sealed class RoyalCombatBody
{
    public Citizen Citizen { get; }
    private readonly RoyalCombatState? state;
    private RoyalCombatBody(Citizen citizen,RoyalCombatState? storage){Citizen=citizen;state=storage;}

    public static RoyalCombatBody Resolve(Village village,IEnumerable<Citizen> residents,DateTimeOffset now)
    {
        if(residents.FirstOrDefault(c=>c.AccountId==village.OwnerAccountId) is {} citizen)return new(citizen,null);
        var state=village.RoyalCombat??=new();
        return new(state.Bind(village,now),state);
    }
    /// <summary>The admitted body keeps its source even if the account's resident membership changes later.</summary>
    public static RoyalCombatBody? ForFighter(Village village,RaidFighter fighter,IEnumerable<Citizen> residents,DateTimeOffset now)
    {
        if(fighter.CitizenId!=Guid.Empty)
        {
            var resident=residents.FirstOrDefault(c=>c.Id==fighter.CitizenId&&c.AccountId==village.OwnerAccountId);
            return resident is null?null:new(resident,null);
        }
        var state=village.RoyalCombat??=new();return new(state.Bind(village,now),state);
    }
    public void Commit()=>state?.Capture(Citizen);
    public static Citizen Preview(Village village,IEnumerable<Citizen> residents,DateTimeOffset now)=>
        residents.FirstOrDefault(c=>c.AccountId==village.OwnerAccountId)??(village.RoyalCombat??new RoyalCombatState()).Bind(village,now);
}

public static class CombatantRules
{
    public const int CurrentVersion=1;
    public static double RaidHealth(Citizen body)=>32+body.Stats.Endurance*4;
    public static WeaponDefinition? LiveWeapon(Citizen body)=>ArmoryCatalog.Weapon(body.Weapon) is {} weapon
        &&(weapon.Arrows==0||body.Arrows>0)?weapon:null;
    public static double LivePower(Citizen body)=>CombatRules.Power(body,LiveWeapon(body));
}

public static class RoyalInjury
{
    public static void Refresh(Village village,DateTimeOffset now,IEnumerable<Citizen>? residents=null)
    {
        // A legacy resident alias represents the same wound, not a second rescue reward.
        var alias=residents?.FirstOrDefault(c=>c.AccountId==village.OwnerAccountId&&c.WoundedAt is not null&&c.WoundedAt==village.TsarWoundedAt);
        if(alias?.InjuredUntil is {} residentUntil&&village.TsarInjuredUntil is {} royalUntil)
        {
            var until=residentUntil<royalUntil?residentUntil:royalUntil;
            alias.InjuredUntil=until;village.TsarInjuredUntil=until;alias.RescueRewardEligible=false;
        }
        if(village.TsarInjuredUntil is {} expired&&expired<=now)Clear(village);
    }
    public static void Clear(Village village)
    {
        village.TsarInjuredUntil=null;village.TsarWoundedAt=null;village.TsarHospitalizedAt=null;
        village.TsarRescueByCitizenId=null;village.TsarRescueRequestedAt=null;village.TsarRescueRewardEligible=false;
    }
    public static void Heal(Village village,IEnumerable<Citizen> residents,DateTimeOffset now)
    {
        var body=RoyalCombatBody.Resolve(village,residents,now);
        if(village.TsarInjuredUntil>now&&(body.Citizen.InjuredUntil is null||body.Citizen.InjuredUntil<village.TsarInjuredUntil))
            body.Citizen.InjuredUntil=village.TsarInjuredUntil;
        CombatRules.Heal(body.Citizen,village,now);
        Clear(village);body.Commit();
    }
}
