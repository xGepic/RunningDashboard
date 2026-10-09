using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Tests.Harness;

/// <summary>
/// The real app in an in-memory host with its own SQLite file. Only Strava (HTTP) and the clock are fake.
/// </summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    public const string ClientId = "12345";
    public const string ClientSecret = "test-client-secret";
    public const string RedirectUri = "http://localhost:4200/api/strava/callback";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"running-dashboard-test-{Guid.NewGuid():N}.db");

    public FakeStrava Strava { get; } = new();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 9, 18, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:RunningDashboard", $"Data Source={_databasePath};Pooling=False");
        builder.UseSetting("Strava:ClientId", ClientId);
        builder.UseSetting("Strava:ClientSecret", ClientSecret);
        builder.UseSetting("Strava:RedirectUri", RedirectUri);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddHttpClient<StravaClient>().ConfigurePrimaryHttpMessageHandler(() => Strava);
        });
    }

    public HttpClient CreateBrowser() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(_databasePath);
    }
}
