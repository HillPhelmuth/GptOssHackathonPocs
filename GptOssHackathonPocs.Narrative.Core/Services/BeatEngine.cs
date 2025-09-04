using GptOssHackathonPocs.Narrative.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GptOssHackathonPocs.Narrative.Core.Services;

public interface IBeatEngine
{
    // Push raw actions into the engine as they arrive
    void Add(WorldAgentAction action);

    // Start/stop the windowing loop
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();

    // Fired whenever a beat is produced
    event Action<BeatSummary>? OnBeat;
}

public sealed class BeatEngine : IBeatEngine, IDisposable
{
    private readonly INarrativeOrchestration _orchestration;
    private readonly TimeSpan _window;
    private readonly int _minActions;
    private readonly string _model;
    private readonly ConcurrentQueue<WorldAgentAction> _buffer = new();
    private Timer? _timer;
    private volatile bool _running;
    private readonly TimeSpan _idleFlush;
    public event Action<BeatSummary>? OnBeat;
    private DateTime _lastBeatUtc = DateTime.MinValue;
    public BeatEngine(INarrativeOrchestration orchestration,
        TimeSpan? window = null,
        int minActions = 6,
        string model = "openai/gpt-oss-20b")
    {
        _orchestration = orchestration;
        _window = window ?? TimeSpan.FromSeconds(5);  // tune: 3–8s works well
        _minActions = Math.Max(1, minActions);
        _model = model;
    }

    public void Add(WorldAgentAction action)
    {
        Console.WriteLine($"Add action to buffer from: {action.ActingAgent}");
        _buffer.Enqueue(action);
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        if (_running) return Task.CompletedTask;
        _running = true;
        _timer = new Timer(async _ => await TryMakeBeatAsync(ct), null,
            dueTime: _window, period: _window);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _running = false;
        _timer?.Dispose();
        _timer = null;
        return Task.CompletedTask;
    }

    private async Task TryMakeBeatAsync(CancellationToken ct)
    {
        if (!_running) return;

        var now = DateTime.UtcNow;

        // Fast path: nothing to do
        if (_buffer.IsEmpty) return;

        // Decide if we should flush:
        // 1) enough actions accumulated, OR
        // 2) it's been a while since last beat -> idle flush of whatever we have.
        var count = _buffer.Count; // fine here; perf is OK for typical sizes
        var dueToIdle = (now - _lastBeatUtc) >= _idleFlush;

        if (count < _minActions && !dueToIdle)
            return; // don't drain yet — keep accumulating

        // Build a batch to summarize.
        var take = Math.Min(count, 6);
        var batch = DequeueUpTo(take);

        if (batch.Length == 0) return;

        var start = batch.Min(a => a.Timestamp).ToUniversalTime();
        var end = batch.Max(a => a.Timestamp).ToUniversalTime();

        var payload = string.Join("\n\n", batch.Select(a => a.ToTypeMarkdown()).Select(x => $"{x.Item1} -> {x.Item2}"));

        var prompt = BuildBeatPrompt(payload, start, end);

        string? raw = null;
        try
        {

            var beat = await _orchestration.ExecuteLlmPrompt<BeatSummary>(prompt, _model, ct);

            if (beat is null) return;

            beat.WindowStartUtc = start;
            beat.WindowEndUtc = end;
            beat.SourceActionCount = batch.Length;
            beat.BeatId = Hash($"{beat.ContinuityKey}|{start:O}|{end:O}");

            OnBeat?.Invoke(beat);
            _lastBeatUtc = now;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TryAddBeat failed for: {prompt}\n\nError:\n{ex}");
        }
    }

    private WorldAgentAction[] DequeueUpTo(int n)
    {
        var list = new List<WorldAgentAction>(Math.Max(1, n));
        while (n > 0 && _buffer.TryDequeue(out var a))
        {
            list.Add(a);
            n--;
        }
        return list.ToArray();
    }

    // trick: local static alias gives us access to the instance buffer via closure-less indirection
    // but we can’t do that cleanly; instead, inline the normal approach:
    private WorldAgentAction[] DequeueUpToInstance(int n)
    {
        var list = new System.Collections.Generic.List<WorldAgentAction>(n);
        while (n > 0 && _buffer.TryDequeue(out var a))
        {
            list.Add(a);
            n--;
        }
        return list.ToArray();
    }

    // Rewire DequeueUpTo to instance version (compiler-friendly)
    private WorldAgentAction[] DequeueUpToCompat(int n) => DequeueUpToInstance(n);

    

    private static string BuildBeatPrompt(string actionsJson, DateTime startUtc, DateTime endUtc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("System: You are the Narrative Beat Compiler for a fast-ticking sci-fi station.");
        sb.AppendLine("Rules:");
        sb.AppendLine("- Compress ONLY the events you are given. Do not invent new events.");
        sb.AppendLine("- Output STRICT JSON matching the schema below. No prose, no explanations.");
        sb.AppendLine("- Keep 'Title' ≤ 8 words. 'Summary' 2–3 sentences, concrete and readable.");
        sb.AppendLine("- 'ContinuityKey' must be a SHORT stable phrase (main actors + conflict) reused across beats.");
        sb.AppendLine("- 'Mood' ∈ [tense, hopeful, anxious, determined, chaotic, solemn, jubilant, neutral].");
        sb.AppendLine("- 'Tension' is 0–100 (0=calm, 100=crisis).");
        sb.AppendLine();
        sb.AppendLine($"WindowUtc: {startUtc:O} → {endUtc:O}");
        sb.AppendLine("Actions:");
        sb.AppendLine(actionsJson);
        sb.AppendLine();
        
        return sb.ToString();
    }

    private WorldAgentAction[] DequeueAll()
    {
        Console.WriteLine("DequeueAll");
        var list = new List<WorldAgentAction>(64);
        while (_buffer.TryDequeue(out var a)) list.Add(a);
        return list.ToArray();
    }

    private static string Hash(string s)
    {
        using var sha = System.Security.Cryptography.SHA1.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
        return string.Concat(bytes.Take(10).Select(b => b.ToString("x2")));
    }

    public void Dispose() => _timer?.Dispose();
}