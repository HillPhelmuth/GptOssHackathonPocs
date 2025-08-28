using GptOssHackathonPocs.Core;
using GptOssHackathonPocs.Core.Models;
using GptOssHackathonPocs.Core.Models.Enrichment;
using GptOssHackathonPocs.Core.Services;
using Markdig;
using Microsoft.AspNetCore.Components;
using System.Text.Json;

namespace GptOssHackathonPocs.Client.TriageComponents;
public partial class TriageDashboard
{
    private enum Tab { Map, Feeds, Comm }

    private Tab _activeTab = Tab.Map;
    private IReadOnlyList<Incident>? _incidents;
    private Incident? _selected;
    private string[] _critical = [];
    private List<ActionQueue.ActionQueueItem> _queue = [];
    private string _actionPlanMarkdown;
    private CancellationTokenSource _cts = new();
    [Inject]
    private AiAgentOrchestration Orchestrator { get; set; } = default!;
    [Inject]
    private IncidentCardBuilder CardBuilder { get; set; } = default!;
    [Inject]
    private IncidentAggregator Aggregator { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        
        
        _critical = ["LAX Airport", "Harbor Complex", "Metro Rail"];
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _incidents = await Aggregator.FetchAllAsync();
            _selected = _incidents?[0];
            await GenerateActionPlan();
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    private void SetTab(Tab tab)
    {
        _activeTab = tab;
    }

    private string ActiveTabClass(Tab t) => _activeTab == t ? "td-tab active" : "td-tab";

    private async Task GenerateActionPlan()
    {
        if (_incidents is null) return;
        var cardTasks = _incidents.Select(incident => CardBuilder.Build(incident)).ToList();
        var cards = (await Task.WhenAll(cardTasks)).ToList();
        var token = _cts.Token;
        Console.WriteLine($"Incident Cards: \n\n{JsonSerializer.Serialize(cards, new JsonSerializerOptions() { WriteIndented = true })}");
        var actionPlan = await Orchestrator.PlanAsync(cards, token);
        foreach (var action in actionPlan?.Actions ?? [])
        {
            _queue.Add(new ActionQueue.ActionQueueItem(action.IncidentId ?? "", action.Title, action.Instructions, action.SeverityLevel, action.UrgencyLevel, action.ToMarkdown()));
        }
        _actionPlanMarkdown = actionPlan is null ? "### Error\n\nCould not generate action plan." : actionPlan.ToMarkdown();
        StateHasChanged();
    }
    private void CancelGeneration()
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
    }

    private static string MarkdownAsHtml(string markdownString)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var result = Markdown.ToHtml(markdownString, pipeline);
        return result;
    }
    private Task OnActionStatusChanged((ActionQueue.ActionQueueItem item, string status) arg)
    {
        Console.WriteLine($"Action '{arg.item.Title}' -> {arg.status}");
        return Task.CompletedTask;
    }

    private Task OnChannel(CommunicationsHub.Channel ch)
    {
        Console.WriteLine($"Channel clicked: {ch.Name}");
        return Task.CompletedTask;
    }

    private Task OnQuickMessage(string msg)
    {
        Console.WriteLine($"Quick message: {msg}");
        return Task.CompletedTask;
    }
}
