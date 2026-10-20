using Desertod.Domain.Buildings;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Farming;
using Desertod.Domain.Projects;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.Technologies;
using Desertod.Domain.Weather;
using Desertod.Domain.World;

namespace Desertod.Domain.Catalog;

/// <summary>Roles (professions) and tasks. Names are English; clients translate them by identifier.</summary>
public static class GameCatalog
{
    private static ResourceAmount Amount(ResourceKind resource, int amount) => new(resource, amount);

    private static readonly ResourceAmount[] None = [];

    /// <summary>Craft level of journeyman work and of a journeyman's title.</summary>
    public const int JourneymanSkill = 3;

    /// <summary>
    /// Craft level of master work and of a master's title. With the challenge rule (full experience up to 3 levels
    /// above the work), crafts with work at 0, 3 and 7 always have a task that teaches in full.
    /// </summary>
    public const int MasterSkill = 7;

    private static RoleDefinition Journeyman(string id, string name, Skill skill, string? technology = null) =>
        new(id, name, RoleTier.Journeyman, skill, JourneymanSkill, technology);

    private static RoleDefinition Master(string id, string name, Skill skill, string? technology = null) =>
        new(id, name, RoleTier.Master, skill, MasterSkill, technology);

    public static IReadOnlyList<RoleDefinition> Roles { get; } =
    [
        new("laborer", "Laborer", RoleTier.Basic, null, 0),

        Journeyman("miner", "Miner", Skill.Mining, TechnologyCatalog.Mining),
        Journeyman("smith", "Smith", Skill.Smithing, TechnologyCatalog.Smelting),
        Journeyman("carpenter", "Carpenter", Skill.Carpentry, TechnologyCatalog.Carpentry),
        Journeyman("baker", "Baker", Skill.Baking, TechnologyCatalog.Baking),
        Journeyman("hunter", "Hunter", Skill.Hunting),
        Journeyman("farmer", "Farmer", Skill.Farming),
        Journeyman("scholar", "Scholar", Skill.Scholarship, TechnologyCatalog.Writing),
        new("engineer", "Engineer", RoleTier.Journeyman, Skill.Building, 1),
        Journeyman("merchant", "Merchant", Skill.Trading),
        Journeyman("bard", "Bard", Skill.Entertaining, TechnologyCatalog.Music),
        Journeyman("guard", "Guard", Skill.Guarding, TechnologyCatalog.Warfare),
        Journeyman(WeatherRules.SynopticRole, "Synoptic", Skill.Weatherlore, TechnologyCatalog.WeatherLore),

        Master("deep-miner", "Deep miner", Skill.Mining, TechnologyCatalog.Mining),
        Master("master-smith", "Master smith", Skill.Smithing, TechnologyCatalog.Forging),
        Master("artisan", "Artisan", Skill.Carpentry, TechnologyCatalog.Mechanics),
        Master("scout", "Scout", Skill.Exploring),
        Master("architect", "Architect", Skill.Building),
        Master("alchemist", "Alchemist", Skill.Scholarship, TechnologyCatalog.Alchemy),
        Master("foreman", "Foreman", Skill.Building),
        Master("envoy", "Envoy", Skill.Trading),
        Master("captain", "Captain", Skill.Guarding, TechnologyCatalog.Warfare),
    ];

    public const string KeepWatch = "keep-watch";

    private const int Craft = GameRules.CraftSeconds;
    private const int GuardShiftSeconds = GameRules.GuardShiftSeconds;

    private static readonly DangerDefinition RabbitWoods = new(0.04, [EnemyKind.Wolf]);
    private static readonly DangerDefinition DeepWoods = new(0.08, [EnemyKind.Wolf, EnemyKind.Boar, EnemyKind.Bear]);
    private static readonly DangerDefinition Grove = new(0.03, [EnemyKind.Wolf, EnemyKind.Boar]);
    private static readonly DangerDefinition OpenLand = new(0.04, [EnemyKind.Wolf, EnemyKind.Boar]);
    private static readonly DangerDefinition FarLand = new(0.06, [EnemyKind.Wolf, EnemyKind.Boar, EnemyKind.Bear]);

    public const string CarryToStorage = "carry-to-storage";

    /// <summary>Workplace of farm work: the beds of the village farms.</summary>
    public const string Fields = "fields";

    /// <summary>Each crop needs its Farming level.</summary>
    private static TaskDefinition Sow(string id, string name, CropKind crop) =>
        new(id, name, "laborer", FarmCatalog.Get(crop).SkillLevel, Stat.Endurance, Stat.Intelligence, Skill.Farming, TaskEffect.Sow, 0,
            None, None, Fields, BuildingKind.Farm, Crop: crop);

    public static IReadOnlyList<TaskDefinition> Tasks { get; } =
    [
        new("chop-wood", "Gather wood", "laborer", 0, Stat.Strength, Stat.Endurance, Skill.Woodcutting, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Wood, 5)], None, "forest"),
        new("quarry-stone", "Gather stones", "laborer", 0, Stat.Strength, Stat.Endurance, Skill.Quarrying, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Stone, 4)], None, "stones"),
        new("forage", "Forage berries", "laborer", 0, Stat.Dexterity, Stat.Intelligence, Skill.Gathering, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Food, 4)], None, "berries"),
        new("cut-reeds", "Cut reeds", "laborer", 0, Stat.Dexterity, Stat.Endurance, Skill.Gathering, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Reeds, 6)], None, "reeds"),
        new("hunt", "Hunt rabbits", "laborer", 0, Stat.Dexterity, Stat.Endurance, Skill.Hunting, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Food, 6), Amount(ResourceKind.Leather, 1)], None, "hunting",
            Hunt: new(AnimalKind.Rabbit, 3), Danger: RabbitWoods),
        new("track-game", "Hunt deer", "laborer", 2, Stat.Dexterity, Stat.Endurance, Skill.Hunting, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Food, 12), Amount(ResourceKind.Leather, 4)], None, "deer-woods",
            WorkSeconds: 2 * GameRules.GatherSeconds, Hunt: new(AnimalKind.Deer, 1), Danger: DeepWoods),
        new("hunt-birds", "Hunt birds", "laborer", 3, Stat.Dexterity, Stat.Intelligence, Skill.Hunting, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Food, 1), Amount(ResourceKind.Feathers, 4)], None, "bird-grove",
            WorkSeconds: GameRules.GatherSeconds / 2, Hunt: new(AnimalKind.Bird, 3), Danger: Grove),
        new("tend-fire", "Tend the fire", "laborer", 0, Stat.Charisma, Stat.Endurance, Skill.Entertaining, TaskEffect.Morale, 0.5,
            None, None, WorldMap.Camp, WorkSeconds: Craft),
        new("fish", "Fish from the pier", "laborer", 0, Stat.Dexterity, Stat.Endurance, Skill.Fishing, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Food, 3)], None, WorldMap.Pier, RequiredProject: ProjectCatalog.FishingPier),
        new(GoldVein.DigTask, "Dig for gold", "laborer", 0, Stat.Strength, Stat.Endurance, Skill.Mining, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Gold, 1)], None, WorldMap.GoldVein, RequiredProject: ProjectCatalog.TsarBridge),
        new("clear-thicket", "Clear the thicket", "laborer", 0, Stat.Strength, Stat.Endurance, Skill.Woodcutting, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Wood, 3)], None, Wilderness.Thicket),
        new("break-rockfall", "Break the rockfall", "laborer", 0, Stat.Strength, Stat.Endurance, Skill.Quarrying, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Stone, 4)], None, Wilderness.Rockfall),
        new("dig-cave", "Dig in the cave", "laborer", 0, Stat.Strength, Stat.Endurance, Skill.Mining, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Stone, 3)], None, Wilderness.Cave),
        new("measure-river", "Measure the river", "laborer", 0, Stat.Intelligence, Stat.Strength, Skill.Scholarship, TaskEffect.Resources, 0,
            [Amount(ResourceKind.ResearchPoints, 1)], None, WorldMap.Ford),
        new(CarryToStorage, "Carry to storage", "laborer", 0, Stat.Endurance, Stat.Strength, Skill.Hauling, TaskEffect.Unload, 0,
            None, None, WorldMap.Camp, WorkSeconds: 15),
        new("tend-fields", "Tend the fields", "laborer", 0, Stat.Endurance, Stat.Dexterity, Skill.Farming, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Food, 2)], None, Fields, BuildingKind.Farm),
        Sow("sow-turnips", "Sow turnips", CropKind.Turnip),
        Sow("sow-carrots", "Sow carrots", CropKind.Carrot),
        Sow("sow-cabbage", "Sow cabbage", CropKind.Cabbage),
        Sow("sow-flax", "Sow flax", CropKind.Flax),
        Sow("sow-rye", "Sow rye", CropKind.Rye),
        new("water-fields", "Water the fields", "laborer", 0, Stat.Endurance, Stat.Strength, Skill.Farming, TaskEffect.Water, 0,
            None, None, Fields, BuildingKind.Farm),
        new("harvest", "Harvest", "laborer", 0, Stat.Endurance, Stat.Dexterity, Skill.Farming, TaskEffect.Harvest, 0,
            None, None, Fields, BuildingKind.Farm),
        new("learn-first-aid", "Study first aid", "laborer", 0, Stat.Endurance, Stat.Intelligence, Skill.Recovery, TaskEffect.Resources, 0,
            None, [Amount(ResourceKind.Food, 1), Amount(ResourceKind.Reeds, 1)], "hospital", BuildingKind.Hospital, WorkSeconds: 180),
        new("practice-first-aid", "Practice first aid", "laborer", 3, Stat.Endurance, Stat.Intelligence, Skill.Recovery, TaskEffect.Resources, 0,
            None, [Amount(ResourceKind.Food, 1), Amount(ResourceKind.Leather, 1)], "hospital", BuildingKind.Hospital, WorkSeconds: 240),
        new("read-books", "Read the chronicles", "laborer", 0, Stat.Intelligence, Stat.Charisma, Skill.Scholarship, TaskEffect.Resources, 0,
            [Amount(ResourceKind.ResearchPoints, 2)], None, "library", BuildingKind.Library, WorkSeconds: Craft),
        new("chat-tavern", "Chat at the tavern", "laborer", 0, Stat.Charisma, Stat.Intelligence, Skill.Entertaining, TaskEffect.Morale, 0.5,
            None, None, "tavern", BuildingKind.Tavern, WorkSeconds: Craft),
        new("cook-porridge", "Cook grain porridge", "laborer", 0, Stat.Dexterity, Stat.Intelligence, Skill.Baking, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Porridge,1)], [Amount(ResourceKind.Grain,2),Amount(ResourceKind.Wood,1)], "bakery", BuildingKind.Bakery,
            WorkSeconds: Craft, RequiredTechnology: TechnologyCatalog.Baking),
        new("cook-stew", "Cook hearty stew", "laborer", 0, Stat.Dexterity, Stat.Intelligence, Skill.Baking, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Stew,1)], [Amount(ResourceKind.Food,3),Amount(ResourceKind.Wood,1)], "tavern", BuildingKind.Tavern,
            WorkSeconds: Craft, RequiredTechnology: TechnologyCatalog.Brewing),
        new("brew-vigor", "Brew vigor drinks", "laborer", 0, Stat.Intelligence, Stat.Charisma, Skill.Scholarship, TaskEffect.Resources, 0,
            [Amount(ResourceKind.VigorDrink, 1)], [Amount(ResourceKind.Grain, 4), Amount(ResourceKind.Food, 2)], "tavern", BuildingKind.Tavern,
            WorkSeconds: 480, RequiredTechnology: TechnologyCatalog.VigorDrinks),
        new("deliver-wood", "Deliver wood", "laborer", 0, Stat.Endurance, Stat.Strength, Skill.Hauling, TaskEffect.Deliver, 0,
            [Amount(ResourceKind.Wood, 6)], None, "project", WorkSeconds: Craft),
        new("deliver-stone", "Deliver stone", "laborer", 0, Stat.Strength, Stat.Endurance, Skill.Hauling, TaskEffect.Deliver, 0,
            [Amount(ResourceKind.Stone, 4)], None, "project", WorkSeconds: Craft),
        new("deliver-reeds", "Deliver reeds", "laborer", 0, Stat.Endurance, Stat.Dexterity, Skill.Hauling, TaskEffect.Deliver, 0,
            [Amount(ResourceKind.Reeds, 8)], None, "project", WorkSeconds: Craft),
        new("deliver-gold", "Deliver gold", "laborer", 0, Stat.Endurance, Stat.Strength, Skill.Hauling, TaskEffect.Deliver, 0,
            [Amount(ResourceKind.Gold, 2)], None, "project", WorkSeconds: Craft),
        new("deliver-planks", "Deliver planks", "laborer", 0, Stat.Endurance, Stat.Strength, Skill.Hauling, TaskEffect.Deliver, 0,
            [Amount(ResourceKind.Planks, 4)], None, "project", WorkSeconds: Craft),
        new("deliver-iron", "Deliver iron", "laborer", 0, Stat.Strength, Stat.Endurance, Skill.Hauling, TaskEffect.Deliver, 0,
            [Amount(ResourceKind.IronIngot, 2)], None, "project", WorkSeconds: Craft),
        new("build", "Build", "laborer", 0, Stat.Strength, Stat.Endurance, Skill.Building, TaskEffect.Build, 1,
            None, None, "project", WorkSeconds: Craft),
        new("expedition", "Go on an expedition", "laborer", JourneymanSkill, Stat.Dexterity, Stat.Endurance, Skill.Exploring, TaskEffect.Expedition, 1,
            None, None, "gate", Danger: FarLand),
        new("roam", "Roam the land", "laborer", 0, Stat.Dexterity, Stat.Endurance, Skill.Exploring, TaskEffect.Roam, 0,
            None, None, WorldMap.Wilds, Danger: OpenLand),
        new("tell-stories", "Tell stories by the fire", "laborer", 0, Stat.Intelligence, Stat.Charisma, Skill.Entertaining, TaskEffect.Morale, 0.25,
            None, None, WorldMap.Camp, WorkSeconds: Craft),
        new("study-plants", "Study plants", "laborer", 0, Stat.Intelligence, Stat.Dexterity, Skill.Gathering, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Food, 2)], None, "berries"),
        new("explore-land", "Explore the land", "laborer", 0, Stat.Intelligence, Stat.Endurance, Skill.Exploring, TaskEffect.Roam, 0,
            None, None, WorldMap.Wilds, Danger: OpenLand),
        new("read-sky", "Read the sky", "laborer", 0, Stat.Intelligence, Stat.Endurance, Skill.Weatherlore, TaskEffect.Resources, 0,
            [Amount(ResourceKind.ResearchPoints, 1)], None, WorldMap.Camp),
        new(KeepWatch, "Keep watch", "laborer", 0, Stat.Endurance, Stat.Strength, Skill.Guarding, TaskEffect.Guard, 1,
            None, None, WorldMap.Camp, WorkSeconds: GuardShiftSeconds),
        new("rescue-wounded", "Carry the wounded to hospital", "laborer", 0, Stat.Endurance, Stat.Strength, Skill.Hauling, TaskEffect.Resources, 0,
            None, None, WorldMap.Camp, WorkSeconds: Craft),
        new("repair", "Repair buildings", "laborer", 0, Stat.Strength, Stat.Dexterity, Skill.Building, TaskEffect.Repair, 10,
            None, [Amount(ResourceKind.Wood, 2), Amount(ResourceKind.Stone, 1)], WorldMap.Camp, WorkSeconds: Craft),
        new("make-poultice", "Make poultices", "laborer", 0, Stat.Intelligence, Stat.Dexterity, Skill.Gathering, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Remedy, 1)], [Amount(ResourceKind.Food, 2)], "berries", WorkSeconds: Craft, RequiredTechnology: TechnologyCatalog.HerbLore),
        new("make-spears", "Make spears", "laborer", 0, Stat.Dexterity, Stat.Strength, Skill.Weaponcraft, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Spear, 1)], [Amount(ResourceKind.Wood, 2), Amount(ResourceKind.Stone, 1)], WorldMap.Camp,
            WorkSeconds: Craft, RequiredTechnology: TechnologyCatalog.Weaponcraft),
        new("sew-armor", "Sew leather armor", "laborer", 0, Stat.Dexterity, Stat.Endurance, Skill.Weaponcraft, TaskEffect.Resources, 0,
            [Amount(ResourceKind.LeatherArmor, 1)], [Amount(ResourceKind.Leather, 3)], WorldMap.Camp,
            WorkSeconds: Craft, RequiredTechnology: TechnologyCatalog.Weaponcraft),
        new("make-bows", "Make bows", "laborer", 2, Stat.Dexterity, Stat.Strength, Skill.Weaponcraft, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Bow, 1)], [Amount(ResourceKind.Wood, 3), Amount(ResourceKind.Leather, 1)], WorldMap.Camp,
            WorkSeconds: Craft, RequiredTechnology: TechnologyCatalog.Archery),
        new("make-arrows", "Fletch arrows", "laborer", 0, Stat.Dexterity, Stat.Intelligence, Skill.Weaponcraft, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Arrows, 6)], [Amount(ResourceKind.Wood, 1), Amount(ResourceKind.Feathers, 2)], WorldMap.Camp,
            WorkSeconds: Craft, RequiredTechnology: TechnologyCatalog.Archery),

        new("mine-coal", "Mine coal", "miner", 0, Stat.Strength, Stat.Endurance, Skill.Mining, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Coal, 2)], None, "mine", BuildingKind.Mine),
        new("mine-iron-ore", "Mine iron ore", "miner", JourneymanSkill, Stat.Strength, Stat.Endurance, Skill.Mining, TaskEffect.Resources, 0,
            [Amount(ResourceKind.IronOre, 1)], None, "mine", BuildingKind.Mine),
        new("smelt-ingot", "Smelt iron", "smith", 0, Stat.Strength, Stat.Dexterity, Skill.Smithing, TaskEffect.Resources, 0,
            [Amount(ResourceKind.IronIngot, 1)], [Amount(ResourceKind.IronOre, 1), Amount(ResourceKind.Coal, 1)], "smithy", BuildingKind.Smithy,
            WorkSeconds: Craft),
        new("saw-planks", "Saw planks", "carpenter", 0, Stat.Dexterity, Stat.Strength, Skill.Carpentry, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Planks, 2)], [Amount(ResourceKind.Wood, 2)], "workshop", BuildingKind.Workshop, WorkSeconds: Craft),
        new("bake-bread", "Bake bread", "baker", 0, Stat.Dexterity, Stat.Endurance, Skill.Baking, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Food, 8)], [Amount(ResourceKind.Grain, 2)], "bakery", BuildingKind.Bakery, WorkSeconds: Craft),
        new("forge-sword", "Forge swords", "smith", JourneymanSkill, Stat.Strength, Stat.Dexterity, Skill.Smithing, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Sword, 1)], [Amount(ResourceKind.IronIngot, 2), Amount(ResourceKind.Coal, 1), Amount(ResourceKind.Leather, 1)],
            "smithy", BuildingKind.Smithy, WorkSeconds: Craft, RequiredTechnology: TechnologyCatalog.Warfare),
        new("make-shields", "Make shields", "carpenter", JourneymanSkill, Stat.Dexterity, Stat.Strength, Skill.Carpentry, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Shield, 1)], [Amount(ResourceKind.Planks, 2), Amount(ResourceKind.Leather, 1)],
            "workshop", BuildingKind.Workshop, WorkSeconds: Craft, RequiredTechnology: TechnologyCatalog.Warfare),
        new("make-wooden-axe", "Make a wooden axe", "laborer", 0, Stat.Dexterity, Stat.Strength, Skill.Carpentry, TaskEffect.Resources, 0,
            [Amount(ResourceKind.WoodenAxe, 1)], [Amount(ResourceKind.Wood, 2)], WorldMap.Camp, WorkSeconds: Craft),
        new("make-wooden-pickaxe", "Make a wooden pickaxe", "laborer", 0, Stat.Dexterity, Stat.Strength, Skill.Carpentry, TaskEffect.Resources, 0,
            [Amount(ResourceKind.WoodenPickaxe, 1)], [Amount(ResourceKind.Wood, 2), Amount(ResourceKind.Stone, 1)], WorldMap.Camp, WorkSeconds: Craft),
        new("make-stone-axe", "Make a stone axe", "laborer", 0, Stat.Dexterity, Stat.Strength, Skill.Carpentry, TaskEffect.Resources, 0,
            [Amount(ResourceKind.StoneAxe, 1)], [Amount(ResourceKind.Wood, 1), Amount(ResourceKind.Stone, 2)], WorldMap.Camp,
            WorkSeconds: Craft, RequiredTechnology: TechnologyCatalog.StoneTools),
        new("make-stone-pickaxe", "Make a stone pickaxe", "laborer", 0, Stat.Dexterity, Stat.Strength, Skill.Carpentry, TaskEffect.Resources, 0,
            [Amount(ResourceKind.StonePickaxe, 1)], [Amount(ResourceKind.Wood, 1), Amount(ResourceKind.Stone, 2)], WorldMap.Camp,
            WorkSeconds: Craft, RequiredTechnology: TechnologyCatalog.StoneTools),
        new("forge-iron-axe", "Forge an iron axe", "smith", 0, Stat.Strength, Stat.Dexterity, Skill.Smithing, TaskEffect.Resources, 0,
            [Amount(ResourceKind.IronAxe, 1)], [Amount(ResourceKind.IronIngot, 1), Amount(ResourceKind.Wood, 1), Amount(ResourceKind.Coal, 1)],
            "smithy", BuildingKind.Smithy, WorkSeconds: Craft),
        new("forge-iron-pickaxe", "Forge an iron pickaxe", "smith", 0, Stat.Strength, Stat.Dexterity, Skill.Smithing, TaskEffect.Resources, 0,
            [Amount(ResourceKind.IronPickaxe, 1)], [Amount(ResourceKind.IronIngot, 1), Amount(ResourceKind.Wood, 1), Amount(ResourceKind.Coal, 1)],
            "smithy", BuildingKind.Smithy, WorkSeconds: Craft),
        new("forge-steel-axe", "Forge a steel axe", "smith", JourneymanSkill, Stat.Strength, Stat.Dexterity, Skill.Smithing, TaskEffect.Resources, 0,
            [Amount(ResourceKind.SteelAxe, 1)], [Amount(ResourceKind.IronIngot, 2), Amount(ResourceKind.Wood, 1), Amount(ResourceKind.Coal, 1)],
            "smithy", BuildingKind.Smithy, WorkSeconds: Craft, RequiredTechnology: TechnologyCatalog.Forging),
        new("forge-steel-pickaxe", "Forge a steel pickaxe", "smith", JourneymanSkill, Stat.Strength, Stat.Dexterity, Skill.Smithing, TaskEffect.Resources, 0,
            [Amount(ResourceKind.SteelPickaxe, 1)], [Amount(ResourceKind.IronIngot, 2), Amount(ResourceKind.Wood, 1), Amount(ResourceKind.Coal, 1)],
            "smithy", BuildingKind.Smithy, WorkSeconds: Craft, RequiredTechnology: TechnologyCatalog.Forging),
        new("sharpen-sword", "Sharpen swords", "smith", 0, Stat.Dexterity, Stat.Strength, Skill.Smithing, TaskEffect.Sharpen, 1,
            [Amount(ResourceKind.Sword, 1)], [Amount(ResourceKind.Coal, 1)], "smithy", BuildingKind.Smithy, WorkSeconds: Craft),
        new("sharpen-spear", "Sharpen spears", "smith", 0, Stat.Dexterity, Stat.Strength, Skill.Smithing, TaskEffect.Sharpen, 1,
            [Amount(ResourceKind.Spear, 1)], [Amount(ResourceKind.Coal, 1)], "smithy", BuildingKind.Smithy, WorkSeconds: Craft),
        new("sharpen-arrows", "Sharpen arrows", "smith", 0, Stat.Dexterity, Stat.Strength, Skill.Smithing, TaskEffect.Sharpen, 1,
            [Amount(ResourceKind.Arrows, 1)], [Amount(ResourceKind.Coal, 1)], "smithy", BuildingKind.Smithy, WorkSeconds: Craft),
        new("research", "Research", "scholar", JourneymanSkill, Stat.Intelligence, Stat.Charisma, Skill.Scholarship, TaskEffect.Resources, 0,
            [Amount(ResourceKind.ResearchPoints, 4)], None, "library", BuildingKind.Library, WorkSeconds: Craft),
        new("survey", "Survey the site", "engineer", 1, Stat.Intelligence, Stat.Dexterity, Skill.Building, TaskEffect.Survey, 1,
            None, None, "project", WorkSeconds: Craft),
        new("supervise", "Supervise construction", "engineer", 1, Stat.Intelligence, Stat.Charisma, Skill.Building, TaskEffect.Supervise, 0,
            None, None, "project", WorkSeconds: Craft),
        new("trade", "Trade", "merchant", 0, Stat.Charisma, Stat.Intelligence, Skill.Trading, TaskEffect.NotAvailableYet, 0,
            None, None, "market", BuildingKind.Market, WorkSeconds: Craft),
        new("perform", "Play the gusli", "bard", JourneymanSkill, Stat.Charisma, Stat.Dexterity, Skill.Entertaining, TaskEffect.Morale, 1,
            None, None, "stage", BuildingKind.Stage, WorkSeconds: Craft),
        new("patrol", "Patrol", "guard", JourneymanSkill, Stat.Endurance, Stat.Strength, Skill.Guarding, TaskEffect.Guard, 1.25,
            None, None, "barracks", BuildingKind.Barracks, WorkSeconds: GuardShiftSeconds),

        new("mine-gold", "Mine gold", "deep-miner", MasterSkill, Stat.Strength, Stat.Endurance, Skill.Mining, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Gold, 1)], None, "mine", BuildingKind.Mine),
        new("forge-chain", "Forge chains", "master-smith", MasterSkill, Stat.Strength, Stat.Dexterity, Skill.Smithing, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Chain, 1)], [Amount(ResourceKind.IronIngot, 2), Amount(ResourceKind.Coal, 1)], "smithy", BuildingKind.Smithy,
            WorkSeconds: Craft),
        new("build-mechanism", "Build mechanisms", "artisan", MasterSkill, Stat.Dexterity, Stat.Intelligence, Skill.Carpentry, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Mechanism, 1)], [Amount(ResourceKind.Planks, 2), Amount(ResourceKind.IronIngot, 1)], "workshop", BuildingKind.Workshop,
            WorkSeconds: Craft),
        new("explore", "Long expedition", "scout", MasterSkill, Stat.Dexterity, Stat.Endurance, Skill.Exploring, TaskEffect.Expedition, 2,
            None, None, "gate", Danger: FarLand),
        new("design", "Draft plans", "architect", MasterSkill, Stat.Intelligence, Stat.Charisma, Skill.Building, TaskEffect.NotAvailableYet, 1,
            None, None, "library", BuildingKind.Library, WorkSeconds: Craft),
        new("brew-remedy", "Brew remedies", "alchemist", MasterSkill, Stat.Intelligence, Stat.Dexterity, Skill.Scholarship, TaskEffect.Resources, 0,
            [Amount(ResourceKind.Remedy, 1)], [Amount(ResourceKind.Food, 2)], "library", BuildingKind.Library, WorkSeconds: Craft),
        new("lead-crew", "Lead a crew", "foreman", MasterSkill, Stat.Charisma, Stat.Intelligence, Skill.Building, TaskEffect.NotAvailableYet, 0,
            None, None, "project", WorkSeconds: Craft),
        new("negotiate", "Negotiate", "envoy", MasterSkill, Stat.Charisma, Stat.Intelligence, Skill.Trading, TaskEffect.NotAvailableYet, 0,
            None, None, "market", BuildingKind.Market, WorkSeconds: Craft),
        new("command", "Command", "captain", MasterSkill, Stat.Endurance, Stat.Strength, Skill.Guarding, TaskEffect.Guard, 1,
            None, None, "barracks", BuildingKind.Barracks, WorkSeconds: GuardShiftSeconds),
    ];

    private static readonly Dictionary<string, RoleDefinition> RolesById = Roles.ToDictionary(r => r.Id);
    private static readonly Dictionary<string, TaskDefinition> TasksById = Tasks.ToDictionary(t => t.Id);

    public static RoleDefinition? FindRole(string id) => RolesById.GetValueOrDefault(id);

    public static TaskDefinition? FindTask(string id) => TasksById.GetValueOrDefault(id);

    public static RoleDefinition RoleOf(TaskDefinition task) => RolesById[task.RoleId];

    /// <summary>Highest-tier title earned; ties go to the craft the citizen is best at, then to the earlier role.</summary>
    public static RoleDefinition TitleFor(Citizen citizen) =>
        Roles.Where(r => r.IsQualified(citizen))
            .OrderByDescending(r => r.Tier)
            .ThenByDescending(r => r.Skill is { } skill ? citizen.SkillLevel(skill) : 0)
            .First();
}
