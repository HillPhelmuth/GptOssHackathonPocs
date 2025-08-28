using GptOssHackathonPocs.Core.Models.Enrichment;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GptOssHackathonPocs.Core.Models;

//public sealed record EvidenceLink(
//    [property: JsonPropertyName("label")] string Label,
//    [property: JsonPropertyName("url")] string Url);

//public sealed record IncidentCard(
//    [property: JsonPropertyName("incident_id")]
//    string IncidentId,
//    [property: JsonPropertyName("type")] string Type, // "NWS.Alert" | "USGS.Quake" | "NHC.Storm"
//    [property: JsonPropertyName("severity")]
//    string Severity, // "minor|moderate|severe|extreme"
//    [property: JsonPropertyName("timestamp")]
//    DateTimeOffset Timestamp,
//    [property: JsonPropertyName("admin_areas")]
//    string[] AdminAreas, // ["TX/Harris","LA/Orleans"]
//    [property: JsonPropertyName("population_exposed")]
//    double PopulationExposed, // precomputed
//    [property: JsonPropertyName("svi_percentile")]
//    double SviPercentile, // 0..1 (precomputed)
//    [property: JsonPropertyName("geometry_ref")]
//    string GeometryRef, // server-known key, not raw GeoJSON
//    [property: JsonPropertyName("critical_facilities")]
//    string[] CriticalFacilities, // ["Hospital:BenTaub","School:Foo"]
//    [property: JsonPropertyName("sources")]
//    EvidenceLink[] Sources // canonical URLs of feeds, advisories
//)
//{
//    public string ToMarkdown()
//    {
//        var sb = new StringBuilder();
//        sb.AppendLine($"### Incident {IncidentId}");
//        sb.AppendLine($"- Type: {Type}");
//        sb.AppendLine($"- Severity: {Severity}");
//        sb.AppendLine($"- Timestamp: {Timestamp:O}");
//        sb.AppendLine($"- Admin Areas: {string.Join(", ", AdminAreas)}");
//        sb.AppendLine($"- Population Exposed: {PopulationExposed:N0}");
//        sb.AppendLine($"- SVI Percentile: {SviPercentile:P0}");
//        sb.AppendLine($"- Geometry Ref: {GeometryRef}");
//        if (CriticalFacilities.Length > 0)
//            sb.AppendLine($"- Critical Facilities: {string.Join(", ", CriticalFacilities)}");
//        if (Sources.Length > 0)
//        {
//            sb.AppendLine($"- Sources:");
//            foreach (var s in Sources)
//            {
//                sb.AppendLine($"  - [{s.Label}]({s.Url})");
//            }
//        }
//        return sb.ToString();
//    }
//}

[Description("Represents an actionable task for a specific incident, including rationale, supporting evidence, required tools, priority, audience, and optional parameters.")]
public sealed record ActionItem
{
    public ActionItem(string IncidentId,
        string Title,                 // "Pre-stage pumps at XYZ"
        string Rationale,         // human-readable, cite evidence labels
        string[] EvidenceLabels,     // must match EvidenceLink.label provided
        string[] RequiredTools,      // e.g., ["Publish_Closure","Notify_Channel"]
        string Priority,              // "urgent|high|normal|low"
        string Audience,              // "ops|public|ems"
        Dictionary<string, string>? Parameters)
    {
        this.IncidentId = IncidentId;
        this.Title = Title;
        this.Rationale = Rationale;
        this.EvidenceLabels = EvidenceLabels;
        this.RequiredTools = RequiredTools;
        this.Priority = Priority;
        this.Audience = Audience;
        this.Parameters = Parameters;
    }

    [JsonPropertyName("incident_id")]
    [Description("Identifier of the incident this action item is associated with.")]
    public string IncidentId { get; init; }

    [JsonPropertyName("title")]
    [Description("Short, human-readable title describing the action to take.")]
    public string Title { get; init; }

    [JsonPropertyName("rationale")]
    [Description("Explanation of why this action is recommended, optionally citing evidence labels.")]
    public string Rationale { get; init; }

    [JsonPropertyName("evidence_labels")]
    [Description("Labels referencing evidence items that support this action.")]
    public string[] EvidenceLabels { get; init; }

    [JsonPropertyName("required_tools")]
    [Description("List of tool identifiers required to execute this action.")]
    public string[] RequiredTools { get; init; }

    [JsonPropertyName("priority")]
    [Description("Action priority. Expected values: urgent, high, normal, or low.")]
    public string Priority { get; init; }
    [JsonPropertyName("severity_level")]
    [Description("Numerical severity level from 1 (lowest) to 10 (highest) indicating the Severity of the incident based on a combination of the event severity and the population affected.")]
    public int SeverityLevel { get; set; }
    [JsonPropertyName("urgency_level")]
    [Description("Numerical urgency level from 1 (lowest) to 10 (highest) indicating the Urgency (i.e. time-sensitive nature) of the incident.")]
    public int UrgencyLevel { get; set; }
    [JsonPropertyName("audience")]
    [Description("Intended audience for the action. Expected values: ops, public, or ems.")]
    public string Audience { get; init; }
    [JsonPropertyName("instructions")]
    [Description("Detailed, human-readable instructions for executing the action.")]
    public string Instructions { get; set; }

    [JsonPropertyName("parameters")]
    [Description("Optional key/value parameters consumed by the required tools for execution.")]
    public Dictionary<string, string>? Parameters { get; init; }

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        // Add Each property with its description if available and value in markdown format
        sb.AppendLine($"### Action for Incident {IncidentId}");
        sb.AppendLine($"- {LabelWithDesc("Title", nameof(Title))}: {Title}");
        sb.AppendLine($"- {LabelWithDesc("Rationale", nameof(Rationale))}: {Rationale}");
        sb.AppendLine($"- {LabelWithDesc("Evidence Labels", nameof(EvidenceLabels))}: {string.Join(", ", EvidenceLabels)}");
        sb.AppendLine($"- {LabelWithDesc("Required Tools", nameof(RequiredTools))}: {string.Join(", ", RequiredTools)}");
        sb.AppendLine($"- {LabelWithDesc("Priority", nameof(Priority))}: {Priority}");
        sb.AppendLine($"- {LabelWithDesc("Audience", nameof(Audience))}: {Audience}");
        if (Parameters is { Count: > 0 })
        {
            sb.AppendLine($"- {LabelWithDesc("Parameters", nameof(Parameters))}:");
            foreach (var param in Parameters)
            {
                sb.AppendLine($"  - {param.Key}: {param.Value}");
            }
        }

        return sb.ToString();
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
}

[Description("A collection of action items produced for one or more incidents.")]
public sealed record ActionPlan(
    [property: JsonPropertyName("actions"), Description("The set of action items that comprise this plan.")]
    ActionItem[] Actions
)
{
    [Description("Renders the action plan into a human-readable Markdown string.")]
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Action Plan");
        foreach (var action in Actions)
        {
            sb.AppendLine($"## Action for Incident {action.IncidentId}");
            sb.AppendLine($"- Title: {action.Title}");
            sb.AppendLine($"- Rationale: {action.Rationale}");
            sb.AppendLine($"- Evidence Labels: {string.Join(", ", action.EvidenceLabels)}");
            sb.AppendLine($"- Required Tools: {string.Join(", ", action.RequiredTools)}");
            sb.AppendLine($"- Priority: {action.Priority}");
            sb.AppendLine($"- Audience: {action.Audience}");
            if (action.Parameters is { Count: > 0 })
            {
                sb.AppendLine("- Parameters:");
                foreach (var param in action.Parameters)
                {
                    sb.AppendLine($"  - {param.Key}: {param.Value}");
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}