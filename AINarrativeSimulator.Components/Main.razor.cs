using AINarrativeSimulator.Components.Models;
using GptOssHackathonPocs.Narrative.Core;
using GptOssHackathonPocs.Narrative.Core.Models;
using Microsoft.AspNetCore.Components;

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

    private void HandleAgentChatMessageWritten(string obj)
    {
        WorldState.AddRecentAction(new WorldAgentAction(){Type = ActionType.Decide, Details = obj, Timestamp = DateTime.Now});
    }

    private void HandleWorldStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(WorldState.WorldAgents):
                agents = WorldState.WorldAgents.Agents;
                InvokeAsync(StateHasChanged);
                break;
            case nameof(WorldState.RecentActions):
                actions = WorldState.RecentActions;
                InvokeAsync(StateHasChanged);
                break;
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

    private void HandleInjectRumor(string rumor)
    {
        _rumor = rumor;
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

    //private void HandleInjectEvent(string evt)
    //{
    //    WorldState.GlobalEvents.Add(evt);
    //    actions.Add(new WorldAgentAction
    //    {
    //        Type = "discover",
    //        AgentId = "World",
    //        Content = evt,
    //        Location = "Pineharbor",
    //        Visibility = "public",
    //        Timestamp = DateTime.Now
    //    });
    //    StateHasChanged();
    //}

    private Task OnSelectedAgentChanged(string id)
    {
        selectedAgentId = id;
        return Task.CompletedTask;
    }
}