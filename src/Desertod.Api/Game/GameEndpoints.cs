using System.Security.Claims;
using Desertod.Api.Auth;

namespace Desertod.Api.Game;

public static class GameEndpoints
{
    public static void MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapFeedbackIdentityEndpoints();
        app.MapPlayerReportEndpoints();
        app.MapPost("/api/civic/vote", async (CivicTelegramVote vote, HttpContext http,
            Microsoft.Extensions.Options.IOptions<TelegramOptions> options, GameService game, IConfiguration config, CancellationToken ct) => {
            if (!AlmightyService.Trusted(http, options)) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(config["Hosting:PrimaryVillage"]) || vote.Slug != config["Hosting:PrimaryVillage"]) return Results.Forbid();
            var state=await game.CivicTelegramDecisionAsync(vote.Slug, vote.OwnerId, vote.OrderId, vote.Approve, ct);
            var status=state.CivicOrders.Single(o=>o.Id==vote.OrderId).Status;
            return Results.Ok(new { status, result = status switch { "rejected"=>"Заявка отклонена.", "completed"=>"Заявка выполнена.", "working"=>"Заявка одобрена. Работы начаты.", _=>"Заявка ожидает решения." } });
        });
        app.MapPost("/api/director/vote", async (AlmightyTelegramVote vote, HttpContext http,
            Microsoft.Extensions.Options.IOptions<TelegramOptions> options, GameService game, CancellationToken ct) => {
            if (!AlmightyService.Trusted(http, options)) return Results.Unauthorized();
            var result = await game.DecideAlmightyAsync(vote.ProposalId, null, vote.OwnerId, vote.Accept, ct);
            return Results.Ok(new { result });
        });
        var player = app.MapGroup("/api").RequireAuthorization();
        player.MapGet("/villages/{slug}/farm/history", (string slug,string? plotId,Guid? before,GameService game,CancellationToken ct)=>
            game.GetFarmHistoryAsync(slug,plotId,before,ct));
        player.MapPut("/villages/{slug}/farm/admission", (string slug, FarmAdmissionRequest request, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            game.EnableFarmerAdmissionAsync(slug,user.RequireAccountId(),request,ct));
        player.MapPut("/villages/{slug}/citizens/{citizenId:guid}/farm-access", (string slug, Guid citizenId, FarmAccessRequest request, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            game.SetFarmAccessAsync(slug,user.RequireAccountId(),citizenId,request,ct));
        player.MapPut("/villages/{slug}/me/rest", (string slug, RestRequest request, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            game.SetRestAsync(slug, user.RequireAccountId(), request, ct));

        player.MapGet("/villages/{slug}/combat-diagnostics",(string slug,ClaimsPrincipal user,GameService game,CancellationToken ct)=>game.GetCombatDiagnosticsAsync(slug,user.RequireAccountId(),ct));
        player.MapGet("/villages/{slug}/monastery", (string slug, ClaimsPrincipal user, GameService game, CancellationToken ct) => game.GetMonasteryAsync(slug,user.RequireAccountId(),ct));
        player.MapPost("/villages/{slug}/monastery", (string slug, MonasteryRequest request, ClaimsPrincipal user, GameService game, CancellationToken ct) => game.TradeMonasteryAsync(slug,user.RequireAccountId(),request,ct));
        player.MapGet("/villages/{slug}/economy", (string slug, Guid? before, string? asset, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            game.GetEconomyAsync(slug, user.RequireAccountId(), before, asset, ct));
        player.MapPost("/villages/{slug}/economy", (string slug, EconomyRequest request, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            game.ChangeEconomyAsync(slug, user.RequireAccountId(), request, ct));
        player.MapPut("/villages/{slug}/me/defense", async (string slug, AlmightyMode request, ClaimsPrincipal user, GameService game, CancellationToken ct) => {
            await game.SetDefenseSectorAsync(slug, user.RequireAccountId(), request.Mode, ct); return Results.Ok();
        });
        player.MapPut("/villages/{slug}/battles/{id:guid}/order", async (string slug, Guid id, BattleOrder request, ClaimsPrincipal user, GameService game, CancellationToken ct) => {
            await game.SetBattleOrderAsync(slug,user.RequireAccountId(),id,request,ct); return Results.Ok();
        });
        player.MapPut("/villages/{slug}/gate", async (string slug, GateOrder request, ClaimsPrincipal user, GameService game, CancellationToken ct) => {
            await game.SetGateAsync(slug,user.RequireAccountId(),request,ct); return Results.Ok();
        });
        player.MapGet("/villages/{slug}/director", (string slug, ClaimsPrincipal user, GameService game, AlmightyService director, CancellationToken ct) =>
            game.GetAlmightyAsync(slug, user.RequireAccountId(), director.Enabled, ct));
        player.MapPut("/villages/{slug}/director/mode", async (string slug, AlmightyMode request, ClaimsPrincipal user, GameService game, CancellationToken ct) => {
            await game.SetAlmightyModeAsync(slug, user.RequireAccountId(), request.Mode, ct); return Results.Ok();
        });
        player.MapPost("/villages/{slug}/director/{id:guid}/vote", async (Guid id, AlmightyVote vote, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            Results.Ok(new { result = await game.DecideAlmightyAsync(id, user.RequireAccountId(), null, vote.Accept, ct) }));
        player.MapGet("/villages/{slug}/hospital", (string slug, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            game.GetHospitalAsync(slug, user.RequireAccountId(), ct));
        player.MapPost("/villages/{slug}/hospital/heal", (string slug, HospitalHealRequest request, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            game.TreatAsync(slug, user.RequireAccountId(), request, ct));
        player.MapPost("/villages/{slug}/hospital/{plotId}/upgrade", (string slug, string plotId, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            game.UpgradeHospitalAsync(slug, user.RequireAccountId(), plotId, ct));
        player.MapPost("/villages/{slug}/sramoslav", (string slug, SramoslavQuestion request, ClaimsPrincipal user,
            SramoslavService service, CancellationToken cancellationToken) =>
            service.AskAsync(slug, user.RequireAccountId(), request.Text, cancellationToken)).RequireRateLimiting(ChatService.RateLimit);

        player.MapPost("/villages/{slug}/civic", (string slug, CivicRequest request, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            game.CivicRequestAsync(slug, user.RequireAccountId(), request, ct));
        player.MapPost("/villages/{slug}/civic/{id:guid}", (string slug, Guid id, CivicDecision request, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            game.CivicDecisionAsync(slug, user.RequireAccountId(), id, request, ct));
        player.MapPost("/villages/{slug}/rescue/{patientId:guid}", (string slug,Guid patientId,ClaimsPrincipal user,GameService game,CancellationToken ct)=>
            game.RescueAsync(slug,user.RequireAccountId(),patientId,ct));
        player.MapPost("/villages/{slug}/rescue/request", (string slug,ClaimsPrincipal user,GameService game,CancellationToken ct)=>
            game.RequestRescueAsync(slug,user.RequireAccountId(),ct));
        player.MapGet("/catalog", (CatalogDto catalog) => catalog);

        player.MapGet("/villages/public", async (GameService game, IConfiguration config, CancellationToken cancellationToken) =>
        {
            var villages = await game.ListPublicVillagesAsync(cancellationToken);
            var primary = config["Hosting:PrimaryVillage"];
            return villages.Where(v => string.IsNullOrEmpty(primary) || v.Slug == primary).ToArray();
        });

        player.MapGet("/villages/{slug}", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.GetVillageAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapGet("/villages", async (ClaimsPrincipal user, GameService game, IConfiguration config, CancellationToken cancellationToken) =>
        {
            var villages = await game.ListVillagesAsync(user.RequireAccountId(), cancellationToken);
            var primary = config["Hosting:PrimaryVillage"];
            return villages.Where(v => string.IsNullOrEmpty(primary) || v.Slug == primary).ToArray();
        });

        player.MapPost("/villages", (CreateVillageRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.CreateVillageAsync(user.RequireAccountId(), request, cancellationToken));

        player.MapDelete("/villages/{slug}", async (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
        {
            await game.DeleteVillageAsync(slug, user.RequireAccountId(), cancellationToken);
            return Results.NoContent();
        });

        player.MapPost("/villages/{slug}/invite", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.NewInviteCodeAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapGet("/villages/{slug}/me", async (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            await game.GetCitizenAsync(slug, user.RequireAccountId(), cancellationToken) is { } citizen
                ? Results.Ok(citizen)
                : Results.NotFound());

        player.MapDelete("/villages/{slug}/me", async (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
        {
            await game.LeaveVillageAsync(slug, user.RequireAccountId(), cancellationToken);
            return Results.NoContent();
        });

        player.MapPost("/villages/{slug}/citizens", (string slug, JoinVillageRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.JoinVillageAsync(slug, user.RequireAccountId(), request, cancellationToken));

        player.MapPut("/villages/{slug}/citizens/{citizenId:guid}/subtitle", (string slug, Guid citizenId, SetSubtitleRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.SetSubtitleAsync(slug, user.RequireAccountId(), citizenId, request.Subtitle, cancellationToken));

        player.MapPut("/villages/{slug}/citizens/{citizenId:guid}/governor", (string slug, Guid citizenId, SetGovernorRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.SetGovernorAsync(slug, user.RequireAccountId(), citizenId, request.IsGovernor, cancellationToken));

        player.MapPost("/villages/{slug}/me/task", (string slug, StartTaskRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.StartTaskAsync(slug, user.RequireAccountId(), request, cancellationToken));

        player.MapDelete("/villages/{slug}/me/task", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.CancelTaskAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/me/drop", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.DropLoadAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/research/knowledge", (string slug,KnowledgeInvestment request,ClaimsPrincipal user,GameService game,CancellationToken ct)=>
            game.InvestKnowledgeAsync(slug,user.RequireAccountId(),request,ct));
        player.MapPost("/villages/{slug}/me/meal", (string slug,MealRequest request,ClaimsPrincipal user,GameService game,CancellationToken ct)=>
            game.EatMealAsync(slug,user.RequireAccountId(),request,ct));
        player.MapPost("/villages/{slug}/me/eat", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.EatAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/me/drink", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.DrinkAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/me/heal", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.HealAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/fortifications/upgrade", (string slug, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            game.UpgradeFortificationsAsync(slug,user.RequireAccountId(),ct));

        player.MapGet("/villages/{slug}/raid-editor", (string slug, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            game.GetRaidEditorAsync(slug, user.RequireAccountId(), ct));
        player.MapPost("/villages/{slug}/raid-editor/preview", (string slug, Desertod.Domain.Combat.RaidSettings settings, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            game.PreviewRaidAsync(slug, user.RequireAccountId(), settings, ct));
        player.MapPost("/villages/{slug}/raid-editor/launch", (string slug, RaidLaunchRequest request, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            game.LaunchRaidAsync(slug, user.RequireAccountId(), request, ct));
        player.MapPut("/villages/{slug}/raid-editor/presets", (string slug, RaidPresetRequest request, ClaimsPrincipal user, GameService game, CancellationToken ct) =>
            game.SaveRaidPresetAsync(slug, user.RequireAccountId(), request, ct));

        player.MapPost("/villages/{slug}/undead", (string slug, CallUndeadRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.CallUndeadAsync(slug, user.RequireAccountId(), request, cancellationToken));

        player.MapDelete("/villages/{slug}/undead", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.CancelUndeadAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/dungeon", (string slug, OpenDungeonRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.OpenDungeonAsync(slug, user.RequireAccountId(), request.DungeonId, cancellationToken));

        player.MapDelete("/villages/{slug}/dungeon", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.CancelDungeonAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapGet("/villages/{slug}/me/dungeon", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.GetHeroClassesAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/me/dungeon", (string slug, JoinDungeonRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.JoinDungeonAsync(slug, user.RequireAccountId(), request.Class, cancellationToken));

        player.MapDelete("/villages/{slug}/me/dungeon", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.LeaveDungeonAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/me/take", (string slug, TakeRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.TakeFromStorageAsync(slug, user.RequireAccountId(), request, cancellationToken));

        player.MapPost("/villages/{slug}/me/goto", (string slug, GoToRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.GoToAsync(slug, user.RequireAccountId(), request, cancellationToken));
        player.MapPost("/villages/{slug}/me/journey/resume",(string slug,ResumeJourneyRequest request,ClaimsPrincipal user,GameService game,CancellationToken ct)=>
            game.ResumeJourneyAsync(slug,user.RequireAccountId(),request,ct));

        player.MapPost("/villages/{slug}/me/job", (string slug, StartJobRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.StartJobAsync(slug, user.RequireAccountId(), request, cancellationToken));

        player.MapPost("/villages/{slug}/me/research", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.StartResearchAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/me/forecast", (string slug, ForecastRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.ForecastAsync(slug, user.RequireAccountId(), request.Rain, cancellationToken));

        player.MapPost("/villages/{slug}/me/vote", (string slug, VoteRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.VoteAsync(slug, user.RequireAccountId(), request.TechnologyId, cancellationToken));

        player.MapPost("/villages/{slug}/poll", (string slug, OpenPollRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.OpenPollAsync(slug, user.RequireAccountId(), request.TechnologyIds, cancellationToken));

        player.MapDelete("/villages/{slug}/poll", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.CancelPollAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/tsar/goto", (string slug, GoToRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.TsarGoToAsync(slug, user.RequireAccountId(), request, cancellationToken));

        player.MapPost("/villages/{slug}/blueprints", (string slug, PlaceBlueprintRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.PlaceBlueprintAsync(slug, user.RequireAccountId(), request, cancellationToken));

        player.MapDelete("/villages/{slug}/blueprints/{plotId}", (string slug, string plotId, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.CancelBlueprintAsync(slug, user.RequireAccountId(), plotId, cancellationToken));

        player.MapGet("/villages/{slug}/chat", (string slug, ClaimsPrincipal user, ChatService chat, CancellationToken cancellationToken) =>
            chat.HistoryAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/chat", (string slug, SendChatRequest request, ClaimsPrincipal user, ChatService chat, CancellationToken cancellationToken) =>
                chat.SendAsync(slug, user.RequireAccountId(), request.Text, cancellationToken, request.Mentions))
            .RequireRateLimiting(ChatService.RateLimit);
    }
}
