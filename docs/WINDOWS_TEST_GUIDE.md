# Episode Names – Windows-Testanleitung (Entwicklungsbranch)

**Status:** Vorab-Testversion, **kein Release**, kein Merge nach `master`. Alle echten Dateioperationen nur auf den beiliegenden **Testdateien** ausführen. Ein Windows-GUI- und Live-API-Test kann nicht allein durch GitHub Actions ersetzt werden.

## 1. Testversion starten

1. In GitHub Actions den erfolgreichen **Push**-Workflow des Branches `modernization/metadata-providers` öffnen und das Artifact **EpisodeNames-Windows-Test-…** herunterladen.
2. ZIP vollständig in einen eigenen Ordner entpacken, beispielsweise `C:\Users\<Benutzer>\Downloads\EpisodeNames-Test\`. Programm **nicht direkt innerhalb der ZIP-Vorschau** starten.
3. `START_HIER.cmd` doppelklicken. Alternativ `Episode-Names.exe` öffnen und im Hauptfenster ausdrücklich einen der beiliegenden `Testdaten`-Ordner auswählen.
4. Für die Testversion ist Windows mit **.NET Framework 4.8** erforderlich. Installation, Administratorrechte oder Python sind nicht notwendig, wenn das Framework bereits vorhanden ist.
5. Die eingebauten `.mkv`-Dateien sind kleine **Text-Platzhalter**, keine abspielbaren Videos. Testordner nach Änderungen am besten durch erneutes Entpacken wiederherstellen.

**Sicherheitsmerkmale:** Die Test-`.exe.config` enthält `PortableTestBuild=true` und deaktiviert so den alten automatischen Updater, damit er diesen Build nicht durch eine ältere Anwendung ersetzt. Das Testpaket enthält **keinen TMDB-Key**. Der eigene API-Key wird bei Bedarf erst in der App unter dem aktuellen Windows-Benutzer per DPAPI gespeichert. Microsoft SmartScreen oder eine Unternehmensrichtlinie kann unsignierte Testprogramme blockieren; keine Sicherheitseinstellungen oder Unternehmensrichtlinien dafür umgehen.

## 2. Manuelle Prüfmatrix

Nach jedem Versuch den tatsächlichen Dateibestand im Windows-Explorer überprüfen. **Zuerst Vorschau abbrechen, dann bei einem gesonderten Lauf bewusst bestätigen.** Wenn ein Test fehlschlägt, keine weiteren Dateioperationen im betroffenen Ordner durchführen.

| ID | Bereich | Schritte | Erwartung |
|---|---|---|---|
| R01 | Start | `START_HIER.cmd` starten | Hauptfenster öffnet sich auf `Testdaten\01_Nummerierung`; keine Installation oder unerwartete Update-Aktion |
| R02 | Vorschau/Abbruch | Mit `01_Nummerierung` auf **Nummerierung** klicken, Vorschau lesen, **Abbrechen** | 3 Zuordnungen sichtbar; Originalnamen bleiben unverändert |
| R03 | Bestätigung | Erneut **Nummerierung**, danach **Änderungen ausführen** | 3 Testdateien werden in numerierte Namen umbenannt; keine Überschreibung |
| R04 | Rückgängig | **Wiederherstellen** wählen, Vorschau prüfen und bestätigen | Alle 3 ursprünglichen Namen sind wiederhergestellt |
| R05 | Ziel belegt | Ordner `02_Kollision` auswählen, Menü **Suchen & Ersetzen**, Suche `Alpha`, Ersetzung `Beta`, **Umbenennen** | `Beta.mkv` existiert bereits: Vorschau zeigt Konflikt, Ausführen deaktiviert, Originale bleiben |
| R06 | Windows-Reservat | Ordner `03_Reservierter_Name`, Suchen `Alpha` → Ersetzen `CON` | `CON.mkv` wird als reservierter Windows-Dateiname blockiert |
| R07 | Zu wenig Datenzeilen | Ordner `04_Datenzeilen`, im Menü **Daten → Neue Daten** nur zwei Zeilen eingeben und abschließen; danach `Umbenennen` | 3 Dateien / 2 Zeilen: Vorgang wird vor einer Dateiänderung abgewiesen |
| R08 | Keine Änderungen | Im Ordner `02_Kollision` Suchen & Ersetzen mit einem Text, der in keinem Namen vorkommt | Kein Dateiwechsel; Ausführen bleibt deaktiviert |
| R09 | Reiner Case-Wechsel | In `03_Reservierter_Name` `Alpha` durch `alpha` ersetzen | Sicherheitsblockade statt riskanter Case-only-Umbenennung |
| R10 | Wiederholbarkeit | Nach den Tests die Testordner frisch aus dem ZIP entpacken | Ursprüngliche Platzhalterdateien sind wieder vorhanden |

**Hinweis:** In der aktuellen Sicherheitsversion werden Dateinamen-Tauschaktionen und reine Änderungen der Groß-/Kleinschreibung bewusst abgelehnt. Eine vollständige transaktionale Dateisystem-Garantie ist bei gleichzeitigen Fremdzugriffen nicht möglich. Bei einem I/O-Fehler versucht das Programm die bereits erfolgten Schritte rückgängig zu machen und meldet unvollständige Wiederherstellung.

## 3. TVmaze – Live-Test ohne API-Key

1. Hauptfenster geöffnet lassen; einen Testordner als Pfad ausgewählt lassen.
2. Menü **Suche auf Website** öffnen, Anbieter **TVmaze** wählen.
3. Nach einer bekannten Serie suchen (z. B. `Breaking Bad`), **Enter**, einen Treffer auswählen.
4. Eine konkrete Staffel auswählen. Prüfen, dass die Episodentitel der gewählten Staffel geladen werden.
5. Dialog erneut öffnen und **Alle Staffeln (ohne Specials)** wählen. Format `S01E01`, `S01E02` usw. prüfen.
6. Falls bei der gewählten Serie eine Staffel 0 existiert: **Specials (Staffel 0)** gesondert prüfen.
7. **OK** übernimmt Text in den Dateneditor. Prüfen, dass manuell zugeordnete Episodentitel stimmen. **Keine echten Videodateien umbenennen**.

**Erwartung:** TVmaze funktioniert ohne Registrierung und API-Key. Keine künstlichen absoluten Anime-Episodennummern. Bei fehlenden oder nicht eindeutig nummerierten Einträgen deutliche Warnung oder Auslassung.

## 4. TMDB – Live-Test mit eigenem Schlüssel

1. Auf https://www.themoviedb.org/settings/api einen **persönlichen** TMDB v3 API-Key oder API Read Access Token verwenden. **Nicht in Chat, Issues oder GitHub-Logs posten.**
2. Menü **Suche auf Website**, Anbieter **TMDB (API-Key)**. Schlüssel im verdeckten Feld eingeben und **Sichern** wählen.
3. Sprache für Suche/Episoden auf **Deutsch** setzen, nach einer bekannten Serie suchen und eine Staffel wählen.
4. Prüfen, dass deutsche Titel bevorzugt und für fehlende bzw. generische deutsche Einträge englische Titel übernommen werden. Der Bestätigungsdialog zeigt deutsche, englische und fehlende Episodentitel.
5. Test mit **English** wiederholen und Titel vergleichen. Keine absoluten Anime-Nummern voraussetzen.
6. Eine fehlende oder ungültige TMDB-Anmeldung sollte eine verständliche Fehlermeldung auslösen – **ohne** den Schlüssel anzuzeigen.
7. Optional im TMDB-Bereich **Löschen** wählen und kontrollieren, dass ohne neuen Schlüssel keine TMDB-Suche ausgeführt wird. TVmaze muss weiterhin schlüsselfrei funktionieren.

**Erwartung:** TMDB ist ausschließlich optional; der Schlüssel befindet sich nicht im Testarchiv. Der deutsche/englische Fallback und die Behandlung fehlender Titel sind zudem durch deterministische Offline-Tests abgedeckt. Der Live-Test prüft echte API-Verfügbarkeit und Dialogverhalten.

## 5. Automatisch geprüfte Punkte (GitHub Actions)

Der Job `build` muss erfolgreich abschließen:

- Legacy-Windows-Forms-EXE für **.NET Framework 4.8** bauen.
- Offline-Providerchecks für TVmaze und TMDB ausführen (Suche, Episoden, DE/EN-Fallback, Extras, Authentifizierung, Windows-DPAPI).
- Dateisystem-Checks für ungültige Namen, Zielbelegung, case-insensitive Doppelziele, keine Veränderung bei Konflikten, Wiederherstellung und Rücknahme nach simuliertem Fehler ausführen.
- Portables Testpaket mit `Episode-Names.exe`, `.exe.config`, `HtmlAgilityPack.dll`, Anleitung, Beispiel-Dateien, `BUILD_INFO.txt` und SHA-256-Prüfsummen erzeugen.

**Grenze:** CI verifiziert den Build und automatisierte Checks, aber nicht die Bedienbarkeit der WinForms-Oberfläche mit Maus/Tastatur, reale API-Antworten mit Benutzer-TMDB-Key oder umfassende Abwehr zeitgleicher Dateisystemzugriffe. Das Paket bleibt deshalb ausdrücklich eine Testversion.

## 6. Rückmeldung dokumentieren

Testdatum und Windows-Version: ______________________________

Test-Build-Commit (siehe `BUILD_INFO.txt`): ______________________________

Ergebnisse (R01–R10, TVmaze, TMDB): ______________________________

Auffälligkeiten, exakter Dialogtext, Screenshots ohne API-Key: ______________________________

**Keine Veröffentlichung / kein Merge**, bevor kritische Fehler und die manuellen GUI-Tests geprüft wurden.
