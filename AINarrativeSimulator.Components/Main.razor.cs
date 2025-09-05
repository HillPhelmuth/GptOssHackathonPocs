using Blazored.LocalStorage;
using GptOssHackathonPocs.Narrative.Core;
using GptOssHackathonPocs.Narrative.Core.Models;
using GptOssHackathonPocs.Narrative.Core.Services;
using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.Identity.Client;
using Microsoft.JSInterop;

namespace AINarrativeSimulator.Components;

public partial class Main
{
    private List<WorldAgentAction> _actions = [];
    private List<WorldAgent> _agents = [];
    private List<BeatSummary> _beats = [];
    [Inject]
    private WorldState WorldState { get; set; } = default!;
    [Inject]
    private INarrativeOrchestration NarrativeOrchestration { get; set; } = default!;
    [Inject]
    private IBeatEngine BeatEngine { get; set; } = default!;
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

    private bool _showComics;
    // Throttling for local storage writes of agents
    private static readonly TimeSpan AgentsPersistInterval = TimeSpan.FromMinutes(15);
    private DateTime _lastAgentsPersistedUtc = DateTime.MinValue;
    private DateTime _lastWorldStatePersistedUtc = DateTime.MinValue;
    private int _seenHash;
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

    protected override async Task OnInitializedAsync()
    {
        
    }

    private void HandleBeat(BeatSummary beat)
    {
        // Group by continuity: keep last N beats per storyline; cap total
        const int perStoryMax = 5;
        const int totalMax = 40;

        var existingForStory = _beats.Where(b => b.ContinuityKey == beat.ContinuityKey).ToList();
        _beats.Add(beat);
        if (existingForStory.Count >= perStoryMax)
        {
            var oldest = existingForStory.OrderBy(b => b.WindowEndUtc).First();
            _beats.Remove(oldest);
        }

        while (_beats.Count > totalMax)
        {
            _beats.RemoveAt(0);
        }

        InvokeAsync(StateHasChanged);
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
            BeatEngine.OnBeat += HandleBeat;
            await BeatEngine.StartAsync();
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
                    var nowUtc = DateTime.UtcNow;
                    if (nowUtc - _lastAgentsPersistedUtc >= AgentsPersistInterval)
                    {
                        await LocalStorage.SetItemAsync($"agents-{DateTime.Now:hh:mm:ss}", WorldState.WorldAgents);
                        _lastAgentsPersistedUtc = nowUtc;
                    }
                    await InvokeAsync(StateHasChanged);
                    break;
                case nameof(WorldState.RecentActions):
                    // Cheap change detector (hash of timestamps + counts)
                    var hash = Hash(WorldState.RecentActions);
                    if (hash == _seenHash) return;
                    _actions = WorldState.RecentActions;
                    var lastAction = _actions.FirstOrDefault();
                    if (lastAction is null) return;
                    BeatEngine.Add(lastAction);

                    var nowUtc2 = DateTime.UtcNow;

                    if (nowUtc2 - _lastWorldStatePersistedUtc >= AgentsPersistInterval)
                    {
                        await LocalStorage.SetItemAsync($"worldstate-{DateTime.Now:hh:mm:ss}", WorldState);
                        _lastWorldStatePersistedUtc = nowUtc2;
                    }
                    await InvokeAsync(StateHasChanged);
                    break;
                case nameof(WorldState.Name):
                    await InvokeAsync(StateHasChanged);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling WorldState property change: {ex.Message}");
        }
    }
    private static int Hash(IEnumerable<WorldAgentAction> actions)
    {
        unchecked
        {
            int h = 17;
            foreach (var a in actions)
            {
                h = h * 31 + a.Timestamp.GetHashCode();
                h = h * 31 + (a.Details?.GetHashCode() ?? 0);
            }

            return h;
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