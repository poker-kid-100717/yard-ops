using Portfolio.Yard.Api.Models;
using Portfolio.Yard.Api.Services;

namespace Portfolio.Yard.Api.Tests;

public sealed class YardStoreTests
{
    [Fact]
    public void Asset_update_is_independent_from_external_vendor_state()
    {
        var store = new YardStore();
        Assert.True(store.TryGetAsset("TRL-4101", out var before));

        store.UpdateAsset(before with { Status = "Ready", UpdatedAt = DateTimeOffset.UtcNow });

        Assert.True(store.TryGetAsset("TRL-4101", out var after));
        Assert.Equal("Ready", after.Status);
    }
}
