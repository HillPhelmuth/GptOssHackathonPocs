using System.Net.Http.Json;
using GptOssHackathonPocs.Core.Models;
using Microsoft.Extensions.Logging;

namespace GptOssHackathonPocs.Core.Services.PublishActions;

public class SlackPlanPublisher(ILogger<SlackPlanPublisher> logger, IHttpClientFactory http) : IPlanPublisher
{
    public async Task PublishAsync(ActionItem plan, CancellationToken ct = default)
    {
        var url = Environment.GetEnvironmentVariable("SLACK_WEBHOOK_URL");
        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogInformation("SLACK_WEBHOOK_URL not set; Slack publisher is a no-op.");
            return;
        }

        var text = $"*{plan.Title}*\n{plan.Rationale}\n";

        using var client = http.CreateClient("slack");
        var payload = new { text };
        var resp = await client.PostAsJsonAsync(url, payload, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Slack webhook returned {Status}: {Body}", resp.StatusCode, body);
        }
    }
}
