using System.Security.Cryptography;
using System.Text;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Farming;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Domain.Technologies;
using Desertod.Domain.Weather;
using Desertod.Domain.World;

namespace Desertod.Domain.Entities;

public sealed class Village
{
    public List<BuildingDisplayNumber> BuildingDisplayNumbers { get; set; } = [];
    public RaidAdaptation RaidAdaptation { get; set; } = new();
    public List<SavedRaidPreset> RaidPresets { get; set; } = [];
    public const int NameMinimumLength = 3;
    public const int NameMaximumLength = 64;
    public const int DescriptionMaximumLength = 280;
    public const int InviteCodeLength = 8;

    /// <summary>No 0/O or 1/I/L, so a code read aloud on stream is typed correctly.</summary>
    private const string InviteAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>A private village is hidden from the public list and needs <see cref="InviteCode"/> to join. Anyone can still watch it.</summary>
    public bool IsPrivate { get; set; }

    public string? InviteCode { get; set; }
    public Guid OwnerAccountId { get; set; }

    /// <summary>Morale at <see cref="MoraleChangedAt"/>; it drifts since then, so read it with <see cref="MoraleAt"/>.</summary>
    public double Morale { get; set; } = 1.0;

    public DateTimeOffset MoraleChangedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Research points. Not a physical good, so it has no storage limit.</summary>
    public DateTimeOffset? DefeatedAt { get; set; }
    public Guid? DefeatedByBattleId { get; set; }
    public int Knowledge { get; set; }

    public int VigorEnergy { get; set; } = 8;
    public int VigorCooldownMinutes { get; set; } = 240;

    /// <summary>Gold dug from the vein on the far bank; the vein is empty at <see cref="Rules.GoldVein.Size"/>.</summary>
    public int GoldVeinMined { get; set; }

    public int BuildingSequence { get; set; }

    public List<StorageBuilding> Storages { get; set; } = [];

    /// <summary>Finished buildings that are not storage: shelters and workplaces.</summary>
    public List<Structure> Structures { get; set; } = [];

    public List<ConstructionSite> Sites { get; set; } = [];
    public List<SiteRecovery> SiteRecoveries { get; set; } = [];
    public bool BridgeOpened { get; set; }
    public int BridgeDamage { get; set; }
    public DateTimeOffset? BridgeRuinedAt { get; set; }
    public List<CivicOrder> CivicOrders { get; set; } = [];

    /// <summary>Game animals on the village land, one herd per kind.</summary>
    public List<Herd> Wildlife { get; set; } = [];

    /// <summary>Trees on the camp land, felled ones included: their stumps grow back.</summary>
    public List<Tree> Trees { get; set; } = [];

    /// <summary>Beds of every farm, each sown with one crop or empty.</summary>
    public List<FieldBed> Fields { get; set; } = [];
    /// <summary>Explicit migration step: new public farm work requires an appointment; accepted cycles are untouched.</summary>
    public bool FarmerAdmissionEnabled { get; set; }

    /// <summary>Technologies being researched or discovered.</summary>
    public List<TechnologyProgress> Research { get; set; } = [];

    /// <summary>The open vote on the next discovery, or the last closed one.</summary>
    public TechnologyPoll? Poll { get; set; }

    /// <summary>Technology the last vote chose; researched until it is discovered.</summary>
    public string? ChosenTechnology { get; set; }

    /// <summary>Synoptics' forecasts of the last days and the next one; other days have the weather the sky draws.</summary>
    public List<WeatherForecast> Forecasts { get; set; } = [];

    /// <summary>Where the Tsar walked to on the map; null while they stand at the campfire.</summary>
    public double? TsarSpotX { get; set; }

    public double? TsarSpotY { get; set; }

    public TilePoint? TsarSpot
    {
        get => TsarSpotX is { } x && TsarSpotY is { } y ? new TilePoint(x, y) : null;
        set
        {
            TsarSpotX = value?.X;
            TsarSpotY = value?.Y;
        }
    }

    /// <summary>The Tsar's last walk; the land along it is seen as they get there.</summary>
    public Trip? TsarTrip { get; set; }
    public RestState TsarRestState { get; set; }
    public DateTimeOffset? TsarRestRequestedAt { get; set; }
    public DateTimeOffset? TsarInjuredUntil { get; set; }
    public DateTimeOffset? TsarWoundedAt { get; set; }
    public DateTimeOffset? TsarHospitalizedAt { get; set; }
    public Guid? TsarRescueByCitizenId { get; set; }
    public bool TsarRescueRewardEligible { get; set; }
    public DateTimeOffset? TsarRescueRequestedAt { get; set; }

    /// <summary>Land the tribe has seen, shared by all its players; see <see cref="Exploration"/>.</summary>
    public byte[] Explored { get; set; } = [];

    /// <summary>The ravine, mountain features and obstacles of the wild land; see <see cref="Wilderness"/>.</summary>
    public Wilds? Wilds { get; set; }

    /// <summary>Attacks on the way and the last fought ones; see <see cref="RaidRules"/>.</summary>
    public List<Raid> Raids { get; set; } = [];
    public List<Fortification> Fortifications { get; set; } = [];

    /// <summary>The last night (a <see cref="DayCycle"/> period) the beasts decided whether to attack.</summary>
    public long RaidsPlannedPeriod { get; set; }

    /// <summary>When the next attack on the way strikes; null with none. Lets the server find due raids with a simple query.</summary>
    public DateTimeOffset? NextRaidAt { get; set; }

    /// <summary>A new tribe has a campfire, woods and wild animals around. Everything else, storage too, is built by hand.</summary>
    public static Village Create(string slug, string name, Guid ownerAccountId, DateTimeOffset now)
    {
        var village = new Village { Slug = slug, Name = name, OwnerAccountId = ownerAccountId, CreatedAt = now, MoraleChangedAt = now };
        village.EnsureCamp(now);
        return village;
    }

    public static Village Create(
        string slug,
        string name,
        string? description,
        bool isPrivate,
        Guid ownerAccountId,
        DateTimeOffset now)
    {
        name = name.Trim();
        if (name.Length is < NameMinimumLength or > NameMaximumLength)
        {
            throw new GameRuleException("village.nameLength", "The village name must be 3 to 64 characters long.");
        }

        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (description?.Length > DescriptionMaximumLength)
        {
            throw new GameRuleException(
                "village.descriptionLength",
                $"The description must be at most {DescriptionMaximumLength} characters long.",
                new Dictionary<string, object> { ["maximum"] = DescriptionMaximumLength });
        }

        var village = Create(slug, name, ownerAccountId, now);
        village.Description = description;
        village.IsPrivate = isPrivate;
        if (isPrivate)
        {
            village.NewInviteCode();
        }

        return village;
    }

    /// <summary>Replaces the invite code, so links shared before stop working.</summary>
    public string NewInviteCode()
    {
        InviteCode = RandomNumberGenerator.GetString(InviteAlphabet, InviteCodeLength);
        return InviteCode;
    }

    public bool Admits(string? inviteCode)
    {
        if (!IsPrivate)
        {
            return true;
        }

        if (InviteCode is null || string.IsNullOrWhiteSpace(inviteCode))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(inviteCode.Trim().ToUpperInvariant()),
            Encoding.ASCII.GetBytes(InviteCode));
    }

    /// <summary>
    /// Gives a village the wild land a new tribe starts with, also villages created before it existed, and beds to farms
    /// built before beds existed. Returns true if anything was added.
    /// </summary>
    public bool EnsureCamp(DateTimeOffset now)
    {
        var added = false;
        foreach (var animal in WildlifeCatalog.Animals.Where(a => Wildlife.All(h => h.Kind != a.Kind)))
        {
            Wildlife.Add(Herd.Full(animal.Kind, now));
            added = true;
        }

        if (Trees.Count == 0)
        {
            Trees.AddRange(Forest.Plant(new Random(Id.GetHashCode())));
            added = true;
        }

        if (Wilds is null)
        {
            Wilds = Wilderness.Generate(Id);
            added = true;
        }

        if (Explored.Length != Exploration.Size)
        {
            Explored = Exploration.Upgrade(Explored);
            var plots = Storages.Select(s => s.PlotId).Concat(Structures.Select(s => s.PlotId)).Concat(Sites.Select(s => s.PlotId));
            foreach (var plotId in plots)
            {
                Exploration.Reveal(Explored, WorldMap.Yard(plotId), Exploration.BuildingSightRadius);
            }

            if (TsarSpot is { } spot)
            {
                Exploration.Reveal(Explored, spot, Exploration.SightRadius);
            }

            added = true;
        }

        return FarmRules.EnsureBeds(this) || added;
    }

    /// <summary>The Tsar walks to a spot on the land and stays there. The land on the way is seen as they get there.</summary>
    public void TsarGoTo(TilePoint spot, DateTimeOffset now, bool waitIfBlocked = false)
    {
        SafeRest.RequireActive(TsarRestState);
        if (TsarInjuredUntil > now || TsarHospitalizedAt is not null)
        {
            throw new GameRuleException("tsar.injured", "You are wounded and cannot walk.",
                TsarInjuredUntil is { } until ? new Dictionary<string, object> { ["readyAt"] = until } : null);
        }

        if (!WorldMap.Land.Contains(spot.ToTile()))
        {
            throw new GameRuleException("spot.outsideLand", "This spot is beyond the land.");
        }

        TsarTrip = RegionalTravel.SetOut(this,TsarTrip,TsarSpot??WorldMap.Places[WorldMap.Camp],spot,now,waitIfBlocked:waitIfBlocked);
        TsarSpot = spot;
    }

    /// <summary>Writes the walked parts of these trips into the seen land.</summary>
    public void Explore(IEnumerable<Trip> trips, DateTimeOffset now)
    {
        if (Explored.Length != Exploration.Size)
        {
            return;
        }

        foreach (var trip in TsarTrip is null ? trips : trips.Append(TsarTrip))
        {
            if (trip.Waypoints.Count>0||BankAccess.CanReach(this, trip.To)) Exploration.Reveal(Explored, trip, now, Wilds?.Lookout);
        }
    }

    public bool IsExplored(TileArea area) => Exploration.IsExplored(Explored, area);

    public Herd HerdOf(AnimalKind kind) => Wildlife.Single(h => h.Kind == kind);

    public bool Has(BuildingKind kind) =>
        Structures.Any(s => s.OwnerAccountId == null && s.Kind == kind && !s.Decommissioning && s.RuinedAt is null && s.Damage<100)
        || Storages.Any(s => s.OwnerAccountId == null && BuildingCatalog.ForStorage(s.Kind) == kind && !s.Decommissioning && s.RuinedAt is null && s.Damage<100);

    /// <summary>Finished buildings and open sites of this kind; repeats need more material.</summary>
    public int CountOf(BuildingKind kind, Guid? owner = null) =>
        Structures.Count(s => s.Kind == kind && s.OwnerAccountId == owner)
        + Storages.Count(s => BuildingCatalog.ForStorage(s.Kind) == kind && s.OwnerAccountId == owner)
        + Sites.Count(s => s.Kind == kind && s.OwnerAccountId == owner);

    /// <summary>What is built or planned on a plot.</summary>
    public BuildingKind? KindAt(string plotId) =>
        Sites.FirstOrDefault(s => s.PlotId == plotId)?.Kind
        ?? Structures.FirstOrDefault(s => s.PlotId == plotId)?.Kind
        ?? Storages.Where(s => s.PlotId == plotId).Select(s => BuildingCatalog.ForStorage(s.Kind)).FirstOrDefault();

    /// <summary>True when a building or an open site covers any tile of the area.</summary>
    public bool AreaTaken(TileArea area) => AreaBuilt(area) || Covers(Sites.Select(s => s.PlotId), area);

    /// <summary>True when a finished building covers any tile of the area.</summary>
    public bool AreaBuilt(TileArea area) => Covers(Storages.Select(s => s.PlotId).Concat(Structures.Select(s => s.PlotId)), area);

    private static bool Covers(IEnumerable<string> plotIds, TileArea area) =>
        plotIds.Any(plotId => WorldMap.Footprint(plotId) is { } taken && taken.Overlaps(area));

    public int Stock(ResourceKind kind) =>
        StorageCatalog.IsGood(kind) ? Storages.Where(s => s.OwnerAccountId == null && s.RuinedAt is null && s.Damage<100).Sum(s => s.Amount(kind)) : Knowledge;

    public int FreeSpace(ResourceKind kind) =>
        StorageCatalog.IsGood(kind) ? Storages.Where(s => s.OwnerAccountId == null && s.RuinedAt is null && s.Damage<100).Sum(s => s.FreeSpace(kind)) : int.MaxValue;

    /// <summary>Stores as much as fits, specialised buildings first, and returns the stored amount.</summary>
    public int Store(ResourceKind kind, int amount, int sharpen = 0, int? durability = null)
    {
        if (!StorageCatalog.IsGood(kind))
        {
            Knowledge += amount;
            return amount;
        }

        var stored = 0;
        var order = Storages.Where(s => s.OwnerAccountId == null && s.RuinedAt is null && s.Damage<100)
            .OrderBy(s => s.Definition.IsGeneral)
            .ThenByDescending(s => s.Amount(kind) > 0);
        foreach (var building in order)
        {
            if (amount - stored <= 0)
            {
                break;
            }

            stored += building.Put(kind, amount - stored, sharpen, durability);
        }

        return stored;
    }

    /// <summary>Takes up to <paramref name="amount"/>, general storage first to keep its slots free.</summary>
    /// <param name="sharpen">One weapon edge. Null takes the dullest stacks first.</param>
    public int Take(ResourceKind kind, int amount, int? sharpen = null, int? durability = null)
    {
        if (!StorageCatalog.IsGood(kind))
        {
            var fromKnowledge = Math.Min(amount, Knowledge);
            Knowledge -= fromKnowledge;
            return fromKnowledge;
        }

        var taken = 0;
        foreach (var building in Storages.Where(s => s.OwnerAccountId == null && s.RuinedAt is null && s.Damage<100).OrderBy(s => !s.Definition.IsGeneral))
        {
            if (amount - taken <= 0)
            {
                break;
            }

            taken += building.Take(kind, amount - taken, sharpen, durability);
        }

        return taken;
    }

    /// <summary>Where a citizen walks to drop this good: the nearest building with room, else the nearest that accepts it.</summary>
    public TilePoint DropPoint(ResourceKind kind, TilePoint from)
    {
        var accepting = Storages.Where(s => s.OwnerAccountId == null && s.Definition.AcceptsGood(kind)).ToList();
        var target = accepting.Where(s => s.FreeSpace(kind) > 0).MinBy(s => WorldMap.Yard(s.PlotId).DistanceTo(from))
                     ?? accepting.MinBy(s => WorldMap.Yard(s.PlotId).DistanceTo(from));
        return target is null ? WorldMap.Places[WorldMap.Camp] : WorldMap.Yard(target.PlotId);
    }

    /// <summary>Where a citizen picks up this good: the nearest building holding it.</summary>
    public TilePoint PickupPoint(ResourceKind kind, TilePoint to)
    {
        var target = Storages.Where(s => s.OwnerAccountId == null && s.Amount(kind) > 0).MinBy(s => WorldMap.Yard(s.PlotId).DistanceTo(to));
        return target is null ? WorldMap.Places[WorldMap.Camp] : WorldMap.Yard(target.PlotId);
    }

    /// <summary>Every minute morale moves <see cref="GameRules.MoraleDriftPerMinute"/> of the way back to 1.0.</summary>
    public double MoraleAt(DateTimeOffset now)
    {
        var minutes = Math.Max(0, (now - MoraleChangedAt).TotalMinutes);
        return 1.0 - (1.0 - Morale) * Math.Pow(1.0 - GameRules.MoraleDriftPerMinute, minutes);
    }

    public void AddMorale(double delta, DateTimeOffset now)
    {
        Morale = Math.Clamp(MoraleAt(now) + delta, GameRules.MoraleMinimum, GameRules.MoraleMaximum);
        MoraleChangedAt = now;
    }
}
