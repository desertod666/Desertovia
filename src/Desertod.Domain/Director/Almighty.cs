using Desertod.Domain.Combat;

namespace Desertod.Domain.Director;

public sealed class AlmightyState
{
    public Guid VillageId { get; set; }
    public string Mode { get; set; } = "Propose";
    public long Version { get; set; }
    public long PolicyVersion { get; set; }
    public string[] DisabledKinds { get; set; } = [];
    public string Intent { get; set; } = "Наблюдать за городом и предлагать царю полезные события.";
    public DateTimeOffset NextReviewAt { get; set; }
    public DateTimeOffset? LastReviewAt { get; set; }
    public DateTimeOffset? RestUntil { get; set; }
    public Guid? Lease { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset? LastNoticeAt { get; set; }
    public string LastSignal { get; set; } = "";
    public string LastError { get; set; } = "";
    public DateTimeOffset BudgetDay { get; set; }
    public int CallsToday { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string LastSnapshot { get; set; } = "{}";
}

public sealed class AlmightyProposal
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VillageId { get; set; }
    public Guid ReviewId { get; set; }
    public string Option { get; set; } = "none";
    public string Reason { get; set; } = "";
    public string Intent { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string NoticeState { get; set; } = "Queued";
    [System.Text.Json.Serialization.JsonIgnore]
    public string Snapshot { get; set; } = "{}";
    public string Result { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Title => Descriptor("title");
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Effect => Descriptor("effect");
    private string Descriptor(string property)
    {
        try {
            using var doc = System.Text.Json.JsonDocument.Parse(Snapshot);
            foreach(var option in doc.RootElement.GetProperty("options").EnumerateArray())
                if(option.GetProperty("id").GetString()==Option) return option.GetProperty(property).GetString() ?? Option;
        } catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException) { }
        return Option;
    }
}

public sealed class AlmightyObservation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VillageId { get; set; }
    public DateTimeOffset At { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string Snapshot { get; set; } = "{}";
    public string Option { get; set; } = "none";
    public string Reason { get; set; } = "";
}

/// <summary>Durable acknowledgement and audit of a category request, including no-op requests.</summary>
public sealed class AlmightyPolicyChange
{
    public Guid VillageId { get; set; }
    public Guid RequestId { get; set; }
    public Guid ActorId { get; set; }
    public string Kind { get; set; } = "";
    public bool Enabled { get; set; }
    public long ExpectedPolicyVersion { get; set; }
    public long PolicyVersion { get; set; }
    public bool Changed { get; set; }
    public DateTimeOffset At { get; set; }
    public string[] DisabledKinds { get; set; } = [];
    public Guid[] SupersededProposals { get; set; } = [];
}

public sealed record AlmightyFacts(int Population, int Online, int ReadyGuards, int Patients, int PendingRaids,
    int BuildingSites, int DamagedBuildings, int RecentLosses, bool Peaceful, bool Resting, bool Defeated = false);
public sealed record AlmightyOption(string Id, string Title, string Effect, EnemyKind? Enemy = null, string? Sector = null, string? Composition = null);

public sealed record AlmightyRaidPressure(bool Eligible, bool ProposalDue, IReadOnlyList<string> BlockedBy, DateTimeOffset NextProposalAt);

public static class AlmightyPolicy
{
    public static readonly TimeSpan ReviewInterval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan ImportantInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ProposalLifetime = TimeSpan.FromMinutes(30);
    public const int DailyModelLimit = 96;
    public static readonly TimeSpan RaidQuietInterval = TimeSpan.FromHours(1);
    public static readonly TimeSpan RaidProposalInterval = TimeSpan.FromHours(2);
    public const int MinimumReadyResidents = 3;

    public static string[] RaidOptionIds() => EnemyCatalog.Enemies.Where(e => e.Undead && !e.DungeonOnly)
        .SelectMany(e => {
            var prefix = e.Kind.ToString().ToLowerInvariant() + "s";
            return new[] { prefix + "_small" }.Concat(RaidTactics.Sectors.SelectMany(sector =>
                new[] { prefix + "_" + sector, prefix + "_" + sector + "_swarm", prefix + "_" + sector + "_elite", prefix + "_" + sector + "_boss" }));
        }).ToArray();

    public static IReadOnlyList<string> RaidBlockers(AlmightyFacts f)
    {
        var blocked = new List<string>();
        if (f.Peaceful) blocked.Add("new_village_peace");
        if (f.Resting) blocked.Add("approved_rest_or_raid_cooldown");
        if (f.PendingRaids > 0) blocked.Add("raid_in_progress_or_announced");
        if (f.ReadyGuards < MinimumReadyResidents || f.Online < MinimumReadyResidents) blocked.Add("fewer_than_three_ready_online");
        if (f.RecentLosses > 0) blocked.Add("recent_defeat");
        if (f.Defeated) blocked.Add("village_defeated");
        return blocked;
    }

    public static AlmightyRaidPressure RaidPressure(AlmightyFacts f, DateTimeOffset now,
        DateTimeOffset lastEncounterAt, DateTimeOffset? lastProposalAt)
    {
        var next = lastEncounterAt + RaidQuietInterval;
        if (lastProposalAt is { } proposal && proposal + RaidProposalInterval > next)
            next = proposal + RaidProposalInterval;
        var blockers = RaidBlockers(f);
        return new(blockers.Count == 0, blockers.Count == 0 && now >= next, blockers, next);
    }
    public static IReadOnlyList<AlmightyOption> Options(AlmightyFacts f, IReadOnlyList<RaidApproach>? approaches = null, double? raidBudget = null)
    {
        var result = new List<AlmightyOption> {
            new("none", "Наблюдать", "Не менять мир и не беспокоить царя без причины"),
            new("economy_report", "Разобрать нужды города", "Только рекомендации по известным запасам/работам; деньги и цены не меняются") };
        if (!f.Resting && (f.RecentLosses > 0 || f.Online >= MinimumReadyResidents && f.ReadyGuards < MinimumReadyResidents && f.Patients >= MinimumReadyResidents))
            result.Add(new("rest", "Передышка на 2 реальных часа", "Не назначать новые автоматические нападения; объявленные волны сохраняются"));
        if (f.BuildingSites > 0 || f.DamagedBuildings > 0)
            result.Add(new("work_notice", "Объявить о нужной работе", "Одна короткая реплика о существующей стройке/ремонте; без ускорения, выдачи ресурсов и принуждения"));
        if (RaidBlockers(f).Count == 0)
        {
            foreach (var enemy in EnemyCatalog.Enemies.Where(e => e.Undead && !e.DungeonOnly))
            {
                var prefix = enemy.Kind.ToString().ToLowerInvariant() + "s";
                result.Add(new(prefix + "_small", "Малая волна: " + enemy.Name,
                    $"Предупреждение3 реальные минуты; живой бой с отдельными врагами. Состав ограничен готовностью деревни при объявлении; ранение{enemy.InjuryMinutes} реальных минут. Обычные награды и ремонтируемый ущерб.", enemy.Kind));
                foreach (var approach in approaches ?? [])
                {
                    foreach(var profile in RaidBalance.Compositions)
                    {
                        if(profile=="boss" && (raidBudget is not {} budget || RaidBalance.NormalizeComposition(enemy.Kind,profile,budget)!=profile))continue;
                        var id=prefix+"_"+approach.Sector+(profile=="mixed"?"":"_"+profile);
                        var label=profile=="swarm"?"много слабых":profile=="elite"?"немного сильных":profile=="boss"?"1–3 босса, тяжёлые удары с предупреждением":"смешанный состав";
                        result.Add(new(id,enemy.Name+": "+approach.Sector+", "+label,
                            "Цель — разорить имущество и запасы, а не охотиться за жителями. Одна сторона, обход толпы или пролом, отдельные цели. Общий бюджет по готовности при объявлении, без скрытого усиления после подготовки.",
                            enemy.Kind,approach.Sector,profile));
                    }
                }
            }
        }
        return result;
    }
    public static bool IsMode(string mode) => mode is "Off" or "Observe" or "Propose" or "AutoEconomy";
}
