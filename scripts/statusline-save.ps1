# ClaudeStats: speichert das JSON, das Claude Code an die Statusline übergibt (Windows).
#
# Einrichtung in %USERPROFILE%\.claude\settings.json:
#   { "statusLine": { "type": "command",
#     "command": "powershell -NoProfile -ExecutionPolicy Bypass -File C:\\PFAD\\statusline-save.ps1" } }

$dir = if ($env:CLAUDE_CONFIG_DIR) { $env:CLAUDE_CONFIG_DIR } else { Join-Path $env:USERPROFILE ".claude" }
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$json = [Console]::In.ReadToEnd()
$tmp = Join-Path $dir "usage-snapshot.json.tmp"
$target = Join-Path $dir "usage-snapshot.json"

# .NET schreibt standardmäßig UTF-8 ohne BOM.
[System.IO.File]::WriteAllText($tmp, $json)
Move-Item -Force -Path $tmp -Destination $target
