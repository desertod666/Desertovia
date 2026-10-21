using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Desertod.Domain.Director;
using Desertod.Api.Auth;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Desertod.Api.Game;

public sealed record AlmightyDelivery(string State);
public interface IAlmightyBridge
{
    Task<AlmightyAnswer?> AnalyzeAsync(AlmightyReview review, CancellationToken ct);
    Task<AlmightyDelivery> NotifyAsync(AlmightyNotice notice, CancellationToken ct);
    Task<AlmightyDelivery> NotifyCivicAsync(CivicNotice notice, CancellationToken ct) => Task.FromResult(new AlmightyDelivery("Unavailable"));
    Task<AlmightyDelivery> NotifyReportAsync(PlayerReportNotice notice, CancellationToken ct) => Task.FromResult(new AlmightyDelivery("Unavailable"));
    Task<SramoslavReply?> SpeakAsync(object request, CancellationToken ct);
}
public sealed class AlmightyBridge(IServiceScopeFactory scopes) : IAlmightyBridge
{
    private async Task<T?> Call<T>(object request, CancellationToken ct,bool requireValid=false)
    {
        using var scope = scopes.CreateScope();
        var bridge = scope.ServiceProvider.GetRequiredService<TelegramBrowserBridge>();
        try { return bridge.Enabled ? await bridge.CallAsync<T>("director", request, ct) : default; }
        catch (Exception e) when (!requireValid && e is (InvalidOperationException or JsonException)) { return default; }
    }
    public async Task<AlmightyAnswer?> AnalyzeAsync(AlmightyReview r, CancellationToken ct)
    {
        try {
            var json=await Call<JsonElement?>(new { kind = "analyze", requestId = r.Lease, worldId = r.VillageId, snapshot = r.Snapshot, intent = r.Intent }, ct,requireValid:true);
            return json is {} value?AlmightyAnalysis.ReadAnswer(value,r.Options):AlmightyAnalysis.Failed("bridge.unavailable");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new("none", "", "", "bridge.timeout"); }
        catch (DirectorBridgePayloadException) { return AlmightyAnalysis.Failed("bridge.payload"); }
        catch (JsonException) { return AlmightyAnalysis.Failed("bridge.json"); }
        catch (InvalidOperationException) { return AlmightyAnalysis.Failed("bridge.fields"); }
        catch (HttpRequestException e) when(e.StatusCode==System.Net.HttpStatusCode.TooManyRequests) { return AlmightyAnalysis.Failed("bridge.rate_limit"); }
        catch (HttpRequestException) { return new("none", "", "", "bridge.http"); }
    }
    public async Task<AlmightyDelivery> NotifyAsync(AlmightyNotice n, CancellationToken ct) =>
        await Call<AlmightyDelivery>(new { kind = "notify", notice = n }, ct) ?? new("Unavailable");
    public async Task<AlmightyDelivery> NotifyCivicAsync(CivicNotice n, CancellationToken ct) =>
        await Call<AlmightyDelivery>(new { kind = "civic_notify", notice = n }, ct) ?? new("Unavailable");
    public async Task<AlmightyDelivery> NotifyReportAsync(PlayerReportNotice n, CancellationToken ct) =>
        await Call<AlmightyDelivery>(new { kind = "feedback_notify", notice = n }, ct) ?? new("Unavailable");
    public Task<SramoslavReply?> SpeakAsync(object request, CancellationToken ct) => Call<SramoslavReply>(request, ct);
}

/// <summary>One persisted director per world; bounded economic delegation is separate from event approval.</summary>
public sealed class AlmightyService(GameService game, IDbContextFactory<DesertodDbContext> factory,
    IAlmightyBridge bridge, IConfiguration config, TimeProvider time, ILogger<AlmightyService> logger)
{
    public bool Enabled => config.GetValue<bool>("Game:AlmightyEnabled");
    public async Task TickAsync(CancellationToken ct)
    {
        if (!Enabled) return;
        await using var db = await factory.CreateDbContextAsync(ct);
        var primary = config["Hosting:PrimaryVillage"];
        // Hosting the single approved kingdom does not grant the director access to other worlds.
        if (string.IsNullOrWhiteSpace(primary)) return;
        var ids = await db.Villages.Where(v => v.Slug == primary).Select(v => v.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            var review = await game.BeginAlmightyReviewAsync(id, ct);
            if (review is not null)
            {
                AlmightyAnswer? answer = null;
                try { answer = await AlmightyAnalysis.RunAsync(bridge,review,time,ct); }
                catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { logger.LogWarning("Director model unavailable ({Type}).", e.GetType().Name); }
                await game.CompleteAlmightyReviewAsync(review, answer, ct);
            }
            await DeliverAsync(id, ct);
            var now = time.GetUtcNow();
            await db.AlmightyProposals.Where(p => p.VillageId == id && p.NoticeState == "Sending" && p.CreatedAt < now.AddMinutes(-2)).ExecuteUpdateAsync(s => s.SetProperty(p => p.NoticeState, "Unknown"), ct);
            await db.AlmightyProposals.Where(p => p.VillageId == id && p.Status == "Pending" && p.ExpiresAt <= now).ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, "Expired"), ct);
            await db.AlmightyProposals.Where(p => p.VillageId == id && p.Status != "Pending" && p.CreatedAt < now.AddDays(-30)).ExecuteDeleteAsync(ct);
            // Hypothetical choices have no money postings. Their comparison window is14days; executed financial audits stay.
            await db.DirectorFinanceActions.Where(a => a.VillageId == id && !a.Applied && a.At < now.AddDays(-14)).ExecuteDeleteAsync(ct);
            // Financial receipts retain their authoritative admission, rather than breaking cleanup after14days.
            await db.AlmightyObservations.Where(o => o.VillageId == id && o.At < now.AddDays(-14)
                && !db.DirectorFinanceActions.Any(a => a.VillageId == id && a.ReviewId == o.Id)).ExecuteDeleteAsync(ct);
        }
    }
    private async Task DeliverAsync(Guid villageId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var now = time.GetUtcNow();
        // Atomically claim one notification, before sending. Unknown Telegram outcomes are not resent blindly.
        var p = await db.AlmightyProposals.AsNoTracking().Where(p => p.VillageId == villageId && p.Status == "Pending" && p.ExpiresAt > now && p.NoticeState == "Queued")
            .OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(ct);
        if (p is null) return;
        var claimed = await db.AlmightyProposals.Where(x => x.Id == p.Id && x.NoticeState == "Queued" && x.Status == "Pending" && x.ExpiresAt > now).ExecuteUpdateAsync(s => s.SetProperty(x => x.NoticeState, "Sending"), ct);
        if (claimed == 0) return;
        var owner = await db.Villages.Where(v => v.Id == villageId).Select(v => v.OwnerAccountId).SingleAsync(ct);
        var ownerId = await db.ExternalLogins.Where(l => l.AccountId == owner && l.Provider == "telegram").Select(l => l.ProviderUserId).SingleOrDefaultAsync(ct);
        var slug = await db.Villages.Where(v => v.Id == villageId).Select(v => v.Slug).SingleAsync(ct);
        var options = JsonDocument.Parse(p.Snapshot).RootElement.GetProperty("options").Deserialize<List<AlmightyOption>>(GameService.AlmightyJson) ?? [];
        var option = options.FirstOrDefault(o => o.Id == p.Option);
        var state = "Unavailable";
        if (ownerId is not null && option is not null)
        {
            try { state = (await bridge.NotifyAsync(new(p.Id, slug, ownerId, option.Title, p.Reason, option.Effect, p.ExpiresAt), ct)).State; }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { state = "Unknown"; logger.LogWarning("Director notice outcome unknown ({Type}).", e.GetType().Name); }
        }
        await db.AlmightyProposals.Where(x => x.Id == p.Id && x.NoticeState == "Sending").ExecuteUpdateAsync(s => s.SetProperty(x => x.NoticeState, state), ct);
    }

    public static bool Trusted(HttpContext http, IOptions<TelegramOptions> options)
    {
        if (!options.Value.IsConfigured) return false;
        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("desertovia-bridge:" + options.Value.BotToken)));
        var actual = http.Request.Headers["X-Desertovia-Bridge"].ToString();
        return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
    }
}
public sealed class AlmightyWorker(AlmightyService director, TimeProvider time, ILogger<AlmightyWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await director.TickAsync(stoppingToken); }
            catch (Exception e) when (e is not OperationCanceledException) { logger.LogError("Director tick failed ({Type}).", e.GetType().Name); }
        }
    }
}
public sealed record AlmightyTelegramVote(Guid ProposalId, string OwnerId, bool Accept);
