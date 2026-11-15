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
        private readonly Button stopSearch;
        private readonly CheckedListBox favoriteSources;
        private CancellationTokenSource activeSearch;
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
            queryBox = new TextBox { Width = 340, Text = query };
            searchBar.Controls.Add(new Label { Text = "Sprache:", Width = 57,
                TextAlign = ContentAlignment.MiddleLeft, Height = 26 });
            languageChoice = new ComboBox {
                Width = 148, DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var choice in MetadataSelection.SupportedLanguages())
                languageChoice.Items.Add(choice);
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
                    "TVmaze/Jikan haben keine garantierten Übersetzungen.";
            };
            searchAgain = new Button { Text = "Erneut suchen", Width = 120 };
            searchAgain.Click += async (s, e) => await SearchAsync();
            searchBar.Controls.Add(queryBox);
            searchBar.Controls.Add(languageChoice);
            searchBar.Controls.Add(searchAgain);

            favoriteSources = new CheckedListBox {
                Dock = DockStyle.Top, Height = 57, MultiColumn = true,
                ColumnWidth = 215, CheckOnClick = true,
                BorderStyle = BorderStyle.FixedSingle,
                DisplayMember = "Value"
            };
            var favorites = MetadataSourceFavorites.Load(
                Properties.Settings.Default.AutoSearchFavorites);
            foreach (var source in new[] {
                new KeyValuePair<string, string>("tvmaze", "TVmaze"),
                new KeyValuePair<string, string>("jikan", "Jikan (MyAnimeList)"),
                new KeyValuePair<string, string>("anizip", "AniZip (Anime)"),
                new KeyValuePair<string, string>("tmdb", "TMDB (mit Key)")
            })
                favoriteSources.Items.Add(source, favorites.Contains(source.Key));
            favoriteSources.ItemCheck += (s, e) => {
                if (!busy && IsHandleCreated)
                    BeginInvoke(new Action(() => {
                        InvalidateSuggestions();
                        var checkedSources = SelectedSources();
                        if (checkedSources.Count > 0)
                        {
                            Properties.Settings.Default.AutoSearchFavorites =
                                MetadataSourceFavorites.Save(checkedSources);
                            Properties.Settings.Default.Save();
                        }
                    }));
            };
            matches = new ListBox { Dock = DockStyle.Top, Height = 120 };
            matches.SelectedIndexChanged += (s, e) => load.Enabled = !busy && matches.SelectedItem != null;

            load = new Button { Text = "Ausgewählte Serie abgleichen", Width = 215 };
            load.Click += async (s, e) => await LoadSelectedAsync();
            accept = new Button { Text = "Datenvorschläge übernehmen", Width = 220, Enabled = false };
            accept.Click += (s, e) => AcceptRows();
            cancel = new Button { Text = "Schließen", DialogResult = DialogResult.Cancel, Width = 100 };
            stopSearch = new Button { Text = "Suche stoppen", Width = 125, Enabled = false };
            stopSearch.Click += (s, e) => {
                if (activeSearch != null && !activeSearch.IsCancellationRequested)
                    activeSearch.Cancel();
            };
            var actions = new FlowLayoutPanel {
                Dock = DockStyle.Bottom, Height = 47,
                FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
            actions.Controls.Add(cancel);
            actions.Controls.Add(stopSearch);
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
                HeaderText = "Quelle", Name = "provider", ReadOnly = true, FillWeight = 17 });
            preview.Columns.Add(new DataGridViewTextBoxColumn {
                HeaderText = "Sprache", Name = "language", ReadOnly = true, FillWeight = 18 });
            preview.Columns.Add(new DataGridViewTextBoxColumn {
                HeaderText = "Status", Name = "status", ReadOnly = true, FillWeight = 29 });
            preview.CellEndEdit += (s, e) => UpdateAcceptStatus();

            var box = new Panel { Dock = DockStyle.Fill };
            box.Controls.Add(preview);
            box.Controls.Add(matches);
            box.Controls.Add(favoriteSources);
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

        private void InvalidateSuggestions()
        {
            current = null;
            accept.Enabled = false;
            preview.Rows.Clear();
            matches.Items.Clear();
            candidates.Clear();
            status.Text = "Quellenauswahl geändert – bitte erneut suchen.";
        }

        private List<string> SelectedSources()
        {
            return favoriteSources.CheckedItems.Cast<KeyValuePair<string, string>>()
                .Select(pair => pair.Key).ToList();
        }

        private async Task SearchAsync()
        {
            if (busy || IsDisposed) return;
            string searchTerm = queryBox.Text.Trim();
            List<string> wanted = SelectedSources();
            if (files.Count == 0 || string.IsNullOrWhiteSpace(searchTerm))
            {
                status.Text = "Bitte einen Serienordner mit Dateien und Suchbegriff auswählen.";
                return;
            }
            if (wanted.Count == 0)
            {
                status.Text = "Bitte mindestens eine Datenquelle als Favoriten auswählen.";
                return;
            }
            string language = MetadataSelection.CanonicalLanguage(
                ((KeyValuePair<string, string>)languageChoice.SelectedItem).Key);
            Properties.Settings.Default.PreferredMetadataLanguage = language;
            Properties.Settings.Default.AutoSearchFavorites =
                MetadataSourceFavorites.Save(wanted);
            Properties.Settings.Default.Save();

            InvalidateSuggestions();
            SetBusy(true);
            string errors = "";
            int resultCount = 0;
            string key = "";
            if (wanted.Contains("tmdb"))
            {
                try
                {
                    key = TmdbCredentialStore.Unprotect(
                        Properties.Settings.Default.TmdbCredential);
                    if (string.IsNullOrWhiteSpace(key))
                        errors += "TMDB: API-Key fehlt. ";
                }
                catch (Exception) { errors += "TMDB-Key nicht lesbar. "; }
            }
            var providers = new List<IEpisodeMetadataProvider>();
            if (wanted.Contains("tvmaze")) providers.Add(new TvMazeProvider());
            if (wanted.Contains("tmdb") && !string.IsNullOrWhiteSpace(key))
                providers.Add(new TmdbProvider(key, language));
            if (MetadataSourceFavorites.UsesMalLookup(wanted))
                providers.Add(new JikanProvider(language));
            IEpisodeMetadataProvider aniZip = new AniZipProvider(language);

            using (var limit = CancellationTokenSource.CreateLinkedTokenSource(
                cancellation.Token))
            {
                limit.CancelAfter(TimeSpan.FromSeconds(18));
                activeSearch = limit;
                try
                {
                    foreach (var provider in providers)
                    {
                        if (limit.IsCancellationRequested) break;
                        status.Text = "Suche " + provider.DisplayName +
                            " – maximal 18 Sekunden insgesamt …";
                        try
                        {
                            using (var sourceLimit =
                                CancellationTokenSource.CreateLinkedTokenSource(limit.Token))
                            {
                                sourceLimit.CancelAfter(TimeSpan.FromSeconds(6));
                                IReadOnlyList<SeriesMatch> found =
                                    await provider.SearchSeriesAsync(searchTerm, sourceLimit.Token);
                                if (IsDisposed || cancellation.IsCancellationRequested) return;
                                resultCount += found.Count;
                                AddCandidates(provider, found, searchTerm, 0);
                                if (provider.Id == JikanProvider.ProviderKey &&
                                    wanted.Contains("anizip"))
                                    AddCandidates(aniZip,
                                        AniZipProvider.FromMalSearch(found), searchTerm, 0);
                            }
                            RefreshCandidates(language);
                        }
                        catch (OperationCanceledException)
                        {
                            errors += provider.DisplayName + ": Zeitlimit. ";
                        }
                        catch (Exception)
                        {
                            errors += provider.DisplayName + ": Abfrage fehlgeschlagen. ";
                        }
                    }

                    // Expensive alias lookups are optional, and only useful
                    // if the selected sources did not return a matching title.
                    if (!limit.IsCancellationRequested && candidates.All(x => x.Rank == 0))
                    {
                        status.Text = "Alternative Anime-Titel prüfen …";
                        try
                        {
                            IReadOnlyList<string> aliases;
                            using (var aliasLimit =
                                CancellationTokenSource.CreateLinkedTokenSource(limit.Token))
                            {
                                aliasLimit.CancelAfter(TimeSpan.FromSeconds(3));
                                aliases = await new AnimeTitleAliases()
                                    .FindAsync(searchTerm, aliasLimit.Token);
                            }
                            foreach (string alias in aliases.Take(1))
                            {
                                foreach (IEpisodeMetadataProvider provider in providers.Where(
                                    p => p.Id == "tvmaze" || p.Id == "jikan"))
                                {
                                    if (limit.IsCancellationRequested) break;
                                    try
                                    {
                                        using (var retryLimit =
                                            CancellationTokenSource.CreateLinkedTokenSource(limit.Token))
                                        {
                                            retryLimit.CancelAfter(TimeSpan.FromSeconds(3));
                                            var found = await provider.SearchSeriesAsync(
                                                alias, retryLimit.Token);
                                            if (IsDisposed || cancellation.IsCancellationRequested)
                                                return;
                                            resultCount += found.Count;
                                            AddCandidates(provider, found, searchTerm, 15);
                                            if (provider.Id == "jikan" &&
                                                wanted.Contains("anizip"))
                                                AddCandidates(aniZip,
                                                    AniZipProvider.FromMalSearch(found),
                                                    searchTerm, 15);
                                        }
                                        RefreshCandidates(language);
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        errors += provider.DisplayName + ": Alias-Zeitlimit. ";
                                    }
                                    catch (Exception)
                                    {
                                        errors += provider.DisplayName + ": Aliasfehler. ";
                                    }
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            errors += "Alias-Zeitlimit. ";
                        }
                        catch (Exception)
                        {
                            errors += "Anime-Aliasindex nicht erreichbar. ";
                        }
                    }
                    RefreshCandidates(language);
                    string ending = limit.IsCancellationRequested
                        ? "Suche beendet (Zeitlimit oder manuell gestoppt). " : "";
                    status.Text = ending + "Gefundene Serien: " + candidates.Count +
                        ". Bitte Quelle und richtige Serie auswählen. " + errors;
                    if (candidates.Count == 0)
                        status.Text += "Alternativen: anderer/englischer Serientitel.";
                }
                finally
                {
                    activeSearch = null;
                    if (!IsDisposed) SetBusy(false);
                }
            }
        }

        private void AddCandidates(IEpisodeMetadataProvider provider,
            IReadOnlyList<SeriesMatch> found, string term, int aliasRank)
        {
            string normalized = Normalize(term);
            foreach (var match in found.Where(x =>
                x != null && !string.IsNullOrWhiteSpace(x.Name)))
            {
                if (candidates.Any(x => x.Provider.Id == provider.Id &&
                    x.Series.Id == match.Id))
                    continue;
                string name = Normalize(match.Name);
                int score = aliasRank > 0 ? aliasRank :
                    name == normalized ? 100 :
                    (name.Contains(normalized) || normalized.Contains(name)) ? 40 : 0;
                candidates.Add(new Candidate { Provider = provider,
                    Series = match, Rank = score });
            }
        }

        private void RefreshCandidates(string language)
        {
            candidates.Sort((a, b) => b.Rank != a.Rank ?
                b.Rank.CompareTo(a.Rank) :
                MetadataSourcePriority.Rank(b.Provider.Id, language) !=
                MetadataSourcePriority.Rank(a.Provider.Id, language)
                ? MetadataSourcePriority.Rank(b.Provider.Id, language)
                    .CompareTo(MetadataSourcePriority.Rank(a.Provider.Id, language))
                : string.Compare(a.ToString(), b.ToString(),
                    StringComparison.OrdinalIgnoreCase));
            matches.Items.Clear();
            matches.Items.AddRange(candidates.Cast<object>().ToArray());
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
                " für „" + selected.Series.Name + "“ (max. 20 Sekunden) …";
            using (var loadLimit = CancellationTokenSource.CreateLinkedTokenSource(
                cancellation.Token))
            {
            loadLimit.CancelAfter(TimeSpan.FromSeconds(20));
            activeSearch = loadLimit;
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
                        loadLimit.Token.ThrowIfCancellationRequested();
                        all.AddRange(await tmdb.GetSeasonEpisodesAsync(
                            selected.Series.Id, season, loadLimit.Token));
                    }
                    episodes = all;
                }
                else
                    episodes = await selected.Provider.GetEpisodesAsync(
                        selected.Series.Id, false, loadLimit.Token);
                if (IsDisposed || cancellation.IsCancellationRequested) return;

                current = EpisodeFileMatcher.Match(files, episodes);
                foreach (EpisodeSuggestion suggestion in current)
                {
                    string identity = suggestion.File.Season.HasValue && suggestion.File.Episode.HasValue
                        ? "S" + suggestion.File.Season.Value.ToString("00") +
                          "E" + suggestion.File.Episode.Value.ToString("00") : "?";
                    string requested = MetadataSelection.CanonicalLanguage(
                        ((KeyValuePair<string, string>)languageChoice.SelectedItem).Key);
                    string languageLabel = MetadataSelection.DescribeResultLanguage(
                        requested, suggestion.Language);
                    int index = preview.Rows.Add(suggestion.File.FileName,
                        identity, suggestion.Title, selected.Provider.DisplayName,
                        languageLabel, suggestion.Status);
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
            catch (OperationCanceledException)
            {
                if (!IsDisposed)
                    status.Text = "Episodenabfrage gestoppt oder Zeitlimit (20 Sek.). " +
                        "Keine Dateien geändert.";
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    status.Text = "Episodenabfrage fehlgeschlagen: " + ex.GetType().Name +
                        ". Serie/Provider prüfen; keine Dateien geändert.";
            }
            finally
            {
                activeSearch = null;
                if (!IsDisposed) SetBusy(false);
            }
            } // loadLimit
        }

        private void SetBusy(bool value)
        {
            busy = value;
            load.Enabled = !busy && matches.SelectedItem != null;
            searchAgain.Enabled = !busy;
            stopSearch.Enabled = busy;
            favoriteSources.Enabled = !busy;
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
