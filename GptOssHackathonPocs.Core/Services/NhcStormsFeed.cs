using System.Text.Json;
using GptOssHackathonPocs.Core.Models;

namespace GptOssHackathonPocs.Core.Services;

public sealed class NhcStormsFeed : IIncidentFeed
{
    private readonly HttpClient _http;
    public NhcStormsFeed(IHttpClientFactory f) => _http = f.CreateClient("default");

    public async Task<IReadOnlyList<Incident>> FetchAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync("https://www.nhc.noaa.gov/CurrentStorms.json", ct);
        if (!resp.IsSuccessStatusCode) return [];

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var list = new List<Incident>();

        if (!doc.RootElement.TryGetProperty("activeStorms", out var storms) || storms.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var s in storms.EnumerateArray())
        {
            var id = s.GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
            var name = s.TryGetProperty("name", out var n) ? n.GetString() ?? "Tropical Cyclone" : "Tropical Cyclone";
            var basin = s.TryGetProperty("basin", out var b) ? b.GetString() : null;
            var advisory = s.TryGetProperty("advisoryNumber", out var an) ? an.GetString() : null;
            var ts = s.TryGetProperty("issueTime", out var it) && it.ValueKind==JsonValueKind.String
                ? DateTimeOffset.Parse(it.GetString()!)
                : DateTimeOffset.UtcNow;
            var link = s.TryGetProperty("stormHref", out var href) ? href.GetString() : null;

            var status = s.TryGetProperty("stormType", out var st) ? st.GetString()?.ToLowerInvariant() : null;
            var sev = status switch
            {
                string t when t?.Contains("hurricane") == true || t?.Contains("typhoon") == true => IncidentSeverity.Severe,
                string t when t?.Contains("tropical storm") == true => IncidentSeverity.Moderate,
                string t when t?.Contains("depression") == true => IncidentSeverity.Minor,
                _ => IncidentSeverity.Unknown
            };

            string? geojson = null;
            if (s.TryGetProperty("lat", out var lat) && s.TryGetProperty("lon", out var lon)
                && lat.ValueKind == JsonValueKind.Number && lon.ValueKind == JsonValueKind.Number)
            {
                geojson = $$"""
                { "type":"Feature","geometry":{"type":"Point","coordinates":[{{lon.GetDouble()}},{{lat.GetDouble()}}]},"properties":{"name":"{{name}}"} }
                """;
            }

            list.Add(new Incident
            {
                Id = id,
                Source = IncidentSource.NhcStorm,
                Severity = sev,
                Title = $"{name} ({basin}) — Adv {advisory}",
                Description = status ?? "Tropical cyclone",
                Timestamp = ts,
                GeoJson = geojson,
                Link = link
            });
        }
        return list;
    }
}
