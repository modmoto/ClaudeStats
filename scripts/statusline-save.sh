#!/usr/bin/env bash
# ClaudeStats: saves the JSON that Claude Code passes to the status line,
# so the menu bar app can read the usage limits.
#
# Setup in ~/.claude/settings.json:
#   { "statusLine": { "type": "command", "command": "/ABSOLUTE/PATH/to/statusline-save.sh" } }
#
# Optional: keep running an existing status line
#   export CLAUDE_STATS_CHAIN="/path/to/your-statusline.sh"

dir="${CLAUDE_CONFIG_DIR:-$HOME/.claude}"
mkdir -p "$dir"
tmp="$dir/usage-snapshot.json.tmp"
target="$dir/usage-snapshot.json"

# Write to a temp file first, then replace atomically, so the app never reads a half-written file.
cat > "$tmp" && mv "$tmp" "$target"

if [ -n "$CLAUDE_STATS_CHAIN" ]; then
  "$CLAUDE_STATS_CHAIN" < "$target"
fi
