using GptOssHackathonPocs.Core.Models.Publishing;
using Microsoft.AspNetCore.Components;

namespace GptOssHackathonPocs.Client.TriageComponents;

public partial class CommsHub
{
    private int Step { get; set; } = 1;
    private SelectionModel Model { get; } = new();
    private ComposeModel Compose { get; } = new();
    private ApproveModel Approve { get; } = new();
    private bool Sent { get; set; }
    private Dictionary<Guid, DispatchJob> Jobs { get; } = new();
    private void Next() => Step++;
    [Inject]
    private ActionQueueState ActionQueue { get; set; } = default!;
    private string CapPreview()
    {
        if (!Model.ChannelMap[Channel.CAP] || Queue.Selected.Count == 0) return "(No CAP preview)";
        // Minimal readable preview
        var first = Queue.Selected.First();
        return
            $"<alert><info><headline>{first.Title}</headline><description>{Compose.LongText}</description></info></alert>";
    }

    private async Task SendAll()
    {
        Sent = true;
        foreach (var item in Queue.Selected.ToList())
        {
            var channels = Model.ChannelMap.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
            var target = new DispatchTarget(Model.IsPublic, null, Model.Groups);
            var req = new DispatchRequest(
                JobId: Guid.NewGuid(),
                RequestedBy: "operator@eoc",
                Item: item,
                Channels: channels,
                Target: target,
                RequireAck: Model.RequireAck,
                ScheduledFor: null,
                FollowUpIn: Model.FollowUpMinutes > 0 ? TimeSpan.FromMinutes(Model.FollowUpMinutes) : null,
                TemplateId: null
            );
            var job = await Dispatch.QueueAndSendAsync(req);
            Jobs[job.JobId] = job;
        }

        StateHasChanged();
    }

    private sealed class SelectionModel
    {
        public bool IsPublic { get; set; } = false;
        public bool RequireAck { get; set; } = true;
        public int FollowUpMinutes { get; set; } = 10;

        public string GroupsCsv { get; set; } = "Hospitals – Region 4, EOC Ops";

        public IReadOnlyList<string> Groups => (GroupsCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        public Dictionary<Channel, bool> ChannelMap { get; } =
            Enum.GetValues<Channel>().ToDictionary(c => c, c => false);
    }

    private sealed class ComposeModel
    {
        public string ShortText { get; set; } =
            "Immediate severe weather threat. Shelter-in-place. Check email for details.";

        public string LongText { get; set; } =
            "A severe weather event is affecting parts of the county. Hospitals: activate severe weather protocol stage 1. Secure entrances, confirm generator fuel levels, and prepare surge triage.";
    }

    private sealed class ApproveModel
    {
        public string? Approver1 { get; set; }
        public string? Approver2 { get; set; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Approver1) && !string.IsNullOrWhiteSpace(Approver2) &&
                               !string.Equals(Approver1, Approver2, StringComparison.OrdinalIgnoreCase);
    }
}