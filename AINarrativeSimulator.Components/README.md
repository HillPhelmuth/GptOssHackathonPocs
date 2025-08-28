# AINarrativeSimulator.Components

This Razor Class Library provides Blazor components mirroring the React UI:

- EventFeed.razor — Displays a stream of WorldAction items.
- AgentInspector.razor — Lists agents and shows detailed tabs for a selected agent.
- WorldController.razor — Start/stop/reset controls and rumor/event injectors, and a world state panel.

Models live in `Models/WorldModels.cs` and mirror the TypeScript types used by the React app.

## Usage

1. Reference this RCL from your Blazor app.
2. Import the namespace in your `_Imports.razor`:

```
@using AINarrativeSimulator.Components
@using AINarrativeSimulator.Components.Models
```

3. Render components:

```
<EventFeed Actions="@actions" />
<AgentInspector Agents="@agents" SelectedAgentId="@selectedId" SelectedAgentIdChanged="@(id => selectedId = id)" />
<WorldController IsRunning="@isRunning"
                 OnStart="@Start"
                 OnStop="@Stop"
                 OnReset="@Reset"
                 InjectRumor="@InjectRumor"
                 InjectEvent="@InjectEvent"
                 WorldState="@worldState" />
```

The CSS class names are Tailwind-like tokens for readability; you can map them to your own styles or ignore them.