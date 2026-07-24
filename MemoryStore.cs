using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComicSpeechBalloon;

/// <summary>
/// Memory system combining:
///   - Short-term: a rolling 10-minute queue of activity events (in-memory)
///   - Long-term: daily aggregated usage stats persisted to JSON in %APPDATA%
/// </summary>
public class MemoryStore : IDisposable
{
    // ── Short-term ───────────────────────────────────────
    private readonly ConcurrentQueue<ActivityEvent> _recentEvents = new();
    private static readonly TimeSpan ShortTermWindow = TimeSpan.FromMinutes(10);

    // ── Long-term ────────────────────────────────────────
    private readonly string _storageDir;
    private readonly string _storageFile;
    private DailyMemory _todayMemory = new();
    private bool _disposed;

    public MemoryStore()
    {
        _storageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ComicSpeechBalloon");
        _storageFile = Path.Combine(_storageDir, "memory.json");

        Directory.CreateDirectory(_storageDir);
        LoadFromDisk();
    }

    // ── Public API ───────────────────────────────────────

    /// <summary>
    /// Records an app switch. Called from the ActivityTracker event handler.
    /// </summary>
    public void RecordEvent(ActivityEvent evt)
    {
        // Short-term: push onto rolling queue
        _recentEvents.Enqueue(evt);

        // Long-term: increment daily session count
        var app = evt.AppName.ToLowerInvariant();
        var apps = _todayMemory.Apps;
        if (apps.TryGetValue(app, out var stats))
        {
            stats.Sessions++;
            stats.LastSeen = evt.Timestamp;
        }
        else
        {
            apps[app] = new AppUsageStats
            {
                Sessions = 1,
                LastSeen = evt.Timestamp
            };
        }

        // Persist throttled — write at most once per 30 seconds
        if (_todayMemory.Apps.Count % 10 == 0) // arbitrary throttle
            SaveToDisk();
    }

    /// <summary>
    /// Returns a human-readable summary of the last 10 minutes of activity.
    /// Example: "VS Code (2 min) → Brave - YouTube (1 min) → VS Code (7 min)"
    /// </summary>
    public string GetShortTermSummary()
    {
        var cutoff = DateTime.UtcNow - ShortTermWindow;
        var events = _recentEvents
            .Where(e => e.Timestamp >= cutoff)
            .OrderBy(e => e.Timestamp)
            .ToList();

        if (events.Count == 0)
            return "(just started)";

        // Collapse consecutive same-app events and show durations
        var segments = new List<string>();
        for (int i = 0; i < events.Count; i++)
        {
            int runLength = 1;
            while (i + runLength < events.Count &&
                   string.Equals(events[i + runLength].AppName, events[i].AppName, StringComparison.OrdinalIgnoreCase))
                runLength++;

            var start = events[i].Timestamp;
            var end = i + runLength < events.Count
                ? events[i + runLength].Timestamp
                : DateTime.UtcNow;

            var duration = end - start;
            string label;

            if (duration.TotalMinutes >= 1)
                label = $"{events[i].AppName} ({duration.TotalMinutes:F0} min)";
            else
                label = $"{events[i].AppName} ({duration.TotalSeconds:F0}s)";

            segments.Add(label);
            i += runLength - 1;
        }

        return string.Join(" → ", segments);
    }

    /// <summary>
    /// Returns a summary of the user's app usage patterns today.
    /// Example: "Today: VS Code 2.3 hrs (18 sessions), Chrome 1.1 hrs (24 sessions)"
    /// </summary>
    public string GetDailySummary()
    {
        if (_todayMemory.Apps.Count == 0)
            return "(no data yet today)";

        // Estimate minutes: ~30s per session average (rough heuristic)
        var entries = _todayMemory.Apps
            .OrderByDescending(kv => kv.Value.Sessions)
            .Take(10)
            .Select(kv =>
            {
                double estimatedMinutes = kv.Value.Sessions * 0.5;
                string duration;
                if (estimatedMinutes >= 60)
                    duration = $"{estimatedMinutes / 60:F1} hrs";
                else
                    duration = $"{estimatedMinutes:F0} min";
                return $"  • {kv.Key}: {duration} ({kv.Value.Sessions} switches)";
            });

        return "Today's top apps:\n" + string.Join("\n", entries);
    }

    /// <summary>
    /// Dumps both memory layers into a formatted string for the AI prompt.
    /// </summary>
    public string BuildMemoryContext()
    {
        var sb = new System.Text.StringBuilder();

        sb.Append("[SHORT-TERM — Last 10 minutes]\n");
        sb.AppendLine(GetShortTermSummary());

        sb.AppendLine("\n[LONG-TERM — Today's habits]");
        sb.AppendLine(GetDailySummary());

        return sb.ToString();
    }

    // ── Persistence ──────────────────────────────────────

    private void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_storageFile)) return;

            var json = File.ReadAllText(_storageFile);
            var allDays = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, AppUsageStats>>>(json);
            if (allDays == null) return;

            var todayKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (allDays.TryGetValue(todayKey, out var todayApps))
            {
                _todayMemory = new DailyMemory { Apps = todayApps };
            }

            // Prune old days
            var keepCutoff = DateTime.UtcNow.AddDays(-60).ToString("yyyy-MM-dd");
            var pruned = allDays
                .Where(kv => string.Compare(kv.Key, keepCutoff, StringComparison.Ordinal) >= 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            if (pruned.Count != allDays.Count)
                File.WriteAllText(_storageFile, JsonSerializer.Serialize(pruned));
        }
        catch
        {
            _todayMemory = new DailyMemory();
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var todayKey = DateTime.UtcNow.ToString("yyyy-MM-dd");

            Dictionary<string, Dictionary<string, AppUsageStats>> allDays;
            try
            {
                if (File.Exists(_storageFile))
                {
                    var json = File.ReadAllText(_storageFile);
                    allDays = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, AppUsageStats>>>(json) ?? new();
                }
                else
                {
                    allDays = new();
                }
            }
            catch
            {
                allDays = new();
            }

            allDays[todayKey] = _todayMemory.Apps;

            File.WriteAllText(_storageFile, JsonSerializer.Serialize(allDays));
        }
        catch
        {
            // Best-effort persistence — must not crash the app
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SaveToDisk();
    }
}

// ── Serialization types ──────────────────────────────────

public class AppUsageStats
{
    [JsonPropertyName("sessions")]
    public int Sessions { get; set; }

    [JsonPropertyName("lastSeen")]
    public DateTime LastSeen { get; set; }
}

public class DailyMemory
{
    [JsonPropertyName("apps")]
    public Dictionary<string, AppUsageStats> Apps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
