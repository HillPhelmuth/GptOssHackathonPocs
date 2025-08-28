using System.Text.Json.Serialization;

namespace GptOssHackathonPocs.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter<IncidentSource>))]
public enum IncidentSource { NwsAlert, UsgsQuake, NhcStorm, NasaFirms }
[JsonConverter(typeof(JsonStringEnumConverter<IncidentSeverity>))]
public enum IncidentSeverity { Unknown = 0, Minor = 1, Moderate = 2, Severe = 3, Extreme = 4 }

public sealed class Incident
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public IncidentSource Source { get; init; }
    public IncidentSeverity Severity { get; init; } = IncidentSeverity.Unknown;
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public DateTimeOffset Timestamp { get; init; }
    public string? GeoJson { get; init; }    // Feature or FeatureCollection (WGS84)
    public string? Link { get; init; }        // Source URL
}
