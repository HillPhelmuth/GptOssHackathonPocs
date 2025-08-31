# Possible Hackathon Application 1
## Narrative Zoo

An interactive, agent-based narrative simulator for experimenting with multi-agent behavior, emergent stories, and tool-augmented reasoning. The UI is built with reusable Blazor components in `AINarrativeSimulator.Components`, and the simulation/runtime logic lives in `GptOssHackathonPocs.Narrative.Core`.

### What it does
- Renders a resizable dashboard composed of:
	- World Controller: start/pause/step time, load scenarios, and manage world ticks.
	- Event Feed: live log of agent decisions and actions.
	- Agent Inspector: drill into any agent to view state, intent, and recent actions.
- Orchestrates turns via `NarrativeOrchestration` and updates a shared `WorldState`.
- Loads agent definitions and initial state from `StaticData/WorldAgents.json` (via `FileHelper` and `WorldAgentsPlugin`).
- Supports LLM-backed reasoning through `OpenRouterReasoningHandler` with an `AutoInvocationFilter` to keep calls safe and targeted.

### Key projects and files
- UI (Blazor components)
	- `AINarrativeSimulator.Components/Main.razor` – page layout and grid wiring.
	- `AINarrativeSimulator.Components/WorldController.razor` – time controls and world actions.
	- `AINarrativeSimulator.Components/EventFeed.razor` – stream of events and decisions.
	- `AINarrativeSimulator.Components/AgentInspector.razor` – agent details panel.
	- `AINarrativeSimulator.Components/wwwroot/resizableGrid.js` – resizable grid behavior for the dashboard.
- Core simulation
	- `GptOssHackathonPocs.Narrative.Core/NarrativeOrchestration.cs` – ticks the world, coordinates agent updates, and emits events.
	- `GptOssHackathonPocs.Narrative.Core/WorldState.cs` – shared state of agents and environment.
	- `GptOssHackathonPocs.Narrative.Core/Models/WorldAgents.cs` and `WorldAgentAction.cs` – agent and action data models.
	- `GptOssHackathonPocs.Narrative.Core/Plugins/WorldAgentsPlugin.cs` – loads agents/scenarios.
	- `GptOssHackathonPocs.Narrative.Core/Services/OpenRouterReasoningHandler.cs` – optional LLM reasoning integration.
	- `GptOssHackathonPocs.Narrative.Core/StaticData/WorldAgents.json` – sample agents and initial setup.

### Run it
1. Open the solution `GptOssHackathonPocs.sln` in Visual Studio 2022 (or use `dotnet` CLI).
2. Set `GptOssHackathonPocs` as the startup project.
3. Start debugging (F5) or run the project. Navigate to `/narrativezoo`.

CLI (optional):
- From `GptOssHackathonPocs/GptOssHackathonPocs`, run `dotnet run`, then open the printed URL and go to `/narrativezoo`.

### LLM integration (optional)
If you want agents to use tool-augmented reasoning, provide an OpenRouter API key:
- Set an environment variable `OPENROUTER_API_KEY` with your key before launching the app.
- Or configure it via user secrets/app settings if your environment supports it.

Without an API key, the simulator still runs with deterministic or mock decision paths.

### Using the UI
- World Controller
	- Start/Pause: begin or halt world ticks.
	- Step: advance the simulation one tick.
	- Load scenario: pick a dataset (e.g., from `WorldAgents.json`).
- Event Feed
	- Shows decisions, actions, and notable world events in order.
	- Useful for tracing agent cause/effect over time.
- Agent Inspector
	- Select an agent to view current state, intent, memory, and recent actions.

### Extending
- Add agents: extend `WorldAgents.json` or plug in a new provider via `WorldAgentsPlugin`.
- New actions: add to `WorldAgentAction` and handle them in orchestration.
- Custom reasoning: implement a service like `OpenRouterReasoningHandler` and wire it into orchestration.

### Known limitations
- The sample world data is minimal and for demonstration.
- LLM calls depend on external availability and configured keys.
- The UI is optimized for desktop screens; mobile layout is basic.

### Troubleshooting
- No events? Ensure a scenario is loaded and the world is ticking (Start/Step).
- LLM errors? Verify `OPENROUTER_API_KEY` is set and the network allows outbound requests.
- UI layout issues? Resize panels; the grid uses client-side JS (`resizableGrid.js`).


