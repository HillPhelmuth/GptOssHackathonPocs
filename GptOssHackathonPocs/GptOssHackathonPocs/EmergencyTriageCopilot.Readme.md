# Possible Hackathon Application 2
## Emergency Triage Copilot

Emergency Triage Copilot is a Blazor-based application that aggregates real-time hazard and incident data, enriches it with geospatial context, and helps teams produce concise, actionable triage plans. It combines interactive UI components with a .NET Core backend that ingests multiple data feeds, runs enrichment, and leverages AI reasoning to triage emergencies and recommends and generates specific communications.

## Key features
- Live incident map with clustering and selection (IncidentMap)
- Incident detail panel with enrichment (population, SVI, facilities, geometry)
- Data feeds panel to view/refresh multiple sources (DataFeedsList)
- Action queue for assembling a response plan (ActionQueue)
- Communications hub for drafting and publishing plans (CommunicationsHub)
- AI reasoning hook to suggest triage steps (OpenRouterReasoningHandler in Core)

## Architecture overview
The solution is split into UI components and core services:

- Triage UI (in `GptOssHackathonPocs.Client/TriageComponents`)
	- `TriageDashboard.razor`: Top-level dashboard
	- `IncidentMap.razor`: Map visualization and incident selection
	- `IncidentDetailsPanel.razor`: Details and enrichment for the selected incident
	- `DataFeedsList.razor`: Manage and inspect feeds
	- `ActionQueue.razor`: Queue suggested or chosen actions
	- `CommunicationsHub.razor`: Compose and publish the triage plan

- Core services and models (in `GptOssHackathonPocs.Core`)
	- Models: `Incident`, `AgentData`, and enrichment helpers under `Models/Enrichment`
	- Feeds: `NwsAlertsFeed`, `UsgsQuakesFeed`, `FirmsActiveFiresFeed`, `NhcStormsFeed`
	- Aggregation: `IncidentAggregator` to normalize and merge feed outputs
	- Enrichment: SVI, population, facility index, geometry helpers
	- Plan publishing: `SlackPlanPublisher`, `CapFilePlanPublisher`, `CompositePlanPublisher`
	- Reasoning: `OpenRouterReasoningHandler` for AI-driven triage suggestions

Data flow (high level):

Feeds -> IncidentAggregator -> Enrichment -> UI (map/details) -> ActionQueue -> Plan Publisher(s)

## Getting started
1. Build the solution in Visual Studio or via .NET CLI.
2. Run the host project (`GptOssHackathonPocs`).
3. From the home page, choose "Disaster Triage Copilot" or navigate to `/copilot`.

### Configuration
Some features (e.g., AI reasoning and publishing to Slack or CAP files) may require configuration:
- Slack webhook or token for `SlackPlanPublisher` (if used)
- OpenRouter or LLM credentials for `OpenRouterReasoningHandler` (optional)
- Any external indexes/services used by enrichment providers

Check application settings or environment variables as appropriate for your deployment. If credentials are not configured, the app should still run with core visualization features.

## Using the Copilot
1. Select data feeds in the Data Feeds panel and let incidents populate the map.
2. Click a map marker to open the Incident Details panel.
3. Review enrichment (population, SVI, nearby facilities) and auto-suggested actions (if AI is enabled).
4. Add actions to the Action Queue.
5. Use the Communications Hub to compose and publish a concise triage plan to your chosen channel(s).

## Extending
- Add a new feed: implement `IIncidentFeed` and register it; it will surface in the Data Feeds panel.
- Add enrichment: implement an enrichment service and integrate with `IncidentCardBuilder`/details panel.
- Add a publisher: implement a plan publisher and wire it into `CompositePlanPublisher`.

## Folder quick reference
- `GptOssHackathonPocs.Client/TriageComponents/`: Blazor UI for the Copilot
- `GptOssHackathonPocs.Core/Models/`: Incident and related models
- `GptOssHackathonPocs.Core/Services/`: Feeds, aggregator, publishers, AI handler
- `GptOssHackathonPocs.Core/Models/Enrichment/`: Enrichment utilities and indexes

## Notes
This is a proof-of-capabilities project intended for experimentation. Verify data feed terms of use and exercise caution when sharing or publishing output.

