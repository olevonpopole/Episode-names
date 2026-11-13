# Episode Names — Metadata modernization plan

Status (2026-11-13): TVmaze and optional TMDB providers, WinForms integration and offline tests implemented; centralized rename preview still pending. Branch: `modernization/metadata-providers`.

## Goals
- Keep the existing Windows Forms .NET Framework 4.8 application and its file tools working.
- TVmaze must work without registration or an API key.
- TMDB is optional, with a user-supplied key; prefer German episode titles and fall back to English when a translation is absent.
- Support regular television series and anime; preserve explicit season/episode vs absolute numbering choices.
- Never silently rename files based on ambiguous metadata.

## Current code findings
- `Rename.9/View/SearchWebsite.cs` combines UI, language selection, searches and HTML parsing for AniSearch, AniDB, TheTVDB and fernsehserien.de.
- `Rename.9/View/Form1.cs` performs renames via `File.Move` and contains undo-related logic.
- `Rename.9/Episode-Names.csproj` targets .NET Framework 4.8, Windows Forms, and references HtmlAgilityPack 1.11.4.
- `Rename.9/App.config` persists language, secondary language and selected website by numeric index. Migration must preserve legacy settings.

## Proposed design
1. Add immutable normalized `SeriesMatch`, `EpisodeMetadata`, `SeasonMetadata`, `ProviderError` models with provider IDs, language, season, episode, absolute number (nullable), special flag and source title.
2. Define `IEpisodeMetadataProvider`: search shows, list seasons, get episodes; expose capability flags (languages, API key required, anime support).
3. Implement `TvMazeProvider` using documented JSON endpoints, respectful rate limiting, retries only for transient errors, cancellation, and HTTP timeouts.
4. Implement optional `TmdbProvider` with user-managed key, explicit locale selection, per-episode fallback to English and missing-title indicators.
5. Add provider orchestration: TVmaze default without key; TMDB selectable with key; no cross-provider episode-number mixing without explicit matching/confirmation.
6. Adapt `SearchWebsite` to the normalized models; retain existing manual text insertion and renaming workflows.
7. Add a preview/planner that validates illegal Windows characters, collisions (including case-insensitive), duplicates, destination existence, and numbering gaps before any `File.Move`.
8. Add test fixtures for ordinary TV series, anime absolute numbering, split seasons, specials, German missing translation, API failures and rate limits.

## Execution phases
- A: DONE — Provider interface, models, HTTP infrastructure and offline test runner.
- B: DONE — TVmaze keyless search and episodes; optional TMDB v3/Read Token credentials, German-English episode fallback and WinForms selection.
- C: PARTIAL — UI selection for TVmaze with explicit season choice; anime matching and central safe rename preview remain pending.
- D: CI Windows build, regression checks, documentation and release.

## Acceptance criteria
- Fresh installation with no API key can search and fetch English episode names via TVmaze.
- With TMDB configured, German episode names are preferred with English fallback, with source/language visible.
- Users can select the correct show and numbering scheme before renaming.
- No file changes on failed validation; preview shows exact source-to-destination mapping.
- Existing format strings, settings, manual editing and non-metadata rename tools continue working.
- Build and tests run on a Windows GitHub Actions runner.

## Security and compatibility
- Do not commit API keys or log credentials; store per-user settings locally.
- Treat API responses as untrusted and sanitize filenames.
- Avoid website scraping as a primary metadata path.
- Do not migrate frameworks or redesign the entire UI in the first implementation phase.

## Implemented and checked
- New files in `Rename.9/Metadata/` introduce a provider contract, normalized models and `TvMazeProvider`.
- Existing WinForms `SearchWebsite` can search TVmaze, select a show and fetch one season or explicitly all regular episodes.
- TVmaze show and episode IDs remain provider-specific; absolute numbering is intentionally left unknown.
- Fresh installs select TVmaze (index 4); saved indices 0–3 still map to the legacy providers.
- Offline fixture tests cover show URL validation, search, season/episode mapping, specials and 404 handling.
- GitHub Actions builds the existing .NET Framework 4.8 application and executes the offline checks on Windows.
- Manual GUI validation on Windows and live provider smoke tests are still recommended before merging/releasing.

## Optional TMDB implementation
- `TmdbProvider` implements the same provider contract as TVmaze, with German and English locales and per-episode fallback.
- A per-season method avoids querying unrelated seasons when a single season is requested.
- `TmdbCredentialStore` encrypts the user's credential with Windows DPAPI (CurrentUser); there is no repository credential or global API key.
- UI supports entering, saving and deleting the key, explicit season choice and a language-count confirmation before metadata import.
- Tests verify authenticated requests, localized/English placeholder fallback, missing titles, specials and invalid credentials using offline fixtures.
- A manual Windows GUI and live API smoke test still need to be performed before release.
