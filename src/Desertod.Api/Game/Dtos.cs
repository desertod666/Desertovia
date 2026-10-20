using Desertod.Domain;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Farming;
using Desertod.Domain.Projects;
using Desertod.Domain.Quests;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.Storage;
using Desertod.Domain.Technologies;
using Desertod.Domain.Weather;
using Desertod.Domain.World;

namespace Desertod.Api.Game;

/// <param name="Capacity">Amount plus free room in all storage that accepts the good; null when unlimited.</param>
public sealed record ResourceDto(ResourceKind Kind, int Amount, int? Capacity);

/// <param name="Fill">0 to 1; drawn as stacks on the building, the only storage hint the Tsar gets.</param>
public sealed record StorageSlotDto(ResourceKind Kind, double Fill);

/// <param name="Damage">Harm done by raiders, 0 to <see cref="CombatRules.MaximumDamage"/>.</param>
public sealed record StorageDto(StorageKind Kind, string PlotId, IReadOnlyList<ResourceAmount> Goods, IReadOnlyList<StorageSlotDto> Slots, int Damage)
{
    public int DisplayNumber { get; init; }
    public int Number { get; init; }
    public Guid? OwnerAccountId { get; init; }
    public DateTimeOffset? RuinedAt { get; init; }
    public int ResourceLimit => StorageLimits.PerResource(Kind);
    public int? TotalLimit => StorageLimits.Total(Kind);
}

/// <param name="Damage">Harm done by raiders, 0 to <see cref="CombatRules.MaximumDamage"/>.</param>
public sealed record StructureDto(BuildingKind Kind, string PlotId, int Damage, int Level = 1) { public int Number { get; init; } public int DisplayNumber { get; init; } public Guid? OwnerAccountId { get; init; } public DateTimeOffset? RuinedAt { get; init; } }

/// <summary>An attack on the way, or a fought one kept for the record.</summary>
/// <param name="AnnouncedAt">Players see an attack on the way from then on; clients hide it before.</param>
public sealed record RaidDto(
    Guid Id,
    RaidSource Source,
    EnemyKind Enemy,
    int Count,
    int Wave,
    int Waves,
    DateTimeOffset AnnouncedAt,
    DateTimeOffset StrikesAt,
    RaidOutcome Outcome,
    DateTimeOffset? ResolvedAt,
    int Killed,
    int Defenders,
    int Injured,
    int DamagedBuildings,
    IReadOnlyList<ResourceAmount> Stolen,
    IReadOnlyList<ResourceAmount> Loot,
    string? EntrySector = null, IReadOnlyList<RaidWaypoint>? ApproachPath = null) { public int EngineVersion { get; init; } public RaidSummary? Summary { get; init; } }

/// <param name="Room">Work a labor job can take right now; it cannot run ahead of the delivered material.</param>
public sealed record SiteJobDto(string JobId, int Done, int Required, int Room, int Reserved = 0);

/// <param name="CrewNeeded">Different citizens the stage needs, scaled down for small streams.</param>
/// <param name="Hands">Different citizens who already worked on the stage.</param>
public sealed record SiteStageDto(string Id, int CrewNeeded, int Hands, IReadOnlyList<SiteJobDto> Jobs);

public sealed record SiteDto(BuildingKind Kind, string PlotId, int Stage, double Progress, IReadOnlyList<SiteStageDto> Stages, int TargetLevel = 1)
{
    public int Damage { get; init; }
    public DateTimeOffset? RuinedAt { get; init; }
    public int DisplayNumber { get; init; }
    public int Number { get; init; }
    public Guid? OwnerAccountId { get; init; }
    public Guid? PlacedByAccountId { get; init; }
    public string? OwnerName { get; init; }
    public DateTimeOffset PlacedAt { get; init; }
    public bool BoundaryConflict { get; init; }
    public bool BoundaryApproved { get; init; }
    public bool HasWork { get; init; }
    public string? Operation { get; init; }
    public bool PublicDemolition { get; init; }
}

/// <param name="Count">Animals now; the map shows a share of them, the Tsar never sees the number.</param>
public sealed record HerdDto(AnimalKind Kind, int Count, int Capacity, string Habitat);

public sealed record TreeDto(int X, int Y);

public sealed record RavineSpanDto(int X, int Top, int Height);

public sealed record MountainPassDto(int X, int Y, bool Horizontal);

/// <param name="Cleared">Worked away by citizens: walkers pass and the map shows bare ground.</param>
public sealed record ObstacleDto(ObstacleKind Kind, int X, int Y, bool Cleared);

/// <summary>The ravine, mountain features and obstacles of the wild land; the client shapes the map around them.</summary>
public sealed record WildsDto(
    IReadOnlyList<RavineSpanDto> Ravine,
    IReadOnlyList<MountainPassDto> Passes,
    IReadOnlyList<ObstacleDto> Obstacles,
    int CaveX,
    int CaveY,
    int LookoutX,
    int LookoutY);

/// <param name="Growth">0 when sown, 1 when ripe; 0 for an empty bed.</param>
/// <param name="WateredUntil">The bed is wet until then.</param>
/// <param name="Thirsty">Growing and drying out soon: watering reaches it.</param>
public sealed record FieldBedDto(string PlotId, int Index, CropKind? Crop, double Growth, DateTimeOffset? WateredUntil, bool Thirsty)
{ [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.Never)] public Guid GenerationId { get; init; } }

/// <param name="Available">Not discovered and every technology it builds on is; the Tsar or a Governor can put it to the vote.</param>
/// <param name="CrewNeeded">Different citizens the research needs, scaled down for small streams.</param>
public sealed record TechnologyStateDto(
    string Id,
    bool Discovered,
    bool Available,
    int Done,
    int Required,
    int Hands,
    int CrewNeeded)
{
    public int KnowledgeWork {get;init;}
    public int KnowledgeCost {get;init;}
}

public sealed record PollOptionDto(string TechnologyId, int Votes);

public sealed record PollBallotDto(Guid CitizenId, string TechnologyId);

/// <param name="Electorate">Active citizens who are present now, or when the poll closed.</param>
/// <param name="VotesNeeded">Votes that close the poll: more than half of <paramref name="Electorate"/>.</param>
/// <param name="Winner">Null while open, or when the poll was cancelled.</param>
public sealed record TechnologyPollDto(
    Guid Id,
    IReadOnlyList<PollOptionDto> Options,
    IReadOnlyList<PollBallotDto> Ballots,
    int Electorate,
    int VotesNeeded,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    string? Winner);

/// <param name="SaysRain">What the Synoptic announced; the weather it brought stays secret until the period comes.</param>
public sealed record ForecastDto(Guid CitizenId, bool SaysRain, DateTimeOffset MadeAt);

/// <summary>The sky of the current period: a daytime or a night, see <see cref="DayCycle"/>.</summary>
/// <param name="Kind">Weather of the current period.</param>
/// <param name="Raining">Rain or a thunderstorm: the rain effects apply.</param>
/// <param name="Night">The current period is the night half of the day.</param>
/// <param name="PeriodEndsAt">When the current period ends and the next one's weather comes.</param>
/// <param name="Current">The forecast that was made for the current period, if any.</param>
/// <param name="Next">The forecast for the next period; null until a Synoptic makes one.</param>
public sealed record WeatherDto(
    WeatherKind Kind,
    bool Raining,
    bool Night,
    DateTimeOffset PeriodStartedAt,
    DateTimeOffset PeriodEndsAt,
    ForecastDto? Current,
    ForecastDto? Next);

/// <param name="Trees">
/// Standing trees on the camp land, those waiting to be felled on construction sites included;
/// the client draws the wild beyond it by itself.
/// </param>
/// <param name="CurrentResearch">The technology the last vote chose; null until a vote chooses an undiscovered one.</param>
/// <param name="Poll">The open vote on the next discovery, or the last closed one.</param>
/// <param name="Fields">Beds of every farm.</param>
/// <param name="Tsar">The village owner's figure on the map.</param>
/// <param name="Dungeon">The dungeon run under way, or one that ended a moment ago; see <see cref="DungeonRules.ShownAfterEnd"/>.</param>
/// <param name="Explored">
/// Land the tribe has seen, walks under way included, as base64: one bit per tile of <see cref="WorldMap.Land"/>,
/// row by row from the top-left, lowest bit first.
/// </param>
public sealed record VillageStateDto(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    bool IsPrivate,
    double Morale,
    IReadOnlyList<ResourceDto> Resources,
    IReadOnlyList<StorageDto> Storages,
    IReadOnlyList<StructureDto> Structures,
    IReadOnlyList<SiteDto> Sites,
    ProjectDto? Project,
    IReadOnlyList<string> CompletedProjects,
    IReadOnlyList<QuestDto> Quests,
    IReadOnlyList<HerdDto> Wildlife,
    IReadOnlyList<TreeDto> Trees,
    Era Era,
    string? CurrentResearch,
    IReadOnlyList<TechnologyStateDto> Technologies,
    TechnologyPollDto? Poll,
    int GoldVeinRemaining,
    IReadOnlyList<FieldBedDto> Fields,
    WeatherDto Weather,
    TsarDto Tsar,
    string Explored,
    IReadOnlyList<RaidDto> Raids,
    WildsDto? Wilds,
    DungeonDto? Dungeon)
{
    public DateTimeOffset SnapshotAt { get; init; }
    public bool WorkContractsEnabled { get; init; }
    public bool FarmerAdmissionEnabled { get; init; }
    public TileArea KingdomArea { get; init; } = WorldMap.BuildArea;
    public FortificationQuote? FortificationUpgrade { get; init; }
    public int CastleLevel { get; init; }
    public TileArea? NextKingdomArea { get; init; }
    public DateTimeOffset? DefeatedAt { get; init; }
    public IReadOnlyList<RelocationDto> Relocations { get; init; } = [];
    public IReadOnlyList<CivicOrder> CivicOrders { get; init; } = [];
    public IReadOnlyList<HousingDto> Housing { get; init; } = [];
    public bool BridgeOpened { get; init; }
    public int BridgeDamage { get; init; }
    public DateTimeOffset? BridgeRuinedAt { get; init; }
    public IReadOnlyList<Fortification> Fortifications { get; init; } = [];
    public IReadOnlyList<RaidBattleDto> Battles { get; init; } = [];
}
public sealed record RelocationDto(string PlotId,int Residents,int Available);
public sealed record HousingDto(string PlotId, int Beds, int Occupied, int Level, int Number = 0, Guid? OwnerAccountId = null, string? OwnerName = null, DateTimeOffset? BuiltAt = null);

/// <param name="Appearance">Null until the owner creates their character.</param>
/// <param name="Spot">Where the Tsar walked to; null while they stand at the campfire.</param>
public sealed record TsarDto(string Name, Appearance? Appearance, TilePoint? Spot, DateTimeOffset? InjuredUntil = null)
{
    public RestState RestState { get; init; }
    public DateTimeOffset? RestRequestedAt { get; init; }
    public Guid? AccountId { get; init; }
    public DateTimeOffset? HospitalizedAt { get; init; }
    public Guid? RescueByCitizenId { get; init; }
    public DateTimeOffset? RescueRequestedAt { get; init; }
}

/// <param name="Objectives">Rulers' objectives (planning) carry no numbers; citizen objectives carry progress.</param>
public sealed record QuestDto(Guid Id, QuestKind Kind, ResourceKind? Resource, DateTimeOffset StartedAt, IReadOnlyList<QuestObjective> Objectives);

public sealed record ProjectMaterialDto(ResourceKind Kind, int Delivered, int Required);

public sealed record ProjectDto(
    Guid Id,
    string DefinitionId,
    string Name,
    string Description,
    ProjectPhase Phase,
    double Progress,
    int SurveyDone,
    int SurveyRequired,
    int BuildDone,
    int BuildLimit,
    int BuildRequired,
    IReadOnlyList<ProjectMaterialDto> Materials,
    int ActiveCitizens,
    int SupervisionCapacity,
    int BuildersWorking,
    bool NeedsEngineer)
{
    public int BridgeStage { get; init; }
    public string? StageName { get; init; }
}

/// <param name="Subtitle">Written by the Tsar, shown under the name on the map.</param>
/// <param name="IsGovernor">Appointed by the Tsar: may plan buildings and call votes.</param>
/// <param name="SiteId">Plot the citizen works on: a construction site, or the building where they research.</param>
/// <param name="Spot">Spot on the map the citizen gathers at, picked by the player.</param>
/// <param name="IdleSpot">Where the player sent the citizen to stand while not working.</param>
/// <param name="InjuredUntil">Wounded until then; null when not wounded.</param>
/// <param name="Weapon">Weapon taken from the armory for the current task.</param>
public sealed record CitizenSummaryDto(
    Guid Id,
    string Name,
    string? Subtitle,
    bool IsGovernor,
    Appearance? Appearance,
    string TitleRoleId,
    string? TaskId,
    string? Workplace,
    string? SiteId,
    TilePoint? Spot,
    TilePoint? IdleSpot,
    WorkState State,
    DateTimeOffset? CycleStartedAt,
    DateTimeOffset? CycleEndsAt,
    IReadOnlyList<ResourceKind> Carrying,
    DateTimeOffset? InjuredUntil,
    ResourceKind? Weapon,
    ExternalRecovery? Recovery = null)
{
    public bool IsFarmer { get; init; }
    public bool CanManageFarm { get; init; }
    public RestState RestState { get; init; }
    public int WeaponSharpen { get; init; }
    public DateTimeOffset? HospitalizedAt { get; init; }
    public Guid? RescueByCitizenId { get; init; }
    public Guid? RescuePatientId { get; init; }
    public RescueJourney? RescueJourney { get; init; }
    public DateTimeOffset? RescueRequestedAt { get; init; }
    public string? DefenseSector { get; init; }
    public DateTimeOffset? DefenseReadyAt { get; init; }
}

/// <param name="InviteCode">Only sent to the owner of a private village.</param>
/// <param name="AwayCitizenIds">Citizens who are away, see <see cref="CitizenPresence"/>: hidden from the map while idle.</param>
public sealed record VillageDto(
    VillageStateDto State,
    bool IsOwner,
    string? InviteCode,
    IReadOnlyList<CitizenSummaryDto> Citizens,
    IReadOnlyList<Guid> AwayCitizenIds);

public sealed record VillageListItemDto(string Slug, string Name, bool IsOwner);

public sealed record PublicVillageDto(string Slug, string Name, string? Description, int Citizens);

public sealed record InviteDto(string InviteCode);

public sealed record StatDto(Stat Stat, int Level, int Experience, int ExperienceToNext);

/// <param name="ExperienceToNext">0 at the maximum level.</param>
/// <param name="NextLevel">Stats the next level needs; empty at the maximum level.</param>
/// <param name="WaitsForStats">The experience for the next level is there, but the stats are not; it comes once they are.</param>
public sealed record SkillDto(
    Skill Skill,
    int Level,
    int Experience,
    int ExperienceToNext,
    IReadOnlyList<StatRequirement> NextLevel,
    bool WaitsForStats);

/// <param name="SiteId">Plot of the construction site the citizen works on.</param>
/// <param name="Spot">Spot on the map the last task gathered at, picked by the player.</param>
/// <param name="TechnologyId">Technology the current cycle researches.</param>
/// <param name="Skills">Only the crafts the citizen has trained; any other is at level 0.</param>
/// <param name="RegenerationMultiplier">Shelter quality, halved while hungry and again in the rain under the trees.</param>
/// <param name="EnergyPerHour">Energy regained per hour at the current shelter, hunger and weather.</param>
/// <param name="ForecastReliability">Chance the weather turns out opposite to this citizen's forecast; null if not a Synoptic.</param>
/// <param name="DrinkReadyAt">When the next vigor drink may be drunk; null if never drunk.</param>
/// <param name="InjuredUntil">Wounded until then; null when not wounded.</param>
/// <param name="Power">Fight power with the weapon in hand; see <see cref="CombatRules.Power"/>.</param>
/// <param name="IsGovernor">Appointed by the Tsar: may plan buildings and call votes.</param>
public sealed record CitizenDto(
    Guid Id,
    string Name,
    string? Subtitle,
    bool IsGovernor,
    Appearance? Appearance,
    string TitleRoleId,
    CitizenStatus Status,
    IReadOnlyList<StatDto> Stats,
    int StatTotal,
    IReadOnlyList<SkillDto> Skills,
    int Renown,
    double Energy,
    double MaximumEnergy,
    int Streak,
    string? TaskId,
    string? SiteId,
    int? SiteStage,
    TilePoint? Spot,
    WorkState State,
    DateTimeOffset? CycleStartedAt,
    DateTimeOffset? CycleEndsAt,
    DateTimeOffset? GraceEndsAt,
    bool CycleTired,
    IReadOnlyList<ResourceAmount> Load,
    double ShelterQuality,
    bool IsHungry,
    int CyclesSinceMeal,
    double RegenerationMultiplier,
    double EnergyPerHour,
    DateTimeOffset ServerNow,
    string? TechnologyId,
    double? ForecastReliability,
    DateTimeOffset? DrinkReadyAt,
    DateTimeOffset? InjuredUntil,
    ResourceKind? Weapon,
    ResourceKind? Armor,
    int Arrows,
    double Power,
    ExternalRecovery? Recovery = null)
{
    public bool IsFarmer { get; init; }
    public bool CanManageFarm { get; init; }
    public RestState RestState { get; init; }
    public DateTimeOffset? RestRequestedAt { get; init; }
    public int WeaponSharpen { get; init; }
    public int ArrowSharpen { get; init; }
    public Guid AccountId { get; init; }
    public bool CyclePrivateOutput { get; init; }
    public Desertod.Domain.Economy.WorkOwnership CycleOwnership { get; init; }
    public Desertod.Domain.Economy.WorkPaymentMode CyclePaymentMode { get; init; }
    public IReadOnlyList<Desertod.Domain.Economy.PersonalGood> PersonalGoods { get; init; } = [];
    public IReadOnlyDictionary<string, double>? ActionEnergyCosts { get; init; }
    public string? DefenseSector { get; init; }
    public DateTimeOffset? DefenseReadyAt { get; init; }
    public ResourceKind? MealKind {get;init;}
    public DateTimeOffset? MealUntil {get;init;}
    public DateTimeOffset? MealReadyAt {get;init;}
    public int VigorEnergy { get; init; } = 8;
    public int VigorCooldownMinutes { get; init; } = 240;
    public string? HomePlotId { get; init; }
    public Guid? SpouseAccountId { get; init; }
    public string? SpouseName { get; init; }
    public DateTimeOffset? MarriedSince { get; init; }
    public DateTimeOffset? ExhaustedUntil { get; init; }
    public int ExhaustedCycles { get; init; }
}

public sealed record RulesDto(
    int CycleSeconds,
    int GraceSeconds,
    int StatMaximum,
    int TalentCap,
    double SecondsPerTile,
    int MaximumOpenSites,
    int CyclesPerMeal,
    int TakeLimit,
    double RepeatCostGrowth,
    int SkillMaximum,
    int SkillBonusLevels,
    double SkillYieldPerLevel,
    double SkillWorkTimePerLevel,
    double VigorDrinkEnergy,
    double SightRadius,
    double NightAmbushMultiplier,
    int MaximumDamage,
    double EnergyBase,
    double EnergyPerEndurance,
    double EnergyFullRegenerationHours,
    double EnergyCostPerCycle,
    double TiredYieldMultiplier,
    double HungryExperienceMultiplier,
    double HungryRegenerationMultiplier,
    double UnderTreesRegeneration,
    double RainUnderTreesRegeneration,
    double StreakPerRenewal,
    double StreakMaximumBonus,
    double MoraleMinimum,
    double MoraleMaximum);

/// <param name="DevTools">Development and local deployments: the client shows helpers such as the day-night toggle.</param>
public sealed record CatalogDto(
    IReadOnlyList<RoleDefinition> Roles,
    IReadOnlyList<SkillDefinition> Skills,
    IReadOnlyList<TaskDefinition> Tasks,
    IReadOnlyList<ProjectDefinition> Projects,
    IReadOnlyList<StorageDefinition> Storages,
    IReadOnlyList<BuildingDefinition> Buildings,
    IReadOnlyList<JobDefinition> Jobs,
    TileArea BuildArea,
    IReadOnlyList<TileArea> ReservedAreas,
    IReadOnlyDictionary<string, TilePoint> Places,
    IReadOnlyDictionary<QuestKind, QuestReward> QuestRewards,
    RulesDto Rules,
    IReadOnlyList<TechnologyDefinition> Technologies,
    IReadOnlyList<CropDefinition> Crops,
    IReadOnlyList<EnemyDefinition> Enemies,
    IReadOnlyList<WeaponDefinition> Weapons,
    IReadOnlyList<ArmorDefinition> Armors,
    IReadOnlyList<HeroClassDefinition> HeroClasses,
    IReadOnlyList<DungeonFoe> DungeonFoes,
    IReadOnlyList<DungeonInfoDto> Dungeons,
    bool DevTools);

public sealed record CreateVillageRequest(string Name, string? Description = null, bool IsPrivate = false);

/// <param name="InviteCode">Needed only for private villages.</param>
public sealed record JoinVillageRequest(string? InviteCode = null);

/// <param name="Spot">Where on the map to gather, for tasks that allow it; null works at the task's workplace.</param>
public sealed record StartTaskRequest(string TaskId, TilePoint? Spot = null, string? WorkplacePlotId = null,
    Desertod.Domain.Economy.WorkOwnership? Ownership = null, Guid? WorkOrderId = null, Guid? RequestId = null,
    Desertod.Domain.Farming.FarmWorkTarget? FarmTarget = null);

/// <param name="Spot">Where on the land the idle citizen walks to and waits.</param>
public sealed record GoToRequest(TilePoint Spot);

/// <param name="PlotId">The storage building to take from.</param>
/// <param name="Amount">How much to take; less is taken when the building has less or the hands are nearly full.</param>
public sealed record TakeRequest(string PlotId, ResourceKind Resource, int Amount = StorageRules.TakeLimit, int? Sharpen = null, int? Durability = null);

public sealed record StartJobRequest(string PlotId, int Stage, string JobId);

/// <param name="X">Top-left tile of the building.</param>
public sealed record PlaceBlueprintRequest(BuildingKind Kind, int X, int Y);

/// <param name="TechnologyIds">Available technologies put to the vote.</param>
public sealed record OpenPollRequest(IReadOnlyList<string> TechnologyIds);

public sealed record VoteRequest(string TechnologyId);

/// <param name="Subtitle">Null or blank removes the subtitle.</param>
public sealed record SetSubtitleRequest(string? Subtitle);

/// <param name="IsGovernor">True appoints the citizen Governor, false dismisses them.</param>
public sealed record SetGovernorRequest(bool IsGovernor);
public sealed record FarmAccessRequest(Guid RequestId, bool? IsFarmer = null, bool? CanManageFarm = null);
public sealed record FarmAdmissionRequest(Guid RequestId);

/// <param name="Rain">The Synoptic announces rain for the next day or night; false announces a clear sky.</param>
public sealed record ForecastRequest(bool Rain);

/// <param name="Enemy">Zombies or skeletons.</param>
public sealed record CallUndeadRequest(EnemyKind Enemy, RaidSize Size);

public static class DtoMapper
{
    public static VillageStateDto ToStateDto(
        this Village village,
        WorkContext context,
        IReadOnlyList<string> completedProjects,
        int electorate,
        IReadOnlyList<VillageQuest> quests,
        TsarDto tsar,
        IReadOnlyList<Trip> citizenTrips,
        DungeonRun? dungeon,
        DateTimeOffset now)
    {
        var labels=BuildingLabels.Visible(village);
        return new(
        village.Id,
        village.Slug,
        village.Name,
        village.Description,
        village.IsPrivate,
        village.MoraleAt(now),
        Enum.GetValues<ResourceKind>()
            .Select(kind => new ResourceDto(
                kind,
                village.Stock(kind),
                StorageCatalog.IsGood(kind) ? village.Stock(kind) + village.FreeSpace(kind) : null))
            .ToList(),
        village.Storages
            .Select(s => new StorageDto(
                s.Kind,
                s.PlotId,
                (s.OwnerAccountId is null ? s.Goods : []).OrderBy(g => g.Kind).ThenBy(g => g.Sharpen).ThenBy(g => g.Durability).Select(g => ToolCatalog.Amount(g.Kind, g.Amount, g.Sharpen, g.Durability)).ToList(),
                s.Slots().Select(slot => new StorageSlotDto(slot.Kind, slot.Fill)).ToList(),
                s.Damage) { Number = s.Number, DisplayNumber=labels.GetValueOrDefault(s.Number), OwnerAccountId = s.OwnerAccountId, RuinedAt=s.RuinedAt })
            .ToList(),
        village.Structures.Select(s => new StructureDto(s.Kind, s.PlotId, s.Damage, s.Level) { Number = s.Number, DisplayNumber=labels.GetValueOrDefault(s.Number), OwnerAccountId = s.OwnerAccountId, RuinedAt=s.RuinedAt }).ToList(),
        village.Sites.OrderBy(s => s.PlacedAt).Select(s => s.ToDto(context.RecentCitizenCount, context) with {DisplayNumber=labels.GetValueOrDefault(s.Number)}).ToList(),
        context.Project is { } project ? project.ToDto(context) : null,
        completedProjects,
        quests
            .Select(q => new QuestDto(q.Id, q.Kind, q.Resource, q.StartedAt, QuestRules.Objectives(q, village, context.ActiveCitizenCount, completedProjects)))
            .ToList(),
        village.Wildlife
            .OrderBy(h => h.Kind)
            .Select(h => new HerdDto(h.Kind, h.Current(now), h.Definition.Capacity, h.Definition.Habitat))
            .ToList(),
        Forest.Drawn(village, now).Select(t => new TreeDto(t.X, t.Y)).ToList(),
        TechnologyRules.EraOf(village),
        TechnologyRules.Current(village)?.Id,
        TechnologyCatalog.Technologies
            .Select(technology =>
            {
                var progress = TechnologyRules.ProgressOf(village, technology.Id);
                return new TechnologyStateDto(
                    technology.Id,
                    TechnologyRules.Knows(village, technology.Id),
                    TechnologyRules.IsAvailable(village, technology),
                    progress?.Done ?? 0,
                    technology.Cycles,
                    progress?.Hands.Count ?? 0,
                    TechnologyRules.CrewNeeded(technology, context.RecentCitizenCount)) {KnowledgeWork=progress?.KnowledgeWork??0,
                        KnowledgeCost=TechnologyRules.KnowledgeWorkAvailable(village,technology)*TechnologyRules.KnowledgePerCycle};
            })
            .ToList(),
        village.Poll?.ToDto(electorate),
        GoldVein.Remaining(village),
        village.Fields
            .OrderBy(b => b.PlotId)
            .ThenBy(b => b.Index)
            .Select(b => new FieldBedDto(b.PlotId, b.Index, b.Crop, FarmRules.GrowthOf(village, b, now), b.WateredUntil, FarmRules.IsThirsty(village, b, now)){GenerationId=b.GenerationId})
            .ToList(),
        village.ToWeatherDto(now),
        tsar,
        Convert.ToBase64String(Exploration.Seen(
            village.Explored,
            (village.TsarTrip is null ? citizenTrips : citizenTrips.Append(village.TsarTrip)).Where(t=>BankAccess.CanReach(village,t.To)),
            now,
            village.Wilds?.Lookout)),
        village.Raids.OrderBy(r => r.StrikesAt).Select(r => r.ToDto()).ToList(),
        village.Wilds?.ToDto(),
        dungeon?.ToDto()) { SnapshotAt = now, FarmerAdmissionEnabled=village.FarmerAdmissionEnabled };
    }

    public static WildsDto ToDto(this Wilds wilds) => new(
        wilds.Ravine.Select(s => new RavineSpanDto(s.X, s.Top, s.Height)).ToList(),
        wilds.Passes.Select(p => new MountainPassDto(p.X, p.Y, p.Horizontal)).ToList(),
        wilds.Obstacles.Select(o => new ObstacleDto(o.Kind, o.X, o.Y, !Wilderness.Stands(o))).ToList(),
        wilds.CaveX,
        wilds.CaveY,
        wilds.LookoutX,
        wilds.LookoutY);

    public static RaidDto ToDto(this Raid raid) => new(
        raid.Id,
        raid.Source,
        raid.Enemy,
        raid.Count,
        raid.Wave,
        raid.Waves,
        raid.AnnouncedAt,
        raid.StrikesAt,
        raid.Outcome,
        raid.ResolvedAt,
        raid.Killed,
        raid.Defenders,
        raid.Injured,
        raid.DamagedBuildings,
        raid.Stolen.Select(g => new ResourceAmount(g.Kind, g.Amount)).ToList(),
        raid.Loot.Select(g => new ResourceAmount(g.Kind, g.Amount)).ToList(), raid.EntrySector, raid.ApproachPath) { EngineVersion=raid.EngineVersion, Summary=raid.Summary };

    public static WeatherDto ToWeatherDto(this Village village, DateTimeOffset now)
    {
        var period = DayCycle.PeriodOf(now);
        var kind = WeatherRules.WeatherOf(village, period);
        return new WeatherDto(
            kind,
            WeatherRules.IsWet(kind),
            DayCycle.IsNight(period),
            DayCycle.PeriodStart(period),
            DayCycle.PeriodStart(period + 1),
            WeatherRules.ForecastFor(village, period)?.ToDto(),
            WeatherRules.ForecastFor(village, period + 1)?.ToDto());
    }

    private static ForecastDto ToDto(this WeatherForecast forecast) => new(forecast.CitizenId, forecast.SaysRain, forecast.MadeAt);

    public static TechnologyPollDto ToDto(this TechnologyPoll poll, int activeCitizens)
    {
        var electorate = poll.IsOpen ? activeCitizens : poll.Electorate;
        return new TechnologyPollDto(
            poll.Id,
            poll.Options.Select(id => new PollOptionDto(id, poll.VotesFor(id))).ToList(),
            poll.Ballots.Select(b => new PollBallotDto(b.CitizenId, b.TechnologyId)).ToList(),
            electorate,
            TechnologyPoll.VotesNeeded(electorate),
            poll.OpenedAt,
            poll.ClosedAt,
            poll.Winner);
    }

    public static SiteDto ToDto(this ConstructionSite site, int recentCitizens, WorkContext? context = null) => new(
        site.Kind,
        site.PlotId,
        site.Stage,
        site.Fraction(),
        site.Definition.Stages
            .Select((stage, index) => new SiteStageDto(
                stage.Id,
                site.OwnerAccountId is null ? ConstructionSite.CrewNeeded(stage, recentCitizens) : 1,
                site.HandCount(index),
                site.Jobs(index)
                    .Select(job => new SiteJobDto(
                        job.JobId,
                        site.Done(index, job.JobId),
                        site.Required(index, job.JobId),
                        BuildingCatalog.FindJob(job.JobId)?.Kind == JobKind.Labor
                            ? site.LaborRoom(index, job.JobId)
                            : site.Remaining(index, job.JobId),
                        context?.ReservedWork(site.PlotId, index, job.JobId, site.Remaining(index, job.JobId)) ?? 0))
                    .ToList()))
            .ToList(), site.TargetLevel) { Number = site.Number, OwnerAccountId = site.OwnerAccountId,PlacedByAccountId=site.PlacedByAccountId,
                PlacedAt=site.PlacedAt, BoundaryConflict=SiteManagement.LegacyConflict(site), BoundaryApproved=site.BoundaryApproved,
                Damage=site.Damage,RuinedAt=site.RuinedAt,HasWork=site.HasWork,Operation=site.Operation,PublicDemolition=site.PublicDemolition };

    public static ProjectDto ToDto(this VillageProject project, WorkContext context)
    {
        var definition = project.Definition;
        return new ProjectDto(
            project.Id,
            project.DefinitionId,
            definition.Name,
            definition.Description,
            project.Phase,
            project.Progress,
            project.SurveyProgress,
            definition.SurveyCycles,
            project.BuildProgress,
            project.BuildLimit,
            definition.BuildCycles,
            definition.Materials
                .Select(m => new ProjectMaterialDto(m.Resource, project.Delivered(m.Resource), m.Amount))
                .ToList(),
            context.ActiveCitizenCount,
            context.SupervisionCapacity,
            context.BuildersWorking,
            definition.NeedsEngineer);
    }

    public static CitizenSummaryDto ToSummaryDto(this Citizen citizen, DateTimeOffset now)
    {
        var state = citizen.WorkState(now);
        var active = state != WorkState.Idle && citizen.CurrentTaskId is not null;

        return new CitizenSummaryDto(
            citizen.Id,
            citizen.Name,
            citizen.Subtitle,
            citizen.IsGovernor,
            citizen.Appearance,
            GameCatalog.TitleFor(citizen).Id,
            active ? citizen.CurrentTaskId : null,
            active && citizen.CurrentSiteId is null ? GameCatalog.FindTask(citizen.CurrentTaskId!)?.Workplace : null,
            active ? citizen.CurrentSiteId : null,
            active ? citizen.Spot : null,
            state == WorkState.Working ? null : citizen.IdleSpot,
            state,
            citizen.CycleStartedAt,
            citizen.CycleEndsAt,
            citizen.Load.Select(g => g.Kind).ToList(),
            CombatRules.IsInjured(citizen, now) ? citizen.InjuredUntil : null,
            citizen.Weapon, citizen.Recovery) {IsFarmer=citizen.IsFarmer,CanManageFarm=citizen.CanManageFarm&&citizen.IsGovernor,RestState=citizen.RestState,WeaponSharpen=citizen.WeaponSharpen,HospitalizedAt=citizen.HospitalizedAt,RescueByCitizenId=citizen.RescueByCitizenId,RescuePatientId=citizen.RescuePatientId,RescueJourney=citizen.RescueJourney,RescueRequestedAt=citizen.RescueRequestedAt,DefenseSector=citizen.DefenseSector,DefenseReadyAt=citizen.DefenseReadyAt};
    }

    public static CitizenDto ToDto(this Citizen citizen, Village village, DateTimeOffset now) => new(
        citizen.Id,
        citizen.Name,
        citizen.Subtitle,
        citizen.IsGovernor,
        citizen.Appearance,
        GameCatalog.TitleFor(citizen).Id,
        citizen.Status,
        Enum.GetValues<Stat>()
            .Select(stat => new StatDto(
                stat,
                citizen.Stats.Get(stat),
                citizen.Stats.GetExperience(stat),
                Progression.ExperienceToNext(citizen.Stats.Get(stat))))
            .ToList(),
        citizen.Stats.Total,
        citizen.Skills
            .OrderBy(s => s.Kind)
            .Select(s => new SkillDto(
                s.Kind,
                s.Level,
                s.Experience,
                SkillRules.ExperienceToNext(s.Level),
                s.Level < GameRules.SkillMaximum ? SkillCatalog.Get(s.Kind).RequirementsFor(s.Level + 1) : [],
                s.Level < GameRules.SkillMaximum
                    && s.Experience >= SkillRules.ExperienceToNext(s.Level)
                    && !SkillRules.CanReachNext(citizen, s.Kind)))
            .ToList(),
        citizen.Renown,
        citizen.CurrentEnergy(now, village),
        citizen.EnergyLimit(now),
        citizen.Streak,
        citizen.CurrentTaskId,
        citizen.CurrentTechnologyId is null ? citizen.CurrentSiteId : null,
        citizen.CurrentSiteStage,
        citizen.Spot,
        citizen.WorkState(now),
        citizen.CycleStartedAt,
        citizen.CycleEndsAt,
        citizen.GraceEndsAt,
        citizen.CycleTired && !citizen.CycleSettled,
        citizen.Load.Select(g => ToolCatalog.Amount(g.Kind, g.Amount, g.Sharpen, g.Durability)).ToList(),
        citizen.ShelterQuality,
        citizen.IsHungry,
        citizen.CyclesSinceMeal,
        citizen.RegenerationAt(village, now),
        Energy.RegenerationPerHour(citizen.Stats.Endurance) * citizen.RegenerationAt(village, now),
        now,
        citizen.CurrentTechnologyId,
        WeatherRules.IsSynoptic(village, citizen) ? WeatherRules.Reliability(citizen.SkillLevel(Skill.Weatherlore)) : null,
        VigorDrinks.ReadyAt(citizen, village),
        CombatRules.IsInjured(citizen, now) ? citizen.InjuredUntil : null,
        citizen.Weapon,
        citizen.Armor,
        citizen.Arrows,
        Math.Round(CombatRules.Power(citizen), 1), citizen.Recovery)
        { IsFarmer=citizen.IsFarmer, CanManageFarm=citizen.CanManageFarm&&citizen.IsGovernor, RestState=citizen.RestState, RestRequestedAt=citizen.RestRequestedAt, WeaponSharpen=citizen.WeaponSharpen, ArrowSharpen=citizen.ArrowSharpen, MealKind=citizen.MealKind,MealUntil=citizen.MealUntil,MealReadyAt=citizen.LastMealAt+PreparedMeals.Cooldown, VigorEnergy = village.VigorEnergy, VigorCooldownMinutes = village.VigorCooldownMinutes, AccountId = citizen.AccountId, CyclePrivateOutput = citizen.CyclePrivateOutput, CycleOwnership = citizen.CycleOwnership, CyclePaymentMode = citizen.CyclePaymentMode, PersonalGoods = citizen.PersonalGoods, ActionEnergyCosts = GameCatalog.Tasks.Select(task => new KeyValuePair<string, double>(task.Id, WorkEnergy.WithMedical(citizen, WorkEnergy.TaskCost(village, task, now), now)))
            .Concat(BuildingCatalog.Jobs.Select(job => new KeyValuePair<string, double>(job.Id, WorkEnergy.WithMedical(citizen, WorkEnergy.JobCost(village, job, now), now))))
            .GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Last().Value),
            DefenseSector=citizen.DefenseSector,DefenseReadyAt=citizen.DefenseReadyAt,
            HomePlotId=citizen.HomePlotId, SpouseAccountId=citizen.SpouseAccountId, SpouseName=citizen.SpouseName,
            MarriedSince=citizen.MarriedSince, ExhaustedUntil=citizen.ExhaustedUntil, ExhaustedCycles=Energy.TiredCycles(citizen, now) };

    public static CatalogDto Catalog(GameOptions options) => new(
        GameCatalog.Roles,
        SkillCatalog.Skills,
        GameCatalog.Tasks.Where(t=>t.Effect!=TaskEffect.Guard).ToArray(),
        ProjectCatalog.Projects,
        StorageCatalog.Storages,
        BuildingCatalog.Buildings,
        BuildingCatalog.Jobs,
        WorldMap.BuildArea,
        WorldMap.ReservedAreas,
        WorldMap.Places,
        QuestRules.Rewards,
        new RulesDto(
            (int)GameRules.CycleLength.TotalSeconds,
            (int)GameRules.GraceWindow.TotalSeconds,
            GameRules.StatMaximum,
            GameRules.TalentCap,
            WorldMap.SecondsPerTile,
            BuildingCatalog.MaximumOpenSites,
            GameRules.CyclesPerMeal,
            StorageRules.TakeLimit,
            BuildingCatalog.RepeatCostGrowth,
            GameRules.SkillMaximum,
            GameRules.SkillBonusLevels,
            GameRules.SkillYieldPerLevel,
            GameRules.SkillWorkTimePerLevel,
            GameRules.VigorDrinkEnergy,
            Exploration.SightRadius,
            CombatRules.NightAmbushMultiplier,
            CombatRules.MaximumDamage,
            GameRules.EnergyBase,
            GameRules.EnergyPerEndurance,
            GameRules.EnergyFullRegenerationHours,
            GameRules.EnergyCostPerCycle,
            GameRules.TiredYieldMultiplier,
            GameRules.HungryExperienceMultiplier,
            GameRules.HungryRegenerationMultiplier,
            GameRules.UnderTreesRegeneration,
            WeatherRules.UnderTreesRegeneration,
            GameRules.StreakPerRenewal,
            GameRules.StreakMaximumBonus,
            GameRules.MoraleMinimum,
            GameRules.MoraleMaximum),
        TechnologyCatalog.Technologies,
        FarmCatalog.Crops,
        EnemyCatalog.Enemies,
        ArmoryCatalog.Weapons,
        ArmoryCatalog.Armors,
        HeroClassCatalog.Classes,
        [.. DungeonCatalog.Foes.Values],
        DungeonMapper.Dungeons(),
        options.DevTools);
}
