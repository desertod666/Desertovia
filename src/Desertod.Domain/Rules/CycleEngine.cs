using Desertod.Domain.Economy;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Farming;
using Desertod.Domain.Projects;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.Storage;
using Desertod.Domain.Technologies;
using Desertod.Domain.Weather;
using Desertod.Domain.World;

namespace Desertod.Domain.Rules;

/// <param name="ProjectContribution">Material carried to, or work done on, a project or construction site.</param>
/// <param name="SiteId">Plot of the construction site for construction jobs.</param>
/// <param name="BuildingCompleted">Set when this cycle finished the last stage of a building.</param>
/// <param name="Hungry">The citizen is still hungry after the meal at the end of the cycle.</param>
/// <param name="WorkedHungry">The cycle started while hungry, so experience was halved.</param>
/// <param name="TechnologyId">Technology the cycle researched.</param>
/// <param name="TechnologyDiscovered">Set when this cycle finished the research of <paramref name="TechnologyId"/>.</param>
/// <param name="LevelsGained">Stat levels; skill levels are in <paramref name="SkillLevelsGained"/>.</param>
public sealed record CycleReport(
    Guid CitizenId,
    string CitizenName,
    string TaskId,
    IReadOnlyList<ResourceAmount> Produced,
    IReadOnlyList<ResourceAmount> Carried,
    double MoraleGained,
    int ProjectContribution,
    bool RareFind,
    Stat Primary,
    int PrimaryExperience,
    Stat Secondary,
    int SecondaryExperience,
    Skill Skill,
    int SkillExperience,
    int SkillLevelsGained,
    int LevelsGained,
    int RenownGained,
    bool Tired,
    bool Overflowed,
    string? SiteId = null,
    BuildingKind? BuildingCompleted = null,
    bool Hungry = false,
    bool WorkedHungry = false,
    string? TechnologyId = null,
    bool TechnologyDiscovered = false,
    FightReport? Fight = null,
    int Repaired = 0)
{
    /// <summary>Stable work identity and freshness for reconnect-safe notifications; not the time a client reconnects.</summary>
    public DateTimeOffset? CycleStartedAt { get; init; }
    public DateTimeOffset? CycleEndedAt { get; init; }
    public DateTimeOffset? SettledAt { get; init; }
    /// <summary>Part of <see cref="Produced"/> that fit neither in storage nor in the citizen's hands.</summary>
    public IReadOnlyList<ResourceAmount> Lost { get; init; } = [];
    public string? TargetPlotId { get; init; }

    /// <summary>Tools that broke during this cycle and left the citizen's hands.</summary>
    public IReadOnlyList<ResourceKind> ToolsBroken { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<FarmCropSnapshot> FarmChanges { get; init; } = [];
}

public static class CycleEngine
{
    private const double UnloadExperienceMultiplier = 0.5;

    /// <summary>
    /// Starts a new task. A previous cycle must already be settled (<see cref="Settle"/>).
    /// Renewing inside the grace window keeps the streak; inputs are consumed up front.
    /// </summary>
    /// <param name="spot">Where on the map the player wants the goods gathered; see <see cref="WorkSpots"/>.</param>
    public static void Start(Citizen citizen, TaskDefinition task, WorkContext context, DateTimeOffset now, TilePoint? spot = null, string? workplacePlotId = null, WorkOwnership? ownership = null, FarmWorkTarget? farmTarget = null)
    {
        var village = context.Village;
        if (context.CompletedProjects.Contains(ProjectCatalog.TsarBridge) && village.BridgeRuinedAt is null) village.BridgeOpened=true;
        CheckReady(citizen, now);
        if(farmTarget is not null)
        {
            if(workplacePlotId is not null && workplacePlotId!=farmTarget.PlotId)
                throw new GameRuleException("farm.changed","Choose the farm containing the selected bed.");
            workplacePlotId=farmTarget.PlotId;
        }
        task=ProjectTask(task,context);
        if(task.Id==RescueRules.TaskId)throw new GameRuleException("rescue.target","Click a wounded citizen to carry them.");
        if (spot is { } picked && !Wilderness.IsWild(task))
        {
            WorkSpots.Check(village, task, picked, now);
        }

        if (task.Effect == TaskEffect.NotAvailableYet)
        {
            throw new GameRuleException("task.notAvailable", $"\"{task.Name}\" is not available yet.", new Dictionary<string, object> { ["taskId"] = task.Id });
        }

        if (Wilderness.IsWild(task)) spot = Wilderness.WorkSpot(village, task, spot);
        var selectedWorkplace = workplacePlotId is null
            ? (ownership is WorkOwnership.PublicService or WorkOwnership.PublicContract
                ? village.Structures.FirstOrDefault(b=>b.OwnerAccountId is null && b.Kind==task.RequiredBuilding && !b.Decommissioning && b.RuinedAt is null && b.Damage<100)
                : citizen.CyclePrivateOutput && citizen.Spot is {} previousSpot
                ? village.Structures.SingleOrDefault(b=>b.OwnerAccountId==citizen.AccountId && b.Kind==task.RequiredBuilding && !b.Decommissioning && WorldMap.Yard(b.PlotId)==previousSpot) : null)
            : village.Structures.SingleOrDefault(b=>b.PlotId==workplacePlotId && b.Kind==task.RequiredBuilding && !b.Decommissioning);
        if (workplacePlotId is not null && task.Effect is not (TaskEffect.Repair or TaskEffect.Unload) && (selectedWorkplace is null || selectedWorkplace.OwnerAccountId is {} owner && owner != citizen.AccountId))
            throw new GameRuleException("building.private", "Choose an available workplace belonging to you or the kingdom.");
        var privateWorkplace = selectedWorkplace?.OwnerAccountId == citizen.AccountId || ownership is not (WorkOwnership.PublicService or WorkOwnership.PublicContract) && task.RequiredBuilding is {} needed && !village.Has(needed)
            && village.Structures.Any(b => b.Kind == needed && b.OwnerAccountId == citizen.AccountId && !b.Decommissioning && b.RuinedAt is null && b.Damage<100);
        if (privateWorkplace && task.RequiredBuilding is {} ownKind)
            spot = WorldMap.Yard((selectedWorkplace ?? village.Structures.First(b=>b.Kind==ownKind && b.OwnerAccountId==citizen.AccountId && !b.Decommissioning && b.RuinedAt is null && b.Damage<100)).PlotId);
        citizen.CyclePrivateOutput = privateWorkplace || spot is {} ownSpot && !KingdomRules.Area(village).Contains(ownSpot.ToTile());
        ApplyOwnership(citizen, task, privateWorkplace, ownership);
        if(village.FarmerAdmissionEnabled && !citizen.CyclePrivateOutput)
        {
            if(task.Id=="tend-fields")throw new GameRuleException("farm.manualOnly","Use the actual beds for farm work.");
            if(FarmRules.IsFarmWork(task) && !citizen.IsFarmer)
                throw new GameRuleException("farm.admission","Ask the Tsar or the farm manager for a farmer appointment.");
        }
        if (task.RequiredBuilding is { } building && !village.Has(building) && !privateWorkplace)
        {
            throw new GameRuleException("task.needsBuilding", $"\"{task.Name}\" needs a {BuildingCatalog.Get(building).Name}.",
                new Dictionary<string, object> { ["building"] = building.ToString() });
        }

        if (task.RequiredProject is { } projectId && !context.CompletedProjects.Contains(projectId))
        {
            throw new GameRuleException("task.needsProject", $"\"{task.Name}\" needs the {ProjectCatalog.Find(projectId)?.Name}.",
                new Dictionary<string, object> { ["taskId"] = task.Id, ["project"] = projectId });
        }

        var role = GameCatalog.RoleOf(task);
        foreach (var technology in new[] { task.RequiredTechnology, role.Technology })
        {
            if (!TechnologyRules.Knows(village, technology))
            {
                throw new GameRuleException("task.needsTechnology",
                    $"\"{task.Name}\" needs {TechnologyCatalog.Get(technology!).Name}. The village must discover it first.",
                    new Dictionary<string, object> { ["taskId"] = task.Id, ["technology"] = technology! });
            }
        }

        SkillRules.Check(citizen, task);
        if (task.Effect is TaskEffect.Survey or TaskEffect.Supervise && citizen.Stats.Intelligence < 3)
            throw new GameRuleException("project.intelligence", "Surveying and supervision require Intelligence 3 and Building 1.");
        if (task.Id == "brew-vigor" && (citizen.SkillLevel(Skill.Scholarship) < 3 || citizen.Stats.Intelligence < 3))
            throw new GameRuleException("task.brewer", "Ask a skilled brewer: Scholarship 3 and Intelligence 3 are required.");
        if (task.RequiredBuilding is {} productionKind && (selectedWorkplace ?? village.Structures.Where(s => s.Kind == productionKind && s.OwnerAccountId == (privateWorkplace ? citizen.AccountId : null) && !s.Decommissioning).OrderBy(s=>s.RuinedAt is not null||s.Damage>=100).FirstOrDefault()) is { } production && (production.Damage >= 100 || production.RuinedAt is not null))
            throw new GameRuleException("building.repairFirst", "Repair this building before working inside.");

        if(farmTarget is not null && !FarmRules.IsFarmWork(task))
            throw new GameRuleException("farm.changed","Choose a farm action for the selected bed.");
        List<FarmWorkTarget> farmTargets=[];
        if (FarmRules.IsFarmWork(task))
        {
            farmTargets=FarmRules.PrepareTargets(village,task,now,citizen.CyclePrivateOutput ? citizen.AccountId : null,farmTarget,context.FarmWork,citizen.Id,workplacePlotId);
        }

        CheckHands(citizen, village, task, now, farmTargets.FirstOrDefault());
        CheckProjectTask(citizen, task, context);
        var projectRoom = int.MaxValue;
        var projectKey = ProjectWorkKey(task);
        if (projectKey is not null && context.Project is { } reservedProject)
        {
            var room = task.Effect == TaskEffect.Build
                ? Math.Max(0, reservedProject.BuildLimit - reservedProject.BuildProgress - (reservedProject.StagedBridge && !reservedProject.HasCrew(citizen.AccountId, context.RecentCitizenCount) ? 1 : 0))
                : reservedProject.Remaining(projectKey);
            projectRoom = room - context.ReservedWork(reservedProject.Id.ToString(), 0, projectKey, room, citizen.Id);
            if (projectRoom <= 0) throw new GameRuleException("site.jobBusy", "All needed work is already assigned.");
        }
        CheckPrey(village, task, now);
        var repairAt = task.Effect == TaskEffect.Repair
            ? workplacePlotId ?? CombatRules.MostDamaged(village, citizen.AccountId) ?? throw new GameRuleException("repair.nothing", "No building needs repair.")
            : null;
        if (GoldVein.IsDug(task) && GoldVein.IsEmpty(village))
        {
            throw new GameRuleException("task.veinEmpty", "The gold vein is empty.", new Dictionary<string, object> { ["taskId"] = task.Id });
        }

        if (repairAt is not null)
        {
            if (CombatRules.DamageOf(village, repairAt) <= 0)
                throw new GameRuleException("repair.nothing", "No building needs repair.");
            var repairOwner = village.Structures.FirstOrDefault(s => s.PlotId == repairAt)?.OwnerAccountId
                ?? village.Storages.FirstOrDefault(s => s.PlotId == repairAt)?.OwnerAccountId
                ?? village.Sites.FirstOrDefault(s => s.PlotId == repairAt)?.OwnerAccountId;
            if (repairOwner is not null && repairOwner != citizen.AccountId)
                throw new GameRuleException("building.private", "This building belongs to another player.");
            privateWorkplace = repairOwner == citizen.AccountId;
        }
        if (task.Effect == TaskEffect.Unload && workplacePlotId is not null)
            StorageRules.UnloadTarget(citizen, village, workplacePlotId);

        var privateOutput = privateWorkplace || spot is {} personalSpot && !KingdomRules.Area(village).Contains(personalSpot.ToTile());
        citizen.CyclePrivateOutput = privateOutput;
        ApplyOwnership(citizen, task, privateWorkplace, ownership);
        if (task.Effect == TaskEffect.Sharpen)
        {
            WeaponSharpening.Check(citizen, village, task.Outputs[0].Resource);
        }

        foreach (var input in task.Inputs)
        {
            if (Available(citizen, village, input.Resource) < input.Amount)
            {
                throw new GameRuleException("storage.missingInput", $"Not enough {ResourceNames.Of(input.Resource)} in storage.", new Dictionary<string, object> { ["resource"] = input.Resource.ToString(), ["missing"] = input.Amount - Available(citizen, village, input.Resource) });
            }
        }

        var inputBasis = 0L;
        var inputBasisKnown = true;
        foreach (var input in task.Inputs)
        {
            var cost = TakeFromHandsThenStorage(citizen, village, input.Resource, input.Amount);
            inputBasis = checked(inputBasis + cost.Basis);
            inputBasisKnown &= cost.Known;
        }

        if (task.Effect == TaskEffect.Sharpen)
        {
            WeaponSharpening.Take(citizen, village, task.Outputs[0].Resource);
        }

        var repairYard = repairAt is null ? (TilePoint?)null : CombatRules.RepairYard(village,repairAt);
        var unloadYard = task.Effect == TaskEffect.Unload && workplacePlotId is not null ? WorldMap.Yard(workplacePlotId) : (TilePoint?)null;
        var farmYard = farmTargets.FirstOrDefault() is {} farm ? WorldMap.Yard(farm.PlotId) : (TilePoint?)null;
        if ((spot ?? repairYard ?? unloadYard ?? farmYard ?? NaturalPlace(task)) is { } destination)
        {
            citizen.SetOut(destination, village, now);
        }

        var work = SkillRules.WorkTime(citizen, task.Skill, task.SkillLevel, TimeSpan.FromSeconds(task.WorkSeconds));
        if (task.Effect is TaskEffect.Build or TaskEffect.Repair or TaskEffect.Supervise && DayCycle.IsNight(now)) work *= 1.25;
        if (task.RequiredBuilding is {} used && (selectedWorkplace??village.Structures.FirstOrDefault(s => s.Kind == used && (citizen.CyclePrivateOutput ? s.OwnerAccountId == citizen.AccountId : s.OwnerAccountId == null))) is {} worn)
            work *= worn.Damage > 75 ? 1.25 : worn.Damage > 50 ? 1.1 : 1;
        var walk = (repairYard ?? unloadYard) is { } yard ? WorldMap.Walk(WorldMap.Places[WorldMap.Camp], yard)
            : farmTargets.Count>0 ? FarmRules.Walk(village,task,now,citizen.CyclePrivateOutput ? citizen.AccountId : null,farmTargets)!.Value
            : Walk(citizen, village, task, spot, now);
        Begin(citizen, task.Id, work + walk, WorkEnergy.TaskCost(village, task, now), village, now, walk);
        citizen.CycleInputCostBasis = inputBasis;
        citizen.CycleFarmTargets=farmTargets;
        citizen.CycleFarmContributionVersion=!citizen.CyclePrivateOutput && farmTargets.Count>0 &&
            (task.Effect==TaskEffect.Sow ? village.FarmerAdmissionEnabled : farmTargets.Any(t=>village.Fields.Any(b=>b.PlotId==t.PlotId && b.Index==t.Index && b.GenerationId==t.GenerationId && b.ContributionVersion==FarmContributionRules.Version)))
            ? FarmContributionRules.Version : 0;
        citizen.CycleInputCostBasisKnown = inputBasisKnown;
        if (projectKey is not null && context.Project is { } acceptedProject)
        {
            var factor = TaskYieldFactor(citizen, village, task, now);
            var perCycle = task.Effect == TaskEffect.Deliver ? task.Outputs[0].Amount : task.BaseYield;
            citizen.ReservedSiteWork = Math.Min(projectRoom, YieldMath.Whole(perCycle * factor));
            citizen.ReservedProjectId = acceptedProject.Id;
        }
        citizen.SupervisedProgress = task.Effect==TaskEffect.Supervise ? context.Project?.BuildProgress : null;
        citizen.CurrentSiteId = null;
        citizen.CurrentSiteStage = null;
        citizen.Spot = spot ?? repairYard ?? unloadYard ?? farmYard;
        citizen.CurrentTargetPlotId = task.Effect == TaskEffect.Repair ? repairAt : task.Effect == TaskEffect.Unload ? workplacePlotId : farmTargets.FirstOrDefault()?.PlotId;
        if (IsArmed(task))
        {
            CombatRules.Arm(citizen, village);
        }
    }

    public static void StartRescue(Citizen helper,Citizen patient,WorkContext context,DateTimeOffset now)
    {
        CheckReady(helper,now);
        var journey=RescueRules.Prepare(helper,patient,context.Village,now);
        helper.CyclePrivateOutput=false;
        var walk=journey.ArrivesAt-now;
        Begin(helper,RescueRules.TaskId,walk,1.5,context.Village,now,walk);
        helper.CycleEndsAt=journey.ArrivesAt; // Медицинская скорость уже включена в маршрут.
        helper.CurrentSiteId=null;helper.CurrentSiteStage=null;helper.RescuePatientId=patient.Id;
        helper.RescueJourney=journey;
        helper.Spot=journey.PositionAt(now);helper.Trip=null;patient.RescueByCitizenId=helper.Id;
    }

    /// <summary>Tasks a citizen takes weapons for: work where beasts attack, and guard duty.</summary>
    public static bool IsArmed(TaskDefinition task) => task.Danger is not null || task.Effect == TaskEffect.Guard;

    /// <summary>Starts a job on a construction site: hauling material there, or working on the current stage.</summary>
    public static void StartJob(Citizen citizen, string plotId, int stage, string jobId, WorkContext context, DateTimeOffset now)
    {
        var selectedSite = context.Village.Sites.SingleOrDefault(s => s.PlotId == plotId);
        citizen.CyclePrivateOutput = selectedSite?.OwnerAccountId is not null && !selectedSite.PublicDemolition;
        var village = context.Village;
        if (context.CompletedProjects.Contains(ProjectCatalog.TsarBridge) && village.BridgeRuinedAt is null) village.BridgeOpened=true;
        CheckReady(citizen, now);
        var job = ConstructionRules.CheckJob(village, citizen, plotId, stage, jobId, context.RecentCitizenCount);

        var site = ConstructionRules.FindSite(village, plotId);
        if(site.RuinedAt is not null)throw new GameRuleException("building.ruined","Restore the ruins before continuing construction.");
        var room = jobId == BuildingCatalog.LendAHand
            ? site.RequiredCrew(stage, context.RecentCitizenCount) - site.HandCount(stage)
            : job.Kind == JobKind.Labor ? site.LaborRoom(stage, jobId) : site.Remaining(stage, jobId);
        var free = room - context.ReservedWork(plotId, stage, jobId, room, citizen.Id);
        if (free <= 0)
            throw new GameRuleException("site.jobBusy", "All needed work is already assigned.");

        var yard = WorldMap.Yard(plotId);
        var duration = job.Kind == JobKind.Haul
            ? SkillRules.WorkTime(citizen, job.Skill, 0, BuildingCatalog.HaulBaseTime) + WorldMap.Walk(
                citizen.CyclePrivateOutput || citizen.Carried(job.Resource!.Value) > 0 ? WorldMap.Places[WorldMap.Camp] : village.PickupPoint(job.Resource.Value, yard),
                yard)
            : SkillRules.WorkTime(citizen, job.Skill, 0, GameRules.CraftLength) + WorldMap.Walk(WorldMap.Places[WorldMap.Camp], yard);

        BankAccess.Require(village, yard);
        if (DayCycle.IsNight(now) && job.Kind == JobKind.Labor) duration += GameRules.CraftLength * .25;
        Begin(citizen, job.Id, duration, WorkEnergy.JobCost(village, job, now), village, now, WorldMap.Walk(WorldMap.Places[WorldMap.Camp], yard));
        citizen.CurrentSiteId = plotId;
        citizen.CurrentSiteStage = stage;
        citizen.ReservedSiteWork = Math.Min(free, jobId == BuildingCatalog.LendAHand ? 1 : SiteWorkYield(citizen, village, job, now));
    }

    /// <summary>Starts researching the technology the citizens voted for, at its building.</summary>
    public static void StartResearch(Citizen citizen, WorkContext context, DateTimeOffset now)
    {
        citizen.CyclePrivateOutput = false;
        CheckReady(citizen, now);
        var (technology, plotId) = TechnologyRules.CheckResearch(context.Village, citizen);

        Begin(citizen, TechnologyCatalog.ResearchTaskId,
            SkillRules.WorkTime(citizen, SkillRules.Research, 0, GameRules.CraftLength)
            + WorldMap.Walk(WorldMap.Places[WorldMap.Camp], WorldMap.Yard(plotId)),
            WeatherRules.EnergyCost(context.Village, false, now), context.Village, now,
            WorldMap.Walk(WorldMap.Places[WorldMap.Camp], WorldMap.Yard(plotId)));
        citizen.CurrentTechnologyId = technology.Id;
        citizen.CurrentSiteId = plotId;
        citizen.CurrentSiteStage = null;
    }

    /// <summary>
    /// Stops the running task, job or research. Nothing is given back: the inputs and energy spent at the start
    /// are lost and the cycle earns nothing. The streak stays where it was before this cycle started.
    /// </summary>
    /// <param name="village">Takes back the weapons the citizen took for the task; without it they keep them until the next task ends.</param>
    public static void Cancel(Citizen citizen, DateTimeOffset now, Village? village = null)
    {
        SafeRest.RequireActive(citizen.RestState);
        HospitalRules.RequireReady(citizen, now);
        if (citizen.ExhaustedUntil > now)
            throw new GameRuleException("citizen.exhausted", "You fainted from exhaustion. Wait until you recover.",
                new Dictionary<string, object> { ["readyAt"] = citizen.ExhaustedUntil.Value });
        if (citizen.WorkState(now) != WorkState.Working)
        {
            throw new GameRuleException("task.notRunning", "There is no task running.");
        }

        if (village is not null)
        {
            if (GameCatalog.FindTask(citizen.CurrentTaskId ?? "") is { Effect: TaskEffect.Sharpen } task
                && task.Outputs[0].Resource == ResourceKind.Arrows)
            {
                WeaponSharpening.Return(citizen, village);
            }

            CombatRules.ReturnWorkEquipment(citizen, village, now);
        }

        // A streak above zero means this cycle renewed the last one: keep the grace window open so the next task renews it again.
        if (citizen.Streak > 0)
        {
            citizen.Streak--;
            citizen.CycleEndsAt = now;
        }
        else
        {
            citizen.CycleEndsAt = null;
        }

        citizen.CycleSettled = true;
        citizen.CurrentTaskId = null;
        citizen.CurrentTechnologyId = null;
        citizen.CurrentSiteId = null;
        citizen.CurrentSiteStage = null;
        citizen.CycleStartedAt = null;
        citizen.Spot = null;
    }

    /// <summary>Banks the result of a completed cycle. Returns null if there is nothing to settle.</summary>
    public static CycleReport? Settle(Citizen citizen, WorkContext context, DateTimeOffset now)
    {
        var report = SettleCore(citizen, context, now);
        if(report is not null)report=report with {CycleStartedAt=citizen.CycleStartedAt,CycleEndedAt=citizen.CycleEndsAt,SettledAt=now};
        SafeRest.AfterSettlement(citizen, now, context.RestBlockedByCombat);
        return report;
    }

    private static CycleReport? SettleCore(Citizen citizen, WorkContext context, DateTimeOffset now)
    {
        HospitalRules.Resume(citizen, now);
        if (citizen.RecoveryPausedAt is not null || HospitalRules.IsAdmitted(citizen, now)
            || citizen.CycleSettled || citizen.CurrentTaskId is null || citizen.CycleEndsAt is null
            || now < citizen.CycleEndsAt)
        {
            return null;
        }

        citizen.CycleSettled = true;
        if (citizen.CurrentTaskId == RescueRules.TaskId)
        {
            var renownBefore = citizen.Renown;
            var delivered = citizen.RescueJourney is { } journey && (journey.PatientIsTsar
                ? RescueRules.AdmitTsar(citizen, context.Village, journey.ArrivesAt)
                : context.RescuePatient is { } patient && RescueRules.Admit(patient, context.Village, journey.ArrivesAt, citizen));
            if (!delivered)
            {
                if (citizen.RescueJourney is {PatientIsTsar:true} failed && context.Village.TsarRescueByCitizenId == citizen.Id)
                { context.Village.TsarSpot=failed.PositionAt(now); context.Village.TsarRescueByCitizenId=null; }
                RescueRules.Cancel(citizen, context.RescuePatient, now, context.Village);
            }
            citizen.RescuePatientId = null;
            if (citizen.RescueJourney is { } completed)
            {
                completed.CompletedAt ??= now;
                citizen.Spot = completed.PositionAt(now); citizen.IdleSpot = citizen.Spot; citizen.Trip = null;
            }
            var deliveryRenown = citizen.Renown - renownBefore;
            var (experienceHungry, hungry) = FinishMeal(citizen, context.Village, now, delivered);
            var rescueReport = Finish(citizen, context.Village, RescueRules.TaskId,
                Stat.Endurance, Stat.Strength, Skill.Hauling, 1, delivered ? 1 : 0,
                hungry, [], [], 0, 0, false, !delivered, experienceHungry);
            return rescueReport with { RenownGained = rescueReport.RenownGained + deliveryRenown };
        }
        if (citizen.CurrentTechnologyId is not null)
        {
            return SettleResearch(citizen, context, now);
        }

        var report = citizen.CurrentSiteId is not null
            ? SettleJob(citizen, context, now)
            : SettleTask(citizen, context, now);
        CombatRules.ReturnWorkEquipment(citizen, context.Village, now);
        return report;
    }

    private static CycleReport? SettleResearch(Citizen citizen, WorkContext context, DateTimeOffset now)
    {
        var technology = TechnologyCatalog.Find(citizen.CurrentTechnologyId!);
        if (technology is null)
        {
            return null;
        }

        var village = context.Village;
        var factor = YieldFactor(citizen, village, technology.Primary, technology.Secondary, now) / GameRules.StatFactorBase
                     * SkillRules.YieldMultiplier(citizen, SkillRules.Research, 0)
                     * WeatherRules.ResearchMultiplier(village, WorkedAt(citizen, now));
        var (amount, discovered) = TechnologyRules.Research(village, citizen, technology, factor, context.RecentCitizenCount, now);
        var overflowed = amount == 0;
        var (experienceHungry, hungry) = FinishMeal(citizen, village, now, true);

        var challenge = Progression.ChallengeMultiplier(citizen.Stats.Get(technology.Primary), technology.Researcher.Max(r => r.Minimum));
        var report = Finish(citizen, village, TechnologyCatalog.ResearchTaskId, technology.Primary, technology.Secondary, SkillRules.Research,
            challenge, overflowed ? GameRules.OverflowExperienceMultiplier : 1, hungry,
            [], [], 0, 0, false, overflowed, experienceHungry);
        return report with { TechnologyId = technology.Id, TechnologyDiscovered = discovered };
    }

    private static TaskDefinition ProjectTask(TaskDefinition task,WorkContext context) =>
        context.Project is {} project && project.DefinitionId!=ProjectCatalog.TsarBridge && task.Effect is TaskEffect.Survey or TaskEffect.Supervise
            ? task with {SkillLevel=GameCatalog.JourneymanSkill} : task;

    private static CycleReport? SettleTask(Citizen citizen, WorkContext context, DateTimeOffset now)
    {
        var task = GameCatalog.FindTask(citizen.CurrentTaskId!);
        if (task is null)
        {
            return null;
        }

        task=ProjectTask(task,context);
        var village = context.Village;
        var project = context.Project is { Status: ProjectStatus.Active } active ? active : null;
        var factor = TaskYieldFactor(citizen, village, task, now);
        var projectClaim = citizen.ReservedProjectId is { } projectId
            ? (project?.Id == projectId ? citizen.ReservedSiteWork ?? 0 : 0) : int.MaxValue;

        var produced = new List<ResourceAmount>();
        var carried = new List<ResourceAmount>();
        var lost = new List<ResourceAmount>();
        var overflowed = false;
        var rareFind = false;
        double moraleGained = 0;
        var contributed = 0;
        var repaired = 0;
        double experienceShare = 1;
        var toolsBroken = new List<ResourceKind>();
        var production = new List<(ResourceKind Kind, int Amount, int Kept)>();
        var farmChanges=new List<FarmCropSnapshot>();
        var farmWorker=citizen.CycleFarmContributionVersion==FarmContributionRules.Version && !citizen.CyclePrivateOutput
            ? new FarmWorker(citizen.AccountId,citizen.Name,citizen.CycleStartedAt!.Value) : null;
        var farmAt=farmWorker is not null && citizen.CycleEndsAt is {} due && due<now ? due : now;
        var recipeOutput = false;

        void Produce(ResourceKind kind, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            produced.Add(ToolCatalog.Get(kind) is { } tool
                ? new ResourceAmount(kind, amount, 0, tool.MaxDurability, tool.MaxDurability)
                : new ResourceAmount(kind, amount));
            var kept = Keep(citizen, village, kind, amount, carried, lost, recipeOutput ? citizen.CycleInputCostBasisKnown : true);
            if (recipeOutput && citizen.CyclePrivateOutput) production.Add((kind, amount, kept));
        }

        var fight = task.Danger is { } danger
            ? CombatRules.Ambush(citizen, village, danger, context.Random, WorkedAt(citizen, now), now)
            : null;
        foreach (var loot in fight?.Loot ?? [])
        {
            Produce(loot.Resource, loot.Amount);
        }

        // Beaten by a beast: the citizen runs back with nothing.
        var beaten = fight is { Won: false };
        overflowed = beaten;
        recipeOutput = true;
        switch (beaten ? (TaskEffect?)null : task.Effect)
        {
            case TaskEffect.Guard:
                break;

            case TaskEffect.Repair:
                repaired = CombatRules.Repair(village, (int)task.BaseYield, citizen.CurrentTargetPlotId);
                contributed = repaired > 0 ? 1 : 0;
                overflowed = repaired == 0;
                break;

            case TaskEffect.Sharpen:
            {
                var kind = task.Outputs[0].Resource;
                if (kind == ResourceKind.Arrows)
                {
                    if (WeaponSharpening.Raise(citizen, village) is int edge)
                    {
                        produced.Add(new ResourceAmount(kind, 1, edge));
                    }
                    else
                    {
                        overflowed = true;
                    }
                }
                else if (citizen.Weapon == kind && citizen.WeaponSharpen < WeaponSharpening.Maximum)
                {
                    citizen.WeaponSharpen++;
                    produced.Add(new ResourceAmount(kind, 1, citizen.WeaponSharpen));
                }
                else
                {
                    overflowed = true;
                }

                break;
            }

            case TaskEffect.Resources:
            {
                var share = factor;
                if (task.Hunt is { } hunt)
                {
                    var herd = village.HerdOf(hunt.Prey);
                    var caught = herd.Take(YieldMath.Whole(hunt.Animals * factor * herd.Abundance(now)), now);
                    share = (double)caught / hunt.Animals;
                    overflowed = caught == 0;
                }

                if (GoldVein.IsDug(task))
                {
                    var perShare = task.Outputs.Sum(o => o.Amount);
                    var dug = GoldVein.Take(village, YieldMath.Whole(perShare * share));
                    share = (double)dug / perShare;
                    overflowed = dug == 0;
                }

                if (Wilderness.ObstacleOf(task) is { } obstacle)
                {
                    var perCycle = Wilderness.WorkPerCycle(obstacle);
                    var cleared = Wilderness.Clear(village, obstacle, YieldMath.Whole(perCycle * share),
                        citizen.Spot ?? WorldMap.Places[WorldMap.Camp], now);
                    share = (double)cleared / perCycle;
                    overflowed = cleared == 0;
                }

                if (task.Workplace == Wilderness.Cave && context.Random.NextDouble() < Wilderness.CaveFindChance * share)
                {
                    var find = Loot.Roll(Wilderness.CaveFinds, context.Random);
                    Produce(find.Resource, find.Amount);
                }

                var wood = 0;
                var usedTools = new List<CarriedGood>();
                foreach (var output in task.Outputs)
                {
                    var (toolDef, toolGood) = ToolCatalog.BestToolForResource(citizen, output.Resource);
                    var multiplier = toolDef?.SpeedMultiplier ?? 1.0;
                    var amount = YieldMath.Whole(output.Amount * share * multiplier);
                    Produce(output.Resource, amount);
                    if (output.Resource == ResourceKind.Wood)
                    {
                        wood += amount;
                    }

                    if (amount > 0 && toolGood is not null && !usedTools.Contains(toolGood))
                    {
                        usedTools.Add(toolGood);
                    }
                }

                foreach (var tool in usedTools)
                {
                    if (ToolCatalog.Wear(citizen, tool) is { } broken)
                    {
                        toolsBroken.Add(broken);
                    }
                }

                if (task.Workplace == Forest.Workplace)
                {
                    Forest.Chop(village, wood, now, citizen.Spot);
                }

                break;
            }

            case TaskEffect.Unload:
                produced.AddRange(StorageRules.Unload(citizen, village, citizen.CurrentTargetPlotId));
                overflowed = citizen.Load.Count > 0;
                experienceShare = UnloadExperienceMultiplier;
                break;

            case TaskEffect.Sow:
                overflowed = !FarmRules.Sow(village, task.Crop!.Value, farmAt, citizen.CyclePrivateOutput ? citizen.AccountId : null, citizen.CycleFarmTargets.FirstOrDefault(),farmWorker,citizen.CycleFarmContributionVersion,farmChanges);
                break;

            case TaskEffect.Water:
                overflowed = FarmRules.Water(village, now, citizen.CyclePrivateOutput ? citizen.AccountId : null, citizen.CycleFarmTargets.Count==0 ? null : citizen.CycleFarmTargets,farmWorker,farmChanges,farmAt) == 0;
                break;

            case TaskEffect.Harvest:
            {
                var producedBefore=produced.Count;var lostBefore=lost.Count;
                var crop = FarmRules.Harvest(village, farmAt, citizen.CyclePrivateOutput ? citizen.AccountId : null, citizen.CycleFarmTargets.FirstOrDefault(),farmWorker,farmChanges);
                overflowed = crop is null;
                foreach (var output in crop?.Harvest ?? [])
                {
                    Produce(output.Resource, YieldMath.Whole(output.Amount * factor));
                }
                if(farmChanges.Count>0)farmChanges[^1]=farmChanges[^1] with {Produced=produced.Skip(producedBefore).ToArray(),Lost=lost.Skip(lostBefore).ToArray(),YieldFactor=factor};

                break;
            }

            case TaskEffect.Morale:
                var before = village.MoraleAt(now);
                if (before >= GameRules.MoraleMaximum)
                {
                    overflowed = true;
                }
                else
                {
                    village.AddMorale(GameRules.MoralePerPerformYield * task.BaseYield * factor, now);
                    moraleGained = village.Morale - before;
                }

                break;

            case TaskEffect.Deliver:
            {
                var output = task.Outputs[0];
                var key = output.Resource.ToString();
                var amount = project is null
                    ? 0
                    : Min(YieldMath.Whole(output.Amount * factor), Available(citizen, village, output.Resource), project.Remaining(key),
                        project.ContributionRoom(citizen.Id, key, context.ActiveCitizenCount), projectClaim);

                if (amount <= 0)
                {
                    overflowed = true;
                    break;
                }

                TakeFromHandsThenStorage(citizen, village, output.Resource, amount);
                project!.Record(citizen.Id, key, amount,citizen.AccountId);
                contributed = amount;
                break;
            }

            case TaskEffect.Survey:
            case TaskEffect.Build:
            {
                var key = task.Effect == TaskEffect.Survey ? VillageProject.SurveyKey : VillageProject.BuildKey;
                var amount = 0;
                if (project is not null)
                {
                    var limit = task.Effect == TaskEffect.Survey
                        ? project.Remaining(key)
                        : Math.Max(0, project.BuildLimit - project.BuildProgress - (project.StagedBridge && !project.HasCrew(citizen.AccountId, context.RecentCitizenCount) ? 1 : 0));
                    amount = Min(YieldMath.Whole(task.BaseYield * factor), limit,
                        project.ContributionRoom(citizen.Id, key, context.ActiveCitizenCount), projectClaim);
                }

                if (amount <= 0)
                {
                    overflowed = true;
                    break;
                }

                project!.Record(citizen.Id, key, amount,citizen.AccountId);
                contributed = amount;
                break;
            }

            case TaskEffect.Supervise:
                overflowed = project is null || project.Phase != ProjectPhase.Construction || project.BuildDone || (citizen.SupervisedProgress is {} baseline ? project.BuildProgress<=baseline : context.BuildersWorking==0);
                if (!overflowed && project is not null) { contributed = 1; if (project.StagedBridge) project.RegisterHand(citizen.Id,1,citizen.AccountId); }
                break;

            case TaskEffect.Expedition:
            {
                var scout = task.RoleId == "scout";
                for (var roll = 0; roll < task.BaseYield; roll++)
                {
                    var loot = Expeditions.Roll(context.Random);
                    Produce(loot.Resource, YieldMath.Whole(loot.Amount * factor));
                }

                if (!citizen.CycleTired
                    && context.Random.NextDouble() < Expeditions.RareChance(citizen.Stats.Dexterity, scout))
                {
                    Produce(Expeditions.RareFind.Resource, Expeditions.RareFind.Amount);
                    rareFind = true;
                }

                break;
            }

            case TaskEffect.Roam:
            {
                var found = new Dictionary<ResourceKind, int>();
                var chance = Roaming.FindChance(citizen.Stats.Dexterity);
                for (var glance = 0; glance < Roaming.Glances; glance++)
                {
                    if (context.Random.NextDouble() < chance)
                    {
                        var loot = Loot.Roll(Roaming.Table, context.Random);
                        found[loot.Resource] = found.GetValueOrDefault(loot.Resource) + loot.Amount;
                    }
                }

                foreach (var (kind, amount) in found)
                {
                    Produce(kind, YieldMath.Whole(amount * factor));
                }

                break;
            }
        }

        if (task.Effect != TaskEffect.Unload)
        {
            experienceShare = overflowed ? GameRules.OverflowExperienceMultiplier : 1;
            if (carried.Count > 0 || lost.Count > 0)
            {
                overflowed = true;
                var storedShare = 1 - (double)(carried.Sum(c => c.Amount) + lost.Sum(l => l.Amount)) / produced.Sum(p => p.Amount);
                experienceShare = GameRules.OverflowExperienceMultiplier + (1 - GameRules.OverflowExperienceMultiplier) * storedShare;
            }
        }

        // Prey differ in hunting time; experience follows the time, so every prey gives the same experience per minute.
        if (task.Hunt is not null)
        {
            experienceShare *= task.WorkSeconds / GameRules.CycleLength.TotalSeconds;
        }

        // Discarded output carries its own discarded cost; it must not inflate the remaining items' basis.
        var allocations = ProductionCost.Allocate(citizen.CycleInputCostBasis, production.Select(p => new ResourceAmount(p.Kind, p.Amount)).ToArray());
        foreach (var group in production.GroupBy(p => p.Kind))
        {
            var kept = group.Sum(p => p.Kept);
            if (kept == 0) continue;
            var basis = (long)((Int128)allocations[group.Key] * kept / group.Sum(p => (long)p.Amount));
            var good = citizen.PersonalGoods.Single(g => g.Kind == group.Key);
            good.CostBasis = checked(good.CostBasis + basis);
            good.CostBasisKnown &= citizen.CycleInputCostBasisKnown;
        }
        var (experienceHungry, hungry) = FinishMeal(citizen, village, now, task.Effect != TaskEffect.Unload);
        if(task.Effect is TaskEffect.Survey or TaskEffect.Build && contributed>0)
            experienceShare*=Math.Min(1,(double)contributed/Math.Max(1,YieldMath.Whole(task.BaseYield*factor)));
        if(task.Effect==TaskEffect.Repair && repaired>0)experienceShare*=Math.Min(1,repaired/(double)task.BaseYield);
        if (task.RequiredBuilding is {} producedAt && !overflowed && village.Structures.FirstOrDefault(s => s.Kind == producedAt && (citizen.CyclePrivateOutput ? s.OwnerAccountId == citizen.AccountId : s.OwnerAccountId == null)
            && (!FarmRules.IsFarmWork(task) || citizen.CycleFarmTargets.Count==0 || citizen.CycleFarmTargets.Any(t=>t.PlotId==s.PlotId))) is {} producer)
        {
            producer.ProductionCycles++;
            if (producer.ProductionCycles % 10 == 0) producer.Damage = Math.Min(100, producer.Damage + 1);
        }
        if(FarmRules.IsFarmWork(task) && citizen.CycleFarmTargets.Count>0 && overflowed)experienceShare=0;
        var report = Finish(citizen, village, task.Id, task.Primary, task.Secondary, task.Skill,
            SkillRules.Challenge(citizen, task.Skill, task.SkillLevel), experienceShare, hungry,
            produced, carried, moraleGained, contributed, rareFind, overflowed, experienceHungry);
        return report with { Fight = fight, Repaired = repaired, Lost = lost, TargetPlotId = citizen.CurrentTargetPlotId, ToolsBroken = toolsBroken,FarmChanges=farmChanges };
    }

    /// <summary>
    /// Stores what fits; the rest goes to the citizen's hands up to <see cref="StorageRules.TakeLimit"/> of the good,
    /// and anything beyond that is lost.
    /// </summary>
    private static int Keep(Citizen citizen, Village village, ResourceKind kind, int amount,
        List<ResourceAmount> carried, List<ResourceAmount> lost, bool basisKnown = false)
    {
        if (citizen.CyclePrivateOutput)
        {
            var kept = Math.Min(amount, PersonalInventory.Room(citizen, kind));
            if (kept > 0) PersonalInventory.Add(citizen, kind, kept, 0, basisKnown);
            if (amount > kept) lost.Add(new ResourceAmount(kind, amount - kept));
            return kept;
        }
        var left = amount - village.Store(kind, amount);
        var held = Math.Min(left, Math.Max(0, StorageRules.CarryLimit(citizen, citizen.CycleEndsAt ?? DateTimeOffset.UtcNow) - citizen.Carried(kind)));
        if (held > 0)
        {
            citizen.Carry(kind, held);
            carried.Add(new ResourceAmount(kind, held));
        }

        if (left - held > 0)
        {
            lost.Add(new ResourceAmount(kind, left - held));
        }
        return amount - left + held;
    }

    private static CycleReport? SettleJob(Citizen citizen, WorkContext context, DateTimeOffset now)
    {
        var job = BuildingCatalog.FindJob(citizen.CurrentTaskId!);
        if (job is null)
        {
            return null;
        }

        var village = context.Village;
        var plotId = citizen.CurrentSiteId!;
        var stage = citizen.CurrentSiteStage ?? 0;
        var site = village.Sites.SingleOrDefault(s => s.PlotId == plotId);
        var potential = SiteWorkYield(citizen, village, job, now);
        var ownLimit = citizen.ReservedSiteWork ?? int.MaxValue;
        var amount = 0;
        var produced = new List<ResourceAmount>();
        var carried = new List<ResourceAmount>();
        var lost = new List<ResourceAmount>();

        if (site is not null && stage >= site.Stage && !site.IsFinished)
        {
            if (job.Id == BuildingCatalog.LendAHand)
            {
                var needed = site.RequiredCrew(stage, context.RecentCitizenCount);
                if (stage == site.Stage && site.HandCount(stage) < needed && !site.HasHand(stage, citizen.Id))
                {
                    site.AddHand(stage, citizen.Id);
                    amount = 1;
                }
            }
            else if (job.Kind == JobKind.Haul)
            {
                var resource = job.Resource!.Value;
                amount = Min(potential, site.OwnerAccountId is null ? Available(citizen, village, resource) : PersonalInventory.Amount(citizen, resource), site.Remaining(stage, job.Id), ownLimit);
                if (amount > 0)
                {
                    if (site.OwnerAccountId is null) TakeFromHandsThenStorage(citizen, village, resource, amount);
                    else PersonalInventory.Remove(citizen, resource, amount);
                    site.Record(stage, job.Id, amount, citizen.Id);
                }
            }
            else if (stage == site.Stage)
            {
                amount = Min(potential, site.LaborRoom(stage, job.Id), ownLimit);
                if (amount > 0)
                {
                    site.Record(stage, job.Id, amount, citizen.Id);
                    if (job.Id == BuildingCatalog.FellTrees)
                    {
                        var wood = Forest.Fell(village, WorldMap.Footprint(plotId)!.Value,
                            amount * Forest.WoodPerTree / BuildingCatalog.FellWorkPerTree, now);
                        if (wood > 0)
                        {
                            produced.Add(new ResourceAmount(ResourceKind.Wood, wood));
                            Keep(citizen, village, ResourceKind.Wood, wood, carried, lost);
                        }
                    }
                }
            }
        }

        var overflowed = amount <= 0;
        var completed = site is null ? null : ConstructionRules.Advance(village, site, context.RecentCitizenCount, now, citizen);
        var (experienceHungry, hungry) = FinishMeal(citizen, village, now, true);

        var report = Finish(citizen, village, job.Id, job.Primary, job.Secondary, job.Skill, SkillRules.Challenge(citizen, job.Skill, 0),
            job.Id == "dismantle" || overflowed ? 0 : Math.Min(1, (double)amount / Math.Max(1, potential)), hungry,
            produced, carried, 0, overflowed ? 0 : amount, false, overflowed, experienceHungry);
        return report with { SiteId = plotId, BuildingCompleted = completed, Lost = lost };
    }

    public static string? ProjectWorkKey(TaskDefinition task) => task.Effect switch
    {
        TaskEffect.Build => VillageProject.BuildKey,
        TaskEffect.Survey => VillageProject.SurveyKey,
        TaskEffect.Deliver => task.Outputs[0].Resource.ToString(),
        _ => null,
    };

    private static double TaskYieldFactor(Citizen citizen, Village village, TaskDefinition task, DateTimeOffset now) =>
        YieldFactor(citizen, village, task.Primary, task.Secondary, now) * TechnologyRules.YieldMultiplier(village, task.Id)
        * SkillRules.YieldMultiplier(citizen, task.Skill, task.SkillLevel)
        * WeatherRules.YieldMultiplier(village, task, WorkedAt(citizen, now));

    private static int SiteWorkYield(Citizen citizen, Village village, JobDefinition job, DateTimeOffset now) =>
        YieldMath.Whole(job.PerCycle * YieldFactor(citizen, village, job.Primary, job.Secondary, now) / GameRules.StatFactorBase
            * SkillRules.YieldMultiplier(citizen, job.Skill, 0)
            * WeatherRules.JobMultiplier(village, WorkedAt(citizen, now)));

    private static CycleReport Finish(
        Citizen citizen,
        Village village,
        string taskId,
        Stat primary,
        Stat secondary,
        Skill skill,
        double challenge,
        double experienceShare,
        bool hungry,
        IReadOnlyList<ResourceAmount> produced,
        IReadOnlyList<ResourceAmount> carried,
        double moraleGained,
        int contributed,
        bool rareFind,
        bool overflowed,
        bool experienceHungry)
    {
        if (skill == Skill.Building && (overflowed || taskId == "dismantle")) experienceShare = 0;
        if (skill == Skill.Building) challenge = 1;
        var earned = GameRules.BaseExperiencePerCycle
                     * challenge
                     * (citizen.CycleTired ? GameRules.TiredExperienceMultiplier : 1)
                     * experienceShare
                     * (experienceHungry ? GameRules.HungryExperienceMultiplier : 1);
        var experience = YieldMath.Whole(earned);
        var secondaryExperience = YieldMath.Whole(earned * GameRules.SecondaryExperienceShare);

        var primaryResult = Progression.AddExperience(citizen.Stats, primary, experience);
        var secondaryResult = Progression.AddExperience(citizen.Stats, secondary, secondaryExperience);
        var skillLevelsGained = SkillRules.AddExperience(citizen, skill, experience);
        SkillRules.CatchUp(citizen);
        var usefulBuild = skill == Skill.Building && contributed > 0 && taskId != "dismantle";
        var renown = primaryResult.RenownGained + secondaryResult.RenownGained + (usefulBuild ? 1 : 0);
        citizen.ExhaustedCycles = citizen.CycleTired ? citizen.ExhaustedCycles + 1 : 0;
        if (citizen.ExhaustedCycles >= 3)
        {
            citizen.ExhaustedUntil = citizen.CycleEndsAt!.Value + DayCycle.DayLength / 2;
            citizen.ExhaustedCycles = 0;
        }
        citizen.Renown += renown;

        return new CycleReport(
            citizen.Id,
            citizen.Name,
            taskId,
            produced,
            carried,
            moraleGained,
            contributed,
            rareFind,
            primary,
            experience,
            secondary,
            secondaryExperience,
            skill,
            experience,
            skillLevelsGained,
            primaryResult.LevelsGained + secondaryResult.LevelsGained,
            renown,
            citizen.CycleTired,
            overflowed,
            Hungry: hungry,
            WorkedHungry: experienceHungry);
    }

    /// <summary>
    /// Experience uses hunger at the start of the cycle. The meal still happens at the end:
    /// a fed start keeps full experience even when that meal finds no food, and the report's
    /// <see cref="CycleReport.Hungry"/> says whether the citizen is hungry afterwards.
    /// </summary>
    private static (bool ExperienceHungry, bool Hungry) FinishMeal(Citizen citizen, Village village, DateTimeOffset now, bool eat)
    {
        var experienceHungry = eat && (citizen.CycleHungry ?? citizen.IsHungry);
        return (experienceHungry, eat && Eat(citizen, village, now));
    }

    /// <summary>
    /// Every <see cref="GameRules.CyclesPerMeal"/> finished cycles a citizen eats one food, from their hands first,
    /// then from personal inventory, then from public storage for public work. A hungry citizen tries again after every cycle. Returns true while the citizen is hungry.
    /// </summary>
    private static bool Eat(Citizen citizen, Village village, DateTimeOffset now)
    {
        citizen.CyclesSinceMeal++;
        if (citizen.CyclesSinceMeal < GameRules.CyclesPerMeal && !citizen.IsHungry)
        {
            return false;
        }

        return !FoodRules.Eat(citizen, village, now, allowPublic: !citizen.CyclePrivateOutput);
    }

    /// <summary>Stat factor with tiredness, morale and streak.</summary>
    private static double YieldFactor(Citizen citizen, Village village, Stat primary, Stat secondary, DateTimeOffset now) =>
        YieldMath.StatFactor(YieldMath.EffectiveStat(citizen.Stats.Get(primary), citizen.Stats.Get(secondary)))
        * (citizen.CycleTired ? GameRules.TiredYieldMultiplier : 1)
        * village.MoraleAt(now)
        * YieldMath.StreakFactor(citizen.Streak);

    private static void CheckReady(Citizen citizen, DateTimeOffset now)
    {
        SafeRest.RequireActive(citizen.RestState);
        if (citizen.Status != CitizenStatus.Active)
        {
            throw new GameRuleException("citizen.dormant", "Dormant citizens cannot work.");
        }

        HospitalRules.RequireReady(citizen, now);
        if (citizen.ExhaustedUntil > now)
            throw new GameRuleException("citizen.exhausted", "You fainted from exhaustion. Wait until you recover.",
                new Dictionary<string, object> { ["readyAt"] = citizen.ExhaustedUntil.Value });
        if (CombatRules.IsInjured(citizen, now))
        {
            throw new GameRuleException("citizen.injured", "You are wounded and cannot work until the wound heals. A remedy heals it at once.",
                new Dictionary<string, object> { ["readyAt"] = citizen.InjuredUntil!.Value });
        }

        if (citizen.DungeonRunId is not null)
        {
            throw new GameRuleException("dungeon.busy", "You are away in the dungeon.");
        }

        if (!citizen.CycleSettled && citizen.CycleEndsAt is not null && now < citizen.CycleEndsAt)
        {
            throw new GameRuleException("task.cycleRunning", "The current task is not finished yet.");
        }

        if (!citizen.CycleSettled)
        {
            throw new InvalidOperationException("The previous cycle must be settled before starting a new one.");
        }
    }

    /// <summary>The weather of a cycle is the weather it started in.</summary>
    private static DateTimeOffset WorkedAt(Citizen citizen, DateTimeOffset now) => citizen.CycleStartedAt ?? now;

    private static void Begin(Citizen citizen, string taskId, TimeSpan duration, double energyCost, Village village, DateTimeOffset now, TimeSpan? walkDuration = null)
    {
        citizen.ExhaustedCycles = Energy.TiredCycles(citizen, now);
        var renewing = citizen.GraceEndsAt is not null && now <= citizen.GraceEndsAt;
        citizen.Streak = renewing ? citizen.Streak + 1 : 0;

        energyCost = WorkEnergy.WithMedical(citizen, energyCost, now);
        var energy = citizen.CurrentEnergy(now, village);
        citizen.CycleTired = energy < energyCost;
        citizen.CycleHungry = citizen.IsHungry;
        citizen.StoredEnergy = citizen.CycleTired ? energy : energy - energyCost;
        citizen.EnergyUpdatedAt = now;

        citizen.ReservedSiteWork = null;
        citizen.ReservedProjectId = null;
        citizen.CurrentTargetPlotId = null;
        citizen.CurrentTaskId = taskId;
        citizen.CurrentTechnologyId = null;
        citizen.Spot = null;
        citizen.IdleSpot = null;
        citizen.CycleStartedAt = now;
        // Walking retains its normal speed. Only work is slowed by exhaustion.
        var task = GameCatalog.FindTask(taskId);
        var walking = walkDuration ?? (task is null ? TimeSpan.Zero : Walk(citizen, village, task));
        citizen.CycleEndsAt = now + (citizen.CycleTired ? duration + (duration > walking ? duration - walking : TimeSpan.Zero) * 2 : duration) + walking * (1/citizen.MedicalWalk(now)-1);
        citizen.CycleSettled = false;
        citizen.CycleInputCostBasis = 0;
        citizen.CycleInputCostBasisKnown = true;
        citizen.CycleOwnership = citizen.CyclePrivateOutput
            ? (task?.Inputs.Count > 0 ? WorkOwnership.PrivateCraft : WorkOwnership.PrivateExtraction)
            : WorkOwnership.PublicService;
        citizen.CyclePaymentMode = WorkPaymentMode.Legacy;
        citizen.CycleWorkOrderClaimId = null;
        citizen.CycleFarmTargets=[];
        citizen.CycleFarmContributionVersion=0;
    }

    private static void ApplyOwnership(Citizen citizen, TaskDefinition task, bool privateWorkplace, WorkOwnership? ownership)
    {
        if (ownership is null) return; // Older clients retain their existing workplace selection rules.
        if (ownership is not (WorkOwnership.PublicService or WorkOwnership.PublicContract or WorkOwnership.PrivateExtraction or WorkOwnership.PrivateCraft)
            || !WorkOrderRules.Supports(task))
            throw new GameRuleException("workOrder.ownership", "Choose a supported ownership mode before starting work.");
        var personal = ownership is WorkOwnership.PrivateExtraction or WorkOwnership.PrivateCraft;
        if (personal && (task.Inputs.Count > 0 ? ownership != WorkOwnership.PrivateCraft : ownership != WorkOwnership.PrivateExtraction)
            || personal && task.RequiredBuilding is not null && !privateWorkplace
            || !personal && privateWorkplace)
            throw new GameRuleException("workOrder.ownership", "The workplace, materials and result must have compatible owners.");
        citizen.CyclePrivateOutput = personal;
    }

    /// <summary>The place on the map a task is done at, unless it is done at the camp, in a building or all over the land.</summary>
    private static TilePoint? NaturalPlace(TaskDefinition task) =>
        task.Workplace != WorldMap.Camp && WorldMap.Places.TryGetValue(task.Workplace, out var place) ? place : null;

    /// <summary>Walking time of a task: from where the work happens to where its result is dropped.</summary>
    public static TimeSpan Walk(Citizen citizen, Village village, TaskDefinition task) =>
        Walk(citizen, village, task, Workplace(village, task));

    /// <summary>Where the task is done: the yard of the building it needs once one stands, else its place on the map.</summary>
    public static TilePoint Workplace(Village village, TaskDefinition task) =>
        task.RequiredBuilding is { } kind && village.Structures.FirstOrDefault(s => s.Kind == kind && s.OwnerAccountId is null && !s.Decommissioning && s.RuinedAt is null && s.Damage<100) is { } building
            ? WorldMap.Yard(building.PlotId)
            : WorldMap.Place(task.Workplace);

    /// <summary>
    /// A camp tree the player picked is walked to and from as it is. Other spots cannot be checked,
    /// so they may make the walk longer than from the workplace, never shorter.
    /// </summary>
    private static TimeSpan Walk(Citizen citizen, Village village, TaskDefinition task, TilePoint? spot, DateTimeOffset now)
    {
        if (FarmRules.Walk(village, task, now, citizen.CyclePrivateOutput ? citizen.AccountId : null) is { } farmWalk)
        {
            return farmWalk;
        }

        var standard = Walk(citizen, village, task);
        if (spot is not { } picked)
        {
            return standard;
        }

        var fromSpot = Walk(citizen, village, task, picked);
        return WorkSpots.TreeAt(village, task, picked, now) is not null || Wilderness.IsWild(task) || fromSpot > standard ? fromSpot : standard;
    }

    private static TimeSpan Walk(Citizen citizen, Village village, TaskDefinition task, TilePoint place)
    {
        var camp = WorldMap.Places[WorldMap.Camp];
        return task.Effect switch
        {
            TaskEffect.Resources when task.Outputs.Count > 0 =>
                WorldMap.Walk(place, village.DropPoint(task.Outputs[0].Resource, place)),
            TaskEffect.Expedition => WorldMap.Walk(place, camp),
            TaskEffect.Unload when citizen.Load.Count > 0 => WorldMap.Walk(camp, village.DropPoint(citizen.Load[0].Kind, camp)),
            _ => TimeSpan.Zero,
        };
    }

    private static void CheckProjectTask(Citizen citizen, TaskDefinition task, WorkContext context)
    {
        if (task.Effect is not (TaskEffect.Deliver or TaskEffect.Survey or TaskEffect.Build or TaskEffect.Supervise))
        {
            return;
        }

        var project = context.Project is { Status: ProjectStatus.Active } active
            ? active
            : throw new GameRuleException("project.none", "There is no construction project right now.");

        switch (task.Effect)
        {
            case TaskEffect.Deliver:
            {
                var resource = task.Outputs[0].Resource;
                var key = resource.ToString();
                if (project.Required(key) <= 0)
                {
                    throw new GameRuleException("project.materialNotNeeded", $"The project does not need {ResourceNames.Of(resource)}.", Resource(resource));
                }

                if (project.Remaining(key) == 0)
                {
                    throw new GameRuleException("project.materialComplete", $"Enough {ResourceNames.Of(resource)} has already been delivered.", Resource(resource));
                }

                RequireRoom(project, citizen, key, context);
                if (Available(citizen, context.Village, resource) < 1)
                {
                    throw new GameRuleException("project.storageEmpty", $"There is no {ResourceNames.Of(resource)} in storage. Gather some first.", Resource(resource));
                }

                break;
            }

            case TaskEffect.Survey:
                if (project.SurveyDone)
                {
                    throw new GameRuleException("project.surveyDone", "The survey is already finished.");
                }

                RequireRoom(project, citizen, VillageProject.SurveyKey, context);
                break;

            case TaskEffect.Build:
                if(project.StagedBridge && project.BridgeStage==4 && (citizen.Stats.Intelligence<3 || citizen.SkillLevel(Skill.Building)<1))
                    throw new GameRuleException("project.intelligence","Bridge inspection requires Intelligence 3 and Building 1.");
                if (!project.SurveyDone)
                {
                    throw new GameRuleException("project.surveyFirst", "An engineer must survey the site first.");
                }

                if (project.BuildDone)
                {
                    throw new GameRuleException("project.buildDone", "Construction is already finished.");
                }

                if (project.BuildProgress >= project.BuildLimit)
                {
                    throw new GameRuleException("project.needMaterials", "Not enough materials: deliver them to the site.");
                }

                if (project.Definition.NeedsEngineer && context.BuildersWorking >= context.SupervisionCapacity)
                {
                    throw context.SupervisionCapacity == 0
                        ? new GameRuleException("project.needSupervisor", "An engineer must supervise the construction.")
                        : new GameRuleException("project.supervisionFull", "All supervised builder places are taken. More engineers are needed.");
                }

                RequireRoom(project, citizen, VillageProject.BuildKey, context);
                break;

            case TaskEffect.Supervise:
                if (!project.Definition.NeedsEngineer)
                {
                    throw new GameRuleException("project.noSupervision", "This work is small enough to build without an engineer.");
                }

                if (!project.SurveyDone)
                {
                    throw new GameRuleException("project.surveyFirst", "An engineer must survey the site first.");
                }

                if (project.BuildDone)
                {
                    throw new GameRuleException("project.buildDone", "Construction is already finished.");
                }

                break;
        }
    }

    private static void RequireRoom(VillageProject project, Citizen citizen, string key, WorkContext context)
    {
        if (project.ContributionRoom(citizen.Id, key, context.ActiveCitizenCount) == 0)
        {
            throw new GameRuleException("project.shareTaken", "You already did your share of this part of the project. Let others work.");
        }
    }

    private static Dictionary<string, object> Resource(ResourceKind kind) => new() { ["resource"] = kind.ToString() };

    private static void CheckPrey(Village village, TaskDefinition task, DateTimeOffset now)
    {
        if (task.Hunt is not { } hunt || village.HerdOf(hunt.Prey).Current(now) >= WildlifeCatalog.MinimumToHunt)
        {
            return;
        }

        throw new GameRuleException("hunt.noAnimals",
            $"No {WildlifeCatalog.Get(hunt.Prey).Name} are left near the camp. Give them time to come back.",
            new Dictionary<string, object> { ["animal"] = hunt.Prey.ToString() });
    }

    /// <summary>
    /// A citizen whose hands and storage are both full of every good a task produces cannot start it.
    /// Expedition and roaming loot is random, so they need empty hands. Carrying to storage needs a load and room.
    /// </summary>
    private static void CheckHands(Citizen citizen, Village village, TaskDefinition task, DateTimeOffset now, FarmWorkTarget? farmTarget=null)
    {
        if (task.Effect == TaskEffect.Unload)
        {
            if (citizen.Load.Count == 0)
            {
                throw new GameRuleException("hands.empty", "Your hands are empty.");
            }

            if (citizen.Load.All(g => village.FreeSpace(g.Kind) == 0))
            {
                throw new GameRuleException("hands.noRoom", "Storage has no room for what you carry. Ask the Tsar or a Governor to plan more storage.");
            }

            return;
        }

        if (task.Effect == TaskEffect.Expedition && citizen.Load.Count > 0)
        {
            throw new GameRuleException("hands.fullExpedition", "Your hands are full. Unload or drop your load before an expedition.");
        }

        if (task.Effect == TaskEffect.Roam && citizen.Load.Count > 0)
        {
            throw new GameRuleException("hands.fullRoam", "Your hands are full. Unload or drop your load before roaming.");
        }

        var outputs = task.Effect switch
        {
            TaskEffect.Resources => task.Outputs,
            TaskEffect.Harvest when (farmTarget is null ? FarmRules.RipeBed(village, now, citizen.CyclePrivateOutput ? citizen.AccountId : null)
                : village.Fields.SingleOrDefault(b=>b.PlotId==farmTarget.PlotId && b.Index==farmTarget.Index && b.GenerationId==farmTarget.GenerationId)) is { Crop: { } crop } => FarmCatalog.Get(crop).Harvest,
            _ => [],
        };
        bool NoRoom(ResourceAmount output) =>
            citizen.CyclePrivateOutput ? PersonalInventory.Room(citizen, output.Resource) <= 0 : citizen.Carried(output.Resource) >= StorageRules.CarryLimit(citizen, now) && village.FreeSpace(output.Resource) <= 0;
        var blocked = outputs.FirstOrDefault(NoRoom);
        if (blocked is not null && outputs.All(NoRoom))
        {
            throw new GameRuleException("hands.full",
                $"Your hands are full of {ResourceNames.Of(blocked.Resource)} and storage has no room for it. Carry it to storage or pick another job.",
                Resource(blocked.Resource));
        }
    }

    private static int Available(Citizen citizen, Village village, ResourceKind kind) =>
        citizen.CyclePrivateOutput ? PersonalInventory.Amount(citizen, kind) : citizen.Carried(kind) + village.Stock(kind);

    private static InventoryCost TakeFromHandsThenStorage(Citizen citizen, Village village, ResourceKind kind, int amount)
    {
        if (amount <= 0) return new(0, true);
        if (citizen.CyclePrivateOutput) return PersonalInventory.RemoveWithCost(citizen, kind, amount);
        var fromHands = citizen.TakeFromLoad(kind, amount);
        village.Take(kind, amount - fromHands);
        return new(0, true);
    }

    private static int Min(params int[] values) => values.Min();
}
