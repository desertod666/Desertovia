using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Desertod.Api.Tests;

/// <summary>
/// The real application on a throwaway PostgreSQL container. Runs as Development, so migrations apply, the demo
/// village is seeded and the name-only login is available. Uses Docker by default, or a fresh database
/// on the opt-in loopback cluster configured by DESERTOD_TEST_POSTGRES.
/// </summary>
public sealed class DesertodApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Signs Telegram logins in tests; without a bot user name the login page does not offer Telegram.</summary>
    public const string TelegramBotToken = "123456:test-bot-token";

    private PostgreSqlContainer? database;
    private string? connectionString;
    private string? externalAdminConnection;
    private string? ownedDatabase;

    /// <summary>The game's clock; see <see cref="TestClock"/>.</summary>
    public TestClock Clock { get; } = new();
    public TestHospitalBridge Hospital { get; } = new();

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("DESERTOD_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(external))
        {
            database = new PostgreSqlBuilder("postgres:17").Build();
            await database.StartAsync();
            connectionString = database.GetConnectionString();
            return;
        }

        // Opt-in for local development without Docker Desktop. Always create a fresh database;
        // never run tests against the database named in the supplied admin connection.
        var admin = new NpgsqlConnectionStringBuilder(external) { Database = "postgres", Pooling = false };
        if (admin.Host != "localhost" && (!IPAddress.TryParse(admin.Host, out var host) || !IPAddress.IsLoopback(host)))
            throw new InvalidOperationException("The test PostgreSQL cluster must be reached through loopback (or an SSH tunnel).");
        externalAdminConnection = admin.ConnectionString;
        var name = "desertod_test_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(externalAdminConnection);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await create.ExecuteNonQueryAsync();
        ownedDatabase = name;
        connectionString = new NpgsqlConnectionStringBuilder(external) { Database = name }.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Desertod", connectionString ?? throw new InvalidOperationException("Test database is not initialized."));
        builder.UseSetting("Authentication:Twitch:ClientId", "");
        builder.UseSetting("Authentication:Google:ClientId", "");
        builder.UseSetting("Authentication:Telegram:BotToken", TelegramBotToken);
        builder.UseSetting("Authentication:Password:AccountsPerAddress", "3");
        // Tests move the clock forward; beasts must not strike the test villages while they do.
        builder.UseSetting("Game:PeacefulTime", "7.00:00:00");
        builder.ConfigureServices(services => services.AddTransient<IStartupFilter, TestAddressStartupFilter>());
        builder.ConfigureTestServices(services =>
        {
            // Tests deliberately arrange historical duties directly in SQL after startup.
            // Exercise retirement explicitly in FreeCombatApiTests, rather than racing that fixture setup.
            var retirement=services.Single(d=>d.ServiceType==typeof(Microsoft.Extensions.Hosting.IHostedService)
                &&d.ImplementationType==typeof(Desertod.Api.Game.RetiredGuardWorker));
            services.Remove(retirement);
            var reports = services.Single(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                && d.ImplementationType == typeof(Desertod.Api.Game.PlayerReportWorker));
            services.Remove(reports);
            services.RemoveAll<Desertod.Api.Game.IHospitalBridge>();
            services.AddSingleton<Desertod.Api.Game.IHospitalBridge>(Hospital);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    /// <summary>The test server has no client address; tests pick one with <see cref="AddressHeader"/>.</summary>
    public const string AddressHeader = "X-Test-Address";

    private sealed class TestAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((http, nextMiddleware) =>
            {
                if (IPAddress.TryParse(http.Request.Headers[AddressHeader], out var address))
                {
                    http.Connection.RemoteIpAddress = address;
                }

                return nextMiddleware(http);
            });
            next(app);
        };
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        try { await DisposeAsync(); }
        finally
        {
            if (database is not null) await database.DisposeAsync();
            if (ownedDatabase is not null && externalAdminConnection is not null)
            {
                await using var connection = new NpgsqlConnection(externalAdminConnection);
                await connection.OpenAsync();
                await using var drop = new NpgsqlCommand($"DROP DATABASE \"{ownedDatabase}\" WITH (FORCE)", connection);
                await drop.ExecuteNonQueryAsync();
            }
        }
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<DesertodApiFactory>
{
    public const string Name = "Api";
}
