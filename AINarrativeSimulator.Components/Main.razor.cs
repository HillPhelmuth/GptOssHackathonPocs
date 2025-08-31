using Blazored.LocalStorage;
using GptOssHackathonPocs.Narrative.Core;
using GptOssHackathonPocs.Narrative.Core.Models;
using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.Identity.Client;
using Microsoft.JSInterop;

namespace AINarrativeSimulator.Components;

public partial class Main
{
    private List<WorldAgentAction> _actions = [];
    private List<WorldAgent> _agents = [];

    [Inject]
    private WorldState WorldState { get; set; } = default!;
    [Inject]
    private NarrativeOrchestration NarrativeOrchestration { get; set; } = default!;
    private string? selectedAgentId;
    private bool isRunning;
    private string _rumor = "";
    private CancellationTokenSource _cts = new();

    private ElementReference _grid;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    private IJSObjectReference? _module;
    [Inject]
    private ILocalStorageService LocalStorage { get; set; } = default!;

    private bool _showWorldController = true;

    // Summary modal state
    private bool _showSummaryModal = false;
    private bool _isSummarizing = false;
    private string _summary = "";

    private async Task OpenSummaryModal()
    {
        _showSummaryModal = true;
        _isSummarizing = true;
        _summary = string.Empty;
        await InvokeAsync(StateHasChanged);
        try
        {
            _summary = await NarrativeOrchestration.SummarizeCurrentWorldState();
        }
        catch (Exception ex)
        {
            _summary = $"Error generating summary: {ex.Message}";
        }
        finally
        {
            _isSummarizing = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task CloseSummaryModal()
    {
        _showSummaryModal = false;
        return InvokeAsync(StateHasChanged);
    }

    private async Task ToggleWorldPanel()
    {
        _showWorldController = !_showWorldController;
        await InvokeAsync(StateHasChanged);
        if (_module is not null)
        {
            // Let DOM update, then re-init resizer in correct mode
            await Task.Yield();
            await _module.InvokeVoidAsync("reinitGrid", _grid, !_showWorldController);
        }
    }

    protected override void OnInitialized()
    {
        
        // Optional: seed with an example location so WorldController has content to render gracefully
        //WorldState.Locations["square"] = new LocationState
        //{
        //    Id = "square",
        //    Name = "Town Square",
        //    Atmosphere = "Quiet morning with distant gulls",
        //    Occupants = []
        //};
    }

    private async Task GenerateSummary()
    {
        _isSummarizing = true;
        _summary = string.Empty;
        await InvokeAsync(StateHasChanged);
        try
        {
            _summary = await NarrativeOrchestration.SummarizeCurrentWorldState();
        }
        catch (Exception ex)
        {
            _summary = $"Error generating summary: {ex.Message}";
        }
        finally
        {
            _isSummarizing = false;
            await InvokeAsync(StateHasChanged);
        }
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            WorldState.PropertyChanged += HandleWorldStatePropertyChanged;
            NarrativeOrchestration.WriteAgentChatMessage += HandleAgentChatMessageWritten;
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/AINarrativeSimulator.Components/resizableGrid.js");
            await _module.InvokeVoidAsync("initResizableGrid", _grid);
        }
    }

    private void HandleAgentChatMessageWritten(string chatMessage, string agent)
    {
        if (chatMessage.Contains("Oh, shit!"))
        {
            WorldState.AddRecentAction(new WorldAgentAction()
            {
                Type = ActionType.Error, ActingAgent = agent,
                BriefDescription = $"Error occurred before {agent} Finished their action !", Details = chatMessage,
                Timestamp = DateTime.Now
            });
            return;
        }
        WorldState.AddRecentAction(new WorldAgentAction() { Type = ActionType.None, ActingAgent = agent, BriefDescription = $"{agent} Finished their action and has something to say!", Details = chatMessage, Timestamp = DateTime.Now });
    }

    private async void HandleWorldStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        try
        {
            switch (e.PropertyName)
            {
                case nameof(WorldState.WorldAgents):
                    _agents = WorldState.WorldAgents.Agents;
                    await LocalStorage.SetItemAsync($"agents-{DateTime.Now:hh:mm:ss}", WorldState.WorldAgents);
                    await InvokeAsync(StateHasChanged);
                    break;
                case nameof(WorldState.RecentActions):
                    _actions = WorldState.RecentActions;
                    await InvokeAsync(StateHasChanged);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling WorldState property change: {ex.Message}");
        }
    }

    private async Task HandleStart()
    {
        isRunning = true;
        var token = _cts.Token;
        await NarrativeOrchestration.RunNarrativeAsync(_rumor, token);
        StateHasChanged();
    }

    private void HandleStop()
    {
        isRunning = false;
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        StateHasChanged();
    }

    private void HandleReset()
    {
        HandleStop();
        _actions.Clear();
        selectedAgentId = null;
        // Keep agents/worldState as-is (host app could bind to these later)
        StateHasChanged();
    }

    private void HandleInjectRumor(String rumor)
    {
        _rumor += "\nRumor: " + rumor;
        WorldState.Rumors.Add(rumor);
        _actions.Add(new WorldAgentAction()
        {
            BriefDescription = "A rumor has been injected into the world",
            Type = ActionType.None,
            Target = "public",
            Details = rumor,
            Timestamp = DateTime.Now
        });
        StateHasChanged();
    }

    private void HandleInjectEvent(String evt)
    {
        _rumor += "\nEvent: " + evt;
        WorldState.GlobalEvents.Add(evt);
        _actions.Add(new WorldAgentAction
        {
            BriefDescription = "A world event has been injected into the world",
            Type = ActionType.Discover,
            Target = "public",
            Details = evt,
            Timestamp = DateTime.Now
        });
        StateHasChanged();
    }
    private static string MarkdownAsHtml(string markdownString)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var result = Markdown.ToHtml(markdownString, pipeline);
        return result;
    }
    private Task OnSelectedAgentChanged(string id)
    {
        selectedAgentId = id;
        return Task.CompletedTask;
    }
}