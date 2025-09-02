using GptOssHackathonPocs.Core.Models;
using GptOssHackathonPocs.Core.Models.Publishing;
using GptOssHackathonPocs.Core.Models.StructuredOutput;

namespace GptOssHackathonPocs.Core.Services.PublishActions;

/// <summary>
/// Simple demo implementation. Replace with your directory/roster logic.
/// </summary>
public sealed class InMemoryRecipientDirectory : IRecipientDirectory
{
    private static readonly Dictionary<string, List<Recipient>> _store = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Hospitals – Region 4"] =
        [
            new Recipient("Methodist Hospital-North", "+15551230001", "ops@methodist-north.example", null),
            new Recipient("County General ER", "+15551230002", "er@countygeneral.example", null)
        ],
        ["EOC Ops"] =
        [
            new Recipient("Duty Officer", "+15551239999", "duty.officer@example", null)
        ]
    };

    public Task<IReadOnlyList<Recipient>> ResolveAsync(IReadOnlyList<string> groups, string? geoJson, CancellationToken ct = default)
    {
        var results = new List<Recipient>();
        foreach (var g in groups)
            if (_store.TryGetValue(g, out var list))
                results.AddRange(list);
        // In real impl: apply geofence to filter list.
        return Task.FromResult<IReadOnlyList<Recipient>>(results);
    }

    public Task<IReadOnlyList<Contact>> ResolveAsync(ActionItem item, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}