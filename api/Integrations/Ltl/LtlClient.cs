using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Portfolio.Yard.Api.Models;

namespace Portfolio.Yard.Api.Integrations.Ltl;

public sealed class LtlClient(HttpClient http, IOptions<LtlOptions> options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly LtlOptions cfg = options.Value;

    public async Task<IReadOnlyList<LtlCandidate>> GetCandidatesAsync(YardAsset asset, CancellationToken ct)
    {
        var url = $"api/integrations/v1/yard/candidates?trailerNumber={Uri.EscapeDataString(asset.TrailerNumber)}" +
                  $"&equipment={Uri.EscapeDataString(asset.Equipment)}&maxPallets={asset.PalletCapacity}";
        return await http.GetFromJsonAsync<List<LtlCandidate>>(url, Json, ct) ?? [];
    }

    public async Task SendEventAsync(YardIntegrationEvent evt, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(evt, Json);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/integrations/v1/yard/events");
        request.Headers.Add("X-Portfolio-Signature", Signature.Create(body, cfg.SigningKey));
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
