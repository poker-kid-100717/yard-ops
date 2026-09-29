using Microsoft.EntityFrameworkCore;
using Portfolio.Yard.Api.Data;
using Portfolio.Yard.Api.Models;

namespace Portfolio.Yard.Api.Integrations.Ltl;

/// <summary>Wakes the dispatcher as soon as a yard change has written an outbox message.</summary>
public sealed class OutboxSignal
{
    private readonly SemaphoreSlim signal = new(0, 1);

    public void Notify()
    {
        if (signal.CurrentCount == 0)
        {
            try { signal.Release(); } catch (SemaphoreFullException) { /* already signalled */ }
        }
    }

    public Task WaitAsync(TimeSpan timeout, CancellationToken ct) => signal.WaitAsync(timeout, ct);
}

public sealed record DrainResult(int Attempted, int Delivered, int Failed, int StillPending);

/// <summary>
/// Delivers due outbox messages to LTL Planner. Failures back off exponentially (3 s, 6 s, 12 s … capped at
/// one minute) and stay pending; nothing is ever dropped. The API runs as a single instance, so one pass at a
/// time (guarded here) is enough; a multi-instance deployment would claim rows with FOR UPDATE SKIP LOCKED.
/// </summary>
public sealed class OutboxDelivery(YardDbContext db, LtlClient ltl, TimeProvider clock, ILogger<OutboxDelivery> logger)
{
    private static readonly SemaphoreSlim OnePass = new(1, 1);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(1);

    public static TimeSpan Backoff(int attempts) =>
        TimeSpan.FromSeconds(Math.Min(3 * Math.Pow(2, Math.Min(attempts - 1, 5)), MaxBackoff.TotalSeconds));

    public async Task<DrainResult> DrainAsync(CancellationToken ct)
    {
        await OnePass.WaitAsync(ct);
        try
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var due = await db.Outbox.Where(m => m.SentAt == null && m.NextAttemptAt <= now)
                .OrderBy(m => m.CreatedAt).Take(20).ToListAsync(ct);
            int delivered = 0, failed = 0;
            foreach (var message in due)
            {
                message.Attempts++;
                try
                {
                    await ltl.SendEventAsync(ToEvent(message), ct);
                    message.SentAt = clock.GetUtcNow().UtcDateTime;
                    message.LastError = null;
                    delivered++;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or InvalidOperationException)
                {
                    if (ct.IsCancellationRequested) throw;
                    message.NextAttemptAt = clock.GetUtcNow().UtcDateTime + Backoff(message.Attempts);
                    message.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                    failed++;
                    logger.LogWarning("Yard -> LTL delivery failed for {EventId} (attempt {Attempts}); it remains pending.", message.EventId, message.Attempts);
                }
                await db.SaveChangesAsync(ct);
            }
            var pending = await db.Outbox.CountAsync(m => m.SentAt == null, ct);
            return new DrainResult(due.Count, delivered, failed, pending);
        }
        finally
        {
            OnePass.Release();
        }
    }

    /// <summary>The exact v1 payload LTL Planner verifies; property order and names must not change.</summary>
    public static YardIntegrationEvent ToEvent(OutboxMessage m) =>
        new(m.EventId, m.EventType, m.TrailerNumber, new DateTimeOffset(DateTime.SpecifyKind(m.OccurredAt, DateTimeKind.Utc)), m.Details);
}

/// <summary>Drains the outbox every few seconds, and immediately after a yard change signals it.</summary>
public sealed class OutboxDispatcher(IServiceScopeFactory scopes, OutboxSignal signal, DatabaseStatus database, ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (database.Ready)
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<OutboxDelivery>().DrainAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Outbox pass failed; retrying shortly.");
            }

            try { await signal.WaitAsync(TimeSpan.FromSeconds(3), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
