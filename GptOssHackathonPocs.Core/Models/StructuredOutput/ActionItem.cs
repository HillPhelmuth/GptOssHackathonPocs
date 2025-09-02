using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;
using GptOssHackathonPocs.Core.Models.Enrichment;

namespace GptOssHackathonPocs.Core.Models.StructuredOutput;

[Description("Represents an actionable task for a specific incident, including rationale, supporting evidence, required tools, priority, audience, and optional parameters.")]
public sealed class ActionItem
{

    [JsonPropertyName("incident_id")]
    [Description("Identifier of the incident this action item is associated with.")]
    public required string IncidentId { get; set; }

    [JsonPropertyName("title")]
    [Description("Short, human-readable title describing the action to take.")]
    public required string Title { get; set; }

    [JsonPropertyName("rationale")]
    [Description("Explanation of why this action is recommended, optionally citing evidence labels.")]
    public required string Rationale { get; set; }
    [JsonPropertyName("description")]
    [Description("Description of the Incident details intended for a broad audience")]
    public required string Description { get; set; }
    [JsonPropertyName("evidence_labels")]
    [Description("Labels referencing evidence items that support this action.")]
    public required string[] EvidenceLabels { get; set; }

    [JsonPropertyName("required_tools")]
    [Description("""
                 List of tool identifiers required to execute this action. options are 
                 - `Slack`,
                 - `SmsText`,
                 - `SmsVoice`,
                 - `Email`,
                 - `WeaPublicAlert`,
                 - `FieldOpRadioDispatch`,
                 - `PushNotifications`
                 """)]
    public required AvailableTools[] RequiredTools { get; set; }

    [JsonPropertyName("priority")]
    [Description("Action priority. Expected values: urgent, high, normal, or low.")]
    public required string Priority { get; set; }
    [JsonPropertyName("severity_level")]
    [Description("Numerical severity level from 1 (lowest) to 10 (highest) indicating the Severity of the incident based on a combination of the event severity and the population affected.")]
    public int SeverityLevel { get; set; }
    [JsonPropertyName("urgency_level")]
    [Description("Numerical urgency level from 1 (lowest) to 10 (highest) indicating the Urgency (i.e. time-sensitive nature) of the incident.")]
    public int UrgencyLevel { get; set; }
    [JsonPropertyName("audience")]
    [Description("Intended audience for the action. Expected values: ops, public, or ems.")]
    public required string Audience { get; set; }
    [JsonPropertyName("instructions")]
    [Description("Very Detailed, human-readable, step-by-step instructions for executing the action.")]
    public required string Instructions { get; set; }

    [JsonPropertyName("action_steps")]
    [Description("Detailed steps required to execute the action, including assignees and due dates.")]
    public List<ActionStep>? ActionSteps { get; set; }
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        // Add Each property with its description if available and value in markdown format
        sb.AppendLine($" **Action for _{Title}_**");
        sb.AppendLine($"- {LabelWithDesc(nameof(Description), nameof(Description))}: {Description}");
        sb.AppendLine($"- {LabelWithDesc("Rationale", nameof(Rationale))}: {Rationale}");
        sb.AppendLine($"- {LabelWithDesc("Evidence Labels", nameof(EvidenceLabels))}: {string.Join(", ", EvidenceLabels)}");
        sb.AppendLine($"- {LabelWithDesc("Required Tools", nameof(RequiredTools))}: {string.Join(", ", RequiredTools.Select(x => x.ToString()))}");
        sb.AppendLine($"- {LabelWithDesc("Priority", nameof(Priority))}: {Priority}");
        sb.AppendLine($"- {LabelWithDesc("Audience", nameof(Audience))}: {Audience}");
        sb.AppendLine($"- {LabelWithDesc("Severity Level", nameof(SeverityLevel))}: {SeverityLevel}");
        sb.AppendLine($"- {LabelWithDesc("Urgency Level", nameof(UrgencyLevel))}: {UrgencyLevel}");

        if (ActionSteps is { Count: > 0 })
        {
            sb.AppendLine($"- {LabelWithDesc("Required Steps", nameof(ActionSteps))}:");
            foreach (var param in ActionSteps)
            {
                sb.AppendLine($"  - {param.Name}: {param.Text}");
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
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AvailableTools
{
    Slack,
    SmsText,
    SmsVoice,
    Email,
    WeaPublicAlert,
    FieldOpRadioDispatch,
    PushNotifications
}