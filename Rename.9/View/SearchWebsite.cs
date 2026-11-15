using Episode_Names.Helper;
using Episode_Names.Metadata;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Web;
using System.Windows.Forms;

namespace Episode_Names.Anisearch
{
    public partial class Anisearch_Table : Form
    {
        private List<string> episodeList;
        private const int AnisearchIndex = MetadataSelection.Anisearch;
        private const int TvMazeIndex = MetadataSelection.TvMaze;
        private const int TmdbIndex = MetadataSelection.Tmdb;
        private Label lblTmdbCredential;
        private TextBox txtTmdbCredential;
        private Button btnTmdbSave;
        private Button btnTmdbDelete;
        private bool suppressApiSelection;
        private readonly IEpisodeMetadataProvider tvMaze = new TvMazeProvider();

        public Anisearch_Table()
        {
            InitializeComponent();
            InitializeTmdbCredentialControls();
            setData();
        }

        private void setData()
        {
            TopMost = Properties.Settings.Default.Foreground;
            // Old versions stored indices 0..5. Migrate once; index 2 now means TMDB.
            // The migration flag prevents interpreting a new selection as an old one.
            if (!Properties.Settings.Default.MetadataProviderSelectionMigrated)
            {
                int legacy = Properties.Settings.Default.SelectedWebsite;
                if (string.IsNullOrWhiteSpace(Properties.Settings.Default.PreferredMetadataLanguage))
                    Properties.Settings.Default.PreferredMetadataLanguage =
                        MetadataSelection.MigrateLegacySearchLanguage(
                            legacy, Properties.Settings.Default.SecondLanguage);
                Properties.Settings.Default.SelectedWebsite =
                    MetadataSelection.MigrateLegacyProvider(legacy);
                Properties.Settings.Default.MetadataProviderSelectionMigrated = true;
                Properties.Settings.Default.Save();
            }

            int saved = Properties.Settings.Default.SelectedWebsite;
            cmbWebsite.SelectedIndex = saved >= 0 && saved < cmbWebsite.Items.Count
                ? saved : TvMazeIndex;
            setCmbLanguages();
        }

        private static List<KeyValuePair<string, string>> LanguageChoices(int provider)
        {
            var choices = new List<KeyValuePair<string, string>>();
            if (provider == AnisearchIndex)
            {
                choices.Add(new KeyValuePair<string, string>("de", "Deutsch"));
                choices.Add(new KeyValuePair<string, string>("en", "English"));
                choices.Add(new KeyValuePair<string, string>("ja", "日本語"));
            }
            else if (provider == TmdbIndex)
            {
                choices.Add(new KeyValuePair<string, string>("de", "Deutsch"));
                choices.Add(new KeyValuePair<string, string>("en", "English"));
            }
            else
            {
                // TVmaze only exposes its original (mainly English) titles.
                choices.Add(new KeyValuePair<string, string>("en", "English"));
            }
            return choices;
        }

        private void setCmbLanguages()
        {
            var languages = LanguageChoices(cmbWebsite.SelectedIndex);
            cmbLanguageSearch.DataSource = new List<KeyValuePair<string, string>>(languages);
            cmbLanguageSearch.DisplayMember = "Value";
            cmbLanguageSearch.ValueMember = "Key";
            cmbLanguageEpisodes.DataSource = new List<KeyValuePair<string, string>>(languages);
            cmbLanguageEpisodes.DisplayMember = "Value";
            cmbLanguageEpisodes.ValueMember = "Key";

            string preferred = Properties.Settings.Default.PreferredMetadataLanguage;
            int index = MetadataSelection.FindLanguageIndex(languages, preferred);
            cmbLanguageSearch.SelectedIndex = index;
            cmbLanguageEpisodes.SelectedIndex = index;
        }

        private void RememberSearchLanguage()
        {
            if (cmbWebsite.SelectedIndex == TvMazeIndex ||
                !(cmbLanguageSearch.SelectedItem is KeyValuePair<string, string>))
                return;

            string code = ((KeyValuePair<string, string>)cmbLanguageSearch.SelectedItem).Key;
            Properties.Settings.Default.PreferredMetadataLanguage =
                MetadataSelection.CanonicalLanguage(code);
            Properties.Settings.Default.Save();

            // Use the last searched language as the default episode language too.
            var choices = (List<KeyValuePair<string, string>>)cmbLanguageEpisodes.DataSource;
            cmbLanguageEpisodes.SelectedIndex =
                MetadataSelection.FindLanguageIndex(choices, code);
        }

        private async void btnOk_Click(object sender, EventArgs e)
        {
            if (cmbWebsite.SelectedIndex == TvMazeIndex)
            {
                await LoadTvMazeEpisodesAsync();
                return;
            }
            if (cmbWebsite.SelectedIndex == TmdbIndex)
            {
                await LoadTmdbEpisodesAsync();
                return;
            }
            if (cmbWebsite.SelectedIndex != AnisearchIndex)
                return;

            string language = ((KeyValuePair<string, string>)cmbLanguageEpisodes.SelectedItem).Key;
            string url;
            if (!checkUrlAnisearch(language, txtUrl.Text, out url))
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Warning,
                    "Bitte eine gültige HTTPS-URL einer AniSearch-Anime-Serie eingeben.");
                return;
            }

            StartProgressBar();
            new Thread(() =>
            {
                bool completed = false;
                try
                {
                    List<string> results = getEpisodeList(url, language);
                    if (results == null || results.Count == 0)
                        throw new InvalidOperationException("AniSearch liefert keine Episodentitel.");
                    episodeList = results;
                    completed = true;
                    if (IsHandleCreated && !IsDisposed)
                        BeginInvoke(new MethodInvoker(() =>
                        {
                            if (IsDisposed) return;
                            txtUrl.Text = url;
                            pgLoading.Style = ProgressBarStyle.Blocks;
                            DialogResult = DialogResult.OK;
                            Close();
                        }));
                }
                catch (Exception ex)
                {
                    ErrorMessage(ex);
                }
                finally
                {
                    if (!completed && IsHandleCreated && !IsDisposed)
                        StopProgressBar();
                }
            }).Start();
        }

        private string getAnisearchDomain()
        {
            string language = ((KeyValuePair<string, string>)cmbLanguageSearch.SelectedItem).Key;
            return "https://www.anisearch." + (language.Equals("de") ? language : "com");
        }

        private string getDomainEpisodes(string language)
        {
            return "https://www.anisearch." + (language.Equals("de") ? language : "com");
        }


        private bool checkUrlAnisearch(string language, string inputUrl, out string url)
        {
            url = "";
            Uri uri;
            if (!Uri.TryCreate(inputUrl, UriKind.Absolute, out uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !(uri.Host.Equals("www.anisearch.de", StringComparison.OrdinalIgnoreCase) ||
                  uri.Host.Equals("anisearch.de", StringComparison.OrdinalIgnoreCase) ||
                  uri.Host.Equals("www.anisearch.com", StringComparison.OrdinalIgnoreCase) ||
                  uri.Host.Equals("anisearch.com", StringComparison.OrdinalIgnoreCase)))
                return false;
            string[] parts = uri.AbsolutePath.Split(new[] { '/' },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !parts[0].Equals("anime", StringComparison.OrdinalIgnoreCase))
                return false;
            url = getDomainEpisodes(language) + "/anime/" +
                Uri.EscapeDataString(Uri.UnescapeDataString(parts[1])) + "/" +
                Uri.EscapeDataString(Uri.UnescapeDataString(parts[2])) + "/episodes";
            return true;
        }

        private void txtUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnOk_Click(null, null);
            }
        }


        public List<string> getData()
        {
            return episodeList;
        }


        private void ErrorMessage(Exception exception)
        {
            MethodInvoker LabelUpdate = delegate
            {
                StopProgressBar();
                if (exception is FileNotFoundException)
                {
                    MessageHandler.MessagesOK(MessageBoxIcon.Error, "HtmlAgilityPack.dll konnte nicht gefunden werden");
                }

                else
                {
                    ErrorHelper.HandleException(exception);
                }
            };
            BeginInvoke(LabelUpdate);
        }


        private void StopProgressBar() {
            pgLoading.GetCurrentParent().Invoke(new MethodInvoker(() =>
            {
                pgLoading.Style = ProgressBarStyle.Blocks;
            }));


            BeginInvoke(new MethodInvoker(() =>
            {
                setEnabled(true);
            }));

        }


        private List<string> getEpisodeList(string url, string language)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = web.Load(url);
            language = language == "com" ? "en" : language;

            List<string> episodeList = new List<string>();
            HtmlNode table = doc.DocumentNode.SelectSingleNode("//table[@class='responsive-table episodes']//tbody");
            foreach (HtmlNode row in table.SelectNodes("tr"))
            {
                string line = null;


                #region Falls es mehr anderssprachige Einträge gibt
                try {
                    line = WebUtility.HtmlDecode(row.SelectNodes("th")[0].InnerText) + "\t" +
                            WebUtility.HtmlDecode(row.SelectNodes($"td[@data-title='{(language.Equals("de") ? "Titel" : "Title")}']")[0].SelectSingleNode($"div[@lang='{language}']").InnerText);
                }
                catch (NullReferenceException) {
                }
                #endregion


                if (line != null)
                {
                    episodeList.Add(line.Trim());
                }
            }
            return episodeList;
        }


        private void CloseForm()
        {
            StopProgressBar();
            DialogResult = DialogResult.OK;
            BeginInvoke(new MethodInvoker(() => {
                Close();
            }));
        }

        private void Anisearch_Table_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.Language = cmbLanguageEpisodes.SelectedIndex;
            Properties.Settings.Default.SelectedWebsite = cmbWebsite.SelectedIndex;
            Properties.Settings.Default.SecondLanguage = cmbLanguageSearch.SelectedIndex;
            Properties.Settings.Default.Save();
        }


        private void StartProgressBar()
        {
            pgLoading.GetCurrentParent().BeginInvoke(new MethodInvoker(() => {
                pgLoading.Style = ProgressBarStyle.Marquee;
            })); ;
            BeginInvoke(new MethodInvoker(() =>
            {
                setEnabled(false);
            }));
        }

        private void setEnabled(bool status)
        {
            ControlBox = btnCheckURL.Enabled = cmbLanguageSearch.Enabled = cmbLanguageEpisodes.Enabled = cmbWebsite.Enabled = cmbSeasons.Enabled = btnOk.Enabled = listResult.Enabled = txtSearch.Enabled = status;
        }

        private async void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            if (cmbWebsite.SelectedIndex == TvMazeIndex)
            {
                await SearchTvMazeAsync();
                return;
            }
            if (cmbWebsite.SelectedIndex == TmdbIndex)
            {
                await SearchTmdbAsync();
                return;
            }
            if (cmbWebsite.SelectedIndex != AnisearchIndex)
                return;

            string query = txtSearch.Text.Trim();
            if (query.Length == 0)
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Information,
                    "Bitte einen Serientitel eingeben.");
                return;
            }
            RememberSearchLanguage();
            string domain = getAnisearchDomain();
            string searchUrl = getSearchUrlAnisearch(query, domain);
            StartProgressBar();
            new Thread(() =>
            {
                try
                {
                    searchTextAnisearch(searchUrl, domain);
                }
                catch (Exception ex)
                {
                    ErrorMessage(ex);
                }
                finally
                {
                    if (!IsDisposed && IsHandleCreated)
                        StopProgressBar();
                }
            }).Start();
        }

        private void searchTextAnisearch(string url, string domain) //Thread
        {
            var keyData = GetAnisearchSearchKey(domain);
            var objRequest =  (HttpWebRequest)WebRequest.Create(url + $"&{keyData.key}");
            objRequest.CookieContainer = keyData.context;
            WebResponse objResponse = objRequest.GetResponse();
            if (objResponse.ResponseUri.ToString().Contains("/index/"))
            {
                List<KeyValuePair<string, string>> resultUrl = new List<KeyValuePair<string, string>>();

                getHtmlDocument(out HtmlAgilityPack.HtmlDocument doc, objResponse);

                var errorNotice = doc.DocumentNode.SelectSingleNode("//*[@class=\"msgBox\"]/*[@class=\"error\"]");
                var tables = doc.DocumentNode.SelectNodes("//table//tbody");

                if(tables == null)
                {
                    if(errorNotice != null)
                    {
                        MessageHandler.MessagesOK(MessageBoxIcon.Warning, "Invalid query");
                    }
                    setReceivedUrls(resultUrl);
                    return;
                }

                foreach (HtmlNode row in tables.ElementAt(0).SelectNodes("tr"))
                {
                    string text = WebUtility.HtmlDecode(row.SelectNodes("th")[0].SelectNodes("a")[0].InnerText);
                    string nurl = WebUtility.HtmlDecode(row.SelectNodes("th")[0].SelectNodes("a[@href]")[0].Attributes["href"].Value);
                    resultUrl.Add(new KeyValuePair<string, string>($"{domain}/{nurl}", text));
                }
                setReceivedUrls(resultUrl);
            }
            else
            {
                setUrl(objResponse.ResponseUri.ToString());
            }
        }

        private void setUrl(string url)
        {
            txtUrl.BeginInvoke(new MethodInvoker(() =>
            {
                txtUrl.Text = url;
            }));
        }

        private void setReceivedUrls(List<KeyValuePair<string, string>> result)
        {
            listResult.Invoke(new MethodInvoker(() =>
            {
                listResult.DataSource = null;
                listResult.DataSource = result;
                listResult.DisplayMember = "Value";
                listResult.ValueMember = "Key";
            }));
        }

        private void getHtmlDocument(out HtmlAgilityPack.HtmlDocument doc, WebResponse objResponse)
        {
            StreamReader sr = new StreamReader(objResponse.GetResponseStream());
            doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(sr.ReadToEnd());
            sr.Close();
            objResponse.Close();
        }

        private dynamic GetAnisearchSearchKey(string domain)
        {
            var objRequest = WebRequest.Create(domain);
            var container = new CookieContainer();

            WebResponse objResponse = objRequest.GetResponse();
            List<KeyValuePair<string, string>> resultUrl = new List<KeyValuePair<string, string>>();
            getHtmlDocument(out HtmlAgilityPack.HtmlDocument doc, objResponse);
            var node = doc.DocumentNode.SelectSingleNode("//*[@data-search]");
            string[] parts = node.Attributes["data-search"].Value.Split(',');
            container.Add(new Cookie(parts[0], parts[1], "/", ".anisearch.com"));

            return new {
                key = $"{parts[0]}={parts[1]}",
                context = container,
            };
        }

        private string getSearchUrlAnisearch(string searchText,string domain)
        {
            return domain + "/anime/index/?char=all&text=" + searchText.Replace(" ", "%20") + "&smode=1&view=2";
        }

        private void listResult_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                listResult.SelectedIndex = listResult.IndexFromPoint(e.Location);
            }
        }

        private void linkImBrowserÖffnenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listResult.SelectedIndex != -1)
            {
                Process.Start(((KeyValuePair<string, string>)listResult.SelectedItem).Key);
            }
        }

        private async void listResult_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressApiSelection || listResult.SelectedIndex < 0)
                return;

            string url = ((KeyValuePair<string, string>)listResult.SelectedItem).Key;
            if (cmbWebsite.SelectedIndex == TvMazeIndex)
            {
                await LoadTvMazeSeasonsAsync(url);
                return;
            }
            if (cmbWebsite.SelectedIndex == TmdbIndex)
            {
                await LoadTmdbSeasonsAsync(url);
                return;
            }
            if (cmbWebsite.SelectedIndex == AnisearchIndex)
                txtUrl.Text = url;
        }

        private void cmbWebsite_SelectedIndexChanged(object sender, EventArgs e)
        {
            listResult.DataSource = null;
            cmbSeasons.DataSource = null;
            txtUrl.Clear();
            lblSeason.Visible = cmbSeasons.Visible = cmbWebsite.SelectedIndex != AnisearchIndex;
            setCmbLanguages();
            ShowTmdbCredentialControls();
        }

        private async void btnCheckURL_Click(object sender, EventArgs e)
        {
            if (cmbWebsite.SelectedIndex == TvMazeIndex)
            {
                await LoadTvMazeSeasonsAsync(txtUrl.Text);
                return;
            }
            if (cmbWebsite.SelectedIndex == TmdbIndex)
            {
                await LoadTmdbSeasonsAsync(txtUrl.Text);
                return;
            }
            string language = ((KeyValuePair<string, string>)cmbLanguageEpisodes.SelectedItem).Key;
            string url;
            if (!checkUrlAnisearch(language, txtUrl.Text, out url))
                MessageHandler.MessagesOK(MessageBoxIcon.Warning,
                    "Bitte eine gültige HTTPS-URL einer AniSearch-Anime-Serie eingeben.");
            else
                txtUrl.Text = url;
        }

        private void InitializeTmdbCredentialControls()
        {
            lblTmdbCredential = new Label { Location = new System.Drawing.Point(0, 387),
                Size = new System.Drawing.Size(88, 22), Text = "TMDB-Key:" };
            txtTmdbCredential = new TextBox { Location = new System.Drawing.Point(85, 384),
                Size = new System.Drawing.Size(141, 21), UseSystemPasswordChar = true };
            btnTmdbSave = new Button { Location = new System.Drawing.Point(229, 383),
                Size = new System.Drawing.Size(61, 24), Text = "Sichern" };
            btnTmdbDelete = new Button { Location = new System.Drawing.Point(292, 383),
                Size = new System.Drawing.Size(63, 24), Text = "Löschen" };

            btnTmdbSave.Click += (sender, args) => SaveTmdbCredential();
            btnTmdbDelete.Click += (sender, args) => DeleteTmdbCredential();
            Controls.Add(lblTmdbCredential);
            Controls.Add(txtTmdbCredential);
            Controls.Add(btnTmdbSave);
            Controls.Add(btnTmdbDelete);
        }

        private void ShowTmdbCredentialControls()
        {
            if (lblTmdbCredential == null)
                return;
            bool visible = cmbWebsite.SelectedIndex == TmdbIndex;
            lblTmdbCredential.Visible = txtTmdbCredential.Visible =
                btnTmdbSave.Visible = btnTmdbDelete.Visible = visible;

            ClientSize = new System.Drawing.Size(355, visible ? 476 : 435);
            btnOk.Location = new System.Drawing.Point(0, visible ? 422 : 385);
            Text = visible ? "TMDB – Serien und Episoden" : "Episoden-Suche";
            UpdateTmdbCredentialLabel();
        }

        private void UpdateTmdbCredentialLabel()
        {
            lblTmdbCredential.Text = string.IsNullOrWhiteSpace(Properties.Settings.Default.TmdbCredential)
                ? "TMDB-Key:" : "Key gesichert:";
        }

        private void SaveTmdbCredential()
        {
            try
            {
                Properties.Settings.Default.TmdbCredential = TmdbCredentialStore.Protect(txtTmdbCredential.Text);
                Properties.Settings.Default.Save();
                txtTmdbCredential.Clear();
                UpdateTmdbCredentialLabel();
                MessageHandler.MessagesOK(MessageBoxIcon.Information,
                    "TMDB-Key wurde verschlüsselt für diesen Windows-Benutzer gespeichert.");
            }
            catch (Exception ex)
            {
                ErrorMessage(ex);
            }
        }

        private void DeleteTmdbCredential()
        {
            if (MessageBox.Show("Gespeicherten TMDB-Key entfernen?", "TMDB",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            Properties.Settings.Default.TmdbCredential = "";
            Properties.Settings.Default.Save();
            txtTmdbCredential.Clear();
            UpdateTmdbCredentialLabel();
        }

        private TmdbProvider CreateTmdbProvider(bool forSearch = false)
        {
            try
            {
                string key = TmdbCredentialStore.Unprotect(Properties.Settings.Default.TmdbCredential);
                if (string.IsNullOrWhiteSpace(key))
                {
                    MessageHandler.MessagesOK(MessageBoxIcon.Information,
                        "TMDB benötigt einen persönlichen API-Key oder Read Access Token. " +
                        "Bitte oben eingeben und mit 'Sichern' speichern. " +
                        "TVmaze funktioniert weiterhin ohne Schlüssel.");
                    return null;
                }

                ComboBox languageBox = forSearch ? cmbLanguageSearch : cmbLanguageEpisodes;
                var selected = languageBox.SelectedItem;
                string language = selected is KeyValuePair<string, string>
                    ? ((KeyValuePair<string, string>)selected).Key : "de";
                return new TmdbProvider(key, language);
            }
            catch (MetadataProviderException ex)
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Warning, ex.Message);
                return null;
            }
        }

        private void SetApiSearchResults(List<KeyValuePair<string, string>> entries)
        {
            suppressApiSelection = true;
            try
            {
                listResult.DataSource = null;
                listResult.DisplayMember = "Value";
                listResult.ValueMember = "Key";
                listResult.DataSource = entries;
                listResult.SelectedIndex = -1;
                cmbSeasons.DataSource = null;
            }
            finally
            {
                suppressApiSelection = false;
            }
        }

        private async System.Threading.Tasks.Task SearchTmdbAsync()
        {
            TmdbProvider provider = CreateTmdbProvider(true);
            if (provider == null)
                return;

            if (string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Information, "Bitte einen Serientitel eingeben.");
                return;
            }

            RememberSearchLanguage();
            SetTvMazeBusy(true);
            try
            {
                IReadOnlyList<SeriesMatch> series = await provider.SearchSeriesAsync(txtSearch.Text, CancellationToken.None);
                if (IsDisposed || cmbWebsite.SelectedIndex != TmdbIndex)
                    return;

                var matches = series.Select(item => new KeyValuePair<string, string>(
                    item.WebUrl, item.Name + (item.PremiereYear.HasValue ? " (" + item.PremiereYear.Value + ")" : "")))
                    .ToList();
                SetApiSearchResults(matches);
                if (matches.Count == 0)
                    MessageHandler.MessagesOK(MessageBoxIcon.Information, "Keine Serien auf TMDB gefunden.");
            }
            catch (Exception ex)
            {
                ErrorMessage(ex);
            }
            finally
            {
                if (!IsDisposed)
                    SetTvMazeBusy(false);
            }
        }

        private async System.Threading.Tasks.Task LoadTmdbSeasonsAsync(string showUrl)
        {
            string id;
            if (!TmdbProvider.TryGetShowId(showUrl, out id))
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Warning,
                    "Bitte eine gültige TMDB-Serien-URL oder numerische Serien-ID eingeben.");
                return;
            }

            TmdbProvider provider = CreateTmdbProvider();
            if (provider == null)
                return;

            SetTvMazeBusy(true);
            try
            {
                IReadOnlyList<SeasonMetadata> seasons = await provider.GetSeasonsAsync(id, CancellationToken.None);
                if (IsDisposed || cmbWebsite.SelectedIndex != TmdbIndex)
                    return;
                if (listResult.SelectedIndex >= 0
                    && !string.Equals(((KeyValuePair<string, string>)listResult.SelectedItem).Key,
                        showUrl, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(txtUrl.Text, showUrl, StringComparison.OrdinalIgnoreCase))
                    return;

                txtUrl.Text = showUrl;
                var choices = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("all", "Alle Staffeln (ohne Specials)")
                };
                choices.AddRange(seasons.Select(season => new KeyValuePair<string, string>(
                    season.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    season.Number == 0 ? "Specials (Staffel 0)" : season.Name)));
                cmbSeasons.DataSource = choices;
                cmbSeasons.DisplayMember = "Value";
                cmbSeasons.ValueMember = "Key";
                cmbSeasons.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                ErrorMessage(ex);
            }
            finally
            {
                if (!IsDisposed)
                    SetTvMazeBusy(false);
            }
        }

        private async System.Threading.Tasks.Task LoadTmdbEpisodesAsync()
        {
            string id;
            if (!TmdbProvider.TryGetShowId(txtUrl.Text, out id))
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Warning,
                    "Bitte zuerst eine TMDB-Serie auswählen oder ihre URL eingeben.");
                return;
            }
            if (!(cmbSeasons.SelectedItem is KeyValuePair<string, string>))
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Warning,
                    "Bitte zuerst eine Staffel oder 'Alle Staffeln' ausdrücklich auswählen.");
                return;
            }

            TmdbProvider provider = CreateTmdbProvider();
            if (provider == null)
                return;

            string key = ((KeyValuePair<string, string>)cmbSeasons.SelectedItem).Key;
            bool allSeasons = key == "all";
            int seasonNumber;
            if (!allSeasons && (!int.TryParse(key, out seasonNumber) || seasonNumber < 0))
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Warning, "Ungültige TMDB-Staffel.");
                return;
            }

            SetTvMazeBusy(true);
            try
            {
                IReadOnlyList<EpisodeMetadata> episodes = allSeasons
                    ? await provider.GetEpisodesAsync(id, false, CancellationToken.None)
                    : await provider.GetSeasonEpisodesAsync(id, int.Parse(key), CancellationToken.None);
                var usable = episodes.Where(item => item.EpisodeNumber.HasValue
                    && item.EpisodeNumber.Value > 0
                    && !string.IsNullOrWhiteSpace(item.Title))
                    .OrderBy(item => item.SeasonNumber).ThenBy(item => item.EpisodeNumber.Value).ToList();

                if (usable.Count == 0)
                    throw new MetadataProviderException("TMDB liefert keine eindeutig nummerierten Episodentitel für diese Auswahl.");

                int deCount = usable.Count(item => item.Language == "de");
                int enCount = usable.Count(item => item.Language == "en");
                int skipped = episodes.Count - usable.Count;
                string notice = "Quelle: TMDB\nDeutsche Titel: " + deCount
                    + "\nEnglische Titel: " + enCount
                    + "\nNicht zugeordnete/fehlende Titel: " + skipped
                    + "\n\nEpisodennummerierung und Namen vor dem Umbenennen prüfen. " +
                    "Daten in den Editor übernehmen?";
                if (MessageBox.Show(notice, "TMDB – Titelsprachen", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information) != DialogResult.Yes)
                    return;

                episodeList = usable.Select(item =>
                    (allSeasons
                        ? "S" + item.SeasonNumber.ToString("00") + "E" + item.EpisodeNumber.Value.ToString("00")
                        : item.EpisodeNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    + "\t" + CleanTvMazeTitle(item.Title)).ToList();

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                ErrorMessage(ex);
            }
            finally
            {
                if (!IsDisposed)
                    SetTvMazeBusy(false);
            }
        }

        private void SetTvMazeBusy(bool busy)
        {
            pgLoading.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
            setEnabled(!busy);
        }

        private async System.Threading.Tasks.Task SearchTvMazeAsync()
        {
            string query = txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Information, "Bitte einen Serientitel eingeben.");
                return;
            }

            SetTvMazeBusy(true);
            try
            {
                IReadOnlyList<SeriesMatch> matches = await tvMaze.SearchSeriesAsync(query, CancellationToken.None);
                var links = matches.Where(match => !string.IsNullOrEmpty(match.WebUrl))
                    .Select(match => new KeyValuePair<string, string>(match.WebUrl,
                        match.Name + (match.PremiereYear.HasValue ? " (" + match.PremiereYear.Value + ")" : "")))
                    .ToList();
                setReceivedUrls(links);
                if (links.Count == 0)
                    MessageHandler.MessagesOK(MessageBoxIcon.Information, "Keine Serien auf TVmaze gefunden.");
            }
            catch (Exception ex)
            {
                ErrorMessage(ex);
            }
            finally
            {
                if (!IsDisposed)
                    SetTvMazeBusy(false);
            }
        }

        private async System.Threading.Tasks.Task LoadTvMazeSeasonsAsync(string showUrl)
        {
            string id;
            if (!TvMazeProvider.TryGetShowId(showUrl, out id))
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Warning,
                    "Bitte eine TVmaze-Serien-URL oder eine numerische Serien-ID eingeben.");
                return;
            }

            SetTvMazeBusy(true);
            try
            {
                IReadOnlyList<SeasonMetadata> seasons = await tvMaze.GetSeasonsAsync(id, CancellationToken.None);
                if (IsDisposed || cmbWebsite.SelectedIndex != TvMazeIndex)
                    return;

                // A slower request for an old selection must not overwrite a newer selection.
                if (listResult.SelectedIndex >= 0 && showUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(((KeyValuePair<string, string>)listResult.SelectedItem).Key,
                        showUrl, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(txtUrl.Text, showUrl, StringComparison.OrdinalIgnoreCase))
                    return;

                txtUrl.Text = showUrl;
                var items = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("all", "Alle Staffeln (ohne Specials)")
                };
                items.AddRange(seasons.Select(season => new KeyValuePair<string, string>(
                    season.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    season.Number == 0 ? "Specials (Staffel 0)" : season.Name)));
                cmbSeasons.DataSource = items;
                cmbSeasons.DisplayMember = "Value";
                cmbSeasons.ValueMember = "Key";
                // Do not assume a season for the user's files. Require an explicit choice.
                cmbSeasons.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                ErrorMessage(ex);
            }
            finally
            {
                if (!IsDisposed)
                    SetTvMazeBusy(false);
            }
        }

        private async System.Threading.Tasks.Task LoadTvMazeEpisodesAsync()
        {
            string id;
            if (!TvMazeProvider.TryGetShowId(txtUrl.Text, out id))
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Warning,
                    "Bitte zuerst eine TVmaze-Serie auswählen oder eine gültige TVmaze-URL eingeben.");
                return;
            }

            if (!(cmbSeasons.SelectedItem is KeyValuePair<string, string>))
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Warning,
                    "Bitte zuerst eine Staffel oder 'Alle Staffeln' ausdrücklich auswählen.");
                return;
            }
            string seasonKey = ((KeyValuePair<string, string>)cmbSeasons.SelectedItem).Key;
            bool allSeasons = seasonKey == "all";
            int season = 0;
            if (!allSeasons && !int.TryParse(seasonKey, out season))
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Warning, "Ungültige Staffel.");
                return;
            }

            SetTvMazeBusy(true);
            try
            {
                IReadOnlyList<EpisodeMetadata> episodes = await tvMaze.GetEpisodesAsync(id, true, CancellationToken.None);
                var candidates = episodes.Where(ep => allSeasons ? !ep.IsSpecial : ep.SeasonNumber == season).ToList();
                var usable = candidates.Where(ep => ep.EpisodeNumber.HasValue && ep.EpisodeNumber.Value > 0
                    && !string.IsNullOrWhiteSpace(ep.Title)).OrderBy(ep => ep.SeasonNumber)
                    .ThenBy(ep => ep.EpisodeNumber.Value).ToList();

                if (usable.Count == 0)
                    throw new MetadataProviderException("Keine eindeutig nummerierten Episodentitel für diese Auswahl vorhanden.");

                int skipped = candidates.Count - usable.Count;
                if (skipped > 0)
                    MessageHandler.MessagesOK(MessageBoxIcon.Warning,
                        skipped + " Episoden ohne eindeutige Nummer oder Titel wurden ausgelassen.");

                episodeList = usable.Select(ep =>
                    (allSeasons
                        ? "S" + ep.SeasonNumber.ToString("00") + "E" + ep.EpisodeNumber.Value.ToString("00")
                        : ep.EpisodeNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    + "\t" + CleanTvMazeTitle(ep.Title)).ToList();

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                ErrorMessage(ex);
            }
            finally
            {
                if (!IsDisposed)
                    SetTvMazeBusy(false);
            }
        }

        private static string CleanTvMazeTitle(string title)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(title.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ')
                .Select(ch => invalid.Contains(ch) || char.IsControl(ch) ? ' ' : ch).ToArray())
                .Trim().TrimEnd('.', ' ');
        }

    }
}