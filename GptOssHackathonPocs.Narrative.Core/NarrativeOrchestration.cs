using GptOssHackathonPocs.Narrative.Core.Models;
using GptOssHackathonPocs.Narrative.Core.Plugins;
using GptOssHackathonPocs.Narrative.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents.Magentic;
using Microsoft.SemanticKernel.Agents.Orchestration;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable SKEXP0001

#pragma warning disable SKEXP0110

namespace GptOssHackathonPocs.Narrative.Core;

public class NarrativeOrchestration
{
    public event Action<string, string>? WriteAgentChatMessage;
    private ChatHistory _history = [];
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;
    private readonly WorldState _worldState;
    private readonly AutoInvocationFilter _autoInvocationFilter = new();
    private readonly ILogger<NarrativeOrchestration> _logger;
    public NarrativeOrchestration(ILoggerFactory loggerFactory, IConfiguration configuration, WorldState worldState)
    {
        _loggerFactory = loggerFactory;
        _configuration = configuration;
        _worldState = worldState;
        _autoInvocationFilter.OnAfterInvocation += HandleAutoInvocationFilterAfterInvocation;
        _logger = loggerFactory.CreateLogger<NarrativeOrchestration>();
    }

    private void HandleAutoInvocationFilterAfterInvocation(AutoFunctionInvocationContext invocationContext)
    {

    }

    private Kernel CreateKernel(string model = "openai/gpt-oss-120b")
    {
        var builder = CreateKernelBuilder(model);
        builder.Services.AddSingleton(_worldState);
        var kernel = builder.Build();
        kernel.AutoFunctionInvocationFilters.Add(_autoInvocationFilter);
        return kernel;
    }

    private IKernelBuilder CreateKernelBuilder(string model = "openai/gpt-oss-120b")
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddLogging(b =>
        {
            b.AddConsole();
        });

        var endpoint = new Uri("https://openrouter.ai/api/v1");
        var client = OpenRouterReasoningHandler.CreateOpenRouterResilientClient(_loggerFactory);
        var apiKey = _configuration["OpenRouter:ApiKey"];
        builder.AddOpenAIChatCompletion(modelId: model, apiKey: apiKey, endpoint: endpoint, httpClient: client);
        return builder;
    }


    private static OpenAIPromptExecutionSettings CreatePromptExecutionSettings(Kernel kernelClone)
    {
        var worldAgentPlugin = kernelClone.ImportPluginFromType<WorldAgentsPlugin>();

        var settings = new OpenAIPromptExecutionSettings()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Required(worldAgentPlugin.Where(x =>
            x.Name is nameof(WorldAgentsPlugin.TakeAction))),
            ReasoningEffort = "high"
        };

        return settings;
    }


    public async Task RunNarrativeAsync(string userInput, CancellationToken ct = default)
    {

        _history.AddUserMessage(userInput);
        var errors = 0;
        while (true)
        {
            try
            {
                if (_history.Count > 5)
                {
                    _history.RemoveAt(0);
                }
                if (ct.IsCancellationRequested) break;
                var nextAgent = await SelectAgentAsync(_worldState.WorldAgents.Agents, _history, ct);
                _worldState.ActiveWorldAgent = nextAgent;
                var prompt = nextAgent.GetSystemPrompt(_worldState.WorldStateMarkdown());
                //var history = new ChatHistory(prompt);
                //history.AddRange(_history);
                var kernel = CreateKernel();
                var settings = CreatePromptExecutionSettings(kernel);
                Console.WriteLine($"ExecutionSettings: \n===================\n{JsonSerializer.Serialize(settings)}\n===================\n");
                var args = new KernelArguments(settings);
                //var chat = kernel.GetRequiredService<IChatCompletionService>();
                var response = await kernel.InvokePromptAsync<string>(prompt, args, cancellationToken: ct);
                _history.AddAssistantMessage(response.ToString() ?? "");
                var lastAction = _worldState.RecentActions.Last().ToTypeMarkdown().Item2;

                var updateDynamicStatePrompt = nextAgent.UpdateDynamicStatePrompt(_worldState.WorldStateMarkdown(), lastAction);
                var updateMemoryPrompt = nextAgent.UpdateKnowledgeMemoryPrompt(_worldState.WorldStateMarkdown(), lastAction);
                await UpdateAgentStates(updateDynamicStatePrompt, updateMemoryPrompt, ct);
                WriteLine($"{response}");
            }
            catch (Exception ex)
            {
                errors++;
                WriteLine($"Oh, shit! ERROR:\n\n{ex.Message}");
                if (errors > 10) break;

            }
        }


    }

    private async Task UpdateAgentStates(string updateDynamicStatePrompt, string updateMemoryPrompt, CancellationToken ct)
    {
        var smallKernel = CreateKernel("openai/gpt-oss-20b");
        var updateStateSettings = new OpenAIPromptExecutionSettings() { ReasoningEffort = "medium", ResponseFormat = typeof(UpdateAgentStateRequest) };
        var updateArgs = new KernelArguments(updateStateSettings);
        try
        {
            var agentStateUpdateResponse =
                await smallKernel.InvokePromptAsync<string>(updateDynamicStatePrompt, updateArgs,
                    cancellationToken: ct);
            var agentStateUpdate = JsonSerializer.Deserialize<UpdateAgentStateRequest>(agentStateUpdateResponse ?? "");
            var updateLog = UpdateAgentState(agentStateUpdate.Description, agentStateUpdate.UpdatedDynamicState);
            _logger.LogInformation("Agent State Update: {UpdateLog}", updateLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating agent state");
        }
        var memorySettings = new OpenAIPromptExecutionSettings() { ReasoningEffort = "medium", ResponseFormat = typeof(UpdateAgentMemoryRequest) };
        var memArgs = new KernelArguments(memorySettings);
        try
        {
            var memoryUpdateResponse =
                await smallKernel.InvokePromptAsync<string>(updateMemoryPrompt, memArgs, cancellationToken: ct);
            var agentMemoryUpdate = JsonSerializer.Deserialize<UpdateAgentMemoryRequest>(memoryUpdateResponse ?? "");
            var memLog = UpdateAgentMemory(agentMemoryUpdate.Description, agentMemoryUpdate.UpdatedKnowledgeMemory);
            _logger.LogInformation("Agent Memory Update: {MemLog}", memLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating agent memory");
        }
        
    }

    private string _previewStateDescription = "";
    public async Task<string> SummarizeCurrentWorldState()
    {
        var currentDetails = _worldState.WorldStateMarkdown();
        var prompt =
            """
            Create a detailed overview of the world's events, rumors and actions as it currently exists. Below you'll find current state of the world, and the previous state since the last update. Be sure to note the differences in your overview.

            ## Previous World State
            
            {{ $previousState }}
            
            ## Current World State

            {{ $currentState }}
            """;
        var kernel = CreateKernel();
        var settings = new OpenAIPromptExecutionSettings() { ReasoningEffort = "high" };
        var args = new KernelArguments(settings)
        {
            ["previousState"] = _previewStateDescription,
            ["currentState"] = currentDetails
        };
        _previewStateDescription = currentDetails;
        var response = await kernel.InvokePromptAsync<string>(prompt, args);
        return response;
    }
    private void WriteLine(string text)
    {
        Console.WriteLine(text);
        WriteAgentChatMessage?.Invoke(text, _worldState.ActiveWorldAgent.AgentId);
    }

    private const string NextAgentPromptTemplate = """
	                                               You are in a role play game. Carefully read the conversation history as select the next participant using the provided schema.
	                                               The available participants are:
	                                               - {{$speakerList}}

	                                               ### Conversation history

	                                               - {{$conversationHistory}}

	                                               Select the next participant name. If the information is not sufficient, select the name of any interesting participant from the list.
	                                               """;

    private async Task<WorldAgent> SelectAgentAsync(List<WorldAgent> agents, ChatHistory history, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("SelectAgentAsync");
        var settings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = typeof(NextAgent)
        };
        history = new ChatHistory(history.TakeLast(4).ToList());
        var kernelArgs = UpdateKernelArguments(history, agents, settings);
        var promptFactory = new KernelPromptTemplateFactory();
        var templateConfig = new PromptTemplateConfig(NextAgentPromptTemplate);
        _worldState.ActiveWorldAgent ??= _worldState.WorldAgents.Agents.First();
        var currentAgent = _worldState.ActiveWorldAgent;
        var kernel = CreateKernel();
        var prompt = await promptFactory.Create(templateConfig).RenderAsync(kernel!, kernelArgs, cancellationToken);
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);
        try
        {
            var nextAgentName = await chat.GetChatMessageContentAsync(chatHistory, settings, cancellationToken: cancellationToken);
            var agent = JsonSerializer.Deserialize<NextAgent>(nextAgentName.ToString());
            var name = agent?.Name ?? "";
            Console.WriteLine("AutoSelectNextAgent: " + name);
            var nextAgent = _worldState.WorldAgents.Agents.FirstOrDefault(interactive => interactive.AgentId.Equals(name, StringComparison.InvariantCultureIgnoreCase)) ?? currentAgent;
            Console.WriteLine($"Selected Next Agent: {nextAgent?.AgentId}");
            return nextAgent;
        }
        catch (TaskCanceledException exception)
        {
            Console.WriteLine(exception);
            return currentAgent;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            throw;
        }
    }

    private class NextAgent
    {
        [Description("The **Name** of the participant")]
        public required string Name { get; set; }
    }
    private static KernelArguments UpdateKernelArguments(IReadOnlyList<ChatMessageContent> history, IReadOnlyList<WorldAgent> agents, OpenAIPromptExecutionSettings settings)
    {
        var groupConvoHistory = string.Join("\n ", history?.Select(message => $"From: \n{message?.AuthorName}\n### Message\n {message?.Content}\n") ?? []);
        var kernelArgs = new KernelArguments(settings)
        {
            ["speakerList"] = string.Join("\n ", agents.Select(a => $"**Name:** {a?.AgentId}\n")),
            ["conversationHistory"] = groupConvoHistory
        };
        return kernelArgs;
    }
    public string UpdateAgentMemory(
        [Description("Description of the update")] string description,
        [Description("The updated agent knowledge memory and relationships")] KnowledgeMemory updatedKnowledgeMemory)
    {
        try
        {
            var agent = _worldState.ActiveWorldAgent;
            if (agent == null)
            {
                return $"No WorldState.{nameof(WorldState.ActiveWorldAgent)} found.";
            }

            var agentId = agent.AgentId;

            agent.KnowledgeMemory = updatedKnowledgeMemory;

            agent.AddNotes(description);
            _worldState.UpdateAgent(agent);
            return $"Agent '{agentId}' knowledge memory and relationships updated successfully.";
        }
        catch (Exception ex)
        {
            return $"Error updating agent memory: {ex.Message}";
        }
    }
    public string UpdateAgentState(
        [Description("Description of the update")] string description,
        [Description("The updated agent dynamic state")] DynamicState updatedDynamicState)
    {
        try
        {
            var agent = _worldState.ActiveWorldAgent;
            if (agent == null)
            {
                return $"No WorldState.{nameof(WorldState.ActiveWorldAgent)} found.";
            }

            var agentId = agent.AgentId;

            agent.DynamicState = updatedDynamicState;

            agent.AddNotes(description);
            _worldState.UpdateAgent(agent);
            return $"Agent '{agentId}' state updated successfully.";
        }
        catch (Exception ex)
        {
            return $"Error updating agent state: {ex.Message}";
        }
    }
}

