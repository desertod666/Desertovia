using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Desertod.Api.Auth;
using Desertod.Domain;
using Desertod.Domain.Notifications;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed record NotificationPreferenceDto(long Revision,bool Configured,bool BrowserEnabled,IReadOnlyList<string> Categories,
    bool QuietEnabled,int QuietStartMinute,int QuietEndMinute,string TimeZoneId,DateTimeOffset? ConsentedAt,DateTimeOffset? UpdatedAt)
{ public DateTimeOffset ServerNow { get; init; } }
public sealed record NotificationPreferenceRequest(Guid RequestId,long ExpectedRevision,bool BrowserEnabled,IReadOnlyList<string> Categories,
    bool QuietEnabled,int QuietStartMinute,int QuietEndMinute,string TimeZoneId);
public sealed record NotificationPreferenceResult(NotificationPreferenceDto Settings,bool Replayed);

public static class NotificationPreferenceEndpoints
{
    public static void MapNotificationPreferenceEndpoints(this IEndpointRouteBuilder app)
    {
        var own=app.MapGroup("/api/me/notification-preferences").RequireAuthorization();
        own.MapGet("",(ClaimsPrincipal user,NotificationPreferenceService service,CancellationToken ct)=>service.GetAsync(user.RequireAccountId(),ct));
        own.MapPut("",(NotificationPreferenceRequest request,ClaimsPrincipal user,NotificationPreferenceService service,CancellationToken ct)=>
            service.SaveAsync(user.RequireAccountId(),request,ct));
    }
}

public sealed class NotificationPreferenceService(IDbContextFactory<DesertodDbContext> factory,TimeProvider clock)
{
    private static readonly IReadOnlyDictionary<string,NotificationCategory> Categories=new Dictionary<string,NotificationCategory>
    { ["work"]=NotificationCategory.Work,["building"]=NotificationCategory.Building,["farm"]=NotificationCategory.Farm,
      ["adventure"]=NotificationCategory.Adventure,["danger"]=NotificationCategory.Danger,["civic"]=NotificationCategory.Civic };
    private NotificationPreferenceDto Dto(NotificationPreference? stored)
    {
        var s=stored??new NotificationPreference();
        return new(s.Revision,stored is not null,s.BrowserEnabled,Categories.Where(c=>(s.Categories&c.Value)!=0).Select(c=>c.Key).ToArray(),
            s.QuietEnabled,s.QuietStartMinute,s.QuietEndMinute,s.TimeZoneId,s.ConsentedAt,s.UpdatedAt){ServerNow=clock.GetUtcNow()};
    }

    public async Task<NotificationPreferenceDto> GetAsync(Guid account,CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        if(!await db.Accounts.AnyAsync(a=>a.Id==account,ct))throw new UnauthorizedAccessException();
        return Dto(await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(s=>s.AccountId==account,ct));
    }

    public async Task<NotificationPreferenceResult> SaveAsync(Guid account,NotificationPreferenceRequest request,CancellationToken ct)
    {
        if(request.RequestId==Guid.Empty || request.ExpectedRevision<0 || request.Categories is null || request.Categories.Count>6
            || request.Categories.Distinct().Count()!=request.Categories.Count || request.Categories.Any(c=>c is null||!Categories.ContainsKey(c)))
            throw new GameRuleException("notifications.settings","Choose valid notification settings.");
        var categories=request.Categories.Aggregate((NotificationCategory)0,(value,key)=>value|Categories[key]);
        NotificationPreferenceRules.Validate(categories,request.QuietEnabled,request.QuietStartMinute,request.QuietEndMinute,request.TimeZoneId);
        var fingerprint=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var key="notification-preferences:"+account;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key},0))",ct);
        if(!await db.Accounts.AnyAsync(a=>a.Id==account,ct))throw new UnauthorizedAccessException();
        var stored=await db.NotificationPreferences.SingleOrDefaultAsync(s=>s.AccountId==account,ct);
        var prior=await db.NotificationPreferenceReceipts.SingleOrDefaultAsync(r=>r.AccountId==account&&r.RequestId==request.RequestId,ct);
        if(prior is not null)
        {
            if(prior.Fingerprint!=fingerprint)throw new GameRuleException("notifications.reusedRequest","This request already saved different settings.");
            await transaction.CommitAsync(ct);return new(Dto(stored),true);
        }
        if(request.ExpectedRevision!=(stored?.Revision??0))
            throw new GameRuleException("notifications.changed","Settings changed on another device. Reload before saving.");
        var now=clock.GetUtcNow();
        if(stored is null){stored=new NotificationPreference{AccountId=account};db.NotificationPreferences.Add(stored);}
        if(request.BrowserEnabled&&(!stored.BrowserEnabled||stored.ConsentedAt is null))stored.ConsentedAt=now;
        stored.Revision=checked(stored.Revision+1);stored.BrowserEnabled=request.BrowserEnabled;stored.Categories=categories;
        stored.QuietEnabled=request.QuietEnabled;stored.QuietStartMinute=request.QuietStartMinute;stored.QuietEndMinute=request.QuietEndMinute;
        stored.TimeZoneId=request.TimeZoneId;stored.UpdatedAt=now;
        db.NotificationPreferenceReceipts.Add(new(){AccountId=account,RequestId=request.RequestId,Fingerprint=fingerprint,At=now});
        // Only these settings receipts; report/digest retention and gameplay receipts are untouched.
        await db.NotificationPreferenceReceipts.Where(r=>r.AccountId==account&&r.At<now.AddDays(-30)).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return new(Dto(stored),false);
    }
}
