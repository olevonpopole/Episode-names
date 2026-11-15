# Automatische Episoden-Datenvorschläge (Windows-Testfeature)

## Ablauf

1. Im Hauptfenster einen Ordner mit Episodendateien auswählen.
2. **Daten → Automatische Episoden-Datenvorschläge** anklicken.
3. Der erkannte Serientitel wird lokal aus dem Ordnernamen gewonnen.
4. Asynchrone Suche über **TVmaze ohne API-Key** sowie **TMDB bei vorhandenem, verschlüsselt gespeichertem persönlichem Key**.
5. Zu den Suchergebnissen wird der Provider und gegebenenfalls das Erscheinungsjahr angezeigt. Nur wenn **genau ein** exakter Titeltreffer vorliegt, werden die Episoden automatisch geladen; andernfalls muss eine passende Serie ausdrücklich gewählt werden.
6. Für jede Datei wird lokal eine zuverlässige Staffel-/Episodenkennung gesucht: `S01E02`, `1x02` oder `E02` innerhalb `Staffel 1`/`Season 1`. Keine unbelegte Schätzung aus der Dateireihenfolge oder einer alleinstehenden Anime-Folgennummer.
7. Metadaten werden **ausschließlich über (Staffel, Folge)** zugeordnet. Doppelte lokale Nummern, Specials, fehlende Anbieter-Folgen und unnummerierte Dateien bleiben sichtbar blockiert.
8. Bei vollständiger eindeutiger Zuordnung erscheint pro Datei ein editierbarer Titel. Mit **Datenvorschläge übernehmen** wird lediglich der bestehende Dateneditor geöffnet. Erst dessen Bestätigung setzt die Daten; erst ein späterer Klick auf **Umbenennen** öffnet die verpflichtende sichere Umbenennungsvorschau.

## Treffer ohne exakte Titelübereinstimmung

TVmaze kennt häufig nur Original- oder englische Titel; eine erfolgreiche API-Antwort mit Serienkandidaten kann deshalb bei einem deutschen Ordnernamen von dessen Text abweichen. Die UI zeigt jetzt **alle vom Provider gelieferten Serienkandidaten**, sortiert nach Titelähnlichkeit, statt abweichende Treffer vollständig wegzufiltern. Nur exakt eindeutige Kandidaten können weiterhin automatisch zum Episodenabruf führen. Andere Vorschläge müssen bewusst gewählt werden.

Außerdem kann die Suchanfrage im Vorschlagsdialog bearbeitet und über **Erneut suchen** erneut bei den Anbietern gestellt werden. Meldungen unterscheiden zwischen echten **0 API-Treffern** und verfügbaren, aber unsicheren Kandidaten. Ein englicher/alternativer Anime-Titel kann notwendig sein, wenn TVmaze den deutschen Titel nicht kennt. Es werden niemals allein aufgrund niedriger Namensähnlichkeit Dateien automatisch zugeordnet.

## Sprache, Anbieter, Grenzen

- TMDB verwendet die gespeicherte Metadatensprache `de` oder `en`, bei `ja` wird derzeit `de` verwendet; vorhandenes DE/EN-Fallback aus dem TMDB-Provider gilt. Sprache pro Ergebnis wird angezeigt.
- TVmaze liefert Originaltitel (häufig Englisch) und wird nicht als übersetztes Ergebnis ausgewiesen.
- AniSearch ist ein HTML-Scraper, kein stabiler JSON-Provider; sein manueller Abruf bleibt separat verfügbar. Für den automatischen Hintergrundabgleich wird kein HTML-Scraping über alle Treffer erzwungen.
- Einfache Titelähnlichkeiten sind **nur Kandidaten**, keine Belege für Serienidentität. Bei mehr als einem exakten Treffer müssen Nutzer eine Serie wählen.
- Aufgrund der vorhandenen positionsbasierten Umbenennungsformatierung wird nur eine **vollständig eindeutige** Zuordnung in Dateisortierreihenfolge an den Dateneditor übergeben. Teilzuordnungen werden angezeigt, aber nicht automatisch freigegeben, um falsche Umbenennungen zu verhindern.
- Keine automatische Dateiänderung, kein verdecktes Ändern der gespeicherten Daten, keine Verarbeitung von Episoden ohne eindeutige (Staffel, Folge) Kennung. Die Netzwerkabfrage beginnt erst nach Benutzerklick auf den neuen Menüpunkt.

## Manuelle Windows-Testfälle

**A01:** `Breaking.Bad.S01E01.mkv` und `Breaking.Bad.S01E02.mkv` in einem Testordner `Breaking.Bad`: Menüpunkt aufrufen, Serienkandidaten prüfen, TVmaze auswählen. Zwei passende Datenvorschläge und keine automatische Umbenennung erwarten.

**A02:** Gleicher Ordner mit `S01E01` und `S02E01`: Jede Datei darf nur ihre jeweilige Staffel/Folge erhalten, nicht die nächste Datenzeile aus der Provider-Reihenfolge.

**A03:** Datei ohne SxxExx / 1x02-Kennung, z. B. `Anime - 001.mkv`: Warnstatus; **Datenvorschläge übernehmen** gesperrt.

**A04:** Zwei lokale `S01E01`-Dateien: beide erscheinen als Duplikate, automatische Übernahme gesperrt.

**A05:** Fehlende Titel, Sonderfolgen und nicht vorhandene Episodennummern: nicht stillschweigend mit einer Nachbarfolge verknüpfen.

**A06:** Gespeicherte Sprache `de`, kein TMDB-Schlüssel: TVmaze ist nutzbar, Oberfläche weist auf Originalsprache hin. Es darf nicht behauptet werden, TVmaze liefere deutsche Übersetzungen.

**A07:** Mehrere ähnlich benannte Serien: keine stillschweigende Auswahl; richtige Serie manuell wählen.

**A08:** **Datenvorschläge übernehmen** → Dateneditor öffnen → Abbrechen: vorhandene Daten und Dateien bleiben unverändert. Übernehmen → Umbenennen → Vorschau abbrechen: Dateien bleiben unverändert.

## Regressionstests

`tests/EpisodeNames.Metadata.Tests/EpisodeFileMatcherChecks.cs` prüft Identitätsextraktion, nicht-positionalen Abgleich, Doppelungen, Specials und unnummerierte Dateien mit Offline-Fixdaten. Für Live-Abfragen und GUI-Übernahme bleiben reale Windows-Tests erforderlich.
