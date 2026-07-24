namespace ComicSpeechBalloon;

/// <summary>
/// Polls the foreground window every 500ms and fires an event when the user
/// switches apps. Runs on a background thread — minimal CPU footprint.
/// </summary>
public class ActivityTracker : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private Task? _pollLoop;
    private ActivityEvent _lastEvent;
    private bool _disposed;

    /// <summary>
    /// Fired on the background (non-UI) thread whenever the foreground app changes.
    /// </summary>
    public event Action<ActivityEvent>? OnAppSwitched;

    public ActivityTracker()
    {
        _lastEvent = new ActivityEvent(DateTime.MinValue, string.Empty, string.Empty);
    }

    /// <summary>
    /// Starts the background polling loop. Safe to call multiple times.
    /// </summary>
    public void Start()
    {
        if (_pollLoop != null) return;
        _pollLoop = Task.Run(PollLoopAsync, _cts.Token);
    }

    private async Task PollLoopAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var appInfo = ActiveWindowDetector.GetActiveApp();
                if (appInfo != null)
                {
                    var current = new ActivityEvent(
                        DateTime.UtcNow,
                        appInfo.Value.ProcessName,
                        appInfo.Value.WindowTitle);

                    // Only fire if the app name actually changed (ignore title-only changes)
                    if (!string.Equals(current.AppName, _lastEvent.AppName, StringComparison.OrdinalIgnoreCase))
                    {
                        _lastEvent = current;
                        OnAppSwitched?.Invoke(current);
                    }
                }
            }
            catch
            {
                // Silently eat — polling must never crash
            }

            try
            {
                await Task.Delay(500, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Returns the most recently detected foreground app.
    /// </summary>
    public ActivityEvent GetCurrentApp()
    {
        var appInfo = ActiveWindowDetector.GetActiveApp();
        if (appInfo == null) return _lastEvent;

        return new ActivityEvent(
            DateTime.UtcNow,
            appInfo.Value.ProcessName,
            appInfo.Value.WindowTitle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
