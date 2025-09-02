using System.Runtime.CompilerServices;
using System.Text.Json;
using GptOssHackathonPocs.Core.Models;
using GptOssHackathonPocs.Core.Models.Enrichment;
using GptOssHackathonPocs.Core.Models.StructuredOutput;
using GptOssHackathonPocs.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace GptOssHackathonPocs.Core;

public class AiAgentOrchestration(ILoggerFactory loggerFactory, IConfiguration configuration)
{
    private const string SystemPrompt = """
                                        You are the triage planner for an Emergency Operations Center.
                                        Rules:
                                        - Use ONLY provided incident cards and evidence links. Do NOT invent geometry or sources.
                                        - Think carefully about each incident and how it should inform your corresponding action.
                                        - Return a JSON ActionPlan that passes the provided JSON Schema.
                                        - There must be exactly one action per incident card.
                                        - If critical data is missing, include a low-priority action titled "Request-Info" with parameters describing the gap.
                                        - Keep actions atomic and executable in <30 minutes.
                                        - Prefer high-need areas (SVI ≥ 0.8) when priorities tie.
                                        """;
    private const string UserPrompt = """
                                      Given the following incident cards, create an ActionPlan to triage and respond to the incidents.
                                      Follow the rules provided.
                                      Incident Cards:
                                      {{ $cards }}
                                     
                                      """;
    private readonly ILogger<AiAgentOrchestration> _logger = loggerFactory.CreateLogger<AiAgentOrchestration>();
    private Kernel CreateKernel()
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddLogging(b =>
        {
            b.AddConsole();
        });
        var endpoint = new Uri("https://openrouter.ai/api/v1");
        var client = OpenRouterReasoningHandler.CreateOpenRouterResilientClient(loggerFactory);
        var model = "openai/gpt-oss-120b";
        var apiKey = configuration["OpenRouter:ApiKey"];
        builder.AddOpenAIChatCompletion(modelId: model, apiKey: apiKey, endpoint: endpoint, httpClient: client);
        var kernel = builder.Build();
        return kernel;
    }

    private int tries = 0;
    public async IAsyncEnumerable<ActionItem?> PlanAsync(IEnumerable<IncidentCard> cards, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var settings = new OpenAIPromptExecutionSettings()
        { ReasoningEffort = "high", ResponseFormat = typeof(ActionPlan), ChatSystemPrompt = SystemPrompt };
        var kernelArgs = new KernelArguments(settings) { ["cards"] = string.Join("\n---\n", cards.Select(x => x.ToMarkdown())) };
        var kernel = CreateKernel();
        ActionPlan? actionPlan = null;
        try
        {
            actionPlan = await GenerateActionPlanAsync();
        }
        catch
        {
            tries++;
            _logger.LogWarning("Failed to generate ActionPlan on try {Try}", tries);
            if (tries < 3)
                actionPlan = await GenerateActionPlanAsync();
        }
        if (actionPlan is null || !actionPlan.Actions.Any())
        {
            _logger.LogError("Failed to deserialize ActionPlan from AI response");
            yield break;
        }
        foreach (var action in actionPlan.Actions)
        {
            yield return action;
        }

        //try
        //{
        //foreach (var card in cards)
        //{
        //    var actionItem = await GenerateActionItemAsync(card, ct);
        //    if (actionItem is null) continue;
        //    yield return actionItem;
        //}
        //var tasks = cards.Select(card => GenerateActionItemAsync(card, ct)).ToList();
        //actionPlan.Actions = (await Task.WhenAll(tasks)).Where(x => x is not null).ToList()!;
        //return actionPlan;
        //}
        //catch
        //{
        //    _logger.LogError("Failed to deserialize ActionPlan from AI response: {Response}", actionPlan);
        //    return null;
        //}
        async Task<ActionPlan?> GenerateActionPlanAsync()
        {
            var result = await kernel.InvokePromptAsync<string>(UserPrompt, kernelArgs, cancellationToken: ct);
            var actionPlan1 = JsonSerializer.Deserialize<ActionPlan>(result);
            return actionPlan1;
        }
    }
    public async Task<ActionItem?> GenerateActionItemAsync(IncidentCard card, CancellationToken ct = default)
    {
        var settings = new OpenAIPromptExecutionSettings()
        {
            ReasoningEffort = "high",
            ResponseFormat = typeof(ActionItem),
            ChatSystemPrompt =
                """
                  You are an expert emergency response coordinator. Provided with incident data, think carefully about the data and how it should inform your action.
                  Create a single, complete and well considered ActionItem based on the incident.
                  Follow the JSON schema strictly.
                """
        };
        var kernelArgs = new KernelArguments(settings) { ["cards"] = card.ToMarkdown() };
        var kernel = CreateKernel();
        var result = await kernel.InvokePromptAsync<string>(UserPrompt, kernelArgs, cancellationToken: ct);
        try
        {
            return JsonSerializer.Deserialize<ActionItem>(result);
        }
        catch
        {
            _logger.LogError("Failed to deserialize ActionItem from AI response: {Response}", result);
            return null;
        }
    }
}