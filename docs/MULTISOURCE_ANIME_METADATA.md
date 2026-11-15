# Mehrsprachige automatische Episodenquellen – Entwicklungsstand

**Entwicklungsbranch:** `modernization/metadata-providers` (Draft-PR #1).
**Ziel:** Jikan, AniZip, TVmaze und optional TMDB in der automatischen, editierbaren Episodenvorschau. **Kein automatisches Umbenennen, keine stille Vermischung von Episodennummerierung.**

## Quellen und Identitäten

| Quelle | Seriensuche | Episodentitel | Numerierung | Sprache |
|---|---|---|---|---|
| TMDB (eigener Key) | TMDB TV-Suche | lokale Staffel/Folge | Explizites S/E der TMDB-Staffel | 11 konfigurierbare TMDB-Lokalisierungen; en-US-Fallback |
| TVmaze (ohne Key) | TVmaze Shows | Quelle liefert meist Original-/englische Titel | Explizites S/E von TVmaze | `und`, keine unbelegten deutschen Übersetzungen |
| Jikan v4 / MyAnimeList (ohne Key) | Jikan Anime-Suche via `mal_id` | Jikan `title` bzw. `title_japanese` | Episodenkennungen **nur innerhalb eines MAL-Animeeintrags**; als lokale S01 interpretiert und **manuell zu bestätigen** | `ja` nur bei tatsächlichem `title_japanese`, sonst `und` |
| AniZip (ohne Key) | Nur aus Jikan-MAL-Treffern mit expliziter MAL-ID; kein blindes Textmatching | `title[lang]` per AniZip | **Nur wenn seasonNumber UND episodeNumber explizit im Datensatz stehen**, nicht aus dem JSON-Schlüssel oder der Array-Reihenfolge | Zielsprache, dann English, Japanese, Romaji; tatsächliche Sprache sichtbar |

Die AniZip-Abfrage benutzt `/v1/episodes?mal_id=<numeric-id>`; die Antwort muss **dieselbe `mappings.mal_id`** enthalten wie die ausgewählte Serie. Eine abweichende/fehlende Identität blockiert die Quelle. Vorkommende AniZip-Staffelnummern können TVDB-Nummerierung entsprechen; bitte **nicht** mit MAL-Episoden- oder anderen Staffelkonventionen verwechseln.

AniList kann bekannte alternative englische, japanische und Romaji-Serientitel liefern. Diese **nachgewiesenen Aliasse** werden für höchstens drei Wiederholungssuchen bei TVmaze und Jikan verwendet, wenn kein plausibler direkter Treffer existiert. AniZip-Kandidaten entstehen nur aus den Jikan-Ergebnissen mit MAL-ID. Alle Aliastreffer und alle Jikan-/AniZip-Serien erfordern ausdrückliche Auswahl.

## Sprachen

Im automatischen Dialog und bei TMDB stehen Deutsch (`de-DE`), Englisch (`en-US`), Japanisch (`ja-JP`), Französisch (`fr-FR`), Spanisch (`es-ES`), Italienisch (`it-IT`), Portugiesisch (`pt-PT`), Polnisch (`pl-PL`), Koreanisch (`ko-KR`), Chinesisch (`zh-CN`) und Türkisch (`tr-TR`) zur Auswahl.

TMDB fragt die entsprechende Sprache ab und verwendet vorhandenes Englisch als Fallback, falls kein lokalisierter Titel vorliegt. Ein numerisch passender englischer Titel wird **nicht fälschlich als Übersetzung** ausgegeben. AniZip verwendet vorhandene Übersetzungen (darunter `pt-BR`, `zh-Hans`/`zh-Hant`) und kennzeichnet die tatsächliche Sprache. Jikan garantiert außer dem separaten japanischen Feld keine vollständige Mehrsprachigkeit; TVmaze bietet keine Titelübersetzung.

**Romaji / ja-Latn ist eine Umschrift**, nicht dasselbe wie ein japanischer Episodentitel in japanischer Schrift. Der Status markiert das als Fallback.

## Priorisierung und Freigabe

Vorschläge werden erst nach Titelähnlichkeit, dann nach potentiellem Sprachumfang der Quelle sortiert: TMDB > AniZip > Jikan / TVmaze (je nach Zielsprache). Dies ist nur **Sortierung**, keine Behauptung tatsächlicher Übersetzungsverfügbarkeit und **keine Datenfusion**.

Der Nutzer wählt eine Serienidentität und damit einen Anbieter ausdrücklich. Die Zuordnung zu Dateien erfolgt ausschließlich über **eindeutige Staffel-/Folgenpaare**. Fehlende Nummern, widersprüchliche Zählweisen, Specials und lokale Duplikate blockieren die Übernahme. Eine vollständige, eindeutig zugeordnete Vorschlagsliste wird zunächst an den Dateneditor übergeben; Dateien werden erst über die separate sichere Rename-Vorschau geändert.

Da MAL-Animeeinträge häufig einzelne Staffeln/Serienabschnitte repräsentieren, ist eine Jikan-Staffel **nicht automatisch gleich** einer TVmaze/TMDB/TVDB-Staffel. Eine Umrechnung der Nummerierung wird nicht geraten.

## Tests und Einschränkungen

Offline-Regressionen in `tests/EpisodeNames.Metadata.Tests/AnimeEpisodeSourcesChecks.cs` prüfen Jikan-Suche mit MAL-ID, Paginierung, japanische Titelauswahl, AniZip-MAL-ID-Prüfung, Staffel-/Folgenwerte, Fehlende Werte, Spezialfolgen, regionale Sprachvarianten sowie sichere Zuordnung trotz anders sortierter Providerliste. `MetadataSelectionChecks` deckt alle TMDB-Lokalisierungen und Sprachfallbacks ab.

**Manuelle GUI-Abnahme bleibt erforderlich:**
- Suchordner `Naruto` mit `Naruto.S01E01.mkv`: Jikan-Kandidaten, daraus abgeleitete AniZip-Kandidaten, Quelle und Sprache sichtbar; Jikan/AniZip nicht unbemerkt vorauswählen
- AniZip-Treffer nur nach expliziter Wahl abfragen; fehlende/abweichende `mal_id` darf nichts übernehmen
- Wechsel Deutsch → Französisch → Japanisch mit **Erneut suchen**; vorherige Vorschläge müssen geleert sein, echte Sprache/Fallback pro Episode prüfen
- S01E01 und S02E03 nicht verwechseln; nicht nummerierte Anime-Dateien bleiben gesperrt
- Vorschlag übernehmen → Dateneditor → Abbrechen; anschließend Rename-Vorschau abbrechen; Dateien bleiben unverändert
- Offline-/API-Fehler von Jikan oder AniZip dürfen TVmaze/TMDB nicht beschädigen

Jikan ist ein inoffizielles MAL-API, unterliegt Rate-Limits und zeitweiligen Verfügbarkeitsproblemen. AniZip beruht auf externen Mappings, die nicht für alle Anime eine eindeutige Staffel- und Episodennummer bieten. **Grüne Offline-Tests sind kein Beleg für aktuell erfolgreiche Live-API-Antworten.**

**Quellen zur API-Struktur:** https://github.com/jikan-me/jikan-rest/blob/master/storage/api-docs/api-docs.json und https://github.com/hayase-app/interface/blob/master/src/lib/modules/anizip/types.d.ts (AniZip-Sprachcodes, MAL-ID, Episode-Schema).
