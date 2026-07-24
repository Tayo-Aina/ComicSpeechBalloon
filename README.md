# Comic Speech Balloon 🎈

A Windows desktop app that spawns AI-generated comic speech bubbles near your cursor. 

## Features

- **Context-Aware AI Roasts** — Detects which app you're using and generates witty, unhinged commentary via the [DeepSeek API](https://platform.deepseek.com)
- **10 Mood Profiles** — Troll, Funny, Sarcastic, Wholesome, Philosophical, Chaotic, Dramatic, Deadpan, Hype Man, Conspiracy Theorist
- **70% Randoms / 30% Roasts** — Doesn't just roast you. Goes on tangents about gaming, music, tech news, internet culture, food takes, and more
- **Activity Tracking & Memory** — Remembers your last 10 minutes of app switches and daily usage patterns to make roasts painfully specific
- **Settings UI** — Adjustable spawn interval (5–120s), display duration (2–20s), AI toggle, roast mode toggle
- **System Tray** — Left-click opens settings, right-click opens menu. Close button minimizes to tray.
- **Secure API Key Storage** — Key encrypted with Windows DPAPI in `%APPDATA%`, never in source code

## Setup

1. **Install .NET 8 SDK** from [dotnet.microsoft.com](https://dotnet.microsoft.com)
2. **Get a DeepSeek API key** at [platform.deepseek.com/api_keys](https://platform.deepseek.com/api_keys)
3. **Clone & build:**
   ```bash
   git clone https://github.com/Tayo-Aina/ComicSpeechBalloon.git
   cd ComicSpeechBalloon
   dotnet restore
   dotnet build
   dotnet run
   ```
4. **Set your API key** — right-click the tray icon → "🔑 Set API Key…"

## Tech Stack

- **.NET 8 / WPF** — Native Windows desktop app
- **DeepSeek Chat API** — Text generation
- **DPAPI** — Windows-encrypted credential storage
- **P/Invoke** — Foreground window detection, system tray, click-through overlay

## License

MIT
