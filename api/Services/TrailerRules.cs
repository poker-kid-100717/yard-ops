using Portfolio.Yard.Api.Data;

namespace Portfolio.Yard.Api.Services;

/// <summary>
/// The trailer lifecycle in one table:
/// Expected → Arrived → AtDoor → Loading → Ready → Departed, with holds from Arrived, AtDoor or Loading.
/// Every yard action checks it; anything else is a 409 that lists what the trailer can do next.
/// </summary>
public static class TrailerRules
{
    public const string GateIn = "gate-in";
    public const string GateOut = "gate-out";
    public const string MoveToDoor = "move-to-door";
    public const string MoveToParking = "move-to-parking";
    public const string StartLoading = "start-loading";
    public const string Inspect = "inspect";
    public const string Hold = "hold";
    public const string Release = "release";

    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        [TrailerStatuses.Expected] = [GateIn],
        [TrailerStatuses.Arrived] = [MoveToDoor, MoveToParking, Hold],
        [TrailerStatuses.AtDoor] = [StartLoading, Inspect, MoveToDoor, MoveToParking, Hold],
        [TrailerStatuses.Loading] = [Inspect, Hold],
        [TrailerStatuses.Ready] = [GateOut, MoveToDoor, MoveToParking],
        [TrailerStatuses.OnHold] = [Release, GateOut, MoveToDoor, MoveToParking],
        [TrailerStatuses.Departed] = [GateIn]
    };

    public static bool Can(string status, string action) => Allowed.TryGetValue(status, out var actions) && actions.Contains(action);

    public static IReadOnlyList<string> ActionsFor(string status) => Allowed.TryGetValue(status, out var actions) ? actions : [];

    public static string Describe(string status) => status switch
    {
        TrailerStatuses.AtDoor => "at a door",
        TrailerStatuses.OnHold => "on hold",
        _ => status.ToLowerInvariant()
    };

    public static string NotAllowed(Trailer trailer, string action)
    {
        var next = ActionsFor(trailer.Status);
        return $"{trailer.TrailerNumber} is {Describe(trailer.Status)}, so it can't {action.Replace('-', ' ')}." +
               (next.Count == 0 ? "" : $" It can: {string.Join(", ", next.Select(a => a.Replace('-', ' ')))}.");
    }
}
