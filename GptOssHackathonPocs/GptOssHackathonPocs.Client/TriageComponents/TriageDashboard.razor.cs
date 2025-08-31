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
    private IncidentCard? _selectedCard;
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
    [Inject]
    private NavigationManager Nav { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        
        
        _critical = ["LAX Airport", "Harbor Complex", "Metro Rail"];
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Refresh(false);
            StateHasChanged();
            await Task.Delay(250);
            await GenerateActionPlan();
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    private void HandleSelectChange(ChangeEventArgs e)
    {
        var v = e.Value?.ToString();
        SelectIncident(v);
    }

    private void SelectIncident(string? v)
    {
        var sel = _incidents?.FirstOrDefault(x => x.Id == v);
        if (sel is not null)
        {
            _selected = sel;
            _selectedCard = _cards.FirstOrDefault(c => c.IncidentId == sel.Id);
            StateHasChanged();
        }
    }

    private async Task Refresh(bool callAi = true)
    {
        _incidents = await Aggregator.FetchAllAsync();
        _selected = _incidents?[0];
        if (callAi)
            await GenerateActionPlan();
    }

    private void SetTab(Tab tab)
    {
        _activeTab = tab;
    }

    private string ActiveTabClass(Tab t) => _activeTab == t ? "td-tab active" : "td-tab";
    private List<IncidentCard> _cards = [];
    private bool _isBusy = false;
    private string _busyText = "";
    private async Task GenerateActionPlan()
    {
        if (_incidents is null) return;
        _isBusy = true;
        _busyText = "Hydrating Incidents with vulnerability, population and other data...";
        StateHasChanged();
        await Task.Delay(1);
        var cardTasks = _incidents.Select(incident => CardBuilder.Build(incident)).ToList();
        var cards = (await Task.WhenAll(cardTasks)).ToList();
        _cards = cards;
        var token = _cts.Token;
        Console.WriteLine($"Incident Cards: \n\n{JsonSerializer.Serialize(cards, new JsonSerializerOptions() { WriteIndented = true })}");
        //var actionPlan = await Orchestrator.PlanAsync(cards, token);
        var actions = new List<ActionItem>();
        _queue.Clear();
        _busyText = "Gpt-Oss Generating Action Plan...";
        StateHasChanged();
        
        await foreach (var action in Orchestrator.PlanAsync(cards, token))
        {
            if (action is null) continue;
            actions.Add(action);
            _queue.Add(new ActionQueue.ActionQueueItem(action.IncidentId ?? "", action.Title, action.Instructions, action.SeverityLevel, action.UrgencyLevel, action.ToMarkdown()));
            InvokeAsync(StateHasChanged);
        }

        var actionPlan = new ActionPlan { Actions = actions};
        _actionPlanMarkdown = actionPlan is null ? "### Error\n\nCould not generate action plan." : actionPlan.ToMarkdown();
        _isBusy = false;
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
