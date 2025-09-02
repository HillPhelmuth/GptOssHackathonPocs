using GptOssHackathonPocs.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GptOssHackathonPocs.Core.Models.StructuredOutput;
using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace GptOssHackathonPocs.Core.Services.PublishActions;

internal class TwilioPublisher
{
}
public sealed class TwilioPublisherOptions
{
    public string FromSms { get; set; } = default!;          // e.g., "+15551234567"
    public string? FromVoice { get; set; }                   // optional; if null use FromSms
    public string? StatusCallbackUrl { get; set; }           // where Twilio POSTs status events
    public int VoiceEscalationMinScore { get; set; } = 9;    // severity or urgency threshold
}

//public interface IRecipientDirectory
//{
//    Task<IReadOnlyList<Contact>> ResolveAsync(ActionItem item, CancellationToken ct = default);
//}
public sealed record Contact(string Name, string? Phone, string? Email);

public interface IPlanPublisher
{
    Task PublishAsync(ActionItem item, CancellationToken ct = default);
    string Channel { get; }
}

/// <summary>
/// Sends SMS for ActionItems that request "SMS alerts". Optionally escalates to a voice call
/// when severity/urgency exceed configured thresholds.
/// </summary>
public sealed class TwilioSmsAndVoicePlanPublisher : IPlanPublisher
{
    private readonly ITwilioRestClient _twilio;
    private readonly IRecipientDirectory _recipients;
    private readonly TwilioPublisherOptions _opts;
    private readonly ILogger<TwilioSmsAndVoicePlanPublisher> _log;

    public string Channel => "SMS/VOICE";

    public TwilioSmsAndVoicePlanPublisher(
        ITwilioRestClient twilio,
        IRecipientDirectory recipients,
        IOptions<TwilioPublisherOptions> opts,
        ILogger<TwilioSmsAndVoicePlanPublisher> log)
    {
        _twilio = twilio;
        _recipients = recipients;
        _opts = opts.Value;
        _log = log;
    }

    public async Task PublishAsync(ActionItem item, CancellationToken ct = default)
    {
        // Only act if the LLM requested this channel.
        if (!item.RequiredTools.Any(t => t is AvailableTools.SmsText or AvailableTools.SmsVoice))
            return;

        var targets = await _recipients.ResolveAsync(item, ct);
        if (targets.Count == 0) { _log.LogWarning("No SMS targets for incident {Incident}", item.IncidentId); return; }

        var body = BuildSmsBody(item);

        foreach (var t in targets)
        {
            if (string.IsNullOrWhiteSpace(t.Phone)) continue;

            var msg = await MessageResource.CreateAsync(
                to: new PhoneNumber(t.Phone),
                from: new PhoneNumber(_opts.FromSms),
                body: body,
                statusCallback: _opts.StatusCallbackUrl is null ? null : new Uri(_opts.StatusCallbackUrl),
                client: _twilio
                );

            _log.LogInformation("Twilio SMS queued {Sid} → {Phone} for incident {Incident}",
                msg.Sid, t.Phone, item.IncidentId);
            // Persist msg.Sid to correlate status callbacks → delivery telemetry
        }

        // Optional escalation to voice for high-severity/urgency ops/ems items.
        var high = Math.Max(item.SeverityLevel, item.UrgencyLevel) >= _opts.VoiceEscalationMinScore;
        if (high && item.Audience is "ops" or "ems")
        {
            var twiml = new Twiml($"<Response><Say>{EscapeForSay(BuildVoiceScript(item))}</Say></Response>");
            foreach (var t in targets)
            {
                if (string.IsNullOrWhiteSpace(t.Phone)) continue;

                var call = await CallResource.CreateAsync(
                    to: new PhoneNumber(t.Phone),
                    from: new PhoneNumber(_opts.FromVoice ?? _opts.FromSms),
                    twiml: twiml,
                    statusCallback: _opts.StatusCallbackUrl is null ? null : new Uri(_opts.StatusCallbackUrl),
                    client: _twilio);

                _log.LogInformation("Twilio Voice call queued {Sid} → {Phone} for incident {Incident}",
                    call.Sid, t.Phone, item.IncidentId);
            }
        }
    }

    private static string BuildSmsBody(ActionItem a)
    {
        // keep SMS concise; include title + strongest imperative
        var head = $"[{a.Priority.ToUpperInvariant()}] {a.Title}".Trim();
        var instr = a.Instructions?.Split('\n').FirstOrDefault()?.Trim() ?? a.Description;
        var body = $"{head} – {instr}";
        if (body.Length > 320) body = body[..320] + "…";
        return body;
    }

    private static string BuildVoiceScript(ActionItem a)
    {
        var sb = new StringBuilder();
        sb.Append($"{a.Title}. Priority {a.Priority}. ");
        sb.Append($"Rationale: {a.Rationale}. ");
        sb.Append("Instructions: ");
        // Read the first 500 chars of instructions
        var instr = a.Instructions?.Trim() ?? a.Description;
        sb.Append(instr.Length <= 500 ? instr : instr[..500] + "...");
        return sb.ToString();
    }

    private static string EscapeForSay(string text)
        => System.Security.SecurityElement.Escape(text) ?? text;
}