using GptOssHackathonPocs.Narrative.Core.Models;
using GptOssHackathonPocs.Narrative.Core.Plugins;
using GptOssHackathonPocs.Narrative.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.ComponentModel;
using System.Text.Json;

#pragma warning disable SKEXP0001

#pragma warning disable SKEXP0110

namespace GptOssHackathonPocs.Narrative.Core;

public interface INarrativeOrchestration
{
    event Action<string, string>? WriteAgentChatMessage;
    event Action<string, string>? OnAgentFunctionCompleted;
    Task RunNarrativeAsync(string userInput, CancellationToken ct = default);
    Task<T?> ExecuteLlmPrompt<T>(string inputPrompt, string model = "openai/gpt-oss-20b", CancellationToken ct = default);
    Task<string> SummarizeCurrentWorldState();

    Task<string?> ExecuteLlmPrompt(string inputPrompt, string model = "openai/gpt-oss-20b",
        OpenAIPromptExecutionSettings? settings = null, CancellationToken ct = default);

    Task GenerateAgents(string input, int numberOfAgents = 3);
}

public class NarrativeOrchestration : INarrativeOrchestration
{
    public event Action<string, string>? WriteAgentChatMessage;
    private ChatHistory _history = [];
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;
    private readonly WorldState _worldState;
    private readonly AutoInvocationFilter _autoInvocationFilter = new();
    private FunctionInvocationFilter _functionInvocationFilter = new();
    private readonly ILogger<NarrativeOrchestration> _logger;
    public event Action<string, string>? OnAgentFunctionCompleted;
    private Dictionary<string, int> _agentParticipationCount = [];
    public NarrativeOrchestration(ILoggerFactory loggerFactory, IConfiguration configuration, WorldState worldState)
    {
        _loggerFactory = loggerFactory;
        _configuration = configuration;
        _worldState = worldState;
        _autoInvocationFilter.OnAfterInvocation += HandleAutoInvocationFilterAfterInvocation;
        _functionInvocationFilter.OnAfterInvocation += HandleFunctionInvocationFilterAfterInvocation;
        _logger = loggerFactory.CreateLogger<NarrativeOrchestration>();
        _agentParticipationCount = _worldState.WorldAgents.Agents.ToDictionary(a => a.AgentId, _ => 0);
    }

    private void HandleFunctionInvocationFilterAfterInvocation(FunctionInvocationContext functionInvocationContext)
    {
        var activeAgent = _worldState.ActiveWorldAgent?.AgentId;
        var functionInfo =
            $"{functionInvocationContext.Function.Name} invoked for {activeAgent}\n\nResults:\n\n{functionInvocationContext.Result.ToString()}";
        OnAgentFunctionCompleted?.Invoke(functionInfo, activeAgent ?? "Unknown");
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
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(worldAgentPlugin.Where(x =>
            x.Name is nameof(WorldAgentsPlugin.TakeAction))),
            ReasoningEffort = "high"
        };

        return settings;
    }


    public async Task RunNarrativeAsync(string userInput, CancellationToken ct = default)
    {

        //_history.AddUserMessage(userInput);
        var errors = 0;
        while (true)
        {
            try
            {
                if (_history.Count > 15)
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
                WriteLine($"{response}");
                _history.AddAssistantMessage(response.ToString() ?? "");
                var lastAction = _worldState.RecentActions.Last().ToTypeMarkdown().Item2;

                var updateDynamicStatePrompt = nextAgent.UpdateDynamicStatePrompt(_worldState.WorldStateMarkdown(), lastAction);
                var updateMemoryPrompt = nextAgent.UpdateKnowledgeMemoryPrompt(_worldState.WorldStateMarkdown(), lastAction);
                await UpdateAgentStates(updateDynamicStatePrompt, updateMemoryPrompt, ct);
                
            }
            catch (Exception ex)
            {
                errors++;
                WriteLine($"Oh, shit! ERROR:\n\n{ex.Message}");
                if (errors > 10) break;

            }
        }


    }

    public async Task<T?> ExecuteLlmPrompt<T>(string inputPrompt, string model = "openai/gpt-oss-20b", CancellationToken ct = default)
    {
        var executionSettings = new OpenAIPromptExecutionSettings() { ResponseFormat = typeof(T) };
        Console.WriteLine($"Execution settings for generic type prompt:\n\n{typeof(T).Name})");
        var response = await ExecuteLlmPrompt(inputPrompt, model,
            executionSettings, ct);
        var json = StripCodeFences(response);
        return JsonSerializer.Deserialize<T>(json);
    }

    public async Task<string?> ExecuteLlmPrompt(string inputPrompt, string model = "openai/gpt-oss-20b",
        OpenAIPromptExecutionSettings? settings = null, CancellationToken ct = default)
    {
        var kernel = CreateKernel(model);
        return await kernel.InvokePromptAsync<string>(inputPrompt, new KernelArguments(settings), cancellationToken:ct);
    }
    private static string StripCodeFences(string s)
    {
        s = s.Replace("```json", "").Replace("```", "");
        return s.Trim();
    }
    public async Task GenerateAgents(string input, int numberOfAgents = 3)
    {
        var kernel = CreateKernel();
        var prompt = $"Create {numberOfAgents} that fit the following description:\n\n## Description\n\n{input}";
        var settings = new OpenAIPromptExecutionSettings()
            { ResponseFormat = typeof(WorldAgents), ChatSystemPrompt = WorldAgentsPlugin.Prompt, ReasoningEffort = "high" };
        var result = await kernel.InvokePromptAsync<string>(prompt, new KernelArguments(settings));
        _worldState.WorldAgents = JsonSerializer.Deserialize<WorldAgents>(result ?? "") ?? WorldAgents.DefaultFromJson();
    }
    private async Task UpdateAgentStates(string updateDynamicStatePrompt, string updateMemoryPrompt, CancellationToken ct)
    {
        var smallKernel = CreateKernel("openai/gpt-oss-20b");
        smallKernel.FunctionInvocationFilters.Add(_functionInvocationFilter);
        var updateStateSettings = new OpenAIPromptExecutionSettings() { ReasoningEffort = "medium", ResponseFormat = typeof(UpdateAgentStateRequest), Temperature = 0.5};
        var stateFunction = KernelFunctionFactory.CreateFromPrompt(updateDynamicStatePrompt, updateStateSettings, "UpdateAgentState");
        var memorySettings = new OpenAIPromptExecutionSettings() { ReasoningEffort = "medium", ResponseFormat = typeof(UpdateAgentMemoryRequest), Temperature = 0.5};
        var memoryFunction =
            KernelFunctionFactory.CreateFromPrompt(updateMemoryPrompt, memorySettings, "UpdateAgentMemory");
        var updateArgs = new KernelArguments(updateStateSettings);
        try
        {
            var agentStateUpdateResponse =
                await smallKernel.InvokeAsync<string>(stateFunction, updateArgs, cancellationToken: ct);
                //await smallKernel.InvokePromptAsync<string>(updateDynamicStatePrompt, updateArgs,
                //    cancellationToken: ct);
            var agentStateUpdate = JsonSerializer.Deserialize<UpdateAgentStateRequest>(agentStateUpdateResponse ?? "");
            var updateLog = UpdateAgentState(agentStateUpdate.Description, agentStateUpdate.UpdatedDynamicState);
            _logger.LogInformation("Agent State Update: {UpdateLog}", updateLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating agent state");
        }
        
        var memArgs = new KernelArguments(memorySettings);
        try
        {
            var memoryUpdateResponse = await smallKernel.InvokeAsync<string>(memoryFunction, memArgs, cancellationToken: ct);
            /*await smallKernel.InvokePromptAsync<string>(updateMemoryPrompt, memArgs, cancellationToken: ct)*/
            ;
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
	                                               
	                                               Prioritize selecting participants who have not spoken many times recently, or who have not spoken at all.
	                                               
	                                               The available participants along with the number of times they've spoken are:
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
            ResponseFormat = typeof(NextAgent), Temperature = 0.2
        };
        history = new ChatHistory(history);
        var messageHistory = history.ToList();
        messageHistory.ForEach(m =>
        {
            m.Content = m.Content.Length > 250 ? m.Content[..250] + "..." : m.Content;
        });
        history = new ChatHistory(messageHistory);
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
            _agentParticipationCount[nextAgent.AgentId]++;
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
    
    private KernelArguments UpdateKernelArguments(IReadOnlyList<ChatMessageContent> history, IReadOnlyList<WorldAgent> agents, OpenAIPromptExecutionSettings settings)
    {
        var groupConvoHistory = string.Join("\n ", history?.Select(message => $"From: \n{message?.AuthorName}\n### Message\n {message?.Content}\n") ?? []);
        var kernelArgs = new KernelArguments(settings)
        {
            ["speakerList"] = string.Join("\n ", _agentParticipationCount.Select(a => $"**Name:** {a.Key} (spoken {a.Value} times)\n")),
            ["conversationHistory"] = groupConvoHistory
        };
        return kernelArgs;
    }
    private string UpdateAgentMemory(
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
            if (updatedKnowledgeMemory.RecentMemories.Count == 1)
            {
                var memories = agent.KnowledgeMemory.RecentMemories;
                memories.AddRange(updatedKnowledgeMemory.RecentMemories);
                updatedKnowledgeMemory.RecentMemories = memories.Distinct().ToList();
            }
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
    private string UpdateAgentState(
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

