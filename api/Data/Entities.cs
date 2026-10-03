namespace Portfolio.Yard.Api.Data;

// Persistence entities. Timestamps are UTC DateTime so SQL Server and the SQLite demo store
// can both sort and filter on them in the database.

public sealed class YardSpot
{
    public string Code { get; set; } = "";
    public string Kind { get; set; } = SpotKinds.Parking;
    public int Position { get; set; }
}

public sealed class Trailer
{
    public string TrailerNumber { get; set; } = "";
    public string Equipment { get; set; } = EquipmentTypes.DryVan;
    public int PalletCapacity { get; set; }
    public string Status { get; set; } = TrailerStatuses.Expected;
    public string? SpotCode { get; set; }
    public string? CurrentLoadNumber { get; set; }
    public string? Carrier { get; set; }
    public string? HoldReason { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class GateEventRecord
{
    public Guid Id { get; set; }
    public string TrailerNumber { get; set; } = "";
    public string Direction { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string? Note { get; set; }
}

public sealed class TrailerMove
{
    public Guid Id { get; set; }
    public string TrailerNumber { get; set; } = "";
    public string? FromSpot { get; set; }
    public string ToSpot { get; set; } = "";
    public DateTime OccurredAt { get; set; }
}

public sealed class Inspection
{
    public Guid Id { get; set; }
    public string TrailerNumber { get; set; } = "";
    public bool Passed { get; set; }
    public bool Tires { get; set; }
    public bool Lights { get; set; }
    public bool DoorsAndSeal { get; set; }
    public bool Floor { get; set; }
    /// <summary>Only checked on reefers; null when not applicable.</summary>
    public bool? ReeferUnit { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// An event for LTL Planner, written in the same transaction as the yard change that caused it
/// (a transactional outbox). The dispatcher delivers it later; yard work never waits on LTL.
/// </summary>
public sealed class OutboxMessage
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = "";
    public string TrailerNumber { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? LastError { get; set; }
}

public static class SpotKinds
{
    public const string Parking = "Parking";
    public const string Door = "Door";
}

public static class TrailerStatuses
{
    public const string Expected = "Expected";
    public const string Arrived = "Arrived";
    public const string AtDoor = "AtDoor";
    public const string Loading = "Loading";
    public const string Ready = "Ready";
    public const string OnHold = "OnHold";
    public const string Departed = "Departed";
    public static readonly string[] All = [Expected, Arrived, AtDoor, Loading, Ready, OnHold, Departed];
}

public static class EquipmentTypes
{
    public const string DryVan = "Dry Van";
    public const string Reefer = "Reefer";
    public const string Flatbed = "Flatbed";
    public static readonly string[] All = [DryVan, Reefer, Flatbed];
}

public static class Limits
{
    public const int Trailer = 20;
    public const int Note = 500;
}
