using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.Weather;
using Desertod.Domain.World;
using Desertod.Domain;

namespace Desertod.Domain.Entities;

public enum CitizenStatus
{
    Active,
    Dormant,
}

public enum WorkState
{
    Idle,
    Working,
    AwaitingRenewal,
    Paused,
}

public sealed class Citizen
{
    public const int SubtitleMaximumLength = 24;

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VillageId { get; set; }
    public Guid AccountId { get; set; }
    public required string Name { get; set; }

    /// <summary>A short line the Tsar writes under the citizen's name on the map.</summary>
    public string? Subtitle { get; set; }
    public string? HomePlotId { get; set; }
    public Guid? SpouseAccountId { get; set; }
    public string? SpouseName { get; set; }
    public DateTimeOffset? MarriedSince { get; set; }
    public DateTimeOffset? MarriageCheckedAt { get; set; }
    public DateTimeOffset? WoundedAt { get; set; }
    public DateTimeOffset? HospitalizedAt { get; set; }
    public Guid? RescueByCitizenId { get; set; }
    public int? SupervisedProgress { get; set; }
    public Guid? RescuePatientId { get; set; }
    public RescueJourney? RescueJourney { get; set; }
    public bool RescueRewardEligible { get; set; }
    public DateTimeOffset? RescueRequestedAt { get; set; }
    public int ExhaustedCycles { get; set; }
    public DateTimeOffset? ExhaustedUntil { get; set; }

    /// <summary>Appointed by the Tsar: still a citizen who works and votes, who may also plan buildings and call votes.</summary>
    public bool IsGovernor { get; set; }
    public bool IsFarmer { get; set; }
    /// <summary>An additional narrow delegation, effective only while this citizen is a Governor.</summary>
    public bool CanManageFarm { get; set; }

    /// <summary>Null for citizens created before character creation; they get a sprite picked from their id.</summary>
    public Appearance? Appearance { get; set; }

    public CitizenStatus Status { get; set; } = CitizenStatus.Active;
    public RestState RestState { get; set; }
    public DateTimeOffset? RestRequestedAt { get; set; }
    public StatBlock Stats { get; set; } = new();

    /// <summary>Crafts the citizen has trained; a skill never trained is not listed and counts as level 0.</summary>
    public List<SkillProgress> Skills { get; set; } = [];

    public int Renown { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public double StoredEnergy { get; set; }
    public DateTimeOffset EnergyUpdatedAt { get; set; }

    /// <summary>Last vigor drink; the next one waits for <see cref="GameRules.VigorDrinkCooldown"/>.</summary>
    public ResourceKind? MealKind { get; set; }
    public DateTimeOffset? MealUntil { get; set; }
    public DateTimeOffset? LastMealAt { get; set; }
    public DateTimeOffset? LastDrinkAt { get; set; }
    public DateTimeOffset? LastEnergyDrinkAt { get; set; }

    /// <summary>Hurt in a fight: no work until then, unless a remedy heals the wound sooner.</summary>
    public DateTimeOffset? InjuredUntil { get; set; }

    /// <summary>Taken from the village armory for the current task and put back when it ends.</summary>
    /// <summary>External duel recovery; independent from native battle injuries.</summary>
    public ExternalRecovery? Recovery { get; set; }
    public DateTimeOffset? RecoveryPausedAt { get; set; }

    public ResourceKind? Weapon { get; set; }

    /// <summary>Edge of <see cref="Weapon"/>, ground at the smithy. 0 is an ordinary weapon.</summary>
    public int WeaponSharpen { get; set; }

    public ResourceKind? Armor { get; set; }

    /// <summary>Arrows in the quiver; a bow is no use without them.</summary>
    public int Arrows { get; set; }

    /// <summary>Edge of the arrows in the quiver. 0 is an ordinary arrow.</summary>
    public int ArrowSharpen { get; set; }

    /// <summary>Explicit repair or unloading destination, persisted for the whole paid cycle.</summary>
    public string? CurrentTargetPlotId { get; set; }

    public string? CurrentTaskId { get; set; }
    public DateTimeOffset? CycleStartedAt { get; set; }
    public DateTimeOffset? CycleEndsAt { get; set; }
    public bool CycleSettled { get; set; } = true;
    public bool CycleTired { get; set; }
    public long CycleInputCostBasis { get; set; }
    /// <summary>Legacy unfinished cycles have unknown input cost until explicitly reconciled.</summary>
    public bool CycleInputCostBasisKnown { get; set; }
    /// <summary>Голод при старте оплаченного цикла; null для работ, начатых до обновления.</summary>
    public bool? CycleHungry { get; set; }
    public int Streak { get; set; }

    /// <summary>Plot of the construction site the current job is on; null for ordinary tasks.</summary>
    public string? CurrentSiteId { get; set; }

    public int? CurrentSiteStage { get; set; }

    /// <summary>Useful construction work claimed by this paid cycle; null for pre-reservation cycles.</summary>
    public int? ReservedSiteWork { get; set; }
    public Guid? ReservedProjectId { get; set; }

    /// <summary>Spot the player picked on the map for the current gathering task; null works at the task's workplace.</summary>
    public double? SpotX { get; set; }

    public double? SpotY { get; set; }

    public TilePoint? Spot
    {
        get => SpotX is { } x && SpotY is { } y ? new TilePoint(x, y) : null;
        set
        {
            SpotX = value?.X;
            SpotY = value?.Y;
        }
    }

    /// <summary>Where the player sent the citizen to stand while not working; cleared when a task starts.</summary>
    public double? IdleSpotX { get; set; }

    public double? IdleSpotY { get; set; }

    public TilePoint? IdleSpot
    {
        get => IdleSpotX is { } x && IdleSpotY is { } y ? new TilePoint(x, y) : null;
        set
        {
            IdleSpotX = value?.X;
            IdleSpotY = value?.Y;
        }
    }

    /// <summary>The last walk out to a spot or a place on the map; the land along it is seen as the citizen gets there.</summary>
    public string? DefenseSector { get; set; }
    public DateTimeOffset? DefenseReadyAt { get; set; }
    public Trip? Trip { get; set; }

    /// <summary>The dungeon run the citizen answered; no tasks and no walks until they are back from it.</summary>
    public Guid? DungeonRunId { get; set; }

    /// <summary>Technology the current cycle researches; <see cref="CurrentSiteId"/> is then the plot of its building.</summary>
    public string? CurrentTechnologyId { get; set; }

    /// <summary>Energy regeneration multiplier of where the citizen sleeps (under the trees by default).</summary>
    public double ShelterQuality { get; set; } = GameRules.UnderTreesRegeneration;

    /// <summary>The last meal found no food in storage.</summary>
    public bool IsHungry { get; set; }

    /// <summary>Cycles finished since the last meal; see <see cref="GameRules.CyclesPerMeal"/>.</summary>
    public int CyclesSinceMeal { get; set; }

    /// <summary>Goods that did not fit in storage. They stay in the citizen's hands until there is room.</summary>
    public List<CarriedGood> Load { get; set; } = [];
    public List<Desertod.Domain.Economy.PersonalGood> PersonalGoods { get; set; } = [];
    public bool CyclePrivateOutput { get; set; }
    public Desertod.Domain.Economy.WorkOwnership CycleOwnership { get; set; }
    public Desertod.Domain.Economy.WorkPaymentMode CyclePaymentMode { get; set; }
    public Guid? CycleWorkOrderClaimId { get; set; }
    private List<Desertod.Domain.Farming.FarmWorkTarget>? cycleFarmTargets;
    public List<Desertod.Domain.Farming.FarmWorkTarget> CycleFarmTargets { get=>cycleFarmTargets??=[];set=>cycleFarmTargets=value; }
    public int CycleFarmContributionVersion { get; set; }

    /// <summary>Mirrors <c>Load.Count &gt; 0</c> so the server can find waiting carriers with a simple query.</summary>
    public bool IsCarrying { get; set; }

    public int SkillLevel(Skill skill) => Skills.SingleOrDefault(s => s.Kind == skill)?.Level ?? 0;

    public void ClearWeapon()
    {
        Weapon = null;
        WeaponSharpen = 0;
    }

    /// <summary>Spends shots from the quiver. An empty quiver forgets the edge.</summary>
    public void SpendArrows(int amount)
    {
        Arrows = Math.Max(0, Arrows - amount);
        if (Arrows == 0)
        {
            ArrowSharpen = 0;
        }
    }

    public int Carried(ResourceKind kind) => Load.Where(g => g.Kind == kind).Sum(g => g.Amount);

    /// <param name="durability">Remaining uses of a tool. Omitted means a fresh tool; other goods stay at 0.</param>
    public void Carry(ResourceKind kind, int amount, int sharpen = 0, int? durability = null)
    {
        if (amount <= 0)
        {
            return;
        }

        if (sharpen < 0 || kind is not (ResourceKind.Sword or ResourceKind.Spear or ResourceKind.Arrows))
        {
            sharpen = 0;
        }

        var tool = ToolCatalog.Get(kind);
        var uses = tool is null
            ? 0
            : durability is int given && given >= 1 && given <= tool.MaxDurability
                ? given
                : tool.MaxDurability;

        var good = Load.SingleOrDefault(g => g.Kind == kind && g.Sharpen == sharpen && g.Durability == uses);
        if (good is null)
        {
            good = new CarriedGood { Kind = kind, Sharpen = sharpen, Durability = uses };
            Load.Add(good);
        }

        good.Amount += amount;
        IsCarrying = true;
    }

    /// <summary>Takes from the dullest, then the most worn, stack of this kind first.</summary>
    public int TakeFromLoad(ResourceKind kind, int amount) => TakeFromLoad(kind, amount, null, null);

    public int TakeFromLoad(ResourceKind kind, int amount, int sharpen) => TakeFromLoad(kind, amount, sharpen, null);

    /// <param name="durability">One tool wear. Null takes every wear of the sharpen, most worn first.</param>
    public int TakeFromLoad(ResourceKind kind, int amount, int? sharpen, int? durability)
    {
        var taken = 0;
        foreach (var good in Load
            .Where(g => g.Kind == kind && (sharpen is null || g.Sharpen == sharpen) && (durability is null || g.Durability == durability))
            .OrderBy(g => g.Sharpen)
            .ThenBy(g => g.Durability)
            .ToList())
        {
            if (taken >= amount)
            {
                break;
            }

            var slice = Math.Min(amount - taken, good.Amount);
            good.Amount -= slice;
            taken += slice;
            if (good.Amount <= 0)
            {
                Load.Remove(good);
            }
        }

        IsCarrying = Load.Count > 0;
        return taken;
    }

    /// <summary>Blank removes the subtitle.</summary>
    public void SetSubtitle(string? subtitle)
    {
        subtitle = subtitle?.Trim();
        if (subtitle?.Length > SubtitleMaximumLength)
        {
            throw new GameRuleException(
                "citizen.subtitleLength",
                $"The subtitle can be at most {SubtitleMaximumLength} characters long.",
                new Dictionary<string, object> { ["maximum"] = SubtitleMaximumLength });
        }

        Subtitle = string.IsNullOrEmpty(subtitle) ? null : subtitle;
    }

    /// <summary>
    /// "Come here": an idle citizen walks to a spot on the land and stays there until the next task.
    /// The land on the way is seen as they get there.
    /// </summary>
    public void GoTo(TilePoint spot, Village village, DateTimeOffset now)
    {
        SafeRest.RequireActive(RestState);
        HospitalRules.RequireReady(this, now);
        if (CombatRules.IsInjured(this, now))
        {
            throw new GameRuleException("citizen.injured", "You are wounded and cannot walk until the wound heals.",
                new Dictionary<string, object> { ["readyAt"] = InjuredUntil!.Value });
        }

        if (ExhaustedUntil > now) throw new GameRuleException("citizen.exhausted", "You are unconscious and recovering.");
        if (WorkState(now) == Entities.WorkState.Working)
        {
            throw new GameRuleException("goto.working", "Finish the current task first.");
        }

        if (DungeonRunId is not null)
        {
            throw new GameRuleException("dungeon.busy", "You are away in the dungeon.");
        }

        if (!WorldMap.Land.Contains(spot.ToTile()))
        {
            throw new GameRuleException("spot.outsideLand", "This spot is beyond the land.");
        }

        SetOut(spot, village, now);
        IdleSpot = spot;
    }

    /// <summary>Starts a walk from where the citizen stands: their idle spot, or the camp.</summary>
    public void SetOut(TilePoint to, Village village, DateTimeOffset now)
    {
        BankAccess.Require(village, to);
        Trip = Exploration.SetOut(village.Explored, Trip, IdleSpot ?? WorldMap.Places[WorldMap.Camp], to, now, village.Wilds?.Lookout);
        Trip.SpeedMultiplier = MedicalWalk(now);
    }

    public void DropLoad()
    {
        Load.Clear();
        IsCarrying = false;
    }

    public double RegenerationMultiplier =>
        ShelterQuality * (IsHungry ? GameRules.HungryRegenerationMultiplier : 1);

    public bool SleepsUnderTrees => ShelterQuality <= GameRules.UnderTreesRegeneration + 1e-9;

    /// <summary><see cref="RegenerationMultiplier"/> with the rain, which chills those sleeping under the trees.</summary>
    public double RegenerationAt(Village village, DateTimeOffset now) =>
        RegenerationMultiplier * (SleepsUnderTrees && WeatherRules.IsRaining(village, now) ? WeatherRules.UnderTreesRegeneration : 1);

    public double CurrentEnergy(DateTimeOffset now, Village village)
    {
        if (GameRules.IsFreeEnergy(now))
        {
            return MaximumEnergy;
        }

        var hours = Math.Max(0, (now - EnergyUpdatedAt).TotalHours);
        if (SleepsUnderTrees)
        {
            hours -= WeatherRules.RainHours(village, EnergyUpdatedAt, now) * (1 - WeatherRules.UnderTreesRegeneration);
        }

        return Math.Min(EnergyLimit(now), Energy.Regenerate(StoredEnergy, hours, Stats.Endurance, RegenerationMultiplier));
    }

    /// <summary>Banks regenerated energy before anything that changes the regeneration rate.</summary>
    public void FlushEnergy(DateTimeOffset now, Village village)
    {
        StoredEnergy = CurrentEnergy(now, village);
        EnergyUpdatedAt = now;
    }

    public bool HasMedicalEffect(string kind, DateTimeOffset now) => Recovery is {EffectUntil:{} until} && until>now && Recovery.EffectKind==kind;
    public double MedicalWalk(DateTimeOffset now) => HasMedicalEffect("breakfast",now) ? .75 : HasMedicalEffect("cast",now) ? .8 : 1;
    public double MaximumEnergy => Energy.Maximum(Stats.Endurance);
    public double EnergyLimit(DateTimeOffset now) => MaximumEnergy * (HasMedicalEffect("breakfast",now) ? .8 : 1);

    public DateTimeOffset? GraceEndsAt => CycleEndsAt + GameRules.GraceWindow;

    public WorkState WorkState(DateTimeOffset now)
    {
        if (RecoveryPausedAt is not null) return Entities.WorkState.Paused;

        if (CurrentTaskId is null || CycleEndsAt is null)
        {
            return Entities.WorkState.Idle;
        }

        if (now < CycleEndsAt)
        {
            return Entities.WorkState.Working;
        }

        return now <= GraceEndsAt ? Entities.WorkState.AwaitingRenewal : Entities.WorkState.Idle;
    }
}

public sealed class CarriedGood
{
    public ResourceKind Kind { get; set; }
    public int Amount { get; set; }

    /// <summary>Edge of a carried weapon. Other goods stay at 0.</summary>
    public int Sharpen { get; set; }

    /// <summary>Remaining uses before the item breaks.</summary>
    public int Durability { get; set; }
}
