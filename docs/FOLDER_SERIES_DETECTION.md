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

## Reale Anime-Ordnernamen – bestätigte Parser-Erwartungen

| Original | Erwarteter Suchtext | Abgetrennte Hinweise |
|---|---|---|
| `[AO]Elainas.Reise.S01E01.DTS.1080p.BD.10bit.AV1` | `Elainas Reise` | Gruppe AO, S01E01, DTS, 1080p, BD, 10-bit, AV1 |
| `Goodbye.Lara.S01E01.English.DL.AAC.1080p.WebDL.x264-PuddingSama` | `Goodbye Lara` | S01E01, Englisch, DL, AAC, 1080p, WebDL, x264, Gruppe PuddingSama |
| `Daemons of the Shadow Realm [1080p][x264][Sub][AAC 2.0][Crunchyroll WebDL]` | `Daemons of the Shadow Realm` | 1080p, x264, Untertitel, AAC-Stereo, Crunchyroll/WebDL |
| `Saga.of.Tanya.the.Evil.2017.S02E01.Ger.Eng.Sub.AAC.1080p.WebDL.x264-PuddingSama` | `Saga of Tanya the Evil` | mögliches Jahr 2017, S02E01, Ger/Eng, Untertitel, AAC, 1080p, WebDL, x264 |
| `The.Daily.Life.of.a.Middle.Aged.Online.Shopper.in.Another.World.S01E01.Ger.Eng.Sub.AAC.1080p.CR.WEB.h264-DRiFTKiNG` | `The Daily Life of a Middle Aged Online Shopper in Another World` | S01E01, Ger/Eng, Untertitel, AAC, 1080p, CR, WEB, h264, Gruppe DRiFTKiNG |
| `Nichijou： My Ordinary Life [1080p][Sub][FLAC 2.0][x264]` | `Nichijou： My Ordinary Life` | 1080p, Untertitel, FLAC-Stereo, x264 |

Die Stichwörter sind **Release-Angaben**, keine extern verifizierten Medienmerkmale. `DL` bedeutet häufig Dual Language, `CR` wird häufig für Crunchyroll verwendet und `BD` häufig für Blu-ray; dies wird im Suchvorschlag nicht als verbindliche Tatsache behauptet.

**Titel bleiben Titel:** Der Unicode-Doppelpunkt `：` und Wörter wie `English` werden nicht pauschal überall entfernt. `The English` bleibt der Titel; `Nichijou： My Ordinary Life` behält seinen Untertitel. Technische Markierungen in aufeinanderfolgenden eckigen Klammern werden dagegen erkannt und vom Suchvorschlag abgetrennt. Jahreszahlen werden nur in typischen Release-Positionen entfernt.

Diese Regeln beeinflussen nur die lokale, bearbeitbare Sucheingabe. Ordnernamen werden nicht automatisch umbenannt und die Episodenpositionen nicht daraus abgeleitet.
