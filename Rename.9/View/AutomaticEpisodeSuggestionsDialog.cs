using Episode_Names.Metadata;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Episode_Names.View
{
    /// <summary>
    /// Explicit opt-in lookup: online search runs asynchronously, file identity
    /// matching is strict, and only user-approved data goes to the data editor.
    /// </summary>
    internal sealed class AutomaticEpisodeSuggestionsDialog : Form
    {
        private sealed class Candidate
        {
            internal IEpisodeMetadataProvider Provider;
            internal SeriesMatch Series;
            internal int Rank;
            public override string ToString()
            {
                return Provider.DisplayName + " | " + Series.Name +
                    (Series.PremiereYear.HasValue ? " (" + Series.PremiereYear.Value + ")" : "");
            }
        }

        private readonly string folder;
        private readonly string query;
        private readonly List<EpisodeFileIdentity> files;
        private readonly List<Candidate> candidates = new List<Candidate>();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly Label status;
        private readonly TextBox queryBox;
        private readonly ComboBox languageChoice;
        private readonly Button searchAgain;
        private readonly ListBox matches;
        private readonly DataGridView preview;
        private readonly Button load;
        private readonly Button accept;
        private readonly Button cancel;
        private readonly Label notice;
        private bool busy;
        private IReadOnlyList<EpisodeSuggestion> current;
        private List<string> acceptedRows;

        internal IReadOnlyList<string> AcceptedRows { get { return acceptedRows; } }

        internal AutomaticEpisodeSuggestionsDialog(string folderPath, IReadOnlyList<string> fileNames)
        {
            folder = folderPath;
            query = FolderSeriesName.Suggest(folderPath);
            string parent = new DirectoryInfo(folderPath).Name;
            files = fileNames.Select(name => EpisodeFileMatcher.Identify(name, parent)).ToList();

            Text = "Automatische Episoden-Datenvorschläge";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 580);
            Size = new Size(1040, 660);
            TopMost = Properties.Settings.Default.Foreground;

            status = new Label {
                Dock = DockStyle.Top, Height = 44, Padding = new Padding(9),
                Text = "Suche wird vorbereitet …" };
            notice = new Label {
                Dock = DockStyle.Top, Height = 46, Padding = new Padding(9),
                Text = "Online-Abgleich nur nach ausdrücklich ausgewählter Serie. " +
                    "Die vorgeschlagenen Episodentitel sind editierbar." };

            var searchBar = new FlowLayoutPanel {
                Dock = DockStyle.Top, Height = 39, Padding = new Padding(7, 4, 7, 4) };
            searchBar.Controls.Add(new Label { Text = "Serientitel:", Width = 82,
                TextAlign = ContentAlignment.MiddleLeft, Height = 26 });
            queryBox = new TextBox { Width = 372, Text = query };
            searchBar.Controls.Add(new Label { Text = "Sprache:", Width = 57,
                TextAlign = ContentAlignment.MiddleLeft, Height = 26 });
            languageChoice = new ComboBox {
                Width = 115, DropDownStyle = ComboBoxStyle.DropDownList
            };
            languageChoice.Items.Add(new KeyValuePair<string, string>("de", "Deutsch"));
            languageChoice.Items.Add(new KeyValuePair<string, string>("en", "English"));
            languageChoice.Items.Add(new KeyValuePair<string, string>("ja", "日本語"));
            languageChoice.DisplayMember = "Value";
            languageChoice.ValueMember = "Key";
            string preferred = MetadataSelection.CanonicalLanguage(
                Properties.Settings.Default.PreferredMetadataLanguage);
            for (int i = 0; i < languageChoice.Items.Count; i++)
            {
                if (((KeyValuePair<string, string>)languageChoice.Items[i]).Key == preferred)
                {
                    languageChoice.SelectedIndex = i;
                    break;
                }
            }
            if (languageChoice.SelectedIndex < 0) languageChoice.SelectedIndex = 0;
            languageChoice.SelectedIndexChanged += (s, e) => {
                // Never allow a preview generated for another language to be accepted.
                current = null;
                accept.Enabled = false;
                preview.Rows.Clear();
                matches.Items.Clear();
                candidates.Clear();
                status.Text = "Zielsprache geändert. Bitte „Erneut suchen“ wählen. " +
                    "TVmaze liefert Originaltitel; TMDB unterstützt Deutsch und Englisch.";
            };
            searchAgain = new Button { Text = "Erneut suchen", Width = 120 };
            searchAgain.Click += async (s, e) => await SearchAsync();
            searchBar.Controls.Add(queryBox);
            searchBar.Controls.Add(languageChoice);
            searchBar.Controls.Add(searchAgain);

            matches = new ListBox { Dock = DockStyle.Top, Height = 120 };
            matches.SelectedIndexChanged += (s, e) => load.Enabled = !busy && matches.SelectedItem != null;

            load = new Button { Text = "Ausgewählte Serie abgleichen", Width = 215 };
            load.Click += async (s, e) => await LoadSelectedAsync();
            accept = new Button { Text = "Datenvorschläge übernehmen", Width = 220, Enabled = false };
            accept.Click += (s, e) => AcceptRows();
            cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Width = 100 };
            var actions = new FlowLayoutPanel {
                Dock = DockStyle.Bottom, Height = 47,
                FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
            actions.Controls.Add(cancel);
            actions.Controls.Add(accept);
            actions.Controls.Add(load);

            preview = new DataGridView {
                Dock = DockStyle.Fill, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false, MultiSelect = false };
            preview.Columns.Add(new DataGridViewTextBoxColumn {
                HeaderText = "Datei", Name = "file", ReadOnly = true, FillWeight = 42 });
            preview.Columns.Add(new DataGridViewTextBoxColumn {
                HeaderText = "Staffel / Episode", Name = "identity",
                ReadOnly = true, FillWeight = 16 });
            preview.Columns.Add(new DataGridViewTextBoxColumn {
                HeaderText = "Episodentitel (editierbar)", Name = "title", FillWeight = 43 });
            preview.Columns.Add(new DataGridViewTextBoxColumn {
                HeaderText = "Sprache", Name = "language", ReadOnly = true, FillWeight = 12 });
            preview.Columns.Add(new DataGridViewTextBoxColumn {
                HeaderText = "Status", Name = "status", ReadOnly = true, FillWeight = 29 });
            preview.CellEndEdit += (s, e) => UpdateAcceptStatus();

            var box = new Panel { Dock = DockStyle.Fill };
            box.Controls.Add(preview);
            box.Controls.Add(matches);
            box.Controls.Add(searchBar);
            box.Controls.Add(notice);
            box.Controls.Add(status);
            Controls.Add(box);
            Controls.Add(actions);
            AcceptButton = load;
            CancelButton = cancel;

            Shown += async (s, e) => await SearchAsync();
            FormClosing += (s, e) => cancellation.Cancel();
        }

        private static string Normalize(string input)
        {
            return Regex.Replace((input ?? "").ToLowerInvariant(),
                @"[^\p{L}\p{Nd}]+", "");
        }

        private async Task SearchAsync()
        {
            string searchTerm = queryBox.Text.Trim();
            if (files.Count == 0 || string.IsNullOrWhiteSpace(searchTerm))
            {
                status.Text = "Kein geeigneter Ordner bzw. keine Dateien gefunden.";
                return;
            }
            SetBusy(true);
            candidates.Clear();
            current = null;
            preview.Rows.Clear();
            matches.Items.Clear();
            accept.Enabled = false;
            string language = MetadataSelection.CanonicalLanguage(
                ((KeyValuePair<string, string>)languageChoice.SelectedItem).Key);
            // Consistent with the manual provider dialog: remember the language
            // when a search starts, not on an uncommitted selection change.
            Properties.Settings.Default.PreferredMetadataLanguage = language;
            Properties.Settings.Default.Save();
            var providers = new List<IEpisodeMetadataProvider>();
            string tmdbError = "";
            try
            {
                string key = TmdbCredentialStore.Unprotect(Properties.Settings.Default.TmdbCredential);
                if (!string.IsNullOrWhiteSpace(key))
                    providers.Add(new TmdbProvider(key, language));
            }
            catch (Exception)
            {
                tmdbError = "Gespeicherter TMDB-Schlüssel nicht lesbar. ";
            }
            providers.Add(new TvMazeProvider());

            status.Text = "Suche: " + searchTerm + " | Zielsprache: " + language.ToUpperInvariant() +
                " | TVmaze: Originaltitel; TMDB: DE/EN mit Key" +
                (language == "ja" ? " (bei JA keine TMDB-Japanisch-Unterstützung)" : "") + ".";
            string errors = tmdbError;
            int rawResults = 0;
            foreach (IEpisodeMetadataProvider provider in providers)
            {
                try
                {
                    IReadOnlyList<SeriesMatch> results = await provider.SearchSeriesAsync(
                        searchTerm, cancellation.Token);
                    if (IsDisposed || cancellation.IsCancellationRequested) return;
                    rawResults += results.Count;
                    foreach (SeriesMatch match in results.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
                    {
                        int score = Normalize(match.Name) == Normalize(searchTerm) ? 100 :
                            Normalize(match.Name).Contains(Normalize(searchTerm)) ||
                            Normalize(searchTerm).Contains(Normalize(match.Name)) ? 40 : 0;
                        // Show even low-confidence results. TVmaze may use the
                        // English/original name for a German folder name.
                        // These are suggestions, never automatically selected.
                        candidates.Add(new Candidate { Provider = provider, Series = match, Rank = score });
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    // No credentials or HTTP endpoints in the visible error string.
                    errors += provider.DisplayName + ": " + ex.GetType().Name + ". ";
                }
            }
            // When a translated/localized folder title is not indexed by
            // TVmaze, ask AniList for documented alternate anime names, then
            // retry TVmaze with up to three aliases. Do not guess translations.
            // Aliased results are always manual choices, never auto-approved.
            if (candidates.Count == 0 || candidates.All(x => x.Rank == 0))
            {
                try
                {
                    IReadOnlyList<string> aliases = await new AnimeTitleAliases()
                        .FindAsync(searchTerm, cancellation.Token);
                    IEpisodeMetadataProvider tvmaze = providers.First(p => p.Id == "tvmaze");
                    foreach (string alias in aliases.Take(3))
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        IReadOnlyList<SeriesMatch> suggestions = await tvmaze.SearchSeriesAsync(
                            alias, cancellation.Token);
                        if (IsDisposed || cancellation.IsCancellationRequested) return;
                        rawResults += suggestions.Count;
                        foreach (SeriesMatch suggestion in suggestions.Where(x =>
                            !string.IsNullOrWhiteSpace(x.Name)))
                        {
                            if (!candidates.Any(x => x.Provider.Id == tvmaze.Id &&
                                    x.Series.Id == suggestion.Id))
                                candidates.Add(new Candidate {
                                    Provider = tvmaze, Series = suggestion, Rank = 15
                                });
                        }
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception)
                {
                    // The optional alias index is a hint, not a requirement.
                    errors += "Alternative Anime-Titel konnten nicht geladen werden. ";
                }
            }

            candidates.Sort((a, b) => b.Rank != a.Rank ?
                b.Rank.CompareTo(a.Rank) :
                string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase));

            matches.Items.Clear();
            matches.Items.AddRange(candidates.Cast<object>().ToArray());
            SetBusy(false);
            if (candidates.Count == 0)
                status.Text = rawResults == 0
                    ? "Keine Treffer von den Datenquellen für „" + searchTerm +
                      "“. Anderen/englischen Titel eingeben und erneut suchen. " + errors
                    : "Keine verwendbaren Treffer; anderen Titel suchen. " + errors;
            else
                status.Text = "Gefundene Serien: " + candidates.Count +
                    " (inkl. unsicherer Treffer). Bitte selbst prüfen und auswählen. " + errors;

            // Auto-fetch only when exactly one match is a perfect title match.
            // Multiple provider hits or aliases need explicit user selection.
            if (candidates.Count == 1 && candidates[0].Rank == 100)
            {
                matches.SelectedIndex = 0;
                await LoadSelectedAsync();
            }
        }

        private async Task LoadSelectedAsync()
        {
            Candidate selected = matches.SelectedItem as Candidate;
            if (selected == null) return;
            current = null;
            accept.Enabled = false;
            preview.Rows.Clear();
            SetBusy(true);
            status.Text = "Lade Episodendaten von " + selected.Provider.DisplayName +
                " für „" + selected.Series.Name + "“ …";
            try
            {
                IReadOnlyList<EpisodeMetadata> episodes;
                TmdbProvider tmdb = selected.Provider as TmdbProvider;
                if (tmdb != null)
                {
                    var seasonNumbers = files.Where(f => f.IsUnambiguous)
                        .Select(f => f.Season.Value).Distinct().OrderBy(i => i).ToList();
                    var all = new List<EpisodeMetadata>();
                    foreach (int season in seasonNumbers)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        all.AddRange(await tmdb.GetSeasonEpisodesAsync(
                            selected.Series.Id, season, cancellation.Token));
                    }
                    episodes = all;
                }
                else
                    episodes = await selected.Provider.GetEpisodesAsync(
                        selected.Series.Id, false, cancellation.Token);
                if (IsDisposed || cancellation.IsCancellationRequested) return;

                current = EpisodeFileMatcher.Match(files, episodes);
                foreach (EpisodeSuggestion suggestion in current)
                {
                    string identity = suggestion.File.Season.HasValue && suggestion.File.Episode.HasValue
                        ? "S" + suggestion.File.Season.Value.ToString("00") +
                          "E" + suggestion.File.Episode.Value.ToString("00") : "?";
                    string requested = MetadataSelection.CanonicalLanguage(
                        ((KeyValuePair<string, string>)languageChoice.SelectedItem).Key);
                    string actual = suggestion.Language ?? "und";
                    string languageLabel = actual == requested ? actual :
                        actual + (actual == "und" ? " (Original/unbekannt)" :
                            " (Fallback für " + requested + ")");
                    int index = preview.Rows.Add(suggestion.File.FileName,
                        identity, suggestion.Title, languageLabel, suggestion.Status);
                    if (!suggestion.IsMatched)
                    {
                        preview.Rows[index].Cells[2].ReadOnly = true;
                        preview.Rows[index].DefaultCellStyle.BackColor = Color.MistyRose;
                    }
                }
                int fallback = current.Count(s => s.IsMatched &&
                    !string.Equals(s.Language,
                        ((KeyValuePair<string, string>)languageChoice.SelectedItem).Key,
                        StringComparison.OrdinalIgnoreCase));
                status.Text = "Zuordnung von " + selected.Provider.DisplayName + ": " +
                    current.Count(s => s.IsMatched) + " / " + current.Count +
                    " Folgen eindeutig. " + fallback +
                    " Titel nicht in gewählter Sprache (Original/Fallback).";
                UpdateAcceptStatus();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                status.Text = "Episodenabfrage fehlgeschlagen: " + ex.GetType().Name +
                    ". Serie/Provider prüfen; keine Dateien geändert.";
            }
            finally
            {
                if (!IsDisposed) SetBusy(false);
            }
        }

        private void SetBusy(bool value)
        {
            busy = value;
            load.Enabled = !busy && matches.SelectedItem != null;
            searchAgain.Enabled = !busy;
            queryBox.Enabled = !busy;
            languageChoice.Enabled = !busy;
            accept.Enabled = !busy && current != null && current.Count > 0 &&
                current.All(x => x.IsMatched);
        }

        private void UpdateAcceptStatus()
        {
            accept.Enabled = !busy && current != null && current.Count == files.Count &&
                current.Count > 0 && current.All(x => x.IsMatched) &&
                preview.Rows.Cast<DataGridViewRow>().All(row =>
                    !string.IsNullOrWhiteSpace(Convert.ToString(row.Cells[2].Value)));
        }

        private void AcceptRows()
        {
            preview.EndEdit();
            UpdateAcceptStatus();
            if (!accept.Enabled)
                return;

            // The existing editor and rename engine join rows by sorted file
            // position. We only hand over a full, unambiguous row for every file.
            var result = new List<string>();
            for (int i = 0; i < preview.Rows.Count; i++)
            {
                string title = Convert.ToString(preview.Rows[i].Cells[2].Value);
                if (string.IsNullOrWhiteSpace(title) || title.IndexOfAny(new[] { '\t', '\r', '\n' }) >= 0)
                {
                    MessageBox.Show(this, "Episodentitel darf nicht leer sein und keine Tabs/Zeilenumbrüche enthalten.");
                    return;
                }
                EpisodeFileIdentity identity = current[i].File;
                string episodeId = "S" + identity.Season.Value.ToString("00") +
                    "E" + identity.Episode.Value.ToString("00");
                result.Add(episodeId + "\t" + title.Trim());
            }

            string message = "Es werden " + result.Count +
                " bearbeitbare Datenvorschläge in den Dateneditor übernommen. " +
                "Die tatsächliche Umbenennung erfolgt erst später über die eigene Vorschau. Fortfahren?";
            if (MessageBox.Show(this, message, "Datenvorschläge prüfen",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            acceptedRows = result;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
