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


## Erweiterung: fremdsprachige Anime-Titel und gespeicherte Zielsprache

Die Suche nutzt zunächst den lokal erkannten Ordnernamen. Falls die gefundenen Serienkandidaten keinen plausiblen Namensgleichstand haben, fragt sie den **öffentlichen AniList-GraphQL-Titelindex** ab. Nur **dokumentierte Alternativnamen derselben Anime-Identität** (romaji, englischer/japanischer Titel oder Synonyme) werden benutzt, um TVmaze erneut abzufragen. AniList ist hierbei eine Hilfe bei der **Serienidentifikation**, kein Anbieter übersetzter Episodentitel. Die ergänzten Kandidaten werden **niemals aufgrund des Alias automatisch ausgewählt**; der Nutzer bestätigt die Serie.

Für die Ausgabesprache gelten weiterhin die tatsächlich verfügbaren Quellen: **TMDB** bei vorhandenem API-Key liefert DE/EN mit bereits eingebautem englischem Rückfall, **TVmaze** originalsprachige Episodentitel. Gibt es keinen veröffentlichten Episodentitel in der gewünschten Sprache, erfindet oder maschinenübersetzt die Anwendung ihn nicht. Bei `ja` bietet der vorhandene TMDB-Provider derzeit nur DE-Fallback. Fehlende Sprachabdeckung ist im Vorschlag sichtbar.

AniList-Abfragen sind optional und begrenzt: höchstens eine Suchabfrage pro gestarteter automatischer Suche, anschließend maximal drei alternative TVmaze-Suchanfragen. Offline oder bei API-Fehlern bleibt die bisherige Suche nutzbar.

## Neuer Umbenennungsbaustein und Doppelpunkt

- In **Einstellungen → Format String** wird `%series` als weiterer Formatbaustein angezeigt. Er setzt den lokal aus dem aktuellen Serienordner erkannten Serientitel ein. `%dir` bleibt der rohe Ordnername. Ist kein Titel ermittelbar, wird die Umbenennung sicher blockiert.
- Beispiel `%series - %pos` mit einem Ordner `Elainas.Reise.S01E01.DTS.1080p.BD.10bit.AV1` und dem Episodentitel `Wanderung` ergibt `Elainas Reise - Wanderung` (vorbehaltlich vorhandener Daten und Formatposition).
- Der normale ASCII-Doppelpunkt `:` ist in Windows-Dateinamen verboten. Die Anwendung verwendet künftig `∶` (U+2236, mathematisches Verhältniszeichen) anstelle des bisherigen breiten `：` (U+FF1A). Sonderzeichen-Dialog und Umbenennungsersetzung nutzen denselben Wert. Bereits existierende Dateinamen werden nicht automatisch geändert.
- C#-Tests prüfen den Ersatzausdruck und einen echten Umbenennungsvorgang in einem temporären Testordner auf Windows.

**Manuelle Abnahme:** In Windows den Dialog **Special Characters** öffnen und den Doppelpunkt kopieren, testweise in Editor/Dateinamen prüfen; mit `%series - %pos` eine Testdatei über die sichere Vorschau umbenennen, danach Abbrechen. Alias-Suche mit `Elainas Reise` anstoßen, danach deutsche/englische Titel und Status der Ergebniszeilen prüfen. Kein Release ohne bestätigten GUI-Test.

## Formatbaustein `%season` – Staffelnummer

Zusätzlich zu `%series` gibt es in den Einstellungen unter Format String nun `%season`. Er wird als **zweistellige Staffelnummer** dargestellt, z. B. `01`, `02` oder `12`. Der Wert wird **für jede Datei separat** aus einer eindeutigen `S02E03`-/`2x03`-Angabe, einem passenden Staffelordner oder der vorausgefüllten Datenzeile `S02E03<TAB>Titel` ermittelt.

Beispiel: `%series - S%season - %pos` ergibt `Elainas Reise - S01 - Episodentitel`, sofern der Dateiname oder die Datenzeile Staffel 1 eindeutig belegt. `S02E01` ergibt entsprechend `S02`.

Fehlende oder widersprüchliche Angaben (z. B. Datei S01E03, Datenzeile S02E03) **blockieren die Vorschau**, statt eine falsche Staffelnummer zu erfinden. Auch widersprechende Episodennummern und mehrdeutige Staffel-/Folgenmarker werden zurückgewiesen.

Manuelle Abnahme: Testordner `Show` mit `Show.S02E03.mkv`, Datenzeile `S02E03<TAB>Titel`, Format `%series - S%season - %pos`; in der Umbenennungsvorschau `Show - S02 - Titel.mkv` erwarten. Vorschau abbrechen und die Originaldatei auf Unverändertheit prüfen.

## Sprachauswahl direkt in der automatischen Vorschau

Der automatische Datenvorschlagsdialog besitzt jetzt eine eigene Auswahl **Sprache: Deutsch / English / 日本語**, die mit der zuletzt gespeicherten Metadatensprache vorausgefüllt ist. Beim Start einer Suche wird diese Auswahl als neue gemeinsame Präferenz gespeichert. Nach einer Änderung im Dialog werden frühere Suchtreffer und Episodentitel geleert; **Erneut suchen** lädt neue Treffer mit der ausgewählten Sprache.

TMDB kann mit eigenem API-Key deutsche und englische Episodentitel liefern und nutzt seinen DE/EN-Fallback. Für Japanisch ist die TMDB-Lokalisierung in der derzeitigen Implementierung **nicht** verfügbar; TVmaze gibt ausschließlich Originaltitel zurück. Das Ergebnisraster kennzeichnet deshalb pro Folge die tatsächliche Sprache und gegebenenfalls `Fallback für de`/`Fallback für ja` bzw. `Original/unbekannt`. Eine Zielsprache wird nie als tatsächlich gelieferte Sprache ausgegeben, wenn die Quelle sie nicht bestätigt.

**Windows-GUI-Nachtests:** Zunächst Deutsch auswählen und suchen, dann Englisch umschalten; vorherige Treffer/Zuordnungen müssen verschwinden und die erneute Suche muss die gewählte Sprache benutzen. Dialog schließen/erneut öffnen: letzte Suchsprache muss vorausgewählt sein. Ohne TMDB-Key muss TVmaze weiterhin funktionieren und Original/unbekannt anzeigen. 日本語 prüfen: keine Behauptung, dass TMDB japanische Episodentitel liefert.
