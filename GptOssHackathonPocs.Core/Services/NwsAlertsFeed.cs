using System.Text.Json;
using GptOssHackathonPocs.Core.Models;
using NetTopologySuite.IO;

namespace GptOssHackathonPocs.Core.Services;

public sealed class NwsAlertsFeed : IIncidentFeed
{
    private readonly HttpClient _http;
    private readonly NwsGeometryResolver _geom;

    public NwsAlertsFeed(IHttpClientFactory f, NwsGeometryResolver geom)
    {
        _http = f.CreateClient("nws");
        _geom = geom;
    }

    public async Task<IReadOnlyList<Incident>> FetchAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync("https://api.weather.gov/alerts/active?status=actual&message_type=alert&urgency=Immediate,Expected&severity=Extreme,Severe&certainty=Observed,Likely", ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var list = new List<Incident>();
        if (!doc.RootElement.TryGetProperty("features", out var features)) return list;

        foreach (var f in features.EnumerateArray())
        {
            var prop = f.GetProperty("properties");
            var id = prop.GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
            var headline = prop.GetProperty("headline").GetString() ?? prop.GetProperty("event").GetString() ?? "NWS Alert";
            var desc = prop.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            var sent = prop.TryGetProperty("sent", out var ts) && ts.ValueKind == JsonValueKind.String
                ? DateTimeOffset.Parse(ts.GetString()!)
                : DateTimeOffset.UtcNow;

            var sevStr = prop.TryGetProperty("severity", out var s) ? s.GetString() ?? "" : "";
            var sev = sevStr.ToLowerInvariant() switch
            {
                "extreme" => IncidentSeverity.Extreme,
                "severe"  => IncidentSeverity.Severe,
                "moderate"=> IncidentSeverity.Moderate,
                "minor"   => IncidentSeverity.Minor,
                _ => IncidentSeverity.Unknown
            };

            var link = prop.TryGetProperty("uri", out var u) ? u.GetString() : null;
            var nts = await _geom.ResolveAsync(f, ct);
            string? geojson = null;

            if (nts != null && !nts.IsEmpty)
            {
                var geomJson = new GeoJsonWriter().Write(nts); // geometry JSON

                // Build properties with source, severity and title
                var sourceStr = nameof(IncidentSource.NwsAlert);
                var severityStr = sev.ToString().ToLowerInvariant();

                using var geomDoc = JsonDocument.Parse(geomJson);
                var geometryElement = geomDoc.RootElement.Clone();

                var feature = new
                {
                    type = "Feature",
                    geometry = geometryElement,
                    properties = new
                    {
                        // standard properties for popup and styling
                        id,
                        source = sourceStr,
                        severity = severityStr,
                        title = headline
                    }
                };

                geojson = JsonSerializer.Serialize(feature);
            }
            
            

            // Attach to your Incident or skip if null:
            //if (geometry != null)
            //{
            //    incident.Geometry = geometry; // your model’s property
            //    incident.Bounds = geometry.EnvelopeInternal; // if you need bounds
            //}
            list.Add(new Incident
            {
                Id = id,
                Source = IncidentSource.NwsAlert,
                Severity = sev,
                Title = headline,
                Description = desc,
                Timestamp = sent,
                GeoJson = geojson,
                Link = link
            });
        }
        return list;
    }
}
