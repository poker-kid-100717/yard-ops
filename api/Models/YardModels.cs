namespace Portfolio.Yard.Api.Models;

public sealed record YardAsset(
    string TrailerNumber,
    string Equipment,
    int PalletCapacity,
    string Status,
    string Spot,
    string? CurrentLoadNumber,
    DateTimeOffset UpdatedAt);

public sealed record GateRequest(string TrailerNumber, string Direction, string? Note);
public sealed record GateEvent(Guid Id, string TrailerNumber, string Direction, DateTimeOffset OccurredAt, string? Note);
public sealed record InspectionRequest(string TrailerNumber, bool Passed, string? Notes);
public sealed record InspectionRecord(Guid Id, string TrailerNumber, bool Passed, DateTimeOffset OccurredAt, string? Notes);

public sealed record YardIntegrationEvent(
    Guid EventId,
    string EventType,
    string TrailerNumber,
    DateTimeOffset OccurredAt,
    string? Details,
    int SchemaVersion = 1);

public sealed record LtlCandidate(
    string OrderId,
    string Customer,
    string Origin,
    string Destination,
    int Pallets,
    int Weight,
    string Equipment,
    string Reason);

public sealed record ExternalTrailer(
    string TrailerNumber,
    string Status,
    string? EquipmentType,
    string? EquipmentSize,
    string? FleetName,
    string Source);

public sealed record ExternalTrailerResult(
    string Provider,
    bool Live,
    bool Degraded,
    string? DegradedReason,
    IReadOnlyList<ExternalTrailer> Trailers);
