namespace ComicSpeechBalloon;

/// <summary>
/// Thin orchestration layer — delegates to the RoastEngine for
/// memory-rich contextual roasts, with cooldown enforcement and fallback.
/// </summary>
public class AppContextService : IDisposable
{
    private readonly RoastEngine _roastEngine;
    private DateTime _lastApiCall = DateTime.MinValue;
    private bool _disposed;

    public AppContextService(RoastEngine roastEngine)
    {
        _roastEngine = roastEngine;
    }

    /// <summary>
    /// Returns the next message to display — a rich contextual roast when available.
    /// </summary>
    public async Task<string> GetContextualMessageAsync()
    {
        // ── Gate: AI disabled or no key? ──
        if (!DeepSeekConfig.IsEnabled || string.IsNullOrWhiteSpace(DeepSeekConfig.ApiKey))
            return SpeechBalloonControl.PickRandomPhrase();

        // ── Cooldown check ──
        var elapsed = DateTime.UtcNow - _lastApiCall;
        if (elapsed.TotalSeconds < DeepSeekConfig.CooldownSeconds)
            return SpeechBalloonControl.PickRandomPhrase();

        _lastApiCall = DateTime.UtcNow;

        try
        {
            var phrase = await _roastEngine.GenerateRoastAsync();
            if (!string.IsNullOrWhiteSpace(phrase))
                return phrase;
        }
        catch
        {
            // Fall through to fallback
        }

        return SpeechBalloonControl.PickRandomPhrase();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _roastEngine.Dispose();
    }
}
