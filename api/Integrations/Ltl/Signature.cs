using System.Security.Cryptography;
using System.Text;

namespace Portfolio.Yard.Api.Integrations.Ltl;

public static class Signature
{
    public static string Create(string body, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }
}
