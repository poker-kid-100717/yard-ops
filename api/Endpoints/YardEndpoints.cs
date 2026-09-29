using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Portfolio.Yard.Api.Data;
using Portfolio.Yard.Api.Integrations.Alvys;
using Portfolio.Yard.Api.Integrations.Ltl;
using Portfolio.Yard.Api.Models;
using Portfolio.Yard.Api.Services;

namespace Portfolio.Yard.Api.Endpoints;

public sealed record TrailerRequest(string? TrailerNumber, string? Equipment, int PalletCapacity, string? LoadNumber, string? Carrier);
public sealed record MoveRequest(string? Spot);
public sealed record HoldRequest(string? Reason);
public sealed record TrailerDto(string TrailerNumber, string Equipment, int PalletCapacity, string Status, string? Spot, string? SpotKind,
    string? CurrentLoadNumber, string? Carrier, string? HoldReason, DateTime UpdatedAt, IReadOnlyList<string> Actions);
public sealed record SpotDto(string Code, string Kind, TrailerDto? Trailer);
public sealed record MoveDto(Guid Id, string TrailerNumber, string? FromSpot, string ToSpot, DateTime OccurredAt);
public sealed record OutboxDto(Guid Id, YardIntegrationEvent Event, DateTimeOffset CreatedAt, int Attempts, DateTimeOffset NextAttemptAt,
    DateTimeOffset? SentAt, string? LastError);

public static class YardEndpoints
{
    public static void MapYardEndpoints(this RouteGroupBuilder api)
    {
        // ----- the original endpoints, same shapes -----
        api.MapGet("/assets", async (YardDbContext db, CancellationToken ct) =>
            Results.Ok((await db.Trailers.AsNoTracking().Where(t => t.Status != TrailerStatuses.Departed).ToListAsync(ct))
                .Select(t => new YardAsset(t.TrailerNumber, t.Equipment, t.PalletCapacity, t.Status, SpotLabel(t), t.CurrentLoadNumber, Utc(t.UpdatedAt)))
                .OrderBy(a => a.Spot, StringComparer.Ordinal))).WithTags("Yard");

        api.MapGet("/gate/history", async (YardDbContext db, string? trailerNumber, int? take, CancellationToken ct) =>
        {
            var query = db.GateEvents.AsNoTracking();
            if (Http.Clean(trailerNumber) is { } n) query = query.Where(g => g.TrailerNumber == n);
            return Results.Ok((await query.OrderByDescending(g => g.OccurredAt).Take(Math.Clamp(take ?? 50, 1, 200)).ToListAsync(ct))
                .Select(g => new GateEvent(g.Id, g.TrailerNumber, g.Direction, Utc(g.OccurredAt), g.Note)));
        }).WithTags("Gate");

        api.MapGet("/inspections", async (YardDbContext db, string? trailerNumber, int? take, CancellationToken ct) =>
        {
            var query = db.Inspections.AsNoTracking();
            if (Http.Clean(trailerNumber) is { } n) query = query.Where(i => i.TrailerNumber == n);
            return Results.Ok((await query.OrderByDescending(i => i.OccurredAt).Take(Math.Clamp(take ?? 50, 1, 200)).ToListAsync(ct)).Select(ToDto));
        }).WithTags("Inspections");

        api.MapGet("/outbox", async (YardDbContext db, string? state, int? take, CancellationToken ct) =>
        {
            var query = db.Outbox.AsNoTracking();
            query = state switch
            {
                "pending" => query.Where(m => m.SentAt == null),
                "failing" => query.Where(m => m.SentAt == null && m.Attempts > 0),
                "delivered" => query.Where(m => m.SentAt != null),
                _ => query
            };
            return Results.Ok((await query.OrderByDescending(m => m.CreatedAt).Take(Math.Clamp(take ?? 50, 1, 200)).ToListAsync(ct)).Select(ToDto));
        }).WithTags("LTL outbox");

        api.MapPost("/gate", Gate).WithTags("Gate");
        api.MapPost("/inspections", Inspect).WithTags("Inspections");

        // ----- trailers and spots -----
        var trailers = api.MapGroup("/trailers").WithTags("Trailers");
        trailers.MapGet("/", ListTrailers);
        trailers.MapGet("/{trailerNumber}", GetTrailer);
        trailers.MapPost("/", ExpectTrailer);
        trailers.MapPut("/{trailerNumber}", UpdateTrailer);
        trailers.MapPost("/{trailerNumber}/move", Move);
        trailers.MapPost("/{trailerNumber}/start-loading", StartLoading);
        trailers.MapPost("/{trailerNumber}/hold", Hold);
        trailers.MapPost("/{trailerNumber}/release", Release);

        api.MapGet("/spots", async (YardDbContext db, CancellationToken ct) =>
        {
            var spots = await db.Spots.AsNoTracking().OrderBy(s => s.Position).ToListAsync(ct);
            var occupants = await db.Trailers.AsNoTracking().Where(t => t.SpotCode != null).ToDictionaryAsync(t => t.SpotCode!, ct);
            return Results.Ok(spots.Select(s => new SpotDto(s.Code, s.Kind, occupants.TryGetValue(s.Code, out var t) ? ToDto(t, s.Kind) : null)));
        }).WithTags("Yard");

        api.MapPost("/outbox/drain", async (OutboxDelivery delivery, CancellationToken ct) => Results.Ok(await delivery.DrainAsync(ct)))
            .WithTags("LTL outbox");

        api.MapGet("/dashboard", Dashboard).WithTags("Yard");
    }

    internal static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static string SpotLabel(Trailer t) => t.SpotCode ?? (t.Status == TrailerStatuses.Expected ? "Inbound" : t.Status);

    internal static TrailerDto ToDto(Trailer t, string? spotKind) => new(t.TrailerNumber, t.Equipment, t.PalletCapacity, t.Status, t.SpotCode,
        spotKind, t.CurrentLoadNumber, t.Carrier, t.HoldReason, t.UpdatedAt, TrailerRules.ActionsFor(t.Status));

    private static InspectionRecord ToDto(Inspection i) =>
        new(i.Id, i.TrailerNumber, i.Passed, Utc(i.OccurredAt), i.Notes, i.Tires, i.Lights, i.DoorsAndSeal, i.Floor, i.ReeferUnit);

    private static OutboxDto ToDto(OutboxMessage m) => new(m.EventId, OutboxDelivery.ToEvent(m), Utc(m.CreatedAt), m.Attempts,
        Utc(m.NextAttemptAt), m.SentAt is { } s ? Utc(s) : null, m.LastError);

    private static string Normalize(string? trailerNumber) => (trailerNumber ?? "").Trim().ToUpperInvariant();

    /// <summary>Writes the outbox row in the caller's unit of work, so it commits or rolls back with the yard change.</summary>
    private static void Enqueue(YardDbContext db, string type, string trailer, DateTime at, string details) =>
        db.Outbox.Add(new OutboxMessage
        {
            EventId = Guid.NewGuid(), EventType = type, TrailerNumber = trailer, OccurredAt = at, Details = details,
            CreatedAt = at, NextAttemptAt = at
        });

    private static async Task<string?> SpotKind(YardDbContext db, string? code, CancellationToken ct) =>
        code is null ? null : await db.Spots.Where(s => s.Code == code).Select(s => s.Kind).SingleOrDefaultAsync(ct);

    // ---------- gate ----------

    private static async Task<IResult> Gate(GateRequest request, YardDbContext db, TimeProvider clock, OutboxSignal signal, CancellationToken ct)
    {
        var number = Normalize(request.TrailerNumber);
        var direction = (request.Direction ?? "").Equals("Out", StringComparison.OrdinalIgnoreCase) ? "Out" : "In";
        var checks = new Checks().Required("trailerNumber", number, Limits.Trailer).Optional("note", request.Note, Limits.Note);
        if (!checks.Ok) return checks.Problem();

        var now = clock.GetUtcNow().UtcDateTime;
        var trailer = await db.Trailers.FindAsync([number], ct);

        if (direction == "Out")
        {
            if (trailer is null) return Http.NotFound("Trailer");
            if (!TrailerRules.Can(trailer.Status, TrailerRules.GateOut)) return Http.Conflict(TrailerRules.NotAllowed(trailer, TrailerRules.GateOut));
            trailer.Status = TrailerStatuses.Departed;
            trailer.SpotCode = null;
            trailer.HoldReason = null;
        }
        else
        {
            if (trailer is null)
            {
                // A trailer the yard has never seen: register it at the gate.
                checks.OneOf("equipment", request.Equipment, EquipmentTypes.All);
                if (request.PalletCapacity is not { } cap || cap < 1 || cap > 30) checks.Add("palletCapacity", "Must be between 1 and 30.");
                if (!checks.Ok) return checks.Problem();
                trailer = new Trailer { TrailerNumber = number, Equipment = request.Equipment!, PalletCapacity = request.PalletCapacity!.Value };
                db.Trailers.Add(trailer);
            }
            else if (!TrailerRules.Can(trailer.Status, TrailerRules.GateIn))
            {
                return Http.Conflict(TrailerRules.NotAllowed(trailer, TrailerRules.GateIn));
            }

            var spot = Http.Clean(request.Spot)?.ToUpperInvariant();
            var target = spot is null
                ? await FreeSpots(db).Where(s => s.Kind == SpotKinds.Parking).OrderBy(s => s.Position).Select(s => s.Code).FirstOrDefaultAsync(ct)
                : await FreeSpots(db).Where(s => s.Code == spot && s.Kind == SpotKinds.Parking).Select(s => s.Code).FirstOrDefaultAsync(ct);
            if (target is null)
                return spot is null ? Http.Conflict("The yard is full: no free parking spot.") : Http.Conflict($"Parking spot {spot} is not free.");

            trailer.Status = TrailerStatuses.Arrived;
            trailer.SpotCode = target;
            trailer.HoldReason = null;
            if (Http.Clean(request.LoadNumber) is { } load) trailer.CurrentLoadNumber = load;
            if (Http.Clean(request.Carrier) is { } carrier) trailer.Carrier = carrier;
            db.Moves.Add(new TrailerMove { Id = Guid.NewGuid(), TrailerNumber = number, FromSpot = null, ToSpot = target, OccurredAt = now });
        }

        trailer.UpdatedAt = now;
        var gate = new GateEventRecord { Id = Guid.NewGuid(), TrailerNumber = number, Direction = direction, OccurredAt = now, Note = Http.Clean(request.Note) };
        db.GateEvents.Add(gate);
        Enqueue(db, $"TrailerGate{direction}", number, now,
            string.IsNullOrWhiteSpace(request.Note) ? $"Gate {direction.ToLowerInvariant()} recorded." : request.Note.Trim());
        await db.SaveChangesAsync(ct);
        signal.Notify();
        return Results.Ok(new GateEvent(gate.Id, gate.TrailerNumber, gate.Direction, Utc(gate.OccurredAt), gate.Note));
    }

    private static IQueryable<YardSpot> FreeSpots(YardDbContext db) =>
        db.Spots.Where(s => !db.Trailers.Any(t => t.SpotCode == s.Code));

    // ---------- inspections ----------

    private static async Task<IResult> Inspect(InspectionRequest request, YardDbContext db, TimeProvider clock, OutboxSignal signal, CancellationToken ct)
    {
        var number = Normalize(request.TrailerNumber);
        var checks = new Checks().Optional("notes", request.Notes, Limits.Note);
        if (!checks.Ok) return checks.Problem();
        var trailer = await db.Trailers.FindAsync([number], ct);
        if (trailer is null) return Http.NotFound("Trailer");
        if (!TrailerRules.Can(trailer.Status, TrailerRules.Inspect)) return Http.Conflict(TrailerRules.NotAllowed(trailer, TrailerRules.Inspect));

        var reefer = trailer.Equipment == EquipmentTypes.Reefer;
        var hasChecklist = request.Tires is not null || request.Lights is not null || request.DoorsAndSeal is not null || request.Floor is not null;
        if (hasChecklist && (request.Tires is null || request.Lights is null || request.DoorsAndSeal is null || request.Floor is null || (reefer && request.ReeferUnit is null)))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["checklist"] = [reefer ? "Answer every item, including the reefer unit." : "Answer every checklist item."] });

        var passed = hasChecklist
            ? request.Tires!.Value && request.Lights!.Value && request.DoorsAndSeal!.Value && request.Floor!.Value && (!reefer || request.ReeferUnit!.Value)
            : request.Passed;

        var now = clock.GetUtcNow().UtcDateTime;
        var record = new Inspection
        {
            Id = Guid.NewGuid(), TrailerNumber = number, Passed = passed, OccurredAt = now, Notes = Http.Clean(request.Notes),
            Tires = request.Tires ?? passed, Lights = request.Lights ?? passed, DoorsAndSeal = request.DoorsAndSeal ?? passed,
            Floor = request.Floor ?? passed, ReeferUnit = reefer ? request.ReeferUnit ?? passed : null
        };
        db.Inspections.Add(record);

        if (passed)
        {
            trailer.Status = TrailerStatuses.Ready;
            trailer.HoldReason = null;
            Enqueue(db, "TrailerReadyForPlanning", number, now, "Dock inspection passed; trailer is ready for LTL planning.");
        }
        else
        {
            trailer.Status = TrailerStatuses.OnHold;
            trailer.HoldReason = "Failed inspection" + (record.Notes is null ? "." : $": {record.Notes}");
        }
        trailer.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        if (passed) signal.Notify();
        return Results.Ok(ToDto(record));
    }

    // ---------- trailers ----------

    private static async Task<IResult> ListTrailers(YardDbContext db, string? status, string? search, int? page, int? pageSize, CancellationToken ct)
    {
        var query = db.Trailers.AsNoTracking();
        query = Http.Clean(status) switch
        {
            null => query.Where(t => t.Status != TrailerStatuses.Departed),
            "All" => query,
            var s => query.Where(t => t.Status == s)
        };
        if (Http.Clean(search) is { } term)
        {
            var t = term.ToLower();
            query = query.Where(x => x.TrailerNumber.ToLower().Contains(t) || (x.CurrentLoadNumber != null && x.CurrentLoadNumber.ToLower().Contains(t)) ||
                                     (x.Carrier != null && x.Carrier.ToLower().Contains(t)));
        }
        var kinds = await db.Spots.AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Kind, ct);
        var paged = await query.OrderByDescending(t => t.UpdatedAt).ToPagedAsync(page, pageSize, ct);
        return Results.Ok(new Paged<TrailerDto>(paged.Items.Select(t => ToDto(t, t.SpotCode is null ? null : kinds.GetValueOrDefault(t.SpotCode))).ToList(),
            paged.Total, paged.Page, paged.PageSize));
    }

    private static async Task<IResult> GetTrailer(string trailerNumber, YardDbContext db, CancellationToken ct)
    {
        var number = Normalize(trailerNumber);
        var trailer = await db.Trailers.AsNoTracking().SingleOrDefaultAsync(t => t.TrailerNumber == number, ct);
        if (trailer is null) return Http.NotFound("Trailer");
        return Results.Ok(new
        {
            trailer = ToDto(trailer, await SpotKind(db, trailer.SpotCode, ct)),
            gate = (await db.GateEvents.AsNoTracking().Where(g => g.TrailerNumber == number).OrderByDescending(g => g.OccurredAt).Take(50).ToListAsync(ct))
                .Select(g => new GateEvent(g.Id, g.TrailerNumber, g.Direction, Utc(g.OccurredAt), g.Note)),
            moves = await db.Moves.AsNoTracking().Where(m => m.TrailerNumber == number).OrderByDescending(m => m.OccurredAt).Take(50)
                .Select(m => new MoveDto(m.Id, m.TrailerNumber, m.FromSpot, m.ToSpot, m.OccurredAt)).ToListAsync(ct),
            inspections = (await db.Inspections.AsNoTracking().Where(i => i.TrailerNumber == number).OrderByDescending(i => i.OccurredAt).Take(50).ToListAsync(ct))
                .Select(ToDto),
            outbox = (await db.Outbox.AsNoTracking().Where(m => m.TrailerNumber == number).OrderByDescending(m => m.CreatedAt).Take(50).ToListAsync(ct))
                .Select(ToDto)
        });
    }

    private static Checks Validate(TrailerRequest r) => new Checks()
        .OneOf("equipment", r.Equipment, EquipmentTypes.All)
        .Range("palletCapacity", r.PalletCapacity, 1, 30)
        .Optional("loadNumber", r.LoadNumber, 30)
        .Optional("carrier", r.Carrier, 120);

    /// <summary>Pre-advises an inbound trailer so the gate can check it in.</summary>
    private static async Task<IResult> ExpectTrailer(TrailerRequest request, YardDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var number = Normalize(request.TrailerNumber);
        var checks = Validate(request).Required("trailerNumber", number, Limits.Trailer);
        if (!checks.Ok) return checks.Problem();
        var existing = await db.Trailers.FindAsync([number], ct);
        if (existing is not null && existing.Status != TrailerStatuses.Departed)
            return Http.Conflict($"{number} is already on the yard or expected.");

        var trailer = existing ?? new Trailer { TrailerNumber = number };
        if (existing is null) db.Trailers.Add(trailer);
        trailer.Equipment = request.Equipment!;
        trailer.PalletCapacity = request.PalletCapacity;
        trailer.CurrentLoadNumber = Http.Clean(request.LoadNumber);
        trailer.Carrier = Http.Clean(request.Carrier);
        trailer.Status = TrailerStatuses.Expected;
        trailer.HoldReason = null;
        trailer.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/trailers/{number}", new { trailerNumber = number });
    }

    private static async Task<IResult> UpdateTrailer(string trailerNumber, TrailerRequest request, YardDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var checks = Validate(request);
        if (!checks.Ok) return checks.Problem();
        var trailer = await db.Trailers.FindAsync([Normalize(trailerNumber)], ct);
        if (trailer is null) return Http.NotFound("Trailer");
        if (trailer.Status == TrailerStatuses.Departed) return Http.Conflict($"{trailer.TrailerNumber} has departed; its record is read-only.");
        trailer.Equipment = request.Equipment!;
        trailer.PalletCapacity = request.PalletCapacity;
        trailer.CurrentLoadNumber = Http.Clean(request.LoadNumber);
        trailer.Carrier = Http.Clean(request.Carrier);
        trailer.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Move(string trailerNumber, MoveRequest request, YardDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var trailer = await db.Trailers.FindAsync([Normalize(trailerNumber)], ct);
        if (trailer is null) return Http.NotFound("Trailer");
        var code = Http.Clean(request.Spot)?.ToUpperInvariant();
        var spot = code is null ? null : await db.Spots.FindAsync([code], ct);
        if (spot is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["spot"] = ["Choose a spot on the yard."] });
        if (spot.Code == trailer.SpotCode) return Http.Conflict($"{trailer.TrailerNumber} is already at {spot.Code}.");

        var action = spot.Kind == SpotKinds.Door ? TrailerRules.MoveToDoor : TrailerRules.MoveToParking;
        if (!TrailerRules.Can(trailer.Status, action)) return Http.Conflict(TrailerRules.NotAllowed(trailer, action));
        if (await db.Trailers.AnyAsync(t => t.SpotCode == spot.Code, ct)) return Http.Conflict($"{spot.Code} is occupied.");

        var now = clock.GetUtcNow().UtcDateTime;
        db.Moves.Add(new TrailerMove { Id = Guid.NewGuid(), TrailerNumber = trailer.TrailerNumber, FromSpot = trailer.SpotCode, ToSpot = spot.Code, OccurredAt = now });
        trailer.SpotCode = spot.Code;
        if (spot.Kind == SpotKinds.Door && trailer.Status == TrailerStatuses.Arrived) trailer.Status = TrailerStatuses.AtDoor;
        if (spot.Kind == SpotKinds.Parking && trailer.Status == TrailerStatuses.AtDoor) trailer.Status = TrailerStatuses.Arrived;
        trailer.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> StartLoading(string trailerNumber, YardDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var trailer = await db.Trailers.FindAsync([Normalize(trailerNumber)], ct);
        if (trailer is null) return Http.NotFound("Trailer");
        if (!TrailerRules.Can(trailer.Status, TrailerRules.StartLoading)) return Http.Conflict(TrailerRules.NotAllowed(trailer, TrailerRules.StartLoading));
        trailer.Status = TrailerStatuses.Loading;
        trailer.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Hold(string trailerNumber, HoldRequest request, YardDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var checks = new Checks().Required("reason", request.Reason, Limits.Note);
        if (!checks.Ok) return checks.Problem();
        var trailer = await db.Trailers.FindAsync([Normalize(trailerNumber)], ct);
        if (trailer is null) return Http.NotFound("Trailer");
        if (!TrailerRules.Can(trailer.Status, TrailerRules.Hold)) return Http.Conflict(TrailerRules.NotAllowed(trailer, TrailerRules.Hold));
        trailer.Status = TrailerStatuses.OnHold;
        trailer.HoldReason = request.Reason!.Trim();
        trailer.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Release(string trailerNumber, YardDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var trailer = await db.Trailers.FindAsync([Normalize(trailerNumber)], ct);
        if (trailer is null) return Http.NotFound("Trailer");
        if (!TrailerRules.Can(trailer.Status, TrailerRules.Release)) return Http.Conflict(TrailerRules.NotAllowed(trailer, TrailerRules.Release));
        trailer.Status = await SpotKind(db, trailer.SpotCode, ct) == SpotKinds.Door ? TrailerStatuses.AtDoor : TrailerStatuses.Arrived;
        trailer.HoldReason = null;
        trailer.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // ---------- dashboard ----------

    private static async Task<IResult> Dashboard(YardDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var since = clock.GetUtcNow().UtcDateTime.AddHours(-24);
        var statuses = await db.Trailers.AsNoTracking().Select(t => t.Status).ToListAsync(ct);
        var spots = await db.Spots.AsNoTracking().ToListAsync(ct);
        var occupied = await db.Trailers.AsNoTracking().Where(t => t.SpotCode != null).Select(t => t.SpotCode!).ToListAsync(ct);
        var doors = spots.Where(s => s.Kind == SpotKinds.Door).Select(s => s.Code).ToHashSet();
        return Results.Ok(new
        {
            byStatus = TrailerStatuses.All.Where(s => s != TrailerStatuses.Departed).Select(s => new { status = s, count = statuses.Count(x => x == s) }),
            onYard = occupied.Count,
            parkingFree = spots.Count(s => s.Kind == SpotKinds.Parking) - occupied.Count(c => !doors.Contains(c)),
            doorsInUse = occupied.Count(doors.Contains),
            doors = doors.Count,
            expected = statuses.Count(s => s == TrailerStatuses.Expected),
            gateInsToday = await db.GateEvents.CountAsync(g => g.Direction == "In" && g.OccurredAt >= since, ct),
            gateOutsToday = await db.GateEvents.CountAsync(g => g.Direction == "Out" && g.OccurredAt >= since, ct),
            outboxPending = await db.Outbox.CountAsync(m => m.SentAt == null, ct),
            outboxFailing = await db.Outbox.CountAsync(m => m.SentAt == null && m.Attempts > 0, ct)
        });
    }

    // ---------- platform ----------

    public static void MapPlatformEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/meta", (DatabaseStatus database, IOptions<LtlOptions> ltl, IOptions<AlvysOptions> alvys, IConfiguration config) => Results.Ok(new
        {
            equipmentTypes = EquipmentTypes.All,
            trailerStatuses = TrailerStatuses.All,
            storage = new { mode = database.Mode, persistent = database.Persistent, ready = database.Ready },
            demoReset = new { scheduled = !string.IsNullOrEmpty(ResetToken(config)), schedule = "Daily at 08:29 UTC" },
            ltl = new { baseUrl = ltl.Value.BaseUrl },
            integration = new { provider = "Alvys Public API", mode = alvys.Value.LiveConfigured ? "Live" : "Demo", configured = alvys.Value.LiveConfigured }
        })).WithTags("Settings");

        api.MapPost("/admin/reset-demo", async (HttpRequest request, IConfiguration config, YardDbContext db, DemoSeeder seeder,
            DatabaseGate gate, CancellationToken ct) =>
        {
            var expected = ResetToken(config);
            if (string.IsNullOrEmpty(expected)) return Results.NotFound();
            var supplied = request.Headers["X-Demo-Reset-Token"].ToString();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected)))
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid reset token.");
            if (!await gate.EnsureReadyAsync(ct))
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The database is unavailable.");
            await seeder.ResetAsync(db, ct);
            return Results.Ok(new { reset = true });
        }).ExcludeFromDescription();
    }

    private static string? ResetToken(IConfiguration config) => config["Demo:ResetToken"] ?? config["DEMO_RESET_TOKEN"];
}
