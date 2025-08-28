using AINarrativeSimulator.Components.Models;
using GptOssHackathonPocs.Narrative.Core.Models;
using Microsoft.AspNetCore.Components;

namespace AINarrativeSimulator.Components;

public partial class EventFeed
{
    [Parameter] public IEnumerable<WorldAgentAction> Actions { get; set; } = [];
    private List<(ActionType, string)> _actionsMarkdown => Actions.Select(x => x.ToTypeMarkdown()).ToList();
    [Parameter] public string? Class { get; set; }
    private ElementReference _feedDiv;
    [Parameter] public string? ClassName { get; set; }

    private string GetActionTypeClass(ActionType type) => type switch
    {
        ActionType.SpeakTo => "type-speak",
        ActionType.MoveTo => "type-move",
        ActionType.Decide => "type-think",
        ActionType.Emote => "type-emote",
        ActionType.Discover => "type-discover",
        _ => "type-default"
    };
}