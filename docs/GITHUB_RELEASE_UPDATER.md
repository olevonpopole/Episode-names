# GitHub Releases: portabler, zustimmungspflichtiger Updater

**Status:** Implementiert im Entwicklungsbranch; Offline-/Windows-CI-Tests für API-/ZIP-/Token-Logik; **echter privater Release-Download und Update-Neustart auf dem Nutzer-PC noch nicht abgenommen.** Kein Merge/Release.

## Vorbereitung eines GitHub Releases

1. Einen stabilen GitHub-Release-Tag der Form `v2.0.0` erzeugen (SemVer, keine Prerelease).
2. Das **portable Windows-ZIP** als Release-Asset hochladen; bevorzugter Name `EpisodeNames-Windows-Portable.zip`, alternativ `Anwendung.zip` oder `EpisodeNames-Windows-*.zip`.
3. `Episode-Names.exe` muss direkt in der Wurzel des ZIP liegen; erforderliche DLLs und Konfigurationsdateien sind relativ dazu enthalten.
4. GitHub muss für das Asset im Releases-API-Feld `digest` einen `sha256:<64 Hexzeichen>` liefern. **Ohne Digest keine automatische Installation**; vorab das Asset und seine Veröffentlichung prüfen.
5. Die ausführende Benutzerin/der Benutzer muss Schreibrechte im Programmordner haben. Eine portable Installation im eigenen Benutzerordner benötigt keine Administratorrechte.
6. Für private Repositories: unter **GitHub-Releases-Einstellungen** den Namen `Besitzer/Repository` und einen **fine-grained Personal Access Token** nur mit `Contents: Read` (und zugehörigem Repository-Zugriff) eintragen. Token nicht im Repository, Release oder Installer hinterlegen. Die Anwendung speichert das Token per Windows DPAPI ausschließlich für den angemeldeten Windows-Benutzer.

## Ablauf

- Beim Start einer **regulären Anwendung** wird `GET /repos/{owner}/{repo}/releases/latest` asynchron mit 12-Sekunden-Timeout abgefragt.
- Das Menü **Update** löst die Prüfung manuell aus.
- Es wird nur ein **neueres stabiles Release** angezeigt, keine Downgrades oder Prereleases.
- Nach ausdrücklicher Bestätigung wird das passende GitHub-Releases-ZIP mit Bearer-Token (bei privatem Repo) heruntergeladen. Das Download-Limit beträgt 128 MiB.
- Das ZIP wird vor jeder Installation anhand des auf GitHub gemeldeten SHA-256-Digests geprüft; die ZIP-Extraktion prüft Pfad-Traversal, doppelte Pfade, Symlinks, Dateianzahl und Entpackungsgröße. Es wird nur in einen eigenen temporären Ordner entpackt.
- Erst nach **zweiter Zustimmung** wird ein lokal generierter PowerShell-Installer gestartet. Er wartet auf das vollständige Programmende, sichert bestehende Zieldateien im temporären Backup, kopiert nur Dateien aus dem geprüften Paket und startet die neue EXE. Bei einem Kopierfehler werden die gesicherten Dateien zurückkopiert; Fehler stehen unter `update-error.txt` im temporären Verzeichnis.
- **Keine automatische Administrator-Elevation**, kein Herunterladen oder Ausführen fremder Installationsskripte. Unternehmensrichtlinien können PowerShell oder Schreibzugriff dennoch blockieren; dann bleibt der bisherige Programmstand erhalten.
- Die expliziten GitHub-Actions-**Testpakete** enthalten `PortableTestBuild=true`. Bei ihnen ist **jede Selbstinstallation gesperrt**, selbst wenn eine neue Release-Version existiert; über das Menü kann nur die Verbindung/Version geprüft werden.

## Grenzen und Sicherheitsnachweise

- GitHub-Release-Digest bestätigt die Übertragungsintegrität, ist **keine unabhängige Signatur oder Publisher-Authentifizierung**. Zusätzliche Code-Signierung wäre ein sinnvoller Release-Gate.
- Der Zustand der privaten Benutzer-Token oder ihre Gültigkeit werden nicht an ChatGPT übermittelt.
- Ohne veröffentlichtes Release mit gültigem Digest oder ohne Token-Berechtigung kann die Funktion keinen Installer ausführen.
- Offline-Regressionen prüfen GitHub-Auth-Header mit Fake-Token, Release-Version, SHA-256, ZIP-Slip-Schutz, fehlende EXE, DPAPI-Roundtrip und favorisierte Quellen. **Echte private GitHub-API und tatsächliche Installation müssen separat auf Windows mit synthetischen, isolierten Daten verifiziert werden.**

## Empfohlener Windows-Test

1. GitHub-Releases-Einstellungen öffnen, `olevonpopole/Episode-names` eintragen und – nur bei tatsächlich privatem Repo – Token hinterlegen.
2. Im Testbuild **Update** drücken. Eine neue Version wird höchstens angezeigt; die bestehende EXE darf sich nicht ersetzen.
3. Mit einem gesonderten, isolierten, nichtproduktiven Release-Pilot testen: Paket mit gültigem Digest, neuerem Tag und Testdateien; Speicherort unter einem benutzerschreibbaren Testordner.
4. Prüfen: regulärer Neustart, Backup-Rollback bei simuliertem Kopierfehler, keine Änderungen bei fehlendem Digest, kein Zugriff auf ungeprüfte Fremdreleases.

# Suche und Favoriten

Im automatischen Vorschlagsdialog können **TVmaze, Jikan, AniZip und TMDB** per Checkbox als Favoriten gespeichert werden (voreingestellt: nur TVmaze für schnelles Startverhalten). Für AniZip ist bei der Seriensuche ein Jikan-MAL-ID-Lookup technisch nötig, aber Jikan wird nicht zusätzlich als Ergebnisquelle gezeigt, sofern nicht ausgewählt. Suchlauf: **maximal 18 Sekunden insgesamt**, je primärem Anbieter 6 Sekunden; Alias-Fallback höchstens ein Name; **Suche stoppen** beendet laufende Anfragen und lässt bereits vorhandene Serienkandidaten stehen. Ein separat gewählter Episodenabruf hat ein 20-Sekunden-Limit und ist ebenfalls stoppbar. Quellenänderungen werden direkt gespeichert.

Die **manuelle Suche** bietet weiterhin AniSearch/TVmaze/TMDB und zusätzlich **Jikan** und **AniZip**. Anime-Einträge werden über explizite MAL-IDs gewählt. AniZip zeigt nur tatsächlich vorhandene gemappte Staffeln; Jikan kennzeichnet einen MAL-Animeeintrag als lokale Staffel 1. Verschiedene Nummerierungen werden nicht automatisch verschmolzen. Die Wahl und die Sprache müssen weiterhin vor der sicheren Umbenennungsvorschau geprüft werden.

Manuelle Nachtests: nur TVmaze aktiv → keine Jikan-Abfrage; TVmaze+AniZip → ein MAL-Lookup; Anbieter offline → timeout und nutzbare Kandidaten statt Hänger; Suche stoppen → UI wieder bedienbar; Favoriten bei erneutem Öffnen erhalten; manuell Jikan/AniZip samt Staffelwahl, Dateneditor und sicherem Abbruch durchspielen.
