using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using GptOssHackathonPocs.Narrative.Core.Models;

namespace GptOssHackathonPocs.Narrative.Core;

public class WorldState : INotifyPropertyChanged
{
    private WorldAgents? _worldAgents;
    private WorldAgent? _activeWorldAgent;
    private List<WorldAgentAction> _recentActions = [];
    public event PropertyChangedEventHandler? PropertyChanged;

    public WorldAgents WorldAgents
    {
        get
        {
            _worldAgents ??= WorldAgents.DefaultFromJson();
            return _worldAgents;
        }
        set => SetField(ref _worldAgents, value);
    }

    public WorldAgent? ActiveWorldAgent
    {
        get => _activeWorldAgent;
        set => SetField(ref _activeWorldAgent, value);
    }
    public string Weather { get; set; } = "Clear";
    public List<string> GlobalEvents { get; set; } = [];
    public List<string> Rumors { get; set; } = [];
    public string CurrentTime { get; set; } = DateTime.Now.ToString("t");
    public Dictionary<PineharborLocation, string> Locations => EnumHelpers.AllEnumDescriptions<PineharborLocation>();

    public List<WorldAgentAction> RecentActions
    {
        get => _recentActions;
        set => SetField(ref _recentActions, value);
    }
    public void AddRecentAction(WorldAgentAction action)
    {
        _recentActions.Insert(0, action);
        if (_recentActions.Count > 20)
        {
            _recentActions.RemoveAt(_recentActions.Count - 1);
        }
        OnPropertyChanged(nameof(RecentActions));
    }
    public void UpdateAgent(WorldAgent agent)
    {
        var matchedAgent = WorldAgents.Agents.FirstOrDefault(a => a.AgentId == agent.AgentId);
        matchedAgent.DynamicState = agent.DynamicState;
        matchedAgent.KnowledgeMemory = agent.KnowledgeMemory;
        OnPropertyChanged(nameof(WorldAgents));
    }
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}