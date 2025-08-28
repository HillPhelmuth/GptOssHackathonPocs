using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GptOssHackathonPocs.Narrative.Core.Models;
using Microsoft.SemanticKernel;

namespace GptOssHackathonPocs.Narrative.Core.Plugins;

public class WorldAgentsPlugin
{
    [KernelFunction, Description("Update your dynamic state, including short term goals, location or mood")]
    public string UpdateAgentState([FromKernelServices] WorldState worldState,
        [Description("Agent's name")] string agentId, [Description("Description of the update")] string description,
        [Description("The updated agent dynamic state")] DynamicState updatedDynamicState)
    {
        var agent = worldState.WorldAgents.Agents.FirstOrDefault(a => a.AgentId == agentId);
        if (agent == null)
        {
            return $"Agent with ID '{agentId}' not found.";
        }

        agent.DynamicState = updatedDynamicState;

        agent.AddNotes(description);
        worldState.UpdateAgent(agent);
        return $"Agent '{agentId}' state updated successfully.";
    }
    [KernelFunction, Description("Update your memories and relationships")]
    public string UpdateAgentMemory([FromKernelServices] WorldState worldState,
        [Description("Agent's name")] string agentId, [Description("Description of the update")] string description,
        [Description("The updated agent knowledge memory and relationships")] KnowledgeMemory updatedKnowledgeMemory)
    {
        var agent = worldState.WorldAgents.Agents.FirstOrDefault(a => a.AgentId == agentId) ?? worldState.ActiveWorldAgent;
        if (agent == null)
        {
            return $"Agent with ID '{agentId}' not found.";
        }

        agent.KnowledgeMemory = updatedKnowledgeMemory;

        agent.AddNotes(description);
        worldState.UpdateAgent(agent);
        return $"Agent '{agentId}' knowledge memory and relationships updated successfully.";
    }
    [KernelFunction, Description("Take an action")]
    public string TakeAction([FromKernelServices] WorldState worldState,
        [Description("Agent's name")] string agentId, [Description("The action to take")] WorldAgentAction action)
    {
        var agent = worldState.WorldAgents.Agents.FirstOrDefault(a => a.AgentId == agentId) ?? worldState.ActiveWorldAgent;
        if (agent == null)
        {
            return $"Agent with ID '{agentId}' not found.";
        }
        worldState.AddRecentAction(action);
        agent.AddNotes($"Took action: {action.Type} - {action.Details}");
        worldState.UpdateAgent(agent);
        return $"Agent '{agentId}' took action '{action.Type}' successfully.";
    }

    [KernelFunction, Description("Retrieve the current state of the world agents")]
    public string GetWorldState([FromKernelServices] WorldState worldState)
    {
        return JsonSerializer.Serialize(worldState.WorldAgents, new JsonSerializerOptions(){WriteIndented = true});
    }

}