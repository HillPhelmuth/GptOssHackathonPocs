using System.Collections.Concurrent;
using GptOssHackathonPocs.Narrative.Core;
using Microsoft.AspNetCore.Components;

namespace GptOssHackathonPocs.Client.Pages;
public partial class NarrativeZooPage : ComponentBase, IDisposable
{
    [Inject]
    private INarrativeOrchestration NarrativeOrchestration { get; set; } = default!;

    [Inject]
    private WorldState WorldState { get; set; } = default!;

    private readonly List<string> _log = [];
    private string _userInput = "Begin the narrative with any character agent";
    private bool _isRunning;
    private CancellationTokenSource _cts = new();

    protected override void OnInitialized()
    {
        //NarrativeOrchestration.WriteAgentChatMessage += OnAgentMessage;
    }

    private void OnAgentMessage(string msg)
    {
        // Marshal to UI thread by queueing and invoking StateHasChanged
        _log.Add(msg);
        InvokeAsync(StateHasChanged);
    }

    //private async Task RunAsync()
    //{
    //    if (_isRunning) return;
    //    _isRunning = true;
    //    try
    //    {
    //        var token = _cts.Token;
    //        await NarrativeOrchestration.RunNarrativeAsync(token);
    //    }
    //    catch (OperationCanceledException)
    //    {
    //        _log.Add("[canceled]");
    //    }
    //    finally
    //    {
    //        _isRunning = false;
    //        _cts.Dispose();
    //        _cts = new CancellationTokenSource();
    //    }
    //}

    private void Cancel()
    {
        if (_isRunning)
        {
            _cts.Cancel();
        }
    }

    private void Clear()
    {
        _log.Clear();
    }

    public void Dispose()
    {
        //NarrativeOrchestration.WriteAgentChatMessage -= OnAgentMessage;
        _cts.Cancel();
        _cts.Dispose();
    }
}
