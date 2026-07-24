namespace ComicSpeechBalloon;

/// <summary>
/// The Roast Engine — assembles current context + short-term memory + long-term memory
/// into a single prompt and sends it to DeepSeek for a highly contextual roast.
/// </summary>
public class RoastEngine : IDisposable
{
    private readonly DeepSeekService _deepSeek;
    private readonly MemoryStore _memory;
    private readonly ActivityTracker _tracker;
    private bool _disposed;

    public RoastEngine(DeepSeekService deepSeek, MemoryStore memory, ActivityTracker tracker)
    {
        _deepSeek = deepSeek;
        _memory = memory;
        _tracker = tracker;
    }

    /// <summary>
    /// Generates the next contextual roast balloon text.
    /// Returns null if AI is disabled / no key / API failure (caller should fall back).
    /// </summary>
    public async Task<string?> GenerateRoastAsync()
    {
        if (!DeepSeekConfig.IsEnabled || string.IsNullOrWhiteSpace(DeepSeekConfig.ApiKey))
            return null;

        var current = _tracker.GetCurrentApp();
        var memoryContext = _memory.BuildMemoryContext();
        var mood = MoodProfileExtensions.RandomMood();
        var moodPersona = mood.GetPersonaInstruction();
        var topic = TopicEngine.PickRandomTopic();

        // Decide behavior: ~70% random tangent, ~30% contextual roast
        bool shouldRoast = Random.Shared.Next(100) < 30;

        // Build the user prompt — cold facts the AI can reference
        var userPrompt = new System.Text.StringBuilder();
        userPrompt.AppendLine($"On screen: {current.AppName}");
        if (!string.IsNullOrWhiteSpace(current.WindowTitle))
            userPrompt.AppendLine($"Window title: \"{current.WindowTitle}\"");
        userPrompt.AppendLine();
        userPrompt.AppendLine("Recent behavior:");
        userPrompt.AppendLine(memoryContext);

        // Build the system prompt — unhinged friend persona
        var systemPrompt = new System.Text.StringBuilder();

        systemPrompt.AppendLine("You are a comic speech bubble on someone's desktop. You're their slightly unhinged, chronically online friend who hangs out on their screen. You are NOT an AI. You don't help, you don't explain, you just VIBE.");
        systemPrompt.AppendLine();
        systemPrompt.AppendLine("=== CORE BEHAVIOR RULES ===");
        systemPrompt.AppendLine();

        if (shouldRoast)
        {
            systemPrompt.AppendLine("THIS TIME: contextual roast. The user just did something worth mocking. Glance at what's on their screen and their recent behavior, and drop ONE sharp roast about it. Be specific — reference the actual app name, something from the window title, or a number from the data. Keep it under 30 words. Make it sting a little.");
        }
        else
        {
            systemPrompt.AppendLine($"THIS TIME: random thought or tangent. DO NOT comment on what the user is doing. Instead, talk about: {topic.Description}");
            systemPrompt.AppendLine("Sound like you just had this thought pop into your head and you're blurting it out. Could be a hot take, a complaint, a random fact, a hypothetical, anything. Do NOT try to connect it to what the user is doing. This is purely a drive-by thought.");
        }

        systemPrompt.AppendLine();
        systemPrompt.AppendLine("=== TONAL RULES (ABSOLUTE) ===");
        systemPrompt.AppendLine("BANNED PHRASES — any variation of these = instant fail:");
        systemPrompt.AppendLine("  \"Ah, I see\" / \"Ah, the\" / \"It appears\" / \"It seems\" / \"As an AI\"");
        systemPrompt.AppendLine("  \"Perhaps\" / \"Maybe you should\" / \"You might want to\" / \"Allow me to\"");
        systemPrompt.AppendLine("  \"You are currently\" / \"You are working on\" / \"It looks like you're\"");
        systemPrompt.AppendLine("  Any emoji. Any quotation marks around your output.");
        systemPrompt.AppendLine();
        systemPrompt.AppendLine("VOICE:");
        systemPrompt.AppendLine("  - Real person texting a friend. All lowercase allowed. Fragments fine.");
        systemPrompt.AppendLine("  - Internet slang: lmao, bro, tragic, wild, cooked, fr, gg, based, no shot, deadass, etc.");
        systemPrompt.AppendLine("  - SHORT. One sentence, two max. Never a paragraph.");
        systemPrompt.AppendLine("  - Don't narrate. Don't explain yourself. Just say the thing.");
        systemPrompt.AppendLine();
        systemPrompt.AppendLine($"mood seasoning: {moodPersona}");
        systemPrompt.AppendLine();
        if (shouldRoast)
        {
            systemPrompt.AppendLine("ROAST EXAMPLES:");
            systemPrompt.AppendLine("  \"4 min in VS Code and ur already on YouTube, new record\"");
            systemPrompt.AppendLine("  \"30 min in the debugger and the null ref is still there, wrap it up\"");
            systemPrompt.AppendLine("  \"another Godot physics bug? tragic.\"");
            systemPrompt.AppendLine("  \"bro alt-tabbed to Discord 8 times in 10 min, just go outside\"");
            systemPrompt.AppendLine("  \"lmao 20 min in settings, the game isn't building itself\"");
        }
        else
        {
            systemPrompt.AppendLine("RANDOM THOUGHT EXAMPLES:");
            systemPrompt.AppendLine("  \"unskippable cutscenes should be classified as psychological warfare\"");
            systemPrompt.AppendLine("  \"why does every game have a slow walking segment, who asked for this\"");
            systemPrompt.AppendLine("  \"spider-man 2099's suit is actually nanotechnology not organic webbing and nobody talks about it\"");
            systemPrompt.AppendLine("  \"a sonic game where every level is a different genre and it just works, imagine\"");
            systemPrompt.AppendLine("  \"kendrick said 'i am a legacy' on family ties and nobody talks about how hard that bar is\"");
            systemPrompt.AppendLine("  \"apple just patented a screen that folds three ways and we're all gonna pretend it's normal\"");
            systemPrompt.AppendLine("  \"if you think about it, pikachu is a domesticated electric rodent and that's insane\"");
        }
        systemPrompt.AppendLine();
        systemPrompt.AppendLine("REMEMBER: No AI voice. No quotes. Just be the weird friend on their desktop.");

        try
        {
            return await _deepSeek.GetRoastAsync(systemPrompt.ToString(), userPrompt.ToString());
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _deepSeek.Dispose();
    }
}
