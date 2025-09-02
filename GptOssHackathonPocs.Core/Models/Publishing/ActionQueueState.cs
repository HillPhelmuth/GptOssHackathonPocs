using System.Collections.ObjectModel;
using GptOssHackathonPocs.Core.Models.StructuredOutput;

namespace GptOssHackathonPocs.Core.Models.Publishing;

/// <summary>
/// Holds the model-generated ActionItems and tracks selected items for dispatch.
/// </summary>
public sealed class ActionQueueState
{
    public ObservableCollection<ActionItem> Suggested { get; } = [];
    public ObservableCollection<ActionItem> Selected { get; } = [];

    public void SetSuggested(IEnumerable<ActionItem> actions)
    {
        Suggested.Clear();
        foreach (var a in actions) Suggested.Add(a);
    }

    public void ToggleSelection(ActionItem item)
    {
        // CA1868: Remove the Contains check, Remove will do nothing if item is not present
        if (!Selected.Remove(item))
        {
            Selected.Add(item);
        }
       
    }
}
