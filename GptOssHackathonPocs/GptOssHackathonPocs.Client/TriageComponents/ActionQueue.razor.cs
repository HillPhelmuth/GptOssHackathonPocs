using Markdig;
using Microsoft.AspNetCore.Components;
using System.Collections.Generic;

namespace GptOssHackathonPocs.Client.TriageComponents;

public partial class ActionQueue
{
    public sealed record ActionQueueItem(
        string IncidentId,
        string Title,
        string Rationale,
        int PriorityRank,
        int UrgencyLevel, string FullMarkdown);

    [Parameter] public IReadOnlyList<ActionQueueItem> Items { get; set; } = [];
    [Parameter] public EventCallback<(ActionQueueItem item, string status)> OnStatusChanged { get; set; }

    private static string? UrgencyBadge(ActionQueueItem it)
        => it.UrgencyLevel >= 8 ? "immediate" : it.UrgencyLevel >= 5 ? "high" : null;

    private Task ChangeStatus(ActionQueueItem item, ChangeEventArgs e)
        => OnStatusChanged.InvokeAsync((item, e?.Value?.ToString() ?? string.Empty));
    private static string MarkdownAsHtml(string markdownString)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var result = Markdown.ToHtml(markdownString, pipeline);
        return result;
    }

    // State for expand/collapse of details per item
    private readonly HashSet<string> _expanded = [];
    private static string KeyFor(ActionQueueItem it) => $"{it.IncidentId}|{it.Title}";
    private bool IsExpanded(ActionQueueItem it) => _expanded.Contains(KeyFor(it));
    private void ToggleDetails(ActionQueueItem it)
    {
        var key = KeyFor(it);
        if (!_expanded.Add(key))
            _expanded.Remove(key);
        StateHasChanged();
    }
}