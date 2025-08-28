using System.Text.Json;
using GptOssHackathonPocs.Core;
using GptOssHackathonPocs.Core.Models;
using GptOssHackathonPocs.Core.Models.Enrichment;
using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;


namespace GptOssHackathonPocs.Client.Pages;

public partial class Copilot
{
    private IReadOnlyList<Incident>? _incidents;
    [Inject] 
    private AiAgentOrchestration Orchestrator { get; set; } = default!;
    [Inject]
    private IncidentCardBuilder CardBuilder { get; set; } = default!;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            //await JS.InvokeVoidAsync("triageMap.init", "map");
            //await Task.Delay(500);
            //await Refresh();
        }
    }

    private async Task Refresh()
    {
        _incidents = await Aggregator.FetchAllAsync();
        var geo = _incidents.Where(x => !string.IsNullOrWhiteSpace(x.GeoJson)).Select(x => x.GeoJson!).ToArray();
        await JS.InvokeVoidAsync("triageMap.setIncidents", (object)geo);
        StateHasChanged();
    }

    private string SuggestAction(Incident i) => i.Source switch
    {
        IncidentSource.NwsAlert => i.Severity switch
        {
            IncidentSeverity.Extreme or IncidentSeverity.Severe
                => "Push localized alert; verify polygon coverage against critical facilities.",
            IncidentSeverity.Moderate
                => "Advise readiness; monitor updates.",
            _ => "Monitor."
        },
        IncidentSource.UsgsQuake => i.Severity switch
        {
            IncidentSeverity.Severe or IncidentSeverity.Extreme
                => "Check hospital status; trigger structural safety checks in affected grid.",
            IncidentSeverity.Moderate
                => "Survey damage reports; prep shelters.",
            _ => "Monitor USGS updates."
        },
        IncidentSource.NhcStorm => "Check watch/warning areas; pre-stage resources along forecast track.",
        _ => "Monitor."
    };

    private string _actionPlanMarkdown;
    private CancellationTokenSource _cts = new();
    private async Task GenerateActionPlan()
    {
        if (_incidents is null) return;
        var cardTasks = _incidents.Select(incident => CardBuilder.Build(incident)).ToList();
        var cards = (await Task.WhenAll(cardTasks)).ToList();
        var token = _cts.Token;
        Console.WriteLine($"Incident Cards: \n\n{JsonSerializer.Serialize(cards, new JsonSerializerOptions(){WriteIndented = true})}");
        var actionPlan = await Orchestrator.PlanAsync(cards, token);
        _actionPlanMarkdown = actionPlan is null ? "### Error\n\nCould not generate action plan." : actionPlan.ToMarkdown();
    }
    private void CancelGeneration()
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
    }

    private static string MarkdownAsHtml(string markdownString)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var result = Markdown.ToHtml(markdownString, pipeline);
        return result;
    }
}