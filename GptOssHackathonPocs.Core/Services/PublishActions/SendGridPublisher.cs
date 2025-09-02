using GptOssHackathonPocs.Core.Models.StructuredOutput;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Text;
using GptOssHackathonPocs.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GptOssHackathonPocs.Core.Services.PublishActions;

internal class SendGridPublisher
{
}
public sealed class SendGridPublisherOptions
{
    public string ApiKey { get; set; } = default!;
    public string FromEmail { get; set; } = default!;
    public string FromName { get; set; } = "Emergency Triage Copilot";
    public string? ReplyToEmail { get; set; }
    public string? ReplyToName { get; set; }
    public string? EventWebhookTag { get; set; } = "triage_copilot";
}

public sealed class SendGridEmailPlanPublisher : IPlanPublisher
{
    private readonly SendGridClient _sg;
    private readonly IRecipientDirectory _recipients;
    private readonly SendGridPublisherOptions _opts;
    private readonly ILogger<SendGridEmailPlanPublisher> _log;

    public string Channel => "EMAIL";

    public SendGridEmailPlanPublisher(
        IOptions<SendGridPublisherOptions> opts,
        IRecipientDirectory recipients,
        ILogger<SendGridEmailPlanPublisher> log)
    {
        _opts = opts.Value;
        _sg = new SendGridClient(_opts.ApiKey);
        _recipients = recipients;
        _log = log;
    }

    public async Task PublishAsync(ActionItem item, CancellationToken ct = default)
    {
        if (item.RequiredTools.All(t => t != AvailableTools.Email))
            return;

        var targets = await _recipients.ResolveAsync(item, ct);
        if (targets.Count == 0) { _log.LogWarning("No Email targets for incident {Incident}", item.IncidentId); return; }

        var from = new EmailAddress(_opts.FromEmail, _opts.FromName);
        var subject = $"[{item.Priority.ToUpperInvariant()}] {item.Title}";
        var (html, text) = RenderEmail(item);

        foreach (var t in targets)
        {
            if (string.IsNullOrWhiteSpace(t.Email)) continue;

            var to = new EmailAddress(t.Email, t.Name);
            var msg = MailHelper.CreateSingleEmail(from, to, subject, text, html);

            if (!string.IsNullOrWhiteSpace(_opts.ReplyToEmail))
                msg.ReplyTo = new EmailAddress(_opts.ReplyToEmail, _opts.ReplyToName);

            // Correlate webhook events → Action Queue status
            msg.AddCustomArg("incident_id", item.IncidentId);
            msg.AddCustomArg("action_title", item.Title);
            if (!string.IsNullOrWhiteSpace(_opts.EventWebhookTag))
                msg.AddCategory(_opts.EventWebhookTag);

            var resp = await _sg.SendEmailAsync(msg, ct);
            _log.LogInformation("SendGrid {Status} → {Email} for incident {Incident}",
                resp.StatusCode, t.Email, item.IncidentId);
        }
    }

    private static (string Html, string Text) RenderEmail(ActionItem a)
    {
        var sb = new StringBuilder();
        sb.Append($"<h2>{System.Net.WebUtility.HtmlEncode(a.Title)}</h2>");
        sb.Append($"<p><strong>Priority:</strong> {a.Priority} | <strong>Audience:</strong> {a.Audience}</p>");
        sb.Append($"<p><strong>Rationale:</strong> {System.Net.WebUtility.HtmlEncode(a.Rationale)}</p>");
        sb.Append($"<h3>Instructions</h3><p>{System.Net.WebUtility.HtmlEncode(a.Instructions).Replace("\n", "<br/>")}</p>");

        var text = $"Title: {a.Title}\nPriority: {a.Priority}\nAudience: {a.Audience}\n\nRationale:\n{a.Rationale}\n\nInstructions:\n{a.Instructions}";
        return (sb.ToString(), text);
    }
}
