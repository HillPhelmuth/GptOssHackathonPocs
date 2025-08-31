using GptOssHackathonPocs.Narrative.Core.Models;
using Markdig;
using Microsoft.AspNetCore.Components;

namespace AINarrativeSimulator.Components;

public partial class EventFeed
{
    [Parameter] public IEnumerable<WorldAgentAction> Actions { get; set; } = [];
    private List<(ActionType, string)> _actionsMarkdown => Actions.Select(x => x.ToTypeMarkdown()).ToList();
    [Parameter] public string? Class { get; set; }
    private ElementReference _feedDiv;
    [Parameter] public string? ClassName { get; set; }
    private WorldAgentAction? _expandedAction;
    private void ToggleExpand(WorldAgentAction action)
    {
        _expandedAction = _expandedAction == action ? null : action;
    }
    private string GetActionTypeClass(ActionType type)
    {
        return type switch
        {
            ActionType.SpeakTo => "type-speak",
            ActionType.MoveTo => "type-move",
            ActionType.Decide => "type-think",
            //ActionType.Emote => "type-emote",
            ActionType.Discover => "type-discover",
            ActionType.Attack => "type-attack",
            ActionType.Defend => "type-defend",
            ActionType.Purchase => "type-purchase",
            ActionType.Undermine => "type-undermine",
            ActionType.Flee => "type-flee",
            _ => "type-default"
        };
    }
    private static string MarkdownAsHtml(string markdownString)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var result = Markdown.ToHtml(markdownString, pipeline);
        return result;
    }
}