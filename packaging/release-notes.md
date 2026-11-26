Menüleisten-Anzeige für dein Claude-Nutzungslimit: zwei Ringe für das 5-Stunden- und das Wochenlimit.

## Welches Paket?
- `osx-arm64`: Macs mit Apple Silicon (M1 und neuer)
- `osx-x64`: Intel-Macs

## Installieren
1. Zip entpacken, `ClaudeStats.app` nach `/Applications` ziehen.
2. Die App ist nicht von Apple beglaubigt. Einmalig freigeben:
   ```bash
   xattr -dr com.apple.quarantine /Applications/ClaudeStats.app
   ```
3. Statusline einrichten (siehe [README](https://github.com/modmoto/ClaudeStats#einrichten)), Claude Code starten, App öffnen.

Alternativ selbst bauen mit `scripts/package-macos.sh`, dann entfällt Schritt 2.

Prüfsummen der Dateien stehen in `SHA256SUMS.txt`.
