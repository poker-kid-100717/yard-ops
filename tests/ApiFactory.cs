using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Portfolio.Yard.Api.Integrations.Ltl;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Yard.Api.Data;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Portfolio.Yard.Api.Tests;

/// <summary>
/// Runs the real API. Uses a throwaway SQLite file by default; set TEST_DATABASE_URL to run the
/// same tests against PostgreSQL (CI does both). Each factory starts from freshly seeded data.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string sqlitePath = Path.Combine(Path.GetTempPath(), $"yard-test-{Guid.NewGuid():N}.db");
    protected virtual int WritesPerMinute => 100_000;
    protected virtual string? ResetToken => "test-reset-token";

    public const string SigningKey = "test-signing-key";
    public FakeLtl Ltl { get; } = new();
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Demo:SqlitePath", sqlitePath);
        builder.UseSetting("RateLimiting:WritesPerMinute", WritesPerMinute.ToString());
        builder.UseSetting("Demo:ResetToken", ResetToken ?? "");
        builder.UseSetting("Ltl:SigningKey", SigningKey);
        builder.UseSetting("Ltl:BaseUrl", "http://ltl.test/");
        builder.UseSetting("Outbox:DispatcherEnabled", "false");
        // Deliveries go to a fake LTL Planner that records exactly what Yard sends.
        builder.ConfigureTestServices(services =>
            services.AddHttpClient<LtlClient>().ConfigurePrimaryHttpMessageHandler(() => Ltl));
        var postgres = Environment.GetEnvironmentVariable("TEST_DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(postgres)) builder.UseSetting("ConnectionStrings:Default", postgres);
    }

    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YardDbContext>();
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().ResetAsync(db, CancellationToken.None);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { File.Delete(sqlitePath); } catch (IOException) { }
    }
}

public abstract class ApiTest : IClassFixture<ApiFactory>, IAsyncLifetime
{
    protected readonly ApiFactory Factory;
    protected readonly HttpClient Client;

    protected ApiTest(ApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    // Every test starts from the seeded demo data.
    public Task InitializeAsync() { Factory.Ltl.Reset(); return Factory.ResetAsync(); }
    public Task DisposeAsync() => Task.CompletedTask;

    protected async Task<JsonElement> GetJson(string url)
    {
        var response = await Client.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode, $"GET {url} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
    }

    protected async Task<JsonElement> PostJson(string url, object body, int expectedStatus = 201)
    {
        var response = await Client.PostAsJsonAsync(url, body, ApiFactory.Json);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == expectedStatus, $"POST {url} returned {(int)response.StatusCode}: {text}");
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    protected Task<HttpResponseMessage> Post(string url, object? body = null) =>
        body is null ? Client.PostAsync(url, null) : Client.PostAsJsonAsync(url, body, ApiFactory.Json);
}

/// <summary>Stands in for LTL Planner: records each request and answers with the configured status.</summary>
public sealed class FakeLtl : HttpMessageHandler
{
    public List<(string Path, string Body, string Signature)> Requests { get; } = [];
    public System.Net.HttpStatusCode Status { get; set; } = System.Net.HttpStatusCode.OK;

    public void Reset() { Requests.Clear(); Status = System.Net.HttpStatusCode.OK; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        var signature = request.Headers.TryGetValues("X-Portfolio-Signature", out var v) ? v.Single() : "";
        lock (Requests) Requests.Add((request.RequestUri!.PathAndQuery, body, signature));
        var content = request.Method == HttpMethod.Get ? "[]" : "{\"accepted\":true}";
        return new HttpResponseMessage(Status) { Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json") };
    }
}
