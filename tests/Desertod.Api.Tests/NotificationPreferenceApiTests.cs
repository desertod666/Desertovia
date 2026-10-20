using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class NotificationPreferenceApiTests(DesertodApiFactory factory)
{
    private const string Path="/api/me/notification-preferences";
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private async Task<(HttpClient Client,string Name,Guid Id)> SignIn(string? name=null)
    {
        name??=UniqueName("notice");var client=factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync("/auth/dev-login",new{name})).StatusCode);
        var me=await client.GetFromJsonAsync<JsonElement>("/api/me");return(client,name,me.GetProperty("id").GetGuid());
    }
    private static NotificationPreferenceRequest Request(long revision=0)=>new(Guid.NewGuid(),revision,true,["work","farm"],true,1320,480,"Europe/Oslo");
    private static async Task<NotificationPreferenceDto> Get(HttpClient client)=>(await client.GetFromJsonAsync<NotificationPreferenceDto>(Path))!;
    private static Task<HttpResponseMessage> Put(HttpClient client,NotificationPreferenceRequest request)=>client.PutAsJsonAsync(Path,request);
    private static async Task<NotificationPreferenceResult> Save(HttpClient client,NotificationPreferenceRequest request)
    {
        var response=await Put(client,request);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        return(await response.Content.ReadFromJsonAsync<NotificationPreferenceResult>())!;
    }

    [Fact]
    public async Task Old_account_is_opted_out_without_writing_gameplay_or_migrating_an_unbound_device_choice()
    {
        var(a,_,id)=await SignIn();var settings=await Get(a);
        Assert.False(settings.Configured);Assert.False(settings.BrowserEnabled);Assert.Equal(0,settings.Revision);Assert.Null(settings.ConsentedAt);
        Assert.True(settings.ServerNow>factory.Clock.GetUtcNow().AddSeconds(-10));
        await using var db=await Db();Assert.False(await db.NotificationPreferences.AnyAsync(p=>p.AccountId==id));
        Assert.False(await db.GameWallets.AnyAsync(w=>w.AccountId==id));Assert.False(await db.Citizens.AnyAsync(c=>c.AccountId==id));
    }

    [Fact]
    public async Task Two_devices_share_persisted_settings_and_lost_response_replay_cannot_reenable_revoked_consent()
    {
        var(first,name,id)=await SignIn();var(second,_,_)=await SignIn(name);
        var request=Request();var saved=await Save(first,request);Assert.False(saved.Replayed);Assert.Equal(1,saved.Settings.Revision);Assert.NotNull(saved.Settings.ConsentedAt);
        var read=await Get(second);Assert.Equal("Europe/Oslo",read.TimeZoneId);Assert.Equal(["work","farm"],read.Categories);
        var replay=await Save(second,request);Assert.True(replay.Replayed);Assert.Equal(1,replay.Settings.Revision);
        var revoked=await Save(second,Request(1) with{BrowserEnabled=false});Assert.Equal(2,revoked.Settings.Revision);
        var late=await Save(first,request);Assert.True(late.Replayed);Assert.False(late.Settings.BrowserEnabled);Assert.Equal(2,late.Settings.Revision);
        await AssertRuleBrokenAsync(await Put(first,request with{BrowserEnabled=false}),"notifications.reusedRequest");
        await using var db=await Db();Assert.Equal(2,await db.NotificationPreferenceReceipts.CountAsync(r=>r.AccountId==id));
        Assert.Null((await db.Accounts.SingleAsync(a=>a.Id==id)).Character);
    }

    [Fact]
    public async Task Concurrent_devices_cannot_silently_overwrite_the_same_revision()
    {
        var(first,name,_)=await SignIn();var(second,_,_)=await SignIn(name);await Save(first,Request());
        var responses=await Task.WhenAll(Put(first,Request(1) with{BrowserEnabled=false}),Put(second,Request(1) with{TimeZoneId="Asia/Tokyo"}));
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.BadRequest);
        Assert.Equal(2,(await Get(first)).Revision);
    }

    [Fact]
    public async Task Authentication_and_account_scope_ignore_untrusted_body_and_query_account_ids()
    {
        var anonymous=factory.CreateClient();Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Put(anonymous,Request())).StatusCode);
        var(a,_,id)=await SignIn();var(b,_,other)=await SignIn();await Save(a,Request());
        var body=new{requestId=Guid.NewGuid(),expectedRevision=0,browserEnabled=true,categories=new[]{"danger"},quietEnabled=false,quietStartMinute=1320,quietEndMinute=480,timeZoneId="UTC",accountId=id};
        Assert.Equal(HttpStatusCode.OK,(await b.PutAsJsonAsync(Path,body)).StatusCode);
        Assert.Equal(["work","farm"],(await Get(a)).Categories);Assert.Equal(["danger"],(await Get(b)).Categories);
        var scoped=await b.GetFromJsonAsync<NotificationPreferenceDto>(Path+"?accountId="+id);Assert.Equal(["danger"],scoped!.Categories);
        await using var db=await Db();Assert.Equal(1,(await db.NotificationPreferences.SingleAsync(p=>p.AccountId==other)).Revision);
    }

    [Fact]
    public async Task Invalid_categories_zone_and_ambiguous_quiet_interval_do_not_create_consent()
    {
        var(a,_,id)=await SignIn();
        foreach(var request in new[]{Request() with{Categories=["unknown"]},Request() with{Categories=["farm","farm"]},Request() with{Categories=null!},Request() with{RequestId=Guid.Empty},Request() with{QuietEndMinute=1320}})
            await AssertRuleBrokenAsync(await Put(a,request),"notifications.settings");
        await AssertRuleBrokenAsync(await Put(a,Request() with{TimeZoneId="invalid/time-zone"}),"notifications.timeZone");
        await using var db=await Db();Assert.False(await db.NotificationPreferences.AnyAsync(p=>p.AccountId==id));
    }

    [Fact]
    public async Task Final_commit_failure_rolls_back_consent_and_receipt_and_exact_retry_is_safe()
    {
        var(a,_,id)=await SignIn();var request=Request();await using var db=await Db();
        var trigger=await EconomyCommitFailure.InstallFor(db,"NotificationPreferenceReceipts",request.RequestId);
        try {Assert.Equal(HttpStatusCode.InternalServerError,(await Put(a,request)).StatusCode);}
        finally{await EconomyCommitFailure.Remove(db,trigger,"NotificationPreferenceReceipts");}
        Assert.False((await Get(a)).BrowserEnabled);Assert.False(await db.NotificationPreferenceReceipts.AnyAsync(r=>r.AccountId==id));
        Assert.Equal(1,(await Save(a,request)).Settings.Revision);Assert.True((await Save(a,request)).Replayed);
        Assert.Single(await db.NotificationPreferences.Where(p=>p.AccountId==id).ToArrayAsync());
    }
}
