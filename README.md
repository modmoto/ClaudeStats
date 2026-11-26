# ClaudeStats

A menu bar / tray indicator for your **Claude usage limits**. Two rings show how much of your
**5-hour limit** and your **weekly limit** is left. They count down: full means plenty left,
empty means the limit is reached.

![Preview](docs/preview.svg)

> **Status:** early version. Built with [Avalonia UI](https://avaloniaui.net/) (C#/.NET 11),
> macOS first; the code is structured so that Windows only needs small additions.
> The image above is a mockup, a real screenshot will follow. The app's menu texts are
> currently in German.

## How it works

ClaudeStats needs no login and no network access. On every update, Claude Code passes a JSON
document to its status line. Under `rate_limits` it contains exactly the values that `/usage` shows:

| Field                                   | Meaning                         |
|-----------------------------------------|---------------------------------|
| `rate_limits.five_hour.used_percentage` | Usage in the 5-hour window      |
| `rate_limits.five_hour.resets_at`       | Reset of the 5-hour window      |
| `rate_limits.seven_day.used_percentage` | Usage in the weekly window      |
| `rate_limits.seven_day.resets_at`       | Reset of the weekly window      |

A small script (`scripts/statusline-save.sh` or `.ps1`) stores this JSON as
`~/.claude/usage-snapshot.json`. The app reads that file every 10 seconds and converts
`100 − usage` into the remaining share.

```
Claude Code ──status line JSON──▶ statusline-save.sh ──▶ ~/.claude/usage-snapshot.json ──▶ ClaudeStats (rings)
```

### Good to know

- The values exist only for **Pro/Max plans** and only **after the first response** in a
  Claude Code session.
- The data updates only **while Claude Code is running**. If you are only using the Claude app or
  claude.ai, the display stays at its last value. The menu shows how old the value is.
- If the stored reset time has already passed, the app shows that window as full again.
- The snapshot file contains the complete status line JSON (including the working directory).
  It stays local and is listed in `.gitignore`.
- There is no daily limit in this data, so there are only two rings: 5 hours and week.

## Install (macOS)

**Option A: build it yourself (recommended, no Gatekeeper warning).** Requires the .NET 11 SDK.

```bash
git clone https://github.com/modmoto/ClaudeStats.git
cd ClaudeStats
scripts/package-macos.sh            # picks the architecture of your Mac
cp -R publish/*/ClaudeStats.app /Applications/
open /Applications/ClaudeStats.app
```

**Option B: prebuilt package from [Releases](https://github.com/modmoto/ClaudeStats/releases).**
Download the zip that matches your Mac (`osx-arm64` = Apple Silicon, `osx-x64` = Intel), unzip it
and drag the app to `/Applications`. The app is not notarized by Apple, so macOS blocks it on
first launch. Allow it once with:

```bash
xattr -dr com.apple.quarantine /Applications/ClaudeStats.app
```

The app runs without a Dock icon; you will only find it in the menu bar. Quit it from its menu.

## Setup

1. Make the script executable (macOS/Linux):
   ```bash
   chmod +x scripts/statusline-save.sh
   ```
2. Add it to `~/.claude/settings.json` (Windows: `%USERPROFILE%\.claude\settings.json`):
   ```json
   {
     "statusLine": {
       "type": "command",
       "command": "/ABSOLUTE/PATH/TO/ClaudeStats/scripts/statusline-save.sh"
     }
   }
   ```
   Windows:
   ```json
   {
     "statusLine": {
       "type": "command",
       "command": "powershell -NoProfile -ExecutionPolicy Bypass -File C:\\PATH\\ClaudeStats\\scripts\\statusline-save.ps1"
     }
   }
   ```
   If you already have a status line of your own, set `CLAUDE_STATS_CHAIN` to its path
   (`.sh` only); it is then run after the snapshot is saved.
3. Restart Claude Code and send a message.
4. Start the app:
   ```bash
   dotnet run --project src/ClaudeStats
   ```

## Develop

Requirements: .NET 11 SDK, e.g. with JetBrains Rider.

```bash
dotnet build ClaudeStats.sln
dotnet test tests/ClaudeStats.Core.Tests
```

| Project                         | Contents                                                        |
|---------------------------------|-----------------------------------------------------------------|
| `src/ClaudeStats.Core`          | Read JSON, compute percentages/resets, texts. No UI, platform independent |
| `src/ClaudeStats`               | Avalonia app: tray icon, ring drawing, menu                     |
| `tests/ClaudeStats.Core.Tests`  | Unit tests for the core logic                                   |
| `scripts/`                      | Status line scripts for macOS/Linux and Windows, macOS packaging |

The logic deliberately lives in the Core project. For Windows, only the app needs additions
(autostart, tray details); the calculation stays the same. A GitHub Action builds on macOS and Windows.

## Publish a release

The `.github/workflows/release.yml` pipeline runs the tests, builds `ClaudeStats.app` for Apple
Silicon and Intel on macOS runners, and creates the GitHub release with both zips and checksums.
Versions below 1.0 are automatically marked as pre-releases.

```bash
git tag v0.1.0
git push origin v0.1.0
```

Alternatively, go to **Actions → release → Run workflow** on GitHub and enter the tag.

## Roadmap

- [ ] First real run on macOS, add a screenshot to the README
- [ ] Percentage number next to or inside the rings
- [ ] Popover window instead of a plain menu
- [ ] Autostart (macOS and Windows)
- [ ] Windows test and installer
- [ ] Setup assistant that edits `settings.json` itself
- [ ] English UI texts
