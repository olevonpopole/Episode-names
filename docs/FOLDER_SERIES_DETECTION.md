# Automatischer Serien-Suchvorschlag aus Ordnernamen

**Status:** Entwicklungsfeature, kein automatisches Umbenennen.

Beim Öffnen von **Suche auf Website** wird der aktuelle Pfad aus dem Hauptfenster übergeben. Der letzte Ordnername wird lokal analysiert und ein **editierbarer Suchvorschlag** vorgefüllt. Die Suche startet ausschließlich durch den Benutzer (Enter). Es wird weder ein Anbieterergebnis automatisch ausgewählt noch eine Episodenliste automatisch übernommen.

## Erkennung (bewusst konservativ)

- Ordner unter `Season 02`, `Staffel 1`, `S01` verwenden ihren Elternordner.
- Punkte und Unterstriche werden zu Leerzeichen; Trenner ` - ` werden entfernt. Bindestriche innerhalb eines Titels wie `Spider-Man` bleiben erhalten.
- Ein führender `[Release-Gruppe]`-Zusatz wird entfernt.
- `S01E01`, `1x02`, `Episode 12`, `Folge 12` und saisonbezogene Suffixe begrenzen den Titel.
- Bekannte Release-Tags wie `1080p`, `WEB-DL`, `x265`, `HEVC`, `German` werden als Zusatz entfernt. Abschließende Jahreszahlen werden nur entfernt, wenn sie als Release-Kennzeichnung erkennbar sind. Bei mehrdeutigen Namen bleibt der Nutzer für die Korrektur zuständig.
- Die Erkennung liest **keine Dateiinhalte**, verschickt **keine Ordnerpfade** ins Netz und ist kein automatisches Matching. Ausschließlich der korrigierbare Suchtext wird bei manueller Suche an AniSearch, TVmaze oder TMDB übergeben.

## Beispiele

| Ordnername | Vorschlag |
|---|---|
| `Breaking.Bad.S01E01.1080p.WEB-DL.x264` | `Breaking Bad` |
| `[SubsPlease] Frieren - 01 [1080p]` | `Frieren` |
| `House.of.the.Dragon/Season 02` | `House of the Dragon` |
| `Better_Call_Saul_2015_1080p` | `Better Call Saul` |
| `Fate-Zero` | `Fate-Zero` |

## Manuelle Windows-Tests

1. Einen **Testordner** mit einer der obigen Bezeichnungen auswählen bzw. anlegen; `Suche auf Website` öffnen.
2. Prüfen, ob der Titel korrekt im Suchfeld vorausgefüllt und editierbar ist.
3. Mit einer eigenen, absichtlich ungewöhnlichen Ordnerbezeichnung wiederholen; Suchtext manuell korrigieren und anschließend Enter drücken.
4. Für `Staffel 02` bzw. `Season 02` prüfen, dass stattdessen der Name des Elternordners erkannt wird.
5. Verifizieren, dass nur durch Öffnen des Fensters **keine** Webanfrage ausgelöst, keine Serie automatisch gewählt und keine Datei umbenannt wird.

Die C#-Regressionstests liegen in `tests/EpisodeNames.Metadata.Tests/FolderSeriesNameChecks.cs`. Für echte, unbekannte Release-Namen gelten die Erkennungsvorschläge ausdrücklich nicht als sicher bestätigte Serientitel.
