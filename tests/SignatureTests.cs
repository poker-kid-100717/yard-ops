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

public sealed class SignatureVectorTests
{
    // Fixed vector shared with LTL Planner: if this changes, deployed LTL stops accepting Yard events.
    [Fact]
    public void Signature_matches_the_published_vector()
    {
        const string body = "{\"eventId\":\"11111111-1111-1111-1111-111111111111\",\"eventType\":\"TrailerReadyForPlanning\"}";
        Assert.Equal("ce44528657b4057b7a5512c16186423b48858208941c0e04cab4930f903b04db", Signature.Create(body, "portfolio-test-key"));
    }
}
