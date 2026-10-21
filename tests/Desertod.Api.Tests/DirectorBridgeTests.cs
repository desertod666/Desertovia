using System.Net;
using System.Text;
using System.Text.Json;
using Desertod.Api.Auth;
using Desertod.Api.Game;
using Desertod.Domain.Director;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Desertod.Api.Tests;

public sealed class DirectorBridgeTests
{
    internal static AlmightyReview Review=>new(Guid.NewGuid(),"synthetic-director",Guid.NewGuid(),"{}","Observe",
        [new("none","Watch","Observe"),new("economy_report","Advice","No currency changes")]);
    private const string Valid="""{"option":"economy_report","reason":"Use observed evidence","intent":"Observe"}""";

    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> reply):HttpMessageHandler
    {
        public int Calls{get;private set;}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {Calls++;return Task.FromResult(reply(request));}
    }

    private static async Task<AlmightyAnswer?> Analyze(Handler handler,bool configured=true)
    {
        using var http=new HttpClient(handler);
        var options=Options.Create(new TelegramOptions{BotToken="synthetic-test-token",BotUsername="SyntheticBot",BrowserBridgeUrl=configured?"http://127.0.0.1:1":"https://example.invalid"});
        using var services=new ServiceCollection().AddSingleton(new TelegramBrowserBridge(http,options)).BuildServiceProvider();
        return await new AlmightyBridge(services.GetRequiredService<IServiceScopeFactory>()).AnalyzeAsync(Review,default);
    }

    private static HttpResponseMessage Response(string body,HttpStatusCode status=HttpStatusCode.OK)=>
        new(status){Content=new StringContent(body,Encoding.UTF8,"application/json")};

    [Fact]
    public async Task Existing_signed_bridge_is_used_once_and_disabled_bridge_never_opens_a_connection()
    {
        var handler=new Handler(request=>{
            Assert.Equal("http://127.0.0.1:1/director",request.RequestUri!.ToString());
            Assert.True(request.Headers.Contains("X-Desertovia-Bridge"));return Response(Valid);
        });
        var answer=await Analyze(handler);Assert.Equal("economy_report",answer!.Option);Assert.Null(answer.ErrorCode);Assert.Equal(1,handler.Calls);
        var disabled=new Handler(_=>throw new InvalidOperationException("Must not call transport"));
        Assert.Equal("bridge.unavailable",(await Analyze(disabled,false))!.ErrorCode);Assert.Equal(0,disabled.Calls);
    }

    [Theory]
    [InlineData(429,"bridge.rate_limit")]
    [InlineData(503,"bridge.http")]
    [InlineData(401,"bridge.http")]
    public async Task Http_failure_has_a_safe_code_and_no_immediate_retry(int status,string expected)
    {
        var handler=new Handler(_=>Response("untrusted upstream details",(HttpStatusCode)status));
        var answer=await Analyze(handler);Assert.Equal(expected,answer!.ErrorCode);Assert.Equal("none",answer.Option);
        Assert.Empty(answer.Reason);Assert.Equal(1,handler.Calls);
    }

    [Theory]
    [InlineData("{broken","bridge.json")]
    [InlineData("[]","bridge.fields")]
    [InlineData("{\"option\":\"economy_report\",\"reason\":\"Observe\",\"intent\":\"Observe\",\"wallet_credit\":10}","bridge.fields")]
    [InlineData("{\"option\":\"none\",\"option\":\"economy_report\",\"reason\":\"Observe\",\"intent\":\"Observe\"}","bridge.fields")]
    [InlineData("{\"option\":\"wallet_credit\",\"reason\":\"Mint\",\"intent\":\"Mint\"}","model.option")]
    [InlineData("{\"option\":\"economy_report\",\"reason\":17,\"intent\":\"Observe\"}","bridge.fields")]
    [InlineData("{\"option\":\"economy_report\",\"reason\":\"Observe\",\"intent\":\" \"}","bridge.fields")]
    [InlineData("{\"option\":\"none\",\"reason\":\"\",\"intent\":\"\",\"errorCode\":{}}","bridge.fields")]
    public async Task Malformed_or_ambiguous_structured_answers_do_not_reach_an_executable_option(string body,string expected)
    {
        var handler=new Handler(_=>Response(body));var answer=await Analyze(handler);
        Assert.Equal(expected,answer!.ErrorCode);Assert.Equal("none",answer.Option);Assert.Equal(1,handler.Calls);
    }

    [Theory]
    [InlineData("budget.shared")]
    [InlineData("budget.director")]
    [InlineData("model.timeout")]
    [InlineData("model.provider")]
    public async Task Existing_bot_error_envelopes_keep_their_code_without_becoming_a_fallback_action(string code)
    {
        var handler=new Handler(_=>Response(JsonSerializer.Serialize(new{option="none",reason="",intent="",errorCode=code})));
        var answer=await Analyze(handler);Assert.Equal(code,answer!.ErrorCode);Assert.Equal("none",answer.Option);
    }

    // A non-seekable stream supplies no length header; count reads to prove the transport did not pre-buffer it.
    private sealed class CountingStream(byte[] data):MemoryStream(data)
    {
        public override bool CanSeek=>false;
        public int BytesRead{get;private set;}
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct=default)
        {var count=await base.ReadAsync(buffer,ct);BytesRead+=count;return count;}
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Oversized_response_is_bounded_before_json_deserialization_even_without_a_length_header(bool declared)
    {
        using var stream=new CountingStream(Encoding.UTF8.GetBytes(Valid+new string(' ',1_000_000)));
        var handler=new Handler(_=>{
            var content=new StreamContent(stream);
            if(declared)content.Headers.ContentLength=1_000_000;
            return new(HttpStatusCode.OK){Content=content};
        });
        Assert.Equal("bridge.payload",(await Analyze(handler))!.ErrorCode);
        Assert.Equal(declared?0:TelegramBrowserBridge.MaximumDirectorResponseBytes+1,stream.BytesRead);
    }

    [Fact]
    public async Task Exact_body_limit_and_maximum_supported_unicode_fields_remain_compatible()
    {
        var body=Valid+new string(' ',TelegramBrowserBridge.MaximumDirectorResponseBytes-Encoding.UTF8.GetByteCount(Valid));
        Assert.Null((await Analyze(new Handler(_=>Response(body))))!.ErrorCode);
        var unicode=JsonSerializer.Serialize(new{option="economy_report",reason=new string('Я',1200),intent=new string('Я',1000)});
        Assert.True(Encoding.UTF8.GetByteCount(unicode)<TelegramBrowserBridge.MaximumDirectorResponseBytes);
        Assert.Null((await Analyze(new Handler(_=>Response(unicode))))!.ErrorCode);
    }

    [Fact]
    public async Task Login_bridge_keeps_its_original_reply_path_without_the_director_payload_limit()
    {
        var raw=new string('r',TelegramBrowserBridge.MaximumDirectorResponseBytes+100);
        var handler=new Handler(request=>{
            Assert.EndsWith("/start",request.RequestUri!.AbsolutePath);
            return Response(JsonSerializer.Serialize(new{raw,url="http://127.0.0.1/login",pin="1234"}));
        });
        using var http=new HttpClient(handler);
        var bridge=new TelegramBrowserBridge(http,Options.Create(new TelegramOptions{BotToken="synthetic-test-token",BotUsername="SyntheticBot",BrowserBridgeUrl="http://127.0.0.1:1"}));
        var reply=await bridge.CallAsync<BrowserLoginStart>("start",new{},default);
        Assert.Equal(raw,reply.Raw);Assert.Equal("1234",reply.Pin);Assert.Equal(1,handler.Calls);
    }
}

internal sealed class DirectorDeadlineClock(TimeProvider? utc=null):TimeProvider
{
    private sealed class Timer(TimerCallback callback,object? state):ITimer
    {
        public void Fire()=>callback(state);
        public bool Change(TimeSpan dueTime,TimeSpan period)=>true;
        public void Dispose(){}
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private Timer? timer;
    public TaskCompletionSource Armed{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TimeSpan? DueTime{get;private set;}
    public override DateTimeOffset GetUtcNow()=>(utc??System).GetUtcNow();
    public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period)
    {DueTime=dueTime;timer=new(callback,state);Armed.TrySetResult();return timer;}
    public void Fire()=>timer!.Fire();
}

internal sealed class DirectorPendingBridge(Action? beforeReturn=null):IAlmightyBridge
{
    public int Calls{get;private set;}
    public TaskCompletionSource<AlmightyAnswer?> Pending{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Started{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Returned{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken Token{get;private set;}
    public Task<AlmightyAnswer?> AnalyzeAsync(AlmightyReview review,CancellationToken ct){Calls++;Token=ct;Started.TrySetResult();beforeReturn?.Invoke();Returned.TrySetResult();return Pending.Task;}
    public Task<AlmightyDelivery> NotifyAsync(AlmightyNotice notice,CancellationToken ct)=>Task.FromResult(new AlmightyDelivery("Unavailable"));
    public Task<SramoslavReply?> SpeakAsync(object request,CancellationToken ct)=>throw new NotSupportedException();
}

public sealed class DirectorAnalysisDeadlineTests
{
    [Fact]
    public async Task Noncooperative_adapter_cannot_hold_the_worker_or_return_an_accepted_late_answer()
    {
        var clock=new DirectorDeadlineClock();var bridge=new DirectorPendingBridge();
        var run=AlmightyAnalysis.RunAsync(bridge,DirectorBridgeTests.Review,clock,default);
        await clock.Armed.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(TimeSpan.FromSeconds(50),clock.DueTime);
        await bridge.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Fire();var answer=await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("model.timeout",answer!.ErrorCode);Assert.True(bridge.Token.IsCancellationRequested);
        bridge.Pending.SetResult(new("economy_report","Late result","Observe"));
        Assert.Equal("none",answer.Option);Assert.Equal("model.timeout",answer.ErrorCode);
    }

    [Fact]
    public async Task Host_cancellation_propagates_and_does_not_get_reported_as_a_provider_failure()
    {
        var clock=new DirectorDeadlineClock();var bridge=new DirectorPendingBridge();using var stop=new CancellationTokenSource();
        var run=AlmightyAnalysis.RunAsync(bridge,DirectorBridgeTests.Review,clock,stop.Token);
        await clock.Armed.Task.WaitAsync(TimeSpan.FromSeconds(5));await bridge.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>run);
        Assert.True(bridge.Token.IsCancellationRequested);bridge.Pending.SetException(new IOException("Synthetic late failure"));
    }

    [Fact]
    public async Task Synchronously_blocked_adapter_startup_is_inside_the_same_deadline()
    {
        using var release=new ManualResetEventSlim();var clock=new DirectorDeadlineClock();
        var bridge=new DirectorPendingBridge(()=>release.Wait());
        var run=AlmightyAnalysis.RunAsync(bridge,DirectorBridgeTests.Review,clock,default);
        try
        {
            await clock.Armed.Task.WaitAsync(TimeSpan.FromSeconds(5));await bridge.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Fire();Assert.Equal("model.timeout",(await run.WaitAsync(TimeSpan.FromSeconds(5)))!.ErrorCode);
        }
        finally{release.Set();bridge.Pending.TrySetResult(new("none","Late response","Observe"));await bridge.Returned.Task.WaitAsync(TimeSpan.FromSeconds(5));}
    }

    [Fact]
    public async Task Already_cancelled_host_cannot_start_another_model_request()
    {
        var bridge=new DirectorPendingBridge();using var stop=new CancellationTokenSource();stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>AlmightyAnalysis.RunAsync(bridge,DirectorBridgeTests.Review,TimeProvider.System,stop.Token));
        Assert.Equal(0,bridge.Calls);
    }
}
