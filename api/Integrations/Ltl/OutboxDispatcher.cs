namespace Portfolio.Yard.Api.Integrations.Ltl;

public sealed class OutboxDispatcher(
    OutboxStore store,
    LtlClient ltl,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var message in store.Pending())
            {
                try
                {
                    await ltl.SendEventAsync(message.Event, stoppingToken);
                    store.MarkSent(message.Id);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Yard -> LTL delivery failed for {EventId}; it remains pending.", message.Id);
                    store.MarkFailed(message.Id, ex.Message);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}
