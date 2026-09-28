using System.Collections.Concurrent;
using Portfolio.Yard.Api.Models;

namespace Portfolio.Yard.Api.Integrations.Ltl;

public sealed record OutboxMessage(
    Guid Id,
    YardIntegrationEvent Event,
    DateTimeOffset CreatedAt,
    int Attempts,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset? SentAt,
    string? LastError);

public sealed class OutboxStore
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<Guid, OutboxMessage> messages = new();
    public IReadOnlyCollection<OutboxMessage> Messages => messages.Values.ToArray();

    public void Enqueue(YardIntegrationEvent evt)
    {
        var now = DateTimeOffset.UtcNow;
        messages.TryAdd(evt.EventId, new(evt.EventId, evt, now, 0, now, null, null));
    }

    public IReadOnlyList<OutboxMessage> Pending(int max = 20)
    {
        var now = DateTimeOffset.UtcNow;
        return messages.Values
            .Where(x => x.SentAt is null && x.NextAttemptAt <= now)
            .OrderBy(x => x.CreatedAt)
            .Take(max)
            .ToArray();
    }

    public void MarkSent(Guid id) => messages.AddOrUpdate(id,
        _ => throw new InvalidOperationException("Unknown outbox message."),
        (_, current) => current with
        {
            Attempts = current.Attempts + 1,
            SentAt = DateTimeOffset.UtcNow,
            LastError = null
        });

    public void MarkFailed(Guid id, string error) => messages.AddOrUpdate(id,
        _ => throw new InvalidOperationException("Unknown outbox message."),
        (_, current) =>
        {
            var attempts = current.Attempts + 1;
            var seconds = Math.Min(3 * Math.Pow(2, Math.Min(attempts - 1, 5)), MaxBackoff.TotalSeconds);
            return current with
            {
                Attempts = attempts,
                NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(seconds),
                LastError = error
            };
        });
}
