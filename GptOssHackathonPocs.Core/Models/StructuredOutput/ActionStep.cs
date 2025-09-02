using System.ComponentModel;

namespace GptOssHackathonPocs.Core.Models.StructuredOutput;

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