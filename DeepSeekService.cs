using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComicSpeechBalloon;

/// <summary>
/// Lightweight HTTP client for the DeepSeek Chat Completions API.
/// Returns a short, context-aware phrase based on the user's active application.
/// </summary>
public class DeepSeekService : IDisposable
{
    private readonly HttpClient _http;
    private bool _disposed;

    private const string ApiEndpoint = "https://api.deepseek.com/v1/chat/completions";
    private const string Model = "deepseek-chat";

    public DeepSeekService()
    {
        _http = new HttpClient
        {
            Timeout = DeepSeekConfig.HttpClientTimeout
        };
    }

    /// <summary>
    /// Asks DeepSeek for a short, context-aware phrase based on the foreground app.
    /// A random mood profile colors the delivery style.
    /// Returns null on any failure (network, timeout, bad response) so the caller can fall back.
    /// </summary>
    public async Task<string?> GetContextualPhraseAsync(string appName, string? windowTitle, MoodProfile mood)
    {
        if (string.IsNullOrWhiteSpace(DeepSeekConfig.ApiKey))
            return null;

        var persona = mood.GetPersonaInstruction();

        var userContent = DeepSeekConfig.SendWindowTitle && !string.IsNullOrWhiteSpace(windowTitle)
            ? $"The user is currently using: {appName} — Window title: \"{windowTitle}\". Generate a single context-aware speech bubble line (max 60 words)."
            : $"The user is currently using: {appName}. Generate a single context-aware speech bubble line (max 60 words) that references this app or what people do with it.";

        var systemContent = $"You are a witty comic speech bubble floating on a user's desktop. {persona} Generate a short, surprising one-liner — max 60 words, ideally one punchy sentence. Be concise. Do NOT use emoji. Do NOT wrap in quotes.";

        var payload = new
        {
            model = Model,
            messages = new[]
            {
                new { role = "system", content = systemContent },
                new { role = "user", content = userContent }
            },
            max_tokens = 80,
            temperature = 0.95
        };

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint);
            request.Headers.Add("Authorization", $"Bearer {DeepSeekConfig.ApiKey}");
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var cts = new CancellationTokenSource(DeepSeekConfig.HttpClientTimeout);
            using var response = await _http.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cts.Token);

            // DeepSeek response shape: { choices: [{ message: { content: "..." } }] }
            if (result.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    var text = content.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(text) && text.Length >= 5)
                        return text;
                }
            }

            return null;
        }
        catch (TaskCanceledException)
        {
            return null; // Timeout
        }
        catch (HttpRequestException)
        {
            return null; // Network error
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Generic call to the DeepSeek Chat API with full system and user prompts.
    /// Used by the Roast Engine for memory-rich, highly contextual roasts.
    /// Returns null on any failure.
    /// </summary>
    public async Task<string?> GetRoastAsync(string systemPrompt, string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(DeepSeekConfig.ApiKey))
            return null;

        var payload = new
        {
            model = Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            max_tokens = 120,
            temperature = 0.95
        };

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint);
            request.Headers.Add("Authorization", $"Bearer {DeepSeekConfig.ApiKey}");
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var cts = new CancellationTokenSource(DeepSeekConfig.HttpClientTimeout);
            using var response = await _http.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cts.Token);

            if (result.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    var text = content.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(text) && text.Length >= 5)
                        return text;
                }
            }

            return null;
        }
        catch (TaskCanceledException) { return null; }
        catch (HttpRequestException) { return null; }
        catch { return null; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _http.Dispose();
    }
}
