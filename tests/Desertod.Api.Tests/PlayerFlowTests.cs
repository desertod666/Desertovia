using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain;
using Desertod.Domain.Entities;
using Desertod.Domain.World;
using Desertod.Infrastructure.Data;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Desertod.Api.Tests;

/// <summary>The main path through the game: sign in, found a village, join one, start working.</summary>
[Collection(ApiCollection.Name)]
public sealed class PlayerFlowTests(DesertodApiFactory factory)
{
    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..24];

    private async Task<HttpClient> SignedInClientAsync(string name)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/auth/dev-login", new { name });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        return client;
    }

    private static async Task CreateCharacterAsync(HttpClient client, string name)
    {
        var appearance = new { gender = "Female", style = 0, skinTone = 0, hairColor = 0, outfitColor = 0 };
        var created = await client.PostAsJsonAsync("/api/me/character", new { name, appearance });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
    }

    [Fact]
    public async Task Development_login_signs_in_and_out()
    {
        var name = UniqueName("login");
        var client = await SignedInClientAsync(name);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal(name, me.GetProperty("displayName").GetString());

        var logout = await client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Tsar_founds_a_private_village()
    {
        var client = await SignedInClientAsync(UniqueName("tsar"));

        var created = await client.PostAsJsonAsync("/api/villages", new { name = "Smoke Village", description = "Smoke test", isPrivate = true });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var slug = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        var mine = await client.GetFromJsonAsync<JsonElement>("/api/villages");
        Assert.Contains(mine.EnumerateArray(), village => village.GetProperty("slug").GetString() == slug);

        var village = await client.GetFromJsonAsync<JsonElement>($"/api/villages/{slug}");
        Assert.True(village.GetProperty("isOwner").GetBoolean());
        Assert.False(string.IsNullOrEmpty(village.GetProperty("inviteCode").GetString()));

        var invite = await client.PostAsync($"/api/villages/{slug}/invite", null);
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
    }

    [Fact]
    public async Task Citizen_joins_the_demo_village_and_starts_a_task()
    {
        var client = await SignedInClientAsync(UniqueName("citizen"));
        var path = $"/api/villages/{DemoVillage.Slug}";

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{path}/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{path}/citizens", new { })).StatusCode);

        await CreateCharacterAsync(client, "Smoky");
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal("Smoky", me.GetProperty("character").GetProperty("name").GetString());
        var again = await client.PostAsJsonAsync("/api/me/character", new { name = "Other", appearance = new { gender = "Male" } });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

        var created = await client.PostAsJsonAsync($"{path}/citizens", new { });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var citizen = await client.GetFromJsonAsync<JsonElement>($"{path}/me");
        Assert.Equal("Smoky", citizen.GetProperty("name").GetString());

        var started = await client.PostAsJsonAsync($"{path}/me/task", new { taskId = "chop-wood" });
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal("Working", (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetString());
    }

    [Fact]
    public async Task A_crowd_joins_and_starts_work_in_one_village_at_once()
    {
        const int crowd = 12;
        var path = $"/api/villages/{DemoVillage.Slug}";
        var clients = await Task.WhenAll(Enumerable.Range(0, crowd).Select(_ => SignedInClientAsync(UniqueName("crowd"))));
        await Task.WhenAll(clients.Select((client, index) => CreateCharacterAsync(client, $"Crowd {index}")));

        var joined = await Task.WhenAll(clients.Select(client => client.PostAsJsonAsync($"{path}/citizens", new { })));
        Assert.All(joined, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var started = await Task.WhenAll(clients.Select(client => client.PostAsJsonAsync($"{path}/me/task", new { taskId = "chop-wood" })));
        Assert.All(started, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
    }

    [Fact]
    public async Task Walkers_explore_the_land_the_whole_village_sees()
    {
        var tsar = await SignedInClientAsync(UniqueName("scout-tsar"));
        await CreateCharacterAsync(tsar, "Chief");
        var created = await tsar.PostAsJsonAsync("/api/villages", new { name = "Scout Village" });
        var slug = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
        var path = $"/api/villages/{slug}";

        var citizen = await SignedInClientAsync(UniqueName("scout-citizen"));
        await CreateCharacterAsync(citizen, "Scout");
        Assert.Equal(HttpStatusCode.OK, (await citizen.PostAsJsonAsync($"{path}/citizens", new { })).StatusCode);

        // This is a shared-fog use case, not permission to walk through an authored
        // ravine or a generated pond. Pick distant, reachable ground in this world.
        TilePoint Reachable(Village world,int left,int top)
        {
            for(var y=top;y<top+8;y++)for(var x=left;x<left+8;x++)
            {
                var target=new TilePoint(x+.5,y+.5);
                if(Exploration.IsExplored(world.Explored,x,y))continue;
                try{RegionalTravel.Find(world,WorldMap.Places[WorldMap.Camp],target);return target;}
                catch(GameRuleException e) when(e.Code is "spot.noPath" or "bridge.closed"){}
            }
            throw new InvalidOperationException("The scouting fixture needs reachable unexplored ground.");
        }
        await using var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var world=await db.Villages.AsNoTracking().SingleAsync(v=>v.Slug==slug);
        var royalTarget=Reachable(world,-30,20);var residentTarget=Reachable(world,20,30);
        var walked = await tsar.PostAsJsonAsync($"{path}/tsar/goto", new { spot = royalTarget });
        Assert.Equal(HttpStatusCode.OK, walked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await citizen.PostAsJsonAsync($"{path}/me/goto", new { spot = residentTarget })).StatusCode);

        var village = await citizen.GetFromJsonAsync<JsonElement>(path);
        var explored = Convert.FromBase64String(village.GetProperty("state").GetProperty("explored").GetString()!);
        Assert.Equal(Domain.World.Exploration.Size, explored.Length);
        Assert.False(Exploration.IsExplored(explored,(int)Math.Floor(residentTarget.X),(int)Math.Floor(residentTarget.Y)));

        var blueprint = await tsar.PostAsJsonAsync($"{path}/blueprints", new { kind = "WoodPile", x = 0, y = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, blueprint.StatusCode);
        Assert.Contains("building.unexplored", await blueprint.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Tsar_appoints_a_Governor_who_plans_buildings()
    {
        var tsar = await SignedInClientAsync(UniqueName("rule-tsar"));
        await CreateCharacterAsync(tsar, "Tsar");
        var created = await tsar.PostAsJsonAsync("/api/villages", new { name = "Ruled Village" });
        var slug = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
        var path = $"/api/villages/{slug}";

        var governor = await SignedInClientAsync(UniqueName("rule-governor"));
        await CreateCharacterAsync(governor, "Planner");
        var joined = await governor.PostAsJsonAsync($"{path}/citizens", new { });
        var governorId = (await joined.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var citizen = await SignedInClientAsync(UniqueName("rule-citizen"));
        await CreateCharacterAsync(citizen, "Worker");
        Assert.Equal(HttpStatusCode.OK, (await citizen.PostAsJsonAsync($"{path}/citizens", new { })).StatusCode);

        var blueprint = new { kind = "WoodPile", x = 9, y = 11 };
        Assert.Equal(HttpStatusCode.Forbidden, (await governor.PostAsJsonAsync($"{path}/blueprints", blueprint)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await citizen.PutAsJsonAsync($"{path}/citizens/{governorId}/governor", new { isGovernor = true })).StatusCode);

        var appointed = await tsar.PutAsJsonAsync($"{path}/citizens/{governorId}/governor", new { isGovernor = true });
        Assert.Equal(HttpStatusCode.OK, appointed.StatusCode);
        Assert.True((await appointed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isGovernor").GetBoolean());
        Assert.True((await governor.GetFromJsonAsync<JsonElement>($"{path}/me")).GetProperty("isGovernor").GetBoolean());

        var application = await governor.PostAsJsonAsync($"{path}/blueprints", blueprint);
        Assert.Equal(HttpStatusCode.OK, application.StatusCode);
        var requested = await application.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(requested.GetProperty("sites").EnumerateArray());
        var orderId = requested.GetProperty("civicOrders")[0].GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.OK, (await tsar.PostAsJsonAsync($"{path}/civic/{orderId}", new { approve = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await citizen.DeleteAsync($"{path}/blueprints/9,11")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await governor.DeleteAsync($"{path}/blueprints/9,11")).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await tsar.PutAsJsonAsync($"{path}/citizens/{governorId}/governor", new { isGovernor = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await governor.PostAsJsonAsync($"{path}/blueprints", blueprint)).StatusCode);
    }

    /// <summary>The sign-in cookie as a <c>Cookie</c> header; the test server's handler keeps no cookies for SignalR.</summary>
    private async Task<string> SignInCookieAsync(string name)
    {
        using var client = factory.Server.CreateClient();
        var login = await client.PostAsJsonAsync("/auth/dev-login", new { name });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        return string.Join("; ", login.Headers.GetValues("Set-Cookie").Select(cookie => cookie.Split(';')[0]));
    }

    private HubConnection VillageHubConnection(string? cookie) => new HubConnectionBuilder()
        .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/village"), options =>
        {
            options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            options.Transports = HttpTransportType.LongPolling;
            if (cookie is not null)
            {
                options.Headers["Cookie"] = cookie;
            }
        })
        .Build();

    [Fact]
    public async Task Village_hub_accepts_signed_in_players()
    {
        await using var connection = VillageHubConnection(await SignInCookieAsync(UniqueName("viewer")));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinVillage", DemoVillage.Slug);
        await connection.InvokeAsync("LeaveVillage", DemoVillage.Slug);

        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task Tsar_and_citizens_chat_while_outsiders_cannot()
    {
        var tsar = await SignedInClientAsync(UniqueName("chat-tsar"));
        await CreateCharacterAsync(tsar, "Chief");
        var created = await tsar.PostAsJsonAsync("/api/villages", new { name = "Chat Village" });
        var slug = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
        var path = $"/api/villages/{slug}/chat";

        var citizen = await SignedInClientAsync(UniqueName("chat-citizen"));
        await CreateCharacterAsync(citizen, "Talker");
        Assert.Equal(HttpStatusCode.OK, (await citizen.PostAsJsonAsync($"/api/villages/{slug}/citizens", new { })).StatusCode);

        var outsider = await SignedInClientAsync(UniqueName("chat-outsider"));
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsJsonAsync(path, new { text = "Let me in" })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await citizen.PostAsJsonAsync(path, new { text = "   " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await citizen.PostAsJsonAsync(path, new { text = new string('a', 121) })).StatusCode);

        var hello = await tsar.PostAsJsonAsync(path, new { text = " Welcome! " });
        Assert.Equal(HttpStatusCode.OK, hello.StatusCode);
        var reply = await citizen.PostAsJsonAsync(path, new { text = "Thanks, chief." });
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);

        var history = (await citizen.GetFromJsonAsync<JsonElement>(path)).EnumerateArray().ToList();
        Assert.Equal(2, history.Count);
        Assert.Equal("Welcome!", history[0].GetProperty("text").GetString());
        Assert.Equal("Chief", history[0].GetProperty("authorName").GetString());
        Assert.True(history[0].GetProperty("isTsar").GetBoolean());
        Assert.Equal("Talker", history[1].GetProperty("authorName").GetString());
        Assert.False(history[1].GetProperty("isTsar").GetBoolean());
    }

    [Fact]
    public async Task Village_hub_lets_only_members_into_the_chat()
    {
        var name = UniqueName("hub-chat");
        var client = await SignedInClientAsync(name);
        await CreateCharacterAsync(client, "Listener");
        await using var connection = VillageHubConnection(await SignInCookieAsync(name));
        await connection.StartAsync();

        Assert.False(await connection.InvokeAsync<bool>("JoinChat", DemoVillage.Slug));

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/villages/{DemoVillage.Slug}/citizens", new { })).StatusCode);
        Assert.True(await connection.InvokeAsync<bool>("JoinChat", DemoVillage.Slug));

        var received = new TaskCompletionSource<JsonElement>();
        connection.On<JsonElement>("chatMessage", message => received.TrySetResult(message));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/villages/{DemoVillage.Slug}/chat", new { text = "Hello, hub" })).StatusCode);

        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("Hello, hub", message.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Village_hub_refuses_anonymous_visitors()
    {
        await using var connection = VillageHubConnection(null);

        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }
}
