using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

/// <summary>
/// Represents a summarized incident suitable for card-style display, including type, severity,
/// event time, affected admin areas, estimated exposed population, SVI percentile, a server
/// geometry reference, impacted critical facilities, and supporting evidence sources.
/// </summary>
/// <remarks>
/// This model is intended for UI presentation and downstream enrichment pipelines. It does not
/// store raw geometry; instead it references a server-side geometry key.
/// </remarks>
[Description("Summarized incident details for UI display and enrichment, with sources and context.")]
public sealed record IncidentCard
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IncidentCard"/> record.
    /// </summary>
    /// <param name="IncidentId">Unique identifier for the incident.</param>
    /// <param name="Type">
    /// Source/type of the incident. Expected values include:
    /// "NWS.Alert", "USGS.Quake", or "NHC.Storm".
    /// </param>
    /// <param name="Severity">
    /// Impact level of the incident. One of: "minor", "moderate", "severe", or "extreme".
    /// </param>
    /// <param name="Timestamp">
    /// Timestamp of the incident or its issuance in ISO-8601 with offset (UTC offset preserved).
    /// </param>
    /// <param name="AdminAreas">
    /// Canonical administrative areas affected, formatted as "State/County" (e.g., "TX/Harris", "LA/Orleans").
    /// </param>
    /// <param name="PopulationExposed">
    /// Precomputed estimate of the population exposed within the impacted geometry.
    /// </param>
    /// <param name="SviPercentile">
    /// CDC Social Vulnerability Index percentile in the inclusive range [0, 1],
    /// where 1 indicates highest vulnerability.
    /// </param>
    /// <param name="GeometryRef">
    /// Server-known geometry key referencing stored geometry; not raw GeoJSON.
    /// </param>
    /// <param name="CriticalFacilities">
    /// List of impacted critical facilities using "Category:Name" format (e.g., "Hospital:BenTaub").
    /// </param>
    /// <param name="Sources">Evidence links that support and contextualize this incident.</param>
    [Description("Creates a new incident card with core attributes and supporting evidence.")]
    public IncidentCard(string IncidentId,
        string Type, // "NWS.Alert" | "USGS.Quake" | "NHC.Storm"
        string Severity, // "minor|moderate|severe|extreme"
        DateTimeOffset Timestamp,
        string[] AdminAreas, // ["TX/Harris","LA/Orleans"]
        double PopulationExposed, // precomputed
        double SviPercentile, // 0..1 (precomputed)
        string GeometryRef, // server-known key, not raw GeoJSON
        string[] CriticalFacilities, // ["Hospital:BenTaub","School:Foo"]
        EvidenceLink[] Sources)
    {
        this.IncidentId = IncidentId;
        this.Type = Type;
        this.Severity = Severity;
        this.Timestamp = Timestamp;
        this.AdminAreas = AdminAreas;
        this.PopulationExposed = PopulationExposed;
        this.SviPercentile = SviPercentile;
        this.GeometryRef = GeometryRef;
        this.CriticalFacilities = CriticalFacilities;
        this.Sources = Sources;
    }

    /// <summary>
    /// Renders a human-readable Markdown summary of this incident.
    /// </summary>
    /// <returns>
    /// A Markdown-formatted string containing key incident details, suitable for display.
    /// </returns>
    [Description("Renders a Markdown summary of the incident for display.")]
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### Incident {IncidentId}");
        sb.AppendLine($"- {LabelWithDesc("Type", nameof(Type))}: {Type}");
        sb.AppendLine($"- {LabelWithDesc("Severity", nameof(Severity))}: {Severity}");
        sb.AppendLine($"- {LabelWithDesc("Timestamp", nameof(Timestamp))}: {Timestamp:O}");
        sb.AppendLine($"- {LabelWithDesc("Admin Areas", nameof(AdminAreas))}: {string.Join(", ", AdminAreas)}");
        sb.AppendLine($"- {LabelWithDesc("Population Exposed", nameof(PopulationExposed))}: {PopulationExposed:N0}");
        sb.AppendLine($"- {LabelWithDesc("SVI Percentile", nameof(SviPercentile))}: {SviPercentile:P0}");
        sb.AppendLine($"- {LabelWithDesc("Geometry Ref", nameof(GeometryRef))}: {GeometryRef}");
        if (CriticalFacilities.Length > 0)
            sb.AppendLine($"- {LabelWithDesc("Critical Facilities", nameof(CriticalFacilities))}: {string.Join(", ", CriticalFacilities)}");
        else
        {
            sb.AppendLine($"- {LabelWithDesc("Critical Facilities", nameof(CriticalFacilities))}: None");
        }
        if (Sources.Length > 0)
        {
            sb.AppendLine($"- {LabelWithDesc("Sources", nameof(Sources))}:");
            foreach (var s in Sources)
            {
                sb.AppendLine($"  - [{s.Label}]({s.Url})");
            }
        }
        return sb.ToString();

        // Local helper to read DescriptionAttribute for a property
        static string PropDescription(string propName)
        {
            var prop = typeof(IncidentCard).GetProperty(propName);
            if (prop is null) return string.Empty;
            var attr = (DescriptionAttribute?)Attribute.GetCustomAttribute(prop, typeof(DescriptionAttribute));
            return attr?.Description ?? string.Empty;
        }

        static string LabelWithDesc(string label, string propName)
        {
            var desc = PropDescription(propName);
            return string.IsNullOrWhiteSpace(desc) ? label : $"{label} ({desc})";
        }
    }

    /// <summary>
    /// Unique identifier for the incident.
    /// </summary>
    [Description("Unique incident identifier.")]
    [JsonPropertyName("incident_id")]
    public string IncidentId { get; init; }

    /// <summary>
    /// Source/type of the incident, e.g., "NWS.Alert", "USGS.Quake", or "NHC.Storm".
    /// </summary>
    [Description("Incident source/type (e.g., 'NWS.Alert', 'USGS.Quake', 'NHC.Storm').")]
    [JsonPropertyName("type")]
    public string Type { get; init; }

    /// <summary>
    /// Impact level of the incident: "minor", "moderate", "severe", or "extreme".
    /// </summary>
    [Description("Impact severity: minor | moderate | severe | extreme.")]
    [JsonPropertyName("severity")]
    public string Severity { get; init; }

    /// <summary>
    /// Timestamp of the incident or its issuance in ISO-8601 with offset.
    /// </summary>
    [Description("Event or issuance time as DateTimeOffset (ISO-8601 with UTC offset).")]
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Administrative areas affected by the incident, formatted as "State/County".
    /// </summary>
    [Description("Affected admin area code.")]
    [JsonPropertyName("admin_areas")]
    public string[] AdminAreas { get; init; }

    /// <summary>
    /// Estimated number of people exposed within the incident geometry.
    /// </summary>
    [Description("Precomputed estimate of exposed population.")]
    [JsonPropertyName("population_exposed")]
    public double PopulationExposed { get; init; }

    /// <summary>
    /// Social Vulnerability Index percentile in the range [0, 1].
    /// </summary>
    [Description("Social Vulnerability Index percentile in [0..1]; 1 indicates highest vulnerability population.")]
    [JsonPropertyName("svi_percentile")]
    public double SviPercentile { get; init; }

    /// <summary>
    /// Reference key to server-side stored geometry; not raw GeoJSON.
    /// </summary>
    [Description("Server geometry reference key (not raw GeoJSON).")]
    [JsonPropertyName("geometry_ref")]
    public string GeometryRef { get; init; }

    /// <summary>
    /// Impacted critical facilities, in "Category:Name" format.
    /// </summary>
    [Description("List of impacted critical facilities (e.g., 'Hospital:BenTaub').")]
    [JsonPropertyName("critical_facilities")]
    public string[] CriticalFacilities { get; init; }

    /// <summary>
    /// Evidence links that support and contextualize the incident details.
    /// </summary>
    [Description("Collection of evidence links for the incident.")]
    [JsonPropertyName("sources")]
    public EvidenceLink[] Sources { get; init; }
}
