using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;

namespace GptOssHackathonPocs.Core.Models.StructuredOutput;

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