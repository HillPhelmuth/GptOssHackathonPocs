using GptOssHackathonPocs.Core.Models;
using Microsoft.Extensions.Logging;

namespace GptOssHackathonPocs.Core.Services;

public class CompositePlanPublisher(ILogger<CompositePlanPublisher> logger, IEnumerable<IPlanPublisher> publishers) : IActionPlanPublisher
{
    public async Task PublishAsync(ActionItem plan, CancellationToken ct = default)
    {
        foreach (var p in publishers)
        {
            try
            {
                await p.PublishAsync(plan, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Plan publisher {Publisher} failed", p.GetType().Name);
            }
        }
    }
}
