using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Portfolio.Yard.Api.Integrations.Ltl;

public static class Signature
{
    /// <summary>Unix seconds at signing. LTL rejects requests whose timestamp is more than five minutes off.</summary>
    public const string TimestampHeader = "X-Portfolio-Timestamp";

    /// <summary>What Yard sends: the signature covers "{timestamp}.{body}", so a captured request goes stale.</summary>
    public static string CreateTimestamped(long unixSeconds, string body, string key) => Create(unixSeconds.ToString(CultureInfo.InvariantCulture) + "." + body, key);

    public static string Create(string body, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }
}
