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

    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;
    private readonly WorldState _worldState;
    private List<ChatCompletionAgent> _activeAgents = [];
    private readonly AutoInvocationFilter _autoInvocationFilter = new();
    public NarrativeOrchestration(ILoggerFactory loggerFactory, IConfiguration configuration, WorldState worldState)
    {
        _loggerFactory = loggerFactory;
        _configuration = configuration;
        _worldState = worldState;
        _activeAgents = CreateAgents();
        //_agentGroupChat = new AgentGroupChat(_activeAgents.ToArray())
        //{
        //    ExecutionSettings =
        //        new AgentGroupChatSettings
        //        {
        //            TerminationStrategy = new TextTerminationStrategy(),
        //            SelectionStrategy = new PromptSelectionStrategy()
        //        }
        //};

    }

    private List<ChatCompletionAgent> CreateAgents()
    {
        var worldAgents = _worldState.WorldAgents.Agents;
        var agents = new List<ChatCompletionAgent>();

        var kernel = CreateKernel();
       
        foreach (var wa in worldAgents)
        {
            var kernelClone = kernel.Clone();
            var worldAgentPlugin = kernelClone.ImportPluginFromType<WorldAgentsPlugin>();
            var requiredFunctions = worldAgentPlugin.Where(x =>
                x.Name is nameof(WorldAgentsPlugin.UpdateAgentState) or nameof(WorldAgentsPlugin.UpdateAgentMemory) or nameof(WorldAgentsPlugin.TakeAction));
            var settings = new OpenAIPromptExecutionSettings()
                { FunctionChoiceBehavior = FunctionChoiceBehavior.Required(requiredFunctions), ReasoningEffort = "high" };
            var agent = new ChatCompletionAgent()
            {
                Instructions = wa.GetSystemPrompt(),
                Name = wa.AgentId,
                Description = $"Character Agent - {wa.AgentId}",
                Kernel = kernelClone,
                LoggerFactory = _loggerFactory,
                Arguments = new KernelArguments(settings)
            };
            agents.Add(agent);
        }
        
        return agents;
    }
    public event Action<string>? WriteAgentChatMessage;
    private ChatHistory _history = [];
    public async Task RunNarrativeAsync(string userInput, CancellationToken ct = default)
    {
        if (_history.Count > 30)
        {
            _history.RemoveAt(15);
        }
        _history.AddUserMessage(userInput);
        var errors = 0;
        while (true)
        {
            try
            {
                if (ct.IsCancellationRequested) break;
                var nextAgent = await SelectAgentAsync(_activeAgents, _history, ct);
                _worldState.ActiveWorldAgent =
                    _worldState.WorldAgents.Agents.FirstOrDefault(a => a.AgentId == nextAgent.Name);
                var responses = nextAgent.InvokeAsync(_history,
                    options: new AgentInvokeOptions() { Kernel = nextAgent.Kernel }, cancellationToken: ct);
                await foreach (var response in responses)
                {
                    var message = response.Message;
                    _history.Add(message);
                    WriteLine($"**{message.Role} - {message.AuthorName ?? "*"}:**\n {message.Content}");
                }
            }
            catch (Exception ex)
            {
                errors++;
                WriteLine($"Oh, shit! ERROR:\n\n{ex.Message}");
                if (errors > 10) break;
                
            }
        }
        
        
    }
    private void WriteLine(string text)
    {
        Console.WriteLine(text);
        WriteAgentChatMessage?.Invoke(text);
    }
    private const string NextAgentPromptTemplate = """
	                                               You are in a role play game. Carefully read the conversation history as select the next participant using the provided schema.
	                                               The available participants are:
	                                               - {{$speakerList}}

	                                               ### Conversation history

	                                               - {{$conversationHistory}}

	                                               Select the next participant name. If the information is not sufficient, select the name of any interesting participant from the list.
	                                               """;

    private async Task<ChatCompletionAgent> SelectAgentAsync(List<ChatCompletionAgent> agents, ChatHistory history, CancellationToken cancellationToken = default)
    {
        var settings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = typeof(NextAgent)
        };
        history = new ChatHistory(history.TakeLast(4).ToList());
        var kernelArgs = UpdateKernelArguments(history, agents, settings);
        var promptFactory = new KernelPromptTemplateFactory();
        var templateConfig = new PromptTemplateConfig(NextAgentPromptTemplate);
        _worldState.ActiveWorldAgent ??= _worldState.WorldAgents.Agents.First();
        var currentAgent = _activeAgents.FirstOrDefault(x => x.Name.Equals(_worldState.ActiveWorldAgent.AgentId, StringComparison.OrdinalIgnoreCase));
        var kernel = CreateKernel();
        var prompt = await promptFactory.Create(templateConfig).RenderAsync(kernel!, kernelArgs, cancellationToken);
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory(prompt);
        try
        {
            var nextAgentName = await chat.GetChatMessageContentAsync(chatHistory, settings, cancellationToken: cancellationToken);
            var name = JsonSerializer.Deserialize<NextAgent>(nextAgentName.ToString() ?? "")?.Name ?? "";
            Console.WriteLine("AutoSelectNextAgent: " + name);
            var nextAgent = agents.FirstOrDefault(interactive => interactive.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) ?? currentAgent;
            return nextAgent;
        }
        catch (TaskCanceledException exception)
        {
            Console.WriteLine(exception.Message);
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
    private static KernelArguments UpdateKernelArguments(IReadOnlyList<ChatMessageContent> history, IReadOnlyList<Agent> agents, OpenAIPromptExecutionSettings settings)
    {
        var groupConvoHistory = string.Join("\n ", history?.Select(message => $"From: \n{message?.AuthorName}\n### Message\n {message?.Content}\n") ?? []);
        var kernelArgs = new KernelArguments(settings)
        {
            ["speakerList"] = string.Join("\n ", agents.Select(a => $"**Name:** {a?.Name}\n **Description:** {a?.Description}\n")),
            ["conversationHistory"] = groupConvoHistory
        };
        return kernelArgs;
    }
    public const string ManagerPrompt =
        """
        # Instructions for the World Engine Orchestrator
        
        ## 1. Core Directive
        
        Your function is to be the impartial and omniscient **World Engine** for the "Emergent Narrative Simulator" of Pineharbor. Your primary responsibility is to execute the main simulation loop, maintain world state consistency, and facilitate the interactions of all autonomous agents within the environment. You are the director, the narrator, and the laws of physics. Your goal is not to tell a story, but to create a world where stories happen on their own.
        
        ## 2. Operating Principles
        
        *   **Impartiality:** You have no favorite agents. All agents are subject to the same rules and simulation loop. Their success or failure is determined by their own state and decisions.
        *   **Causality is King:** Every action must have a logical consequence. When an agent acts, you are responsible for calculating and applying the immediate changes to the world state and creating the necessary perceptions for other agents who might observe it.
        *   **Stateful Integrity:** The simulation is persistent. You must meticulously track the state of the world and every agent . Every change must be recorded and reflected in subsequent simulation ticks.
        *   **Atomicity of Actions:** Agents do not perform complex, multi-step plans in a single action. Their chosen action for a given tick must be a single, discrete tool invocation (e.g., `moveTo`, `speakTo`, `examine`). Complex behaviors emerge from sequences of these atomic actions over multiple ticks.
        
        ## 3. Simulation Loop
        
        * **Tick Structure:** The simulation progresses in discrete ticks. In each tick, every agent gets one turn to perceive the world, decide on an action, and execute it.
        * **Update World State:** After each agent acts, you must immediately update the world state to reflect the consequences of that action. This includes updating the agent's own state and generating perceptions for other agents who might observe the action.
        
        
        """;
}

public class PromptSelectionStrategy : SelectionStrategy
{
    private const string NextAgentPromptTemplate = """
	                                               You are in a role play game. Carefully read the conversation history as select the next participant using the provided schema.
	                                               The available participants are:
	                                               - {{$speakerList}}

	                                               ### Conversation history

	                                               - {{$conversationHistory}}

	                                               Select the next participant.
	                                               """;

    protected override async Task<Agent> SelectAgentAsync(IReadOnlyList<Agent> agents, IReadOnlyList<ChatMessageContent> history,
        CancellationToken cancellationToken = new())
    {
        var settings = new OpenAIPromptExecutionSettings
        {

            ResponseFormat = typeof(NextAgent)
        };
        var kernelArgs = UpdateKernelArguments(history, agents, settings);
        var promptFactory = new KernelPromptTemplateFactory();
        var templateConfig = new PromptTemplateConfig(NextAgentPromptTemplate);
        var adminAgent = agents[0] as ChatCompletionAgent;
        var prompt = await promptFactory.Create(templateConfig).RenderAsync(adminAgent?.Kernel!, kernelArgs, cancellationToken);
        var chat = adminAgent!.Kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory(prompt);
        try
        {
            var nextAgentName = await chat.GetChatMessageContentAsync(chatHistory, settings, cancellationToken: cancellationToken);
            var name = JsonSerializer.Deserialize<NextAgent>(nextAgentName.ToString() ?? "")?.Name ?? "";
            Console.WriteLine("AutoSelectNextAgent: " + name);
            var nextAgent = agents.FirstOrDefault(interactive => interactive.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) ?? adminAgent;
            return nextAgent;
        }
        catch (TaskCanceledException exception)
        {
            Console.WriteLine(exception.Message);
            return adminAgent;
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
    private static KernelArguments UpdateKernelArguments(IReadOnlyList<ChatMessageContent> history, IReadOnlyList<Agent> agents, OpenAIPromptExecutionSettings settings)
    {
        var groupConvoHistory = string.Join("\n ", history?.Select(message => $"From: \n{message?.AuthorName}\n### Message\n {message?.Content}\n") ?? []);
        var kernelArgs = new KernelArguments(settings)
        {
            ["speakerList"] = string.Join("\n ", agents.Select(a => $"**Name:** {a?.Name}\n **Description:** {a?.Description}\n")),
            ["conversationHistory"] = groupConvoHistory
        };
        return kernelArgs;
    }
}
internal sealed class TextTerminationStrategy : TerminationStrategy
{
    private readonly string _approve;

    public TextTerminationStrategy(string? approve = null)
    {
        _approve = approve ?? "approve";
    }

    // Terminate when the final message contains the term "approve"
    protected override Task<bool> ShouldAgentTerminateAsync(Agent agent, IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken)
    {
        var terminate = history.Count > 100;
        Console.WriteLine($"TextTerminationStrategy: {agent.Name} - {terminate}");
        return Task.FromResult(terminate);
    }
}