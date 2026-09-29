using Microsoft.EntityFrameworkCore;

namespace Portfolio.Yard.Api.Data;

/// <summary>A small fictional yard: 12 parking spots, 4 dock doors and trailers in every state.</summary>
public sealed class DemoSeeder(TimeProvider clock)
{
    public async Task ResetAsync(YardDbContext db, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Outbox.ExecuteDeleteAsync(ct);
        await db.Inspections.ExecuteDeleteAsync(ct);
        await db.Moves.ExecuteDeleteAsync(ct);
        await db.GateEvents.ExecuteDeleteAsync(ct);
        await db.Trailers.ExecuteDeleteAsync(ct);
        await db.Spots.ExecuteDeleteAsync(ct);
        await SeedCoreAsync(db, ct);
        await tx.CommitAsync(ct);
    }

    public async Task SeedAsync(YardDbContext db, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await SeedCoreAsync(db, ct);
        await tx.CommitAsync(ct);
    }

    private async Task SeedCoreAsync(YardDbContext db, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        DateTime Ago(int minutes) => now.AddMinutes(-minutes);

        var position = 0;
        foreach (var row in new[] { "A", "B" })
            for (var n = 1; n <= (row == "A" ? 8 : 4); n++)
                db.Spots.Add(new YardSpot { Code = $"{row}-{n:00}", Kind = SpotKinds.Parking, Position = position++ });
        for (var n = 1; n <= 4; n++)
            db.Spots.Add(new YardSpot { Code = $"D-{n:00}", Kind = SpotKinds.Door, Position = position++ });
        await db.SaveChangesAsync(ct);

        Trailer T(string number, string equipment, int pallets, string status, string? spot, string? load, string carrier, int minutesAgo, string? hold = null) => new()
        {
            TrailerNumber = number, Equipment = equipment, PalletCapacity = pallets, Status = status, SpotCode = spot,
            CurrentLoadNumber = load, Carrier = carrier, HoldReason = hold, UpdatedAt = Ago(minutesAgo)
        };

        db.Trailers.AddRange(
            T("TRL-4101", EquipmentTypes.DryVan, 26, TrailerStatuses.Arrived, "A-01", "LD-24018", "Roadrunner Line Haul", 42),
            T("TRL-4102", EquipmentTypes.DryVan, 26, TrailerStatuses.Loading, "D-04", null, "Rio Grande Express", 18),
            T("TRL-4115", EquipmentTypes.DryVan, 26, TrailerStatuses.AtDoor, "D-02", "LD-24030", "Four Corners Freight", 25),
            T("TRL-4207", EquipmentTypes.Reefer, 24, TrailerStatuses.Ready, "B-03", "LD-24021", "Coldchain Coyote Transport", 9),
            T("TRL-4220", EquipmentTypes.Reefer, 22, TrailerStatuses.OnHold, "A-05", null, "Coldchain Coyote Transport", 55, "Reefer unit alarm; waiting on the carrier's mechanic."),
            T("TRL-4310", EquipmentTypes.DryVan, 26, TrailerStatuses.Expected, null, null, "Roadrunner Line Haul", 3),
            T("TRL-4502", EquipmentTypes.Flatbed, 18, TrailerStatuses.Arrived, "A-03", "LD-24033", "Llano Flatbed Co.", 70),
            T("TRL-4088", EquipmentTypes.DryVan, 26, TrailerStatuses.Departed, null, "LD-24002", "Front Range Haulers", 180));

        GateEventRecord Gate(string trailer, string direction, int minutesAgo, string note) =>
            new() { Id = Guid.NewGuid(), TrailerNumber = trailer, Direction = direction, OccurredAt = Ago(minutesAgo), Note = note };
        db.GateEvents.AddRange(
            Gate("TRL-4088", "In", 300, "Gate in recorded."), Gate("TRL-4088", "Out", 180, "Loaded; departed for Denver."),
            Gate("TRL-4220", "In", 120, "Gate in recorded."), Gate("TRL-4502", "In", 95, "Gate in recorded."),
            Gate("TRL-4101", "In", 60, "Gate in recorded."), Gate("TRL-4207", "In", 58, "Gate in recorded."),
            Gate("TRL-4115", "In", 45, "Gate in recorded."), Gate("TRL-4102", "In", 40, "Gate in recorded."));

        db.Moves.AddRange(
            new TrailerMove { Id = Guid.NewGuid(), TrailerNumber = "TRL-4102", FromSpot = "A-02", ToSpot = "D-04", OccurredAt = Ago(30) },
            new TrailerMove { Id = Guid.NewGuid(), TrailerNumber = "TRL-4115", FromSpot = "A-04", ToSpot = "D-02", OccurredAt = Ago(25) },
            new TrailerMove { Id = Guid.NewGuid(), TrailerNumber = "TRL-4207", FromSpot = "D-01", ToSpot = "B-03", OccurredAt = Ago(12) });

        db.Inspections.AddRange(
            new Inspection { Id = Guid.NewGuid(), TrailerNumber = "TRL-4207", Passed = true, Tires = true, Lights = true, DoorsAndSeal = true, Floor = true, ReeferUnit = true, OccurredAt = Ago(15), Notes = "Seal 88213 applied." },
            new Inspection { Id = Guid.NewGuid(), TrailerNumber = "TRL-4220", Passed = false, Tires = true, Lights = true, DoorsAndSeal = true, Floor = true, ReeferUnit = false, OccurredAt = Ago(55), Notes = "Reefer unit alarm." });

        await db.SaveChangesAsync(ct);
    }
}
