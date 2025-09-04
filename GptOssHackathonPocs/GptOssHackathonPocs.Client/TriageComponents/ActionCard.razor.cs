using GptOssHackathonPocs.Core.Models;
using GptOssHackathonPocs.Core.Models.StructuredOutput;
using Microsoft.AspNetCore.Components;

namespace GptOssHackathonPocs.Client.TriageComponents;

public partial class ActionCard
{
    [Parameter, EditorRequired] public ActionItem Item { get; set; } = default!;
    [Parameter] public bool Selected { get; set; }
    [Parameter] public EventCallback OnToggle { get; set; }
    private Task Toggle() => OnToggle.InvokeAsync();
}