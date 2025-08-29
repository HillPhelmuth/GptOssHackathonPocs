using AINarrativeSimulator.Components.Models;
using Blazored.LocalStorage;
using GptOssHackathonPocs.Narrative.Core;
using GptOssHackathonPocs.Narrative.Core.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Identity.Client;
using Microsoft.JSInterop;

namespace AINarrativeSimulator.Components;

public partial class Main
{
    private List<WorldAgentAction> actions = [];
    private List<WorldAgent> agents = [];

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
        WorldState.PropertyChanged += HandleWorldStatePropertyChanged;
        NarrativeOrchestration.WriteAgentChatMessage += HandleAgentChatMessageWritten;
        // Optional: seed with an example location so WorldController has content to render gracefully
        //WorldState.Locations["square"] = new LocationState
        //{
        //    Id = "square",
        //    Name = "Town Square",
        //    Atmosphere = "Quiet morning with distant gulls",
        //    Occupants = []
        //};
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/AINarrativeSimulator.Components/resizableGrid.js");
            await _module.InvokeVoidAsync("initResizableGrid", _grid);
        }
    }

    private void HandleAgentChatMessageWritten(string obj)
    {
        WorldState.AddRecentAction(new WorldAgentAction(){Type = ActionType.Decide, Details = obj, Timestamp = DateTime.Now});
    }

    private async void HandleWorldStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        try
        {
            switch (e.PropertyName)
            {
                case nameof(WorldState.WorldAgents):
                    agents = WorldState.WorldAgents.Agents;
                    await LocalStorage.SetItemAsync($"agents-{DateTime.Now:hh:mm:ss}", WorldState.WorldAgents);
                    await InvokeAsync(StateHasChanged);
                    break;
                case nameof(WorldState.RecentActions):
                    actions = WorldState.RecentActions;
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
        actions.Clear();
        selectedAgentId = null;
        // Keep agents/worldState as-is (host app could bind to these later)
        StateHasChanged();
    }

    private void HandleInjectRumor(String rumor)
    {
        _rumor += "\nRumor: " + rumor;
        WorldState.Rumors.Add(rumor);
        actions.Add(new WorldAgentAction()
        {
            Type = ActionType.SpeakTo,
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
        actions.Add(new WorldAgentAction
        {
            Type = ActionType.Discover,
            Target = "public",
            Details = evt,
            Timestamp = DateTime.Now
        });
        StateHasChanged();
    }

    private Task OnSelectedAgentChanged(string id)
    {
        selectedAgentId = id;
        return Task.CompletedTask;
    }
}