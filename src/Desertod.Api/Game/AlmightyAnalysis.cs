using System.Text.Json;
using System.Text.Json.Nodes;

namespace Desertod.Api.Game;

/// <summary>Bounds the model wait independently of the transport. A late result has no continuation into the world.</summary>
public static class AlmightyAnalysis
{
    public static readonly TimeSpan Deadline=TimeSpan.FromSeconds(50);
    public const int SnapshotCharacterLimit=36000;

    /// <summary>Keep authoritative facts, permissions and all economic aggregates within the existing bot input limit.</summary>
    public static string BoundSnapshot(string snapshot)
    {
        if(snapshot.Length<=SnapshotCharacterLimit)return snapshot;
        var root=JsonNode.Parse(snapshot)!.AsObject();var omissions=new JsonArray();root["contextOmissions"]=omissions;
        string Save()=>root.ToJsonString(GameService.AlmightyJson);
        if(root["previous"] is JsonArray previous)
        {
            foreach(var item in previous.OfType<JsonObject>())
                foreach(var (field,limit) in new[]{("reason",400),("result",160)})
                    if(item[field]?.GetValue<string>() is {} value&&value.Length>limit)item[field]=value[..limit];
            omissions.Add("Long previous proposal text shortened; full audit remains stored.");
        }
        if(Save().Length<=SnapshotCharacterLimit)return Save();
        if(root["chat"] is JsonArray chat)
        {
            while(chat.Count>8)chat.RemoveAt(0);
            omissions.Add("Only the latest eight chat messages are included.");
        }
        if(Save().Length<=SnapshotCharacterLimit)return Save();
        if(root["catalog"] is JsonObject catalog)
            foreach(var field in new[]{"recipes","items","enemies","weapons","armor"})
            {
                if(catalog.Remove(field))omissions.Add("Descriptive catalog."+field+" omitted to fit input budget; this never grants new tools.");
                if(Save().Length<=SnapshotCharacterLimit)return Save();
            }
        // Never silently discard economic people/days, current facts or execution admission.
        return Save();
    }

    public static async Task<AlmightyAnswer?> RunAsync(IAlmightyBridge bridge,AlmightyReview review,TimeProvider time,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task<AlmightyAnswer?>? pending=null;
        try
        {
            // Include synchronous adapter startup in the bound as well as its asynchronous response.
            var token=linked.Token;
            pending=Task.Run(()=>bridge.AnalyzeAsync(review,token),token);
            return await pending.WaitAsync(Deadline,time,ct);
        }
        catch(TimeoutException)
        {
            return Failed("model.timeout");
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested)
        {
            return Failed("bridge.timeout");
        }
        finally
        {
            if(pending is {IsCompleted:false})
            {
                // Cancellation callbacks belong to the adapter; do not let a slow callback extend the deadline.
                Observe(linked.CancelAsync());
                Observe(pending);
            }
        }
    }

    private static void Observe(Task task)=>_ = task.ContinueWith(t=>{_ = t.Exception;},CancellationToken.None,
        TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);

    public static AlmightyAnswer Failed(string code)=>new("none","","",code);

    public static AlmightyAnswer ReadAnswer(JsonElement json,IReadOnlyList<Desertod.Domain.Director.AlmightyOption> options)
    {
        if(json.ValueKind!=JsonValueKind.Object)return Failed("bridge.fields");
        var fields=new HashSet<string>(StringComparer.Ordinal);
        foreach(var property in json.EnumerateObject())
            if(!fields.Add(property.Name)||property.Name is not ("option" or "reason" or "intent" or "errorCode"))return Failed("bridge.fields");
        string? Read(string name)=>json.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
        var option=Read("option");var reason=Read("reason");var intent=Read("intent");var error=Read("errorCode");
        if(option is null||option.Length>128||reason is null||reason.Length>1200||intent is null||intent.Length>1000
            ||json.TryGetProperty("errorCode",out var errorValue)&&errorValue.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)
            ||error?.Length>64)return Failed("bridge.fields");
        if(error is {Length:>0})return new("none","","",error);
        if(string.IsNullOrWhiteSpace(reason)||string.IsNullOrWhiteSpace(intent))return Failed("bridge.fields");
        if(!options.Any(o=>o.Id==option))return Failed("model.option");
        return new(option,reason,intent);
    }
}
