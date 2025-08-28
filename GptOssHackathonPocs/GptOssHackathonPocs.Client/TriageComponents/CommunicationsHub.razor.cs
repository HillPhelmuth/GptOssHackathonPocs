using Microsoft.AspNetCore.Components;

namespace GptOssHackathonPocs.Client.TriageComponents;

public partial class CommunicationsHub
{
    public sealed record Channel(string Name, string Description);

    [Parameter]
    public Channel[] Channels { get; set; } = new[]
    {
        new Channel("Slack", "EOC Team Channel"),
        new Channel("Microsoft Teams", "Emergency Response"),
        new Channel("SMS Alert", "First Responders"),
        new Channel("Email", "Stakeholder Update"),
        new Channel("Radio Dispatch", "Field Operations"),
        new Channel("Public Alert", "WEA System"),
    };

    [Parameter]
    public string[] QuickMessages { get; set; } = new[]
    {
        "Incident confirmed - initial response teams dispatched",
        "Request additional resources for ongoing situation",
        "Evacuation orders issued for affected zones",
        "Infrastructure assessment teams deployed",
        "Public alert has been issued through WEA system",
        "Situation stabilized - transitioning to recovery operations"
    };

    [Parameter] public EventCallback<Channel> OnChannelClicked { get; set; }
    [Parameter] public EventCallback<string> OnSendQuickMessage { get; set; }
}