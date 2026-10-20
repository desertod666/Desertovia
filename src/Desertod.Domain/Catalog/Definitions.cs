using Desertod.Domain.Buildings;
using Desertod.Domain.Entities;
using Desertod.Domain.Farming;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.World;

namespace Desertod.Domain.Catalog;

public enum RoleTier
{
    Basic = 1,
    Journeyman = 2,
    Master = 3,
}

public enum TaskEffect
{
    /// <summary>Produces resources into village storage.</summary>
    Resources,

    /// <summary>Raises village morale.</summary>
    Morale,

    /// <summary>Moves the resource in <see cref="TaskDefinition.Outputs"/> from storage into the active project.</summary>
    Deliver,

    /// <summary>Surveys the active project (first phase).</summary>
    Survey,

    /// <summary>Construction labor on the active project; needs a supervising Engineer.</summary>
    Build,

    /// <summary>Lets laborers build while the cycle runs.</summary>
    Supervise,

    /// <summary>Random loot from outside the village.</summary>
    Expedition,

    /// <summary>Walks around the land; common goods are found on the way by chance.</summary>
    Roam,

    /// <summary>Carries the citizen's load to storage, as far as there is room.</summary>
    Unload,

    /// <summary>Sows <see cref="TaskDefinition.Crop"/> in an empty farm bed.</summary>
    Sow,

    /// <summary>Carries water from the river to the thirstiest farm beds; they grow faster while wet.</summary>
    Water,

    /// <summary>Gathers a ripe farm bed into storage.</summary>
    Harvest,

    /// <summary>Watches over the camp; raids striking while the cycle runs meet the watchers. See <see cref="Combat.RaidRules"/>.</summary>
    Guard,

    /// <summary>Mends the most damaged building by <see cref="TaskDefinition.BaseYield"/> points.</summary>
    Repair,

    /// <summary>Needs streamer events or other villages, which are not implemented yet.</summary>
    NotAvailableYet,

    /// <summary>Grinds one weapon a single step sharper at the smithy and returns it.</summary>
    Sharpen,
}

public sealed record StatRequirement(Stat Stat, int Minimum);

/// <summary>
/// A profession: a title earned by a level in a craft, and the group its tasks are listed under.
/// It does not gate the tasks; each task needs its own level of the craft.
/// </summary>
/// <param name="Skill">Craft of the profession; null for laborers, which everyone is.</param>
/// <param name="SkillLevel">Level of <paramref name="Skill"/> that earns the title.</param>
/// <param name="Technology">The village must have discovered it before anyone can work in this role.</param>
public sealed record RoleDefinition(
    string Id,
    string Name,
    RoleTier Tier,
    Skill? Skill,
    int SkillLevel,
    string? Technology = null)
{
    public bool IsQualified(Citizen citizen) => Skill is not { } skill || citizen.SkillLevel(skill) >= SkillLevel;
}

/// <param name="Durability">Remaining uses of a tool. 0 for every other good.</param>
/// <param name="MaxDurability">Uses of a fresh tool. 0 when <paramref name="Resource"/> is not a tool.</param>
public sealed record ResourceAmount(ResourceKind Resource, int Amount, int Sharpen = 0, int Durability = 0, int MaxDurability = 0);

/// <param name="Animals">Animals caught per cycle at a stat factor of 1 while the herd is abundant; the task's outputs are for that catch.</param>
public sealed record HuntDefinition(AnimalKind Prey, int Animals);

/// <summary>Beasts that may jump at a citizen out in the wild; see <see cref="Combat.CombatRules.Ambush"/>.</summary>
/// <param name="Chance">Chance per cycle in daylight; higher at night.</param>
/// <param name="Enemies">Who may attack, each as likely as its ambush weight.</param>
public sealed record DangerDefinition(double Chance, IReadOnlyList<Combat.EnemyKind> Enemies);

/// <param name="RequiredBuilding">The task can be done only once the village has this building.</param>
/// <param name="WorkSeconds">Time spent working; walking time is added on top.</param>
/// <param name="Hunt">Set for hunting: the catch comes out of the village's herd of this animal.</param>
/// <param name="RequiredProject">The task can be done only once the village has finished this project.</param>
/// <param name="RequiredTechnology">The task can be done only once the village has discovered this technology.</param>
/// <param name="Crop">Set for sowing: the crop put in the bed.</param>
/// <param name="SkillLevel">
/// Level of <paramref name="Skill"/> needed to do the task. Levels above it make the task yield more and take less time;
/// far above it, the task teaches little (the challenge rule).
/// </param>
/// <param name="Skill">Craft the task trains.</param>
/// <param name="Danger">Set for work out in the wild, where beasts may attack.</param>
public sealed record TaskDefinition(
    string Id,
    string Name,
    string RoleId,
    int SkillLevel,
    Stat Primary,
    Stat Secondary,
    Skill Skill,
    TaskEffect Effect,
    double BaseYield,
    IReadOnlyList<ResourceAmount> Outputs,
    IReadOnlyList<ResourceAmount> Inputs,
    string Workplace,
    BuildingKind? RequiredBuilding = null,
    int WorkSeconds = GameRules.GatherSeconds,
    HuntDefinition? Hunt = null,
    string? RequiredProject = null,
    string? RequiredTechnology = null,
    CropKind? Crop = null,
    DangerDefinition? Danger = null);
