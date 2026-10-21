using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Desertod.Api.Auth;

public sealed record BrowserLoginStart(string Raw, string Url, string Pin);
public sealed record BrowserLoginPoll(bool Pending, string? Id, string? Name);
public sealed class DirectorBridgePayloadException() : JsonException("Director bridge response exceeds the permitted size.");

/// <summary>Использует уже существующее подтверждение входа у батюшки, только через доверенный loopback.</summary>
public sealed class TelegramBrowserBridge(HttpClient client, IOptions<TelegramOptions> options)
{
    public const int MaximumDirectorResponseBytes = 16 * 1024;
    public bool Enabled => Uri.TryCreate(options.Value.BrowserBridgeUrl, UriKind.Absolute, out var url)
        && url.IsLoopback && url.Scheme == "http" && options.Value.IsConfigured;

    public async Task<T> CallAsync<T>(string action, object body, CancellationToken cancellationToken)
    {
        if (!Enabled || action is not ("start" or "poll" or "speak" or "hospital" or "relationships" or "director")) throw new InvalidOperationException("Browser login is unavailable.");
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.BrowserBridgeUrl!.TrimEnd('/') + "/" + action);
        request.Headers.Add("X-Desertovia-Bridge", Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes("desertovia-bridge:" + options.Value.BotToken))));
        request.Content = JsonContent.Create(body);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(action == "director" ? 50 : 15));
        using var response = await client.SendAsync(request, action=="director"?HttpCompletionOption.ResponseHeadersRead:HttpCompletionOption.ResponseContentRead, deadline.Token);
        response.EnsureSuccessStatusCode();
        if(action=="director")
        {
            if(response.Content.Headers.ContentLength>MaximumDirectorResponseBytes)throw new DirectorBridgePayloadException();
            await using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);
            using var bounded=new MemoryStream();
            var buffer=new byte[4096];
            while(true)
            {
                var count=await stream.ReadAsync(buffer.AsMemory(0,Math.Min(buffer.Length,MaximumDirectorResponseBytes+1-(int)bounded.Length)),deadline.Token);
                if(count==0)break;
                bounded.Write(buffer,0,count);
                if(bounded.Length>MaximumDirectorResponseBytes)throw new DirectorBridgePayloadException();
            }
            bounded.Position=0;
            return await JsonSerializer.DeserializeAsync<T>(bounded,new JsonSerializerOptions(JsonSerializerDefaults.Web),deadline.Token)
                ?? throw new InvalidOperationException("Invalid director reply.");
        }
        return await response.Content.ReadFromJsonAsync<T>(deadline.Token) ?? throw new InvalidOperationException("Invalid login reply.");
    }
}
