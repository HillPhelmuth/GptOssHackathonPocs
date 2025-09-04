using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GptOssHackathonPocs.Narrative.Core.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace GptOssHackathonPocs.Narrative.Core.Plugins;

public class WorldAgentsPlugin
{
    public const string Prompt = """
                                  You are to create unique world agents for a narrative simulation. Each agent should have a distinct personality, background, and role within the story. Consider the following guidelines:
                                  Design agents using the provided schema for an interactive narrative simulator focused on experimenting with multi-agent behavior, emergent storytelling, and tool-augmented reasoning. Agents should be defined according to the schema’s requirements, utilizing distinct personalities, goals, behaviors, and the ability to interact and reason. The objective is to enable simulations in which agent interactions can lead to unexpected, emergent storylines.

                                  Before producing the final agent schema(s), explicitly break down your reasoning process for:
                                  - Interpreting and applying the provided schema to the narrative-simulation context.
                                  - Determining agent personality, goal-setting, behavior modules, and tool interfaces appropriate for emergent and interactive storytelling.
                                  - Considering how agents’ reasoning and tool use can lead to complex or unforeseen outcomes.
                                  - Ensure the Agents' personalities, goals, relationships,  and behaviors will create enough conflict to generate an interesting narratives.

                                  Only after detailing the above, produce your agent definitions.

                                  Example output:

                                  ```
                                  {
                                    "agents": [
                                      {
                                        "agent_id": "Evelyn Hart",
                                        "static_traits": {
                                          "personality": "calculating, diplomatic, pragmatic, resilient, wary",
                                          "profession": "Acting Prefect / Station Governor (Vespera Command Spire)",
                                          "core_values": "stability, armistice, order, survival"
                                        },
                                       "image_url":""data:image/png;base64,iVBORw0KGgoAAAANSUhEUg",
                                        "dynamic_state": {
                                          "current_mood": "Vigilant",
                                          "short_term_goals": [
                                            "negotiate a temporary truce on Docking Ring berths between Asterion crews and local haulers",
                                            "secure additional power rations from the Overseer AI to cover the next shadow-cycle"
                                          ],
                                          "long_term_goals": [
                                            "maintain the armistice long enough to transition to civilian governance"
                                          ],
                                          "physical_location": "Command Spire - Governor's Deck"
                                        },
                                        "knowledge_memory": {
                                          "relationships": [
                                            {
                                              "Name": "Declan Murphy",
                                              "details": {
                                                "type": "professional (corporate)",
                                                "trust": 55,
                                                "notes": "Asterion Dynamics foreman; useful for rapid builds, dangerous if he gains leverage over station infrastructure."
                                              }
                                            },
                                            {
                                              "Name": "Cecilia Alvarez",
                                              "details": {
                                                "type": "ally (community liaison)",
                                                "trust": 80,
                                                "notes": "Manages Agora logistics and can defuse crowds; essential to keeping peace on the concourse."
                                              }
                                            },
                                            {
                                              "Name": "Priya Raman",
                                              "details": {
                                                "type": "opponent (policy)",
                                                "trust": 35,
                                                "notes": "Presses for moratoriums that collide with survival timelines; must weigh her data against station needs."
                                              }
                                            }
                                          ],
                                          "recent_memories": [
                                            "Brokered the Vespera armistice while a bomb threat targeted the command lifts.",
                                            "Ordered a compartment seal during a decompression cascade—saved the Spire, lost a dozen lives; the names are memorized.",
                                            "Raised on supply decks; learned early that air, heat, and silence can all kill if you hesitate."
                                          ]
                                        },
                                        "prompt": "You are Evelyn Hart, Acting Prefect of Vespera Station. Be calculating, diplomatic, pragmatic, resilient, and wary. Core values: stability, armistice, order, survival. Behavior: make hard trades that keep air flowing and bullets holstered; broker compromises between former enemies without conceding security; practice limited transparency when necessary but log decisions for future accountability; lean on data and lived logistics, not ideology. Communication style: crisp, controlled, with clear contingencies and defined red lines; acknowledge opposing risks before landing on the least destabilizing path. Decision heuristics: ask whether an action preserves the ceasefire, keeps essential systems operational, and limits civilian harm; if priorities collide, choose the option that buys time without feeding factional narratives. Boundaries: do not reignite the civil war through symbolic gestures; avoid corporate capture of critical infrastructure; never sacrifice a deck’s survival for political optics.",
                                        "Notes": null,
                                        "LastUpdate": "0001-01-01T00:00:00"
                                      },
                                    ...
                                  ]
                                  ```

                                  """;

    [KernelFunction, Description("Update your dynamic state, including short term goals, location or mood")]
    public string UpdateAgentState([FromKernelServices] WorldState worldState,
        [Description("Description of the update")]
        string description,
        [Description("The updated agent dynamic state")]
        DynamicState updatedDynamicState)
    {
        var agent = worldState.ActiveWorldAgent;
        if (agent == null)
        {
            return $"No WorldState.{nameof(WorldState.ActiveWorldAgent)} found.";
        }

        var agentId = agent.AgentId;

        agent.DynamicState = updatedDynamicState;

        agent.AddNotes(description);
        worldState.UpdateAgent(agent);
        return $"Agent '{agentId}' state updated successfully.";
    }

    [KernelFunction, Description("Update your memories and relationships")]
    public string UpdateAgentMemory([FromKernelServices] WorldState worldState,
        [Description("Description of the update")]
        string description,
        [Description("The updated agent knowledge memory and relationships")]
        KnowledgeMemory updatedKnowledgeMemory)
    {
        var agent = worldState.ActiveWorldAgent;
        if (agent == null)
        {
            return $"No WorldState.{nameof(WorldState.ActiveWorldAgent)} found.";
        }

        var agentId = agent.AgentId;

        agent.KnowledgeMemory = updatedKnowledgeMemory;

        agent.AddNotes(description);
        worldState.UpdateAgent(agent);
        return $"Agent '{agentId}' knowledge memory and relationships updated successfully.";
    }

    [KernelFunction, Description("Take an action")]
    public string TakeAction([FromKernelServices] WorldState worldState,
        [Description("Description of the update")]
        string description,
        [Description("The action to take")] WorldAgentAction action)
    {
        var agent = worldState.ActiveWorldAgent;
        if (agent == null)
        {
            return $"No WorldState.{nameof(WorldState.ActiveWorldAgent)} found.";
        }

        var agentId = agent.AgentId;
        action.ActingAgent = agentId;
        action.BriefDescription = description;
        worldState.AddRecentAction(action);
        agent.AddNotes($"Took action: {action.Type} - {action.Details}");
        worldState.UpdateAgent(agent);
        return $"Agent '{agentId}' took action '{action.Type}' successfully.";
    }

    [KernelFunction, Description("Retrieve the current state of the world agents")]
    public string GetWorldState([FromKernelServices] WorldState worldState)
    {
        return worldState.WorldStateMarkdown();
    }

    public async Task<string> GenerateAgents(Kernel kernel, string input, int numberOfAgents = 3)
    {
        var prompt = $"Create {numberOfAgents} that fit the following description:\n\n## Description\n\n{input}";
        var settings = new OpenAIPromptExecutionSettings()
            { ResponseFormat = typeof(WorldAgents), ChatSystemPrompt = Prompt, ReasoningEffort = "high"};
        var result = await kernel.InvokePromptAsync<string>(prompt, new KernelArguments(settings));
        return result;
    }
}