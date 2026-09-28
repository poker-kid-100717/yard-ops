using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Portfolio.Yard.Api.Models;

namespace Portfolio.Yard.Api.Integrations.Alvys;

public interface IExternalTrailerReader
{
    Task<ExternalTrailerResult> GetTrailersAsync(CancellationToken ct);
}

public sealed class AlvysTrailerReader(
    IHttpClientFactory clients,
    IOptions<AlvysOptions> options,
    AlvysTokenProvider tokens,
    ILogger<AlvysTrailerReader> logger) : IExternalTrailerReader
{
    public const string ApiClient = "alvys-api";
    private readonly AlvysOptions cfg = options.Value;

    public async Task<ExternalTrailerResult> GetTrailersAsync(CancellationToken ct)
    {
        if (!cfg.LiveConfigured) return Demo();
        try
        {
            var token = await tokens.GetAsync(ct);
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{cfg.ApiBaseUrl.TrimEnd('/')}/{cfg.TrailerApiVersion}/trailers/search");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new { Page = 0, PageSize = 25, Status = new[] { "Active" } });
            using var response = await clients.CreateClient(ApiClient).SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken: ct);
            var mapped = (payload?.Items ?? []).Select(x => new ExternalTrailer(
                x.TrailerNum ?? x.Id ?? "unknown", x.Status ?? "Unknown", x.EquipmentType, x.EquipmentSize, x.Fleet?.Name, "Alvys Public API")).ToArray();
            return new("Alvys Public API", true, false, null, mapped);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Alvys trailer read degraded.");
            return Demo() with { Degraded = true, DegradedReason = "Live Alvys trailer read unavailable; synthetic equipment is shown instead." };
        }
    }

    private static ExternalTrailerResult Demo() => new("Synthetic portfolio provider", false, false, null,
    [
        new("EXT-TRL-11", "Active", "Dry Van", "53 ft", "Portfolio Fleet", "Synthetic"),
        new("EXT-TRL-12", "Active", "Reefer", "53 ft", "Portfolio Fleet", "Synthetic")
    ]);

    private sealed record SearchResponse(int Page, int PageSize, int Total, List<TrailerItem>? Items);
    private sealed record TrailerItem(string? Id, string? TrailerNum, Fleet? Fleet, string? Status, string? EquipmentType, string? EquipmentSize);
    private sealed record Fleet(string? Id, string? Name, string? InvoiceNumberPrefix);
}
