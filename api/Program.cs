using Microsoft.Extensions.Http.Resilience;
using Portfolio.Yard.Api.Integrations.Alvys;
using Portfolio.Yard.Api.Integrations.Ltl;
using Portfolio.Yard.Api.Models;
using Portfolio.Yard.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.Configure<LtlOptions>(builder.Configuration.GetSection(LtlOptions.Section));
builder.Services.Configure<AlvysOptions>(builder.Configuration.GetSection(AlvysOptions.Section));
builder.Services.AddSingleton<YardStore>();
builder.Services.AddSingleton<OutboxStore>();
builder.Services.AddSingleton<AlvysTokenProvider>();
builder.Services.AddSingleton<IExternalTrailerReader, AlvysTrailerReader>();
builder.Services.AddHostedService<OutboxDispatcher>();

builder.Services.AddHttpClient<LtlClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LtlOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
})
.AddStandardResilienceHandler(options =>
{
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient(AlvysTokenProvider.AuthClient)
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
    });

builder.Services.AddHttpClient(AlvysTrailerReader.ApiClient)
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(12);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
    });

var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "yard-ops" }));
app.MapGet("/api/assets", (YardStore store) => Results.Ok(store.Assets.OrderBy(x => x.Spot)));
app.MapGet("/api/gate/history", (YardStore store) => Results.Ok(store.GateEvents.OrderByDescending(x => x.OccurredAt)));
app.MapGet("/api/inspections", (YardStore store) => Results.Ok(store.Inspections.OrderByDescending(x => x.OccurredAt)));
app.MapGet("/api/outbox", (OutboxStore store) => Results.Ok(store.Messages.OrderByDescending(x => x.CreatedAt)));

app.MapPost("/api/gate", (GateRequest request, YardStore store, OutboxStore outbox) =>
{
    if (!store.TryGetAsset(request.TrailerNumber, out var asset)) return Results.NotFound();
    var direction = request.Direction.Equals("Out", StringComparison.OrdinalIgnoreCase) ? "Out" : "In";
    var nextStatus = direction == "In" ? "Arrived" : "Departed";
    store.UpdateAsset(asset with { Status = nextStatus, UpdatedAt = DateTimeOffset.UtcNow });
    var gate = store.AddGate(new(Guid.NewGuid(), asset.TrailerNumber, direction, DateTimeOffset.UtcNow, request.Note));

    outbox.Enqueue(new(Guid.NewGuid(), $"TrailerGate{direction}", asset.TrailerNumber, gate.OccurredAt,
        string.IsNullOrWhiteSpace(request.Note) ? $"Gate {direction.ToLowerInvariant()} recorded." : request.Note));
    return Results.Ok(gate);
});

app.MapPost("/api/inspections", (InspectionRequest request, YardStore store, OutboxStore outbox) =>
{
    if (!store.TryGetAsset(request.TrailerNumber, out var asset)) return Results.NotFound();
    var record = store.AddInspection(new(Guid.NewGuid(), asset.TrailerNumber, request.Passed, DateTimeOffset.UtcNow, request.Notes));
    if (request.Passed)
    {
        store.UpdateAsset(asset with { Status = "Ready", UpdatedAt = DateTimeOffset.UtcNow });
        outbox.Enqueue(new(Guid.NewGuid(), "TrailerReadyForPlanning", asset.TrailerNumber, record.OccurredAt,
            "Dock inspection passed; trailer is ready for LTL planning."));
    }
    return Results.Ok(record);
});

app.MapGet("/api/ltl/candidates/{trailerNumber}", async (
    string trailerNumber,
    YardStore store,
    LtlClient ltl,
    CancellationToken ct) =>
{
    if (!store.TryGetAsset(trailerNumber, out var asset)) return Results.NotFound();
    try
    {
        return Results.Ok(await ltl.GetCandidatesAsync(asset, ct));
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        return Results.Problem("LTL Planner is currently unavailable. Yard state was not changed.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/api/alvys/trailers", async (IExternalTrailerReader trailers, CancellationToken ct) =>
    Results.Ok(await trailers.GetTrailersAsync(ct)));

app.Run();
