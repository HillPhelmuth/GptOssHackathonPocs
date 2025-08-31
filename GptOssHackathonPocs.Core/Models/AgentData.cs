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

[Description("A collection of action items produced for one or more incidents.")]
public sealed record ActionPlan
{
    

    [Description("Renders the action plan into a human-readable Markdown string.")]
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Action Plan");
        foreach (var action in Actions)
        {
            sb.AppendLine(action.ToMarkdown());
            sb.AppendLine();
        }
        return sb.ToString();
    }

    [JsonPropertyName("actionItems"), Description("The set of action items that comprise this plan.")]
    public IEnumerable<ActionItem> Actions { get; set; }

    
}
public class ActionStep
{
    [Description("Name of the step")]
    public required string Name { get; set; }
    [Description("Specific text instructions for carrying out the step")]
    public required string Text { get; set; } 
    [Description("Optional assignee for the step. Assignee is the person, entity or service responsible for taking the action step")]
    public string? Assignee { get; set; }
    [Description("Optional due date/time for the step")]
    public DateTimeOffset? Due { get; set; }
}