using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Yard.Api.Data;
using Portfolio.Yard.Api.Integrations.Ltl;

namespace Portfolio.Yard.Api.Tests;

public sealed class YardApiTests(ApiFactory factory) : ApiTest(factory)
{
    private async Task<List<OutboxMessage>> Outbox()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<YardDbContext>().Outbox.AsNoTracking().OrderBy(m => m.CreatedAt).ToListAsync();
    }

    private async Task<JsonElement> Trailer(string number) => (await GetJson($"/api/trailers/{number}")).GetProperty("trailer");

    [Fact]
    public async Task AssetsKeepTheirOriginalShape()
    {
        var assets = await GetJson("/api/assets");
        var first = assets[0];
        foreach (var field in new[] { "trailerNumber", "equipment", "palletCapacity", "status", "spot", "currentLoadNumber", "updatedAt" })
            Assert.True(first.TryGetProperty(field, out _), field);
        Assert.Contains(assets.EnumerateArray(), a => a.GetProperty("trailerNumber").GetString() == "TRL-4310" && a.GetProperty("spot").GetString() == "Inbound");
    }

    [Fact]
    public async Task GateInRegistersANewTrailerParksItAndQueuesAnEvent()
    {
        var before = (await Outbox()).Count;
        var gate = await PostJson("/api/gate", new { trailerNumber = "trl-9001", direction = "In", equipment = "Dry Van", palletCapacity = 26, carrier = "Demo Carrier" }, 200);
        Assert.Equal("TRL-9001", gate.GetProperty("trailerNumber").GetString());

        var trailer = await Trailer("TRL-9001");
        Assert.Equal("Arrived", trailer.GetProperty("status").GetString());
        Assert.Equal("A-02", trailer.GetProperty("spot").GetString()); // first free parking spot

        var outbox = await Outbox();
        Assert.Equal(before + 1, outbox.Count);
        Assert.Equal("TrailerGateIn", outbox[^1].EventType);
    }

    [Fact]
    public async Task RefusedActionsWriteNothingToTheOutbox()
    {
        var before = (await Outbox()).Count;
        var again = await Post("/api/gate", new { trailerNumber = "TRL-4101", direction = "In" }); // already on the yard
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var tooEarly = await Post("/api/gate", new { trailerNumber = "TRL-4101", direction = "Out" }); // not ready yet
        Assert.Equal(HttpStatusCode.Conflict, tooEarly.StatusCode);
        Assert.Contains("can't gate out", await tooEarly.Content.ReadAsStringAsync());
        Assert.Equal(before, (await Outbox()).Count);
    }

    [Fact]
    public async Task FullWorkflowFromExpectedToDeparted()
    {
        await PostJson("/api/gate", new { trailerNumber = "TRL-4310", direction = "In" }, 200);          // Expected -> Arrived
        Assert.Equal(HttpStatusCode.Conflict, (await Post("/api/trailers/TRL-4310/move", new { spot = "D-04" })).StatusCode); // occupied door
        Assert.Equal(HttpStatusCode.NoContent, (await Post("/api/trailers/TRL-4310/move", new { spot = "D-01" })).StatusCode);
        Assert.Equal("AtDoor", (await Trailer("TRL-4310")).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await Post("/api/trailers/TRL-4310/start-loading")).StatusCode);

        var failed = await PostJson("/api/inspections", new { trailerNumber = "TRL-4310", passed = true, tires = true, lights = false, doorsAndSeal = true, floor = true }, 200);
        Assert.False(failed.GetProperty("passed").GetBoolean()); // the checklist decides, not the flag
        Assert.Equal("OnHold", (await Trailer("TRL-4310")).GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await Post("/api/trailers/TRL-4310/release")).StatusCode);
        Assert.Equal("AtDoor", (await Trailer("TRL-4310")).GetProperty("status").GetString()); // back at its door

        var passed = await PostJson("/api/inspections", new { trailerNumber = "TRL-4310", passed = false, tires = true, lights = true, doorsAndSeal = true, floor = true, notes = "Seal 1234" }, 200);
        Assert.True(passed.GetProperty("passed").GetBoolean());
        Assert.Equal("Ready", (await Trailer("TRL-4310")).GetProperty("status").GetString());
        Assert.Equal("TrailerReadyForPlanning", (await Outbox())[^1].EventType);

        await PostJson("/api/gate", new { trailerNumber = "TRL-4310", direction = "Out" }, 200);
        var gone = await Trailer("TRL-4310");
        Assert.Equal("Departed", gone.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, gone.GetProperty("spot").ValueKind);
        var spots = await GetJson("/api/spots");
        Assert.Equal(JsonValueKind.Null, spots.EnumerateArray().Single(s => s.GetProperty("code").GetString() == "D-01").GetProperty("trailer").ValueKind);
    }

    [Fact]
    public async Task ReeferInspectionsMustCheckTheReeferUnit()
    {
        await Post("/api/trailers/TRL-4220/release");
        await Post("/api/trailers/TRL-4220/move", new { spot = "D-01" });
        var problem = await PostJson("/api/inspections", new { trailerNumber = "TRL-4220", passed = true, tires = true, lights = true, doorsAndSeal = true, floor = true }, 400);
        Assert.True(problem.GetProperty("errors").TryGetProperty("checklist", out _));
    }

    [Fact]
    public async Task HoldsNeedAReason()
    {
        await PostJson("/api/trailers/TRL-4101/hold", new { reason = "" }, 400);
        Assert.Equal(HttpStatusCode.NoContent, (await Post("/api/trailers/TRL-4101/hold", new { reason = "Awaiting paperwork" })).StatusCode);
        Assert.Equal("Awaiting paperwork", (await Trailer("TRL-4101")).GetProperty("holdReason").GetString());
    }

    [Fact]
    public async Task ExpectedTrailersCanBePreAdvisedOnce()
    {
        await PostJson("/api/trailers", new { trailerNumber = "TRL-7777", equipment = "Reefer", palletCapacity = 24, loadNumber = "LD-9" });
        Assert.Equal("Expected", (await Trailer("TRL-7777")).GetProperty("status").GetString());
        await PostJson("/api/trailers", new { trailerNumber = "TRL-7777", equipment = "Reefer", palletCapacity = 24 }, 409);
    }

    [Fact]
    public async Task DeliveryIsSignedAndUsesTheV1Payload()
    {
        await PostJson("/api/gate", new { trailerNumber = "TRL-4207", direction = "Out", note = "Departed for Denver." }, 200);
        var drain = await PostJson("/api/outbox/drain", new { }, 200);
        Assert.Equal(1, drain.GetProperty("delivered").GetInt32());

        var (path, body, signature) = Assert.Single(Factory.Ltl.Requests);
        Assert.Equal("/api/integrations/v1/yard/events", path);
        Assert.Equal(Signature.Create(body, ApiFactory.SigningKey), signature);
        var payload = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["eventId", "eventType", "trailerNumber", "occurredAt", "details", "schemaVersion"], payload.EnumerateObject().Select(p => p.Name));
        Assert.Equal("TrailerGateOut", payload.GetProperty("eventType").GetString());
        Assert.Equal(1, payload.GetProperty("schemaVersion").GetInt32());
        Assert.NotNull((await Outbox())[^1].SentAt);
    }

    [Fact]
    public async Task FailedDeliveryBacksOffAndStaysPending()
    {
        Factory.Ltl.Status = HttpStatusCode.Unauthorized;
        await PostJson("/api/gate", new { trailerNumber = "TRL-4207", direction = "Out" }, 200);

        var first = await PostJson("/api/outbox/drain", new { }, 200);
        Assert.Equal(1, first.GetProperty("failed").GetInt32());
        var message = (await Outbox())[^1];
        Assert.Null(message.SentAt);
        Assert.Equal(1, message.Attempts);
        Assert.True(message.NextAttemptAt > DateTime.UtcNow);
        Assert.NotNull(message.LastError);

        // Not due yet: an immediate second pass does not retry it.
        var second = await PostJson("/api/outbox/drain", new { }, 200);
        Assert.Equal(0, second.GetProperty("attempted").GetInt32());
        Assert.Equal(1, second.GetProperty("stillPending").GetInt32());
    }

    [Fact]
    public async Task CandidatesComeFromLtlAndReport503WhenItIsDown()
    {
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/ltl/candidates/TRL-4101")).StatusCode);
        Assert.Contains(Factory.Ltl.Requests, r => r.Path.StartsWith("/api/integrations/v1/yard/candidates?trailerNumber=TRL-4101&equipment=Dry%20Van&maxPallets=26"));
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/ltl/candidates/NOPE")).StatusCode);
    }

    [Fact]
    public async Task DashboardMetaHealthAndReset()
    {
        var dashboard = await GetJson("/api/dashboard");
        Assert.Equal(7, dashboard.GetProperty("onYard").GetInt32() + dashboard.GetProperty("expected").GetInt32());
        Assert.Equal(2, dashboard.GetProperty("doorsInUse").GetInt32());
        Assert.Equal("Ready", (await GetJson("/health/ready")).GetProperty("status").GetString());
        Assert.True((await GetJson("/api/meta")).GetProperty("storage").GetProperty("ready").GetBoolean());

        await PostJson("/api/gate", new { trailerNumber = "TRL-5555", direction = "In", equipment = "Flatbed", palletCapacity = 18 }, 200);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/reset-demo");
        request.Headers.Add("X-Demo-Reset-Token", "test-reset-token");
        Assert.Equal(HttpStatusCode.OK, (await Client.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/trailers/TRL-5555")).StatusCode);
    }
}
