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
        private const int TvMazeIndex = 4;
        private const int TmdbIndex = 5;
        private Label lblTmdbCredential;
        private TextBox txtTmdbCredential;
        private Button btnTmdbSave;
        private Button btnTmdbDelete;
        private bool suppressApiSelection;
        private readonly IEpisodeMetadataProvider tvMaze = new TvMazeProvider();
        private readonly string tvdbDomain = "https://www.thetvdb.com";
        private readonly string aniDBDomain = "https://anidb.net/perl-bin/";
        private readonly string fernsehserienDomain = "https://www.fernsehserien.de";

        public Anisearch_Table()
        {
            InitializeComponent();
            InitializeTmdbCredentialControls();
            setData();
        }

        #region Standardwerte und gespeicherte Daten setzen
        private void setData()
        {
            TopMost = Properties.Settings.Default.Foreground;
            int selected = Properties.Settings.Default.SelectedWebsite;
            cmbWebsite.SelectedIndex = selected >= 0 && selected < cmbWebsite.Items.Count ? selected : TvMazeIndex;
            setCmbLanguages();

        }
        #endregion


        private void setCmbLanguages()
        {
            cmbLanguageEpisodes.DataSource = cmbWebsite.SelectedIndex == TmdbIndex ? getLanguageData() : (cmbWebsite.SelectedIndex == 0 || cmbWebsite.SelectedIndex == TvMazeIndex ? getEnglish() : cmbWebsite.SelectedIndex == 2 ? getGerman() : getLanguagesWithJapanese());
            cmbLanguageEpisodes.DisplayMember = "Value";
            cmbLanguageEpisodes.ValueMember = "Key";


            List<KeyValuePair<string, string>> languages = new List<KeyValuePair<string, string>>();
            if (cmbWebsite.SelectedIndex == 0 || cmbWebsite.SelectedIndex == TvMazeIndex)
            {
                languages = getEnglish();
            }

            else if (cmbWebsite.SelectedIndex == TmdbIndex)
            {
                languages = getLanguageData();
            }
            else if (cmbWebsite.SelectedIndex == 1)
            {
                languages.Add(new KeyValuePair<string, string>("com", "None"));
            }
            else if (cmbWebsite.SelectedIndex == 2)
            {
                languages = getGerman();
            }

            else
            {
                languages.AddRange(getLanguagesWithJapanese());
            }
            cmbLanguageSearch.DataSource = languages;
            cmbLanguageSearch.DisplayMember = "Value";
            cmbLanguageSearch.ValueMember = "Key";

            cmbLanguageSearch.SelectedIndex = Math.Min(Math.Max(0, Properties.Settings.Default.SecondLanguage), cmbLanguageSearch.Items.Count - 1);
            cmbLanguageEpisodes.SelectedIndex = Math.Min(Math.Max(0, Properties.Settings.Default.Language), cmbLanguageEpisodes.Items.Count - 1);

        }

        private void addGerman(List<KeyValuePair<string, string>> languages)
        {
            languages.Add(new KeyValuePair<string, string>("de", "Deutsch"));
        }

        private void addEnglish(List<KeyValuePair<string, string>> languages)
        {
            languages.Add(new KeyValuePair<string, string>("en", "English"));
        }

        private List<KeyValuePair<string, string>> getGerman()
        {
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            addGerman(list);
            return list;
        }

        private List<KeyValuePair<string, string>> getEnglish()
        {
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            addEnglish(list);
            return list;
        }

        private void addJapanese(List<KeyValuePair<string, string>> languages)
        {
            languages.Add(new KeyValuePair<string, string>("ja", "日本語"));
        }

        private List<KeyValuePair<string, string>> getLanguageData()
        {
            List<KeyValuePair<string, string>> languages = new List<KeyValuePair<string, string>>();
            addGerman(languages);
            addEnglish(languages);
            return languages;
        }

        private List<KeyValuePair<string, string>> getLanguagesWithJapanese()
        {
            List<KeyValuePair<string, string>> languages = getLanguageData();
            addJapanese(languages);
            return languages;
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

            string language = ((KeyValuePair<string, string>)cmbLanguageEpisodes.SelectedItem).Key;
            int selectedIndex = cmbWebsite.SelectedIndex;
            string url = txtUrl.Text;
            string urlTVDB = cmbSeasons.SelectedItem != null ? ((KeyValuePair<string, string>)cmbSeasons.SelectedItem).Key : null;
            int season = cmbSeasons.SelectedIndex;

            new Thread(() =>
            {
                bool valid = false;
                try
                {
                    StartProgressBar();
                    List<string> result = null;
                    if (selectedIndex == 1 && (valid = checkUrlAnisearch(language, url, out url))) //Anisearch
                    {
                        result = getEpisodeList(url, language);
                    }
                    else if (selectedIndex == 3 && (valid = checkUrlTVDB(url, out url)))
                    {

                        result = GetEpisodeListTVDB(urlTVDB, language);
                    }
                    else if (selectedIndex == 0 && (valid = checkUrlAniDB(url)))
                    {
                        result = getEpisodeListAniDB(url);
                    }
                    else if (valid = checkUrlFernsehserien(url, out url))
                    {
                        result = getEpisodeListFernsehserien(url, season);
                    }
                    setUrl(url);
                    episodeList = result;

                }
                catch (Exception ex)
                {
                    ErrorMessage(ex);
                }
                finally
                {
                    StopProgressBar();
                    if (valid)
                    {
                        CloseForm();
                    }
                    else
                    {
                        ErrorMessage(new UriFormatException());
                    }
                }
            }).Start();
        }

        private bool checkUrlFernsehserien(string inputUrl, out string url)
        {
            bool valid;
            url = string.Empty;
            string startText = "https://www.fernsehserien.de/";
            if (valid = inputUrl.StartsWith(startText))
            {
                inputUrl = inputUrl.Replace(startText, string.Empty);
                string searchText = inputUrl.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries)[0];
                string changedUrl = startText + searchText + "/episodenguide";
                HtmlWeb htmlWeb = new HtmlWeb();
                HtmlAgilityPack.HtmlDocument doc = htmlWeb.Load(changedUrl);

                if (valid = doc.DocumentNode.SelectSingleNode("//nav[@class='series-menu']") != null)
                {
                    url = changedUrl;
                }
            }
            return valid;
        }

        private List<string> getEpisodeListFernsehserien(string url, int season)
        {

            List<string> episodeList = new List<string>();
            HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = web.Load(url);

            HtmlNodeCollection tables = doc.DocumentNode.SelectNodes("//table[contains(@class,'episodenliste')]//tbody[@itemprop='season' or @itemprop='containsSeason']");
            HtmlNodeCollection newTables = doc.DocumentNode.SelectNodes("//div[@class='episodenliste-2019']");
            int beginCounter = season == 0 ? 0 : season - 1;
            if (newTables == null)
            {
                int endCounter = season == 0 ? tables.Count - 1 : season - 1;
                for (int i = beginCounter; i <= endCounter; i++)
                {
                    foreach (HtmlNode row in tables[i].SelectNodes("tr[@itemprop='episode']"))
                    {
                        StringBuilder builder = new StringBuilder();

                        builder.Append(WebUtility.HtmlDecode(row.SelectNodes("td")[1]?.InnerText ?? string.Empty).Trim()).Append("\t");
                        builder.Append(WebUtility.HtmlDecode(row.SelectNodes("td")[4]?.InnerText ?? string.Empty).Trim()).Append("\t");
                        var episodeTitle = row.SelectSingleNode("td//span[@itemprop='name']")?.InnerText ?? row.SelectSingleNode("td [@lang='ja']//span[@itemprop='name']")?.InnerText ?? string.Empty;
                        builder.Append(WebUtility.HtmlDecode(episodeTitle).Trim());

                        episodeList.Add(builder.ToString().Trim());
                    }
                }
            }
            else
            {
                int endCounter = season == 0 ? newTables.Count - 1 : season - 1;
                for (int i = beginCounter; i <= endCounter; i++)
                {
                    foreach (HtmlNode row in newTables[i].SelectNodes("a"))
                    {
                        string episodeNumber = row.SelectSingleNode("div[@role='cell' and @itemprop='episodeNumber']")?.Attributes["content"]?.Value;
                        string titleGerman = row.SelectSingleNode("div[@role='cell']/span[@itemprop='name']")?.InnerText;
                        string titleFallback = titleGerman ?? row.SelectSingleNode("div[@role='cell' and @lang='ja']")?.InnerText;

                        episodeList.Add($"{FormatWebString(episodeNumber)}\t{FormatWebString(titleFallback)}");
                    }
                }
            }
            return episodeList;
        }

        private string FormatWebString(string text)
        {
            return WebUtility.HtmlDecode(text ?? string.Empty).Trim();
        }

        private List<string> GetEpisodeListTVDB(string url, string language)
        {

            language = language == "com" ? "en" : language;

            List<string> episodeList = new List<string>();
            HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = web.Load(url);

            foreach (HtmlNode table in doc.DocumentNode.SelectNodes("//table[@id='translations']//tbody"))
            {
                foreach (HtmlNode row in table.SelectNodes("tr"))
                {
                    string line = null;
                    try
                    {
                        StringBuilder builder = new StringBuilder();

                        builder.Append(WebUtility.HtmlDecode(row.SelectNodes("td")[0].SelectSingleNode("a").InnerText).Trim()).Append("\t");

                        HtmlNode episodeName = row.SelectNodes("td/a")[1].SelectSingleNode($"span[@data-language='{language}']");

                        line = builder.ToString();
                        if (episodeName != null)
                        {
                            builder.Append(WebUtility.HtmlDecode(episodeName.InnerText).Trim());
                        }

                        line = builder.ToString();
                    }
                    catch (NullReferenceException)
                    {
                    }


                    if (line != null)
                    {
                        episodeList.Add(line.Trim());
                    }
                }
            }
            return episodeList;
        }

        private List<string> getEpisodeListAniDB(string url)
        {

            HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = web.Load(url);


            List<string> episodeList = new List<string>();

            foreach (HtmlNode row in doc.DocumentNode.SelectSingleNode("//table[@id='eplist']//tbody").SelectNodes("tr"))
            {
                string line = null;
                try
                {
                    StringBuilder builder = new StringBuilder();
                    var list = row.SelectNodes("td");

                    builder.Append(WebUtility.HtmlDecode(list[0].InnerText).Trim()).Append("\t");
                    builder.Append(WebUtility.HtmlDecode(list[1].InnerText).Trim());
                    line = builder.ToString();


                }
                catch (NullReferenceException)
                {
                }


                if (line != null)
                {
                    episodeList.Add(line.Trim());
                }
            }
            return episodeList;
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
            bool valid;
            url = string.Empty;
            string[] text = inputUrl.Replace("http://", "").Replace("https://", "").Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
            if (valid = (inputUrl.ToUpper().Contains("ANISEARCH.") && text.Length >= 3))
            {
                url = getDomainEpisodes(language) + "/" + text[1] + "/" + text[2] + "/episodes";
            }
            return valid;
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
            if (e.KeyCode == Keys.Enter)
            {
                if (cmbWebsite.SelectedIndex == TvMazeIndex)
                {
                    e.SuppressKeyPress = true;
                    await SearchTvMazeAsync();
                    return;
                }
                if (cmbWebsite.SelectedIndex == TmdbIndex)
                {
                    e.SuppressKeyPress = true;
                    await SearchTmdbAsync();
                    return;
                }
                StartProgressBar();

                string searchText = txtSearch.Text;
                string domain = getAnisearchDomain();
                int index = cmbWebsite.SelectedIndex;
                int selectedLanguageIndex = cmbLanguageSearch.SelectedIndex;
                string selectedLanguage = ((KeyValuePair<string, string>)cmbLanguageSearch.SelectedItem).Key;
                new Thread(() =>
                {
                    try
                    {
                        switch (index)
                        {
                            case 0: //aniDB
                                searchTextAniDB(searchText);
                                break;


                            case 1: //Anisearch
                                searchTextAnisearch(getSearchUrlAnisearch(searchText, domain), domain);
                                break;

                            case 2: //Fernsehserien.de
                                searchTextFernsehserien(searchText);
                                break;

                            case 3: //TVDB
                                string language = selectedLanguageIndex == 0 ? "de" : selectedLanguage;
                                searchTextTVDB(searchText, language);
                                break;
                        }
                    }
                    catch (Exception exception)
                    {
                        ErrorMessage(exception);
                    }
                    finally
                    {
                        StopProgressBar();
                    }
                }).Start();

            }
        }

        private string adjustSearchTextTVDB(string text)
        {
            return text.Replace("/", "%2F");
        }

        private void searchTextFernsehserien(string searchText)
        {
            Uri ur = new Uri($"{fernsehserienDomain}/suche/{adjustSearchTextTVDB(searchText).Replace("%2F", " ")}");
            List<KeyValuePair<string, string>> resultUrl = new List<KeyValuePair<string, string>>();

            HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = web.Load(ur.AbsoluteUri);
            string url = web.ResponseUri.ToString();
            if (!url.StartsWith(fernsehserienDomain + "/suche/"))
            {
                setUrl(url);
                getSeasonsFernsehserien(url);
            }
            else
            {
                foreach (HtmlNode entry in doc.DocumentNode.SelectNodes("//li[contains(@class,'suchergebnisse-sendung')]"))
                {
                    string text = WebUtility.HtmlDecode(entry.SelectSingleNode("a/dl/dt").InnerText);
                    string nurl = WebUtility.HtmlDecode(entry.SelectSingleNode("a[@href]").Attributes["href"].Value);
                    resultUrl.Add(new KeyValuePair<string, string>(fernsehserienDomain + nurl + "/episodenguide", text));
                }
                setReceivedUrls(resultUrl);
            }
        }


        private void searchTextAniDB(string searchText)
        {
            string baseUrl = "https://anidb.net/perl-bin/animedb.pl?";
            Uri ur = new Uri($"{baseUrl}adb.search={adjustSearchTextTVDB(searchText)}&show=animelist&do.search=search");
            List<KeyValuePair<string, string>> resultUrl = new List<KeyValuePair<string, string>>();

            HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = web.Load(ur.AbsoluteUri);

            string url = web.ResponseUri.ToString();
            if (url.StartsWith($"{baseUrl}?show=anime&aid="))
            {
                setUrl(url);
            }
            else
            {
                HtmlNode table = doc.DocumentNode.SelectNodes("//table[@id= 'animelist']//tbody")[0];
                foreach (HtmlNode row in table.SelectNodes("tr"))
                {
                    string text = WebUtility.HtmlDecode(row.SelectSingleNode("td[@data-label='Title']//a").InnerText);
                    string nurl = WebUtility.HtmlDecode(row.SelectSingleNode("td//a[@href]").Attributes["href"].Value);
                    resultUrl.Add(new KeyValuePair<string, string>(aniDBDomain + nurl, text));
                }
                setReceivedUrls(resultUrl);
            }
        }

        private void searchTextTVDB(string searchText, string language) //Thread
        {
            Uri ur = new Uri($@"{tvdbDomain}/search?q={adjustSearchTextTVDB(searchText)}&l={language}");

            List<KeyValuePair<string, string>> resultUrl = new List<KeyValuePair<string, string>>();

            HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = web.Load(ur.AbsoluteUri);


            HtmlNode table = doc.DocumentNode.SelectNodes("//table//tbody")[0];

            foreach (HtmlNode row in table.SelectNodes("tr"))
            {
                string text = WebUtility.HtmlDecode(row.SelectSingleNode("td//a").InnerText);
                string nurl = WebUtility.HtmlDecode(row.SelectSingleNode("td//a[@href]").Attributes["href"].Value);
                resultUrl.Add(new KeyValuePair<string, string>(tvdbDomain + nurl, text));
            }
            setReceivedUrls(resultUrl);
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
            if (listResult.SelectedIndex != -1)
            {
                if (suppressApiSelection)
                    return;
                if (cmbWebsite.SelectedIndex == TvMazeIndex)
                {
                    await LoadTvMazeSeasonsAsync(((KeyValuePair<string, string>)listResult.SelectedItem).Key);
                    return;
                }
                if (cmbWebsite.SelectedIndex == TmdbIndex)
                {
                    await LoadTmdbSeasonsAsync(((KeyValuePair<string, string>)listResult.SelectedItem).Key);
                    return;
                }
                string url = ((KeyValuePair<string,string>)listResult.SelectedItem).Key;
                StartProgressBar();
                int selectedIndex = cmbWebsite.SelectedIndex;
                new Thread(() =>
                {
                    try
                    {
                        if (selectedIndex == 3) //TVDB
                        {
                            getSeasons(url);
                        }
                        else if (selectedIndex == 2) //Fernsehserien
                        {
                            getSeasonsFernsehserien(url);
                        }
                        StopProgressBar();

                        txtUrl.BeginInvoke(new MethodInvoker(() =>
                        {
                            txtUrl.Text = url;
                        }));
                    }
                    catch(Exception ex)
                    {
                        ErrorMessage(ex);
                    }
                    finally
                    {
                        StopProgressBar();
                    }
                }).Start();
            }
        }

        private void getSeasonsFernsehserien(string url)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = web.Load(url);
            IEnumerable<KeyValuePair<string, string>> seasons = doc.DocumentNode.SelectNodes("//nav[@class='series-menu']/ul/li[a[contains(text(), 'Episoden')]]/ul/li/a[1]")?.Select(node => new KeyValuePair<string, string>(fernsehserienDomain + node.Attributes["href"].Value, node.InnerText));
            List<KeyValuePair<string, string>> resultUrl = seasons?.ToList() ?? new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>(url, "Übersicht") };
            setSeasons(resultUrl);
        }

        private void getSeasons(string url)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = web.Load(url);

            HtmlNode table = doc.DocumentNode.SelectSingleNode("//ul[@class= 'list-group list-group-condensed']");
            List<KeyValuePair<string, string>> resultUrl = new List<KeyValuePair<string, string>>();

            foreach (HtmlNode row in table.SelectNodes("li[@class= 'list-group-item']"))
            {
                string text = WebUtility.HtmlDecode(row.SelectSingleNode("h4/a").InnerText).Trim();
                string nurl = WebUtility.HtmlDecode(row.SelectSingleNode("h4/a[@href]").Attributes["href"].Value);
                resultUrl.Add(new KeyValuePair<string, string>(tvdbDomain + nurl, text));
            }
            setSeasons(resultUrl);
        }

        private void setSeasons(object data)
        {
            cmbSeasons.BeginInvoke(new MethodInvoker(() =>
            {
                cmbSeasons.DataSource = null;
                cmbSeasons.DataSource = data;
                cmbSeasons.DisplayMember = "Value";
                cmbSeasons.ValueMember = "Key";
            }));
        }

        private void cmbWebsite_SelectedIndexChanged(object sender, EventArgs e)
        {
            listResult.DataSource = null;
            cmbSeasons.DataSource = null;
            lblSeason.Visible = cmbSeasons.Visible = cmbWebsite.SelectedIndex > 1;
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
            string url = txtUrl.Text;
            string language = ((KeyValuePair<string, string>)cmbLanguageEpisodes.SelectedItem).Key;
            bool valid = cmbWebsite.SelectedIndex == 1 && checkUrlAnisearch(language,url, out url) || cmbWebsite.SelectedIndex == 0 && checkUrlAniDB(url);
            int selectedIndex = cmbWebsite.SelectedIndex;
            StartProgressBar();

            new Thread(() =>
            {
                if (selectedIndex == 3 && (valid = checkUrlTVDB(url, out url))) {
                
                    getSeasons(url);
                }
                else if(selectedIndex == 2 && (valid = checkUrlFernsehserien(url, out url)))
                {
                    getSeasonsFernsehserien(url);
                }
                
                StopProgressBar();
                if (!valid)
                {
                    ErrorMessage(new UriFormatException());
                }
                setUrl(url);
            }).Start();
        }

        private bool checkUrlTVDB(string inputUrl, out string url)
        {
            bool valid = false;
            url = string.Empty;
            string startText = "https://www.thetvdb.com/series/";
            if (inputUrl.StartsWith(startText))
            {
                string search = inputUrl.Replace(startText, string.Empty);

                if(search.IndexOf("/") != -1)
                {
                    search = search.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries)[0];

                }
                HtmlWeb htmlWeb = new HtmlWeb();
                HttpStatusCode lastStatusCode = HttpStatusCode.OK;

                htmlWeb.PostResponse = (request, response) =>
                {
                    if (response != null)
                    {
                        lastStatusCode = response.StatusCode;
                    }
                };
                htmlWeb.Load(inputUrl);
                if(valid = (lastStatusCode == HttpStatusCode.OK))
                {
                    url = $"{startText}{search}";
                }
            }
            return valid;
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

        private bool checkUrlAniDB(string url)
        {
            HtmlWeb htmlWeb = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = htmlWeb.Load(url);
            return doc.DocumentNode.SelectSingleNode("//table[@id='eplist']//tbody") != null;
        }
    }
}