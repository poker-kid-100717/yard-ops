using Portfolio.Yard.Api.Integrations.Ltl;

namespace Portfolio.Yard.Api.Tests;

public sealed class SignatureTests
{
    [Fact]
    public void Hmac_is_stable_for_exact_payload()
    {
        var a = Signature.Create("{\"hello\":\"yard\"}", "key");
        var b = Signature.Create("{\"hello\":\"yard\"}", "key");
        var c = Signature.Create("{\"hello\":\"ltl\"}", "key");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
