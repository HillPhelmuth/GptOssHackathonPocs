using System.Xml.Linq;
using GptOssHackathonPocs.Core.Models;
using Microsoft.Extensions.Logging;

namespace GptOssHackathonPocs.Core.Services;

public class CapFilePlanPublisher(ILogger<CapFilePlanPublisher> logger) : IPlanPublisher
{
    public Task PublishAsync(ActionItem plan, CancellationToken ct = default)
    {
        var cap = BuildCap(plan);
        var capDir = Path.Combine(AppContext.BaseDirectory, "cap");
        Directory.CreateDirectory(capDir);
        var path = Path.Combine(capDir, $"{plan.IncidentId}.xml");
        cap.Save(path);
        logger.LogInformation("Wrote CAP 1.2 file to {Path}", path);
        return Task.CompletedTask;
    }

    private static XDocument BuildCap(ActionItem plan)
    {
        XNamespace cap = "urn:oasis:names:tc:emergency:cap:1.2";
        var now = DateTimeOffset.UtcNow;
        
        var info = new XElement(cap + "info",
            new XElement(cap + "category", "Safety"),
            new XElement(cap + "event", plan.Title),
            new XElement(cap + "urgency", plan.UrgencyLevel),
            new XElement(cap + "severity", plan.SeverityLevel),
            new XElement(cap + "headline", plan.Title),
            new XElement(cap + "description", plan.Rationale),
            new XElement(cap + "instruction", plan.Instructions)
        );

        var alert = new XElement(cap + "alert",
            new XElement(cap + "identifier", plan.IncidentId),
            new XElement(cap + "sender", "actionplans@example.local"),
            new XElement(cap + "sent", now.ToString("o")),
            new XElement(cap + "status", "Actual"),
            new XElement(cap + "msgType", "Alert"),
            new XElement(cap + "scope", "Public"),
            info
        );

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), alert);
    }
}
