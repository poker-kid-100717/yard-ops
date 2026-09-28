using Portfolio.Yard.Api.Integrations.Ltl;
using Portfolio.Yard.Api.Models;

namespace Portfolio.Yard.Api.Tests;

public sealed class OutboxStoreTests
{
    [Fact]
    public void FailedDeliveryIsDelayedBeforeNextAttempt()
    {
        var store = new OutboxStore();
        var evt = new YardIntegrationEvent(
            Guid.NewGuid(), "TrailerReadyForPlanning", "TRL-DEMO", DateTimeOffset.UtcNow, "Ready");

        store.Enqueue(evt);
        Assert.Single(store.Pending());

        store.MarkFailed(evt.EventId, "LTL unavailable");

        var message = Assert.Single(store.Messages);
        Assert.Equal(1, message.Attempts);
        Assert.True(message.NextAttemptAt > DateTimeOffset.UtcNow);
        Assert.Empty(store.Pending());
    }
}
