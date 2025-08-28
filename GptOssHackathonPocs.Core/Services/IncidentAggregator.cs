using GptOssHackathonPocs.Core.Models;

namespace GptOssHackathonPocs.Core.Services;

public sealed class IncidentAggregator(IEnumerable<IIncidentFeed> feeds)
{
    public async Task<IReadOnlyList<Incident>> FetchAllAsync(CancellationToken ct = default)
    {
        var tasks = feeds.Select(f => f.FetchAsync(ct));
        var results = await Task.WhenAll(tasks);
        var all = results.SelectMany(x => x);

        return all.Where(x => x.Severity is IncidentSeverity.Severe or IncidentSeverity.Extreme)
            .OrderByDescending(i => i.Severity)
            .ThenByDescending(i => i.Timestamp)
            .ToList();
    }
}
