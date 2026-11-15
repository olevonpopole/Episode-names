# Windows-Abnahme und AniSearch-Nachtest – 15.11.2026

## Abgeschlossene manuelle Tests
**Getestete ZIP-Baseline:** `EpisodeNames-Windows-Test-af2dee0e.zip`
**Commit:** `af2dee0ea4a406572190d971d682b837d0fd2e1c`
**Befundquelle:** Nutzer-Rückmeldungen im Episode-Names-Testdialog; kein automatischer GUI-Test und keine unbelegten Live-API-Annahmen.

| ID | Nutzerbestätigung | Ergebnis |
| --- | --- | --- |
| M01 | Nur AniSearch, TVmaze und TMDB sichtbar | PASS |
| M02-A | AniSearch: nach Suche und erneutem Öffnen bleibt English eingestellt | PASS |
| M03 | AniSearch → TVmaze → AniSearch sowie erneutes Öffnen behalten English | PASS |
| M04 | AniSearch findet die Serie, danach Fehlermeldung „Bitte eine gültige HTTPS-URL einer AniSearch-Anime-Serie eingeben“ | FAIL |
| TVmaze | Episodendaten funktionieren laut ausdrücklicher Nutzerbestätigung | PASS |
| TMDB Live | Kein persönlicher API-Key vorhanden | NOT_RUN |
| R07 | 3 Dateien, 2 Datenzeilen: Ja/Nein funktioniert; Vorschau enthält 2 Zuordnungen | PASS |
| R11 | 3 Dateien, 4 Datenzeilen: Ja/Nein funktioniert; Vorschau enthält 3 Zuordnungen und ignoriert vierte Datenzeile | PASS |

Die allgemeinen Umbenennungsfunktionen wurden außerdem vor dieser Prüfrunde vom Nutzer als funktionierend bestätigt; es werden keine zusätzlichen Einzeltest-PASS-Einträge daraus abgeleitet.

## AniSearch – Korrekturrunde

**Reproduktion:** Eine bei AniSearch gefundene Naruto-Serie wird vom eigenen URL-Validator als ungültig zurückgewiesen. Der ältere Validator erwartete `/anime/<ID>/<Name>`; AniSearch-Treffer können jedoch `/anime/<ID>,<Name>` enthalten.

**Bereits ausgeführte Codekorrektur, noch nicht manuell abgenommen:**
- `Rename.9/Metadata/AnisearchUrl.cs`: Unterstützung für ID-Komma-Slug-Treffer und traditionelle AniSearch-Formate; strikte HTTPS-/Host-Prüfung.
- `Rename.9/View/SearchWebsite.cs`: URL-Validierung und Suchergebnis-Verknüpfung auf denselben Validator umgestellt.
- `tests/EpisodeNames.Metadata.Tests/AnisearchUrlChecks.cs`: positive URL-Beispiele und Ablehnung ungültiger bzw. fremder Hosts.

**CI-Gate:** Commit `6c008c94d037c1036ecbd6d2728f4e79e419817d` wurde über GitHub Actions **Push und PR** erfolgreich gebaut und getestet:
- https://github.com/olevonpopole/Episode-names/actions/runs/43732033991
- https://github.com/olevonpopole/Episode-names/actions/runs/43732038924
- Der Push-Run hat ein portables ZIP `EpisodeNames-Windows-Test-6c008c94d037c1036ecbd6d2728f4e79e419817d` hochgeladen.

**Noch offenes manuelles Gate:** Diesen neuen Build in einem neuen Ordner entpacken und M04 mit AniSearch/Naruto wiederholen. Ausdrücklich prüfen: Suchtreffer, URL, Klick auf OK, Episodendaten im Editor. Danach optional mit Englisch wiederholen. Nur wenn diese Schritte funktionieren, darf M04 als PASS markiert werden. Falls Suche funktioniert, aber der Abruf nicht, Fehlermeldung und angezeigte URL protokollieren, ohne sensible Daten preiszugeben.

**TMDB:** Live-Test bleibt mangels Key offen; Offline-Providerchecks sind kein Ersatz für die manuelle Prüfung. Das ist kein Release-Blocker-Fehlerbefund, sondern eine ausdrücklich nicht durchgeführte Live-Prüfung.

## Release-Gates
- Kein Merge und kein Release beantragt. PR #1 bleibt Draft.
- `master` bleibt unverändert und geschützt.
- Hauptziel der nächsten Prüfrunde: nur M04 im neuen Test-Build nachtesten; bei Fehlschlag Ursache untersuchen, einen neuen Fix mit Offline-Gegenprobe erstellen.
- Die zuvor bestätigten R07/R11/M01/M02-A/M03 behalten ihre Baseline-PASS-Bewertung. Sie ersetzen keine zusätzlichen regressiven UI-Tests einer neuen Version.
