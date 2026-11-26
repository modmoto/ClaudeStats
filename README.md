# ClaudeStats

Menüleisten- bzw. Tray-Anzeige für dein **Claude-Nutzungslimit**. Zwei Ringe zeigen, wie viel
vom **5-Stunden-Limit** und vom **Wochenlimit** noch übrig ist. Sie zählen runter: voll heißt
viel übrig, leer heißt Limit erreicht.

![Vorschau](docs/preview.svg)

> **Status:** frühe Version. Gebaut mit [Avalonia UI](https://avaloniaui.net/) (C#/.NET 11),
> zuerst für macOS, der Code ist so aufgebaut, dass Windows nur kleine Ergänzungen braucht.
> Das Bild oben ist ein Mockup, ein echter Screenshot folgt.

## So funktioniert es

ClaudeStats braucht weder Login noch Netzwerk. Claude Code übergibt seiner Statusline bei jedem
Update ein JSON, das unter `rate_limits` genau die Werte enthält, die auch `/usage` zeigt:

| Feld                                  | Bedeutung                       |
|---------------------------------------|---------------------------------|
| `rate_limits.five_hour.used_percentage` | Verbrauch im 5-Stunden-Fenster  |
| `rate_limits.five_hour.resets_at`       | Reset des 5-Stunden-Fensters    |
| `rate_limits.seven_day.used_percentage` | Verbrauch im Wochenfenster      |
| `rate_limits.seven_day.resets_at`       | Reset des Wochenfensters        |

Ein kleines Skript (`scripts/statusline-save.sh` bzw. `.ps1`) legt dieses JSON als
`~/.claude/usage-snapshot.json` ab. Die App liest die Datei alle 10 Sekunden und rechnet
`100 − Verbrauch` in den Rest um.

```
Claude Code ──Statusline-JSON──▶ statusline-save.sh ──▶ ~/.claude/usage-snapshot.json ──▶ ClaudeStats (Ringe)
```

### Wichtig zu wissen

- Die Werte gibt es nur für **Pro/Max-Abos** und erst **nach der ersten Antwort** in einer
  Claude-Code-Sitzung.
- Die Daten aktualisieren sich nur, **solange Claude Code läuft**. Arbeitest du gerade nur in der
  Claude-App oder auf claude.ai, bleibt die Anzeige auf dem letzten Stand. Das Menü zeigt, wie alt
  der Wert ist („Stand: vor 12 Min“).
- Ist der gespeicherte Reset-Zeitpunkt schon vorbei, zeigt die App das Fenster als wieder voll.
- Die Snapshot-Datei enthält das komplette Statusline-JSON (u. a. Arbeitsverzeichnis). Sie bleibt
  lokal und steht in der `.gitignore`.
- Ein Tageslimit gibt es in diesen Daten nicht, daher nur 5 Stunden und Woche.

## Einrichten

1. Skript ausführbar machen (macOS/Linux):
   ```bash
   chmod +x scripts/statusline-save.sh
   ```
2. In `~/.claude/settings.json` (Windows: `%USERPROFILE%\.claude\settings.json`) eintragen:
   ```json
   {
     "statusLine": {
       "type": "command",
       "command": "/ABSOLUTER/PFAD/zu/ClaudeStats/scripts/statusline-save.sh"
     }
   }
   ```
   Windows:
   ```json
   {
     "statusLine": {
       "type": "command",
       "command": "powershell -NoProfile -ExecutionPolicy Bypass -File C:\\PFAD\\ClaudeStats\\scripts\\statusline-save.ps1"
     }
   }
   ```
   Hast du schon eine eigene Statusline, setze `CLAUDE_STATS_CHAIN` auf deren Pfad
   (nur `.sh`), dann wird sie nach dem Speichern weiter ausgeführt.
3. Claude Code neu starten und eine Nachricht schicken.
4. App starten:
   ```bash
   dotnet run --project src/ClaudeStats
   ```

## Entwickeln

Voraussetzung: .NET 11 SDK, z. B. mit JetBrains Rider.

```bash
dotnet build ClaudeStats.sln
dotnet test tests/ClaudeStats.Core.Tests
```

| Projekt                         | Inhalt                                                          |
|---------------------------------|-----------------------------------------------------------------|
| `src/ClaudeStats.Core`          | JSON lesen, Prozent/Reset rechnen, Texte. Keine UI, plattformunabhängig |
| `src/ClaudeStats`               | Avalonia-App: Tray-Icon, Ringe zeichnen, Menü                   |
| `tests/ClaudeStats.Core.Tests`  | Unit-Tests für die Kernlogik                                    |
| `scripts/`                      | Statusline-Skripte für macOS/Linux und Windows                  |

Die Logik steckt bewusst im Core-Projekt. Für Windows muss die App nur ergänzt werden (Autostart,
Tray-Details), die Berechnung bleibt identisch. Eine GitHub-Action baut auf macOS und Windows.

## Roadmap

- [ ] Erster echter Lauf auf macOS, Screenshot ins README
- [ ] Prozentzahl direkt neben bzw. in den Ringen
- [ ] Popover-Fenster statt reinem Menü
- [ ] Dock-Icon ausblenden (`LSUIElement`) und `.app`-Paket
- [ ] Autostart (macOS und Windows)
- [ ] Windows-Test und Installer
- [ ] Einrichtungsassistent, der die `settings.json` selbst ergänzt
- [ ] Release mit fertigen Downloads
