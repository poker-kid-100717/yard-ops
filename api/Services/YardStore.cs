using System.Collections.Concurrent;
using Portfolio.Yard.Api.Models;

namespace Portfolio.Yard.Api.Services;

public sealed class YardStore
{
    private readonly ConcurrentDictionary<string, YardAsset> assets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<GateEvent> gateEvents = new();
    private readonly ConcurrentQueue<InspectionRecord> inspections = new();

    public YardStore()
    {
        foreach (var item in Seed()) assets[item.TrailerNumber] = item;
    }

    public IReadOnlyCollection<YardAsset> Assets => assets.Values.ToArray();
    public IReadOnlyCollection<GateEvent> GateEvents => gateEvents.ToArray();
    public IReadOnlyCollection<InspectionRecord> Inspections => inspections.ToArray();

    public bool TryGetAsset(string trailerNumber, out YardAsset asset) => assets.TryGetValue(trailerNumber, out asset!);
    public void UpdateAsset(YardAsset asset) => assets[asset.TrailerNumber] = asset;
    public GateEvent AddGate(GateEvent item) { gateEvents.Enqueue(item); return item; }
    public InspectionRecord AddInspection(InspectionRecord item) { inspections.Enqueue(item); return item; }

    private static IReadOnlyList<YardAsset> Seed() =>
    [
        new("TRL-4101", "Dry Van", 26, "Arrived", "A-01", "LD-24018", DateTimeOffset.UtcNow.AddMinutes(-42)),
        new("TRL-4102", "Dry Van", 26, "Loading", "D-04", null, DateTimeOffset.UtcNow.AddMinutes(-18)),
        new("TRL-4207", "Reefer", 24, "Ready", "B-03", "LD-24021", DateTimeOffset.UtcNow.AddMinutes(-9)),
        new("TRL-4310", "Dry Van", 26, "Expected", "Inbound", null, DateTimeOffset.UtcNow.AddMinutes(-3))
    ];
}
