using Portfolio.Yard.Api.Data;
using Portfolio.Yard.Api.Integrations.Ltl;
using Portfolio.Yard.Api.Services;

namespace Portfolio.Yard.Api.Tests;

public sealed class TrailerRulesTests
{
    [Theory]
    [InlineData(TrailerStatuses.Expected, TrailerRules.GateIn, true)]
    [InlineData(TrailerStatuses.Expected, TrailerRules.GateOut, false)]
    [InlineData(TrailerStatuses.Arrived, TrailerRules.MoveToDoor, true)]
    [InlineData(TrailerStatuses.Arrived, TrailerRules.StartLoading, false)]
    [InlineData(TrailerStatuses.Arrived, TrailerRules.GateOut, false)]
    [InlineData(TrailerStatuses.Arrived, TrailerRules.GateIn, false)]
    [InlineData(TrailerStatuses.AtDoor, TrailerRules.StartLoading, true)]
    [InlineData(TrailerStatuses.AtDoor, TrailerRules.Inspect, true)]
    [InlineData(TrailerStatuses.Loading, TrailerRules.Inspect, true)]
    [InlineData(TrailerStatuses.Loading, TrailerRules.MoveToParking, false)]
    [InlineData(TrailerStatuses.Loading, TrailerRules.GateOut, false)]
    [InlineData(TrailerStatuses.Ready, TrailerRules.GateOut, true)]
    [InlineData(TrailerStatuses.Ready, TrailerRules.Hold, false)]
    [InlineData(TrailerStatuses.OnHold, TrailerRules.Release, true)]
    [InlineData(TrailerStatuses.OnHold, TrailerRules.GateOut, true)]
    [InlineData(TrailerStatuses.OnHold, TrailerRules.StartLoading, false)]
    [InlineData(TrailerStatuses.Departed, TrailerRules.GateIn, true)]
    [InlineData(TrailerStatuses.Departed, TrailerRules.MoveToDoor, false)]
    public void Lifecycle_allows_only_listed_moves(string status, string action, bool allowed) =>
        Assert.Equal(allowed, TrailerRules.Can(status, action));

    [Fact]
    public void Refusals_say_what_the_trailer_can_do_instead()
    {
        var trailer = new Trailer { TrailerNumber = "TRL-1", Status = TrailerStatuses.Loading };
        var message = TrailerRules.NotAllowed(trailer, TrailerRules.GateOut);
        Assert.Contains("loading", message);
        Assert.Contains("inspect", message);
    }

    [Fact]
    public void Backoff_doubles_and_is_capped_at_a_minute()
    {
        Assert.Equal(TimeSpan.FromSeconds(3), OutboxDelivery.Backoff(1));
        Assert.Equal(TimeSpan.FromSeconds(6), OutboxDelivery.Backoff(2));
        Assert.Equal(TimeSpan.FromSeconds(12), OutboxDelivery.Backoff(3));
        Assert.Equal(TimeSpan.FromMinutes(1), OutboxDelivery.Backoff(10));
    }
}
