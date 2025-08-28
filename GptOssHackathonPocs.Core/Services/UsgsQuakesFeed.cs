using System.Text.Json;
using GptOssHackathonPocs.Core.Models;

namespace GptOssHackathonPocs.Core.Services;

public sealed class UsgsQuakesFeed : IIncidentFeed
{
    private readonly HttpClient _http;
    public UsgsQuakesFeed(IHttpClientFactory f) => _http = f.CreateClient("default");

    public async Task<IReadOnlyList<Incident>> FetchAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync("https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/all_hour.geojson", ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var list = new List<Incident>();
        if (!doc.RootElement.TryGetProperty("features", out var features)) return list;

        foreach (var f in features.EnumerateArray())
        {
            var prop = f.GetProperty("properties");
            var id = prop.GetProperty("code").GetString() ?? Guid.NewGuid().ToString();
            var mag = prop.TryGetProperty("mag", out var m) && m.ValueKind is JsonValueKind.Number ? m.GetDouble() : 0.0;
            var place = prop.TryGetProperty("place", out var p) ? p.GetString() ?? "Earthquake" : "Earthquake";
            var timeMs = prop.TryGetProperty("time", out var t) && t.ValueKind is JsonValueKind.Number ? t.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var url = prop.TryGetProperty("url", out var u) ? u.GetString() : null;

            var sev = mag switch
            {
                >= 7.0 => IncidentSeverity.Extreme,
                >= 6.0 => IncidentSeverity.Severe,
                >= 5.0 => IncidentSeverity.Moderate,
                >= 4.0 => IncidentSeverity.Minor,
                _ => IncidentSeverity.Unknown
            };

            list.Add(new Incident
            {
                Id = id,
                Source = IncidentSource.UsgsQuake,
                Severity = sev,
                Title = $"M{mag:0.0} — {place}",
                Description = $"Magnitude {mag:0.0}",
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timeMs),
                GeoJson = f.GetRawText(),
                Link = url
            });
        }
        return list;
    }
}
