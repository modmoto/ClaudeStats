#!/usr/bin/env bash
# ClaudeStats: speichert das JSON, das Claude Code an die Statusline übergibt,
# damit die Menüleisten-App die Limits lesen kann.
#
# Einrichtung in ~/.claude/settings.json:
#   { "statusLine": { "type": "command", "command": "/ABSOLUTER/PFAD/zu/statusline-save.sh" } }
#
# Optional: eine bestehende Statusline weiter ausführen
#   export CLAUDE_STATS_CHAIN="/pfad/zu/deiner-statusline.sh"

dir="${CLAUDE_CONFIG_DIR:-$HOME/.claude}"
mkdir -p "$dir"
tmp="$dir/usage-snapshot.json.tmp"
target="$dir/usage-snapshot.json"

# Erst in eine Temp-Datei, dann atomar ersetzen, damit die App nie eine halbe Datei liest.
cat > "$tmp" && mv "$tmp" "$target"

if [ -n "$CLAUDE_STATS_CHAIN" ]; then
  "$CLAUDE_STATS_CHAIN" < "$target"
fi
