using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Episode_Names.Metadata
{
    /// <summary>
    /// Public Jikan v4 / MyAnimeList catalogue. Each MAL anime entry has its
    /// own episode sequence: we expose it as local S01 only, never infer TV
    /// broadcast seasons from an unrelated MAL entry.
    /// </summary>
    internal sealed class JikanProvider : IEpisodeMetadataProvider
    {
        internal const string ProviderKey = "jikan";
        private const string BaseUrl = "https://api.jikan.moe/v4";
        private static readonly HttpClient SharedClient = CreateClient();
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static DateTime nextRequest = DateTime.MinValue;
        private readonly HttpClient client;
        private readonly string language;

        internal JikanProvider(string preferredLanguage = "en", HttpClient httpClient = null)
        {
            language = MetadataSelection.CanonicalLanguage(preferredLanguage);
            client = httpClient ?? SharedClient;
        }

        public string Id { get { return ProviderKey; } }
        public string DisplayName { get { return "Jikan (MyAnimeList)"; } }
        public bool RequiresApiKey { get { return false; } }

        public async Task<IReadOnlyList<SeriesMatch>> SearchSeriesAsync(
            string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Leerer Anime-Suchbegriff.", "query");
            SearchResponse result = await ReadJsonAsync<SearchResponse>(
                "/anime?q=" + Uri.EscapeDataString(query.Trim()) + "&limit=12&sfw=true",
                cancellationToken).ConfigureAwait(false);
            return (result.Data ?? new AnimeDto[0])
                .Where(a => a != null && a.MalId > 0)
                .Select(a => new SeriesMatch(ProviderKey,
                    a.MalId.ToString(CultureInfo.InvariantCulture),
                    !string.IsNullOrWhiteSpace(a.TitleEnglish) && language == "en"
                        ? a.TitleEnglish : a.Title ?? a.TitleEnglish ?? "",
                    "und", a.Url, ParseYear(a.Aired == null ? null : a.Aired.From)))
                .ToList();
        }

        public Task<IReadOnlyList<SeasonMetadata>> GetSeasonsAsync(
            string seriesId, CancellationToken cancellationToken)
        {
            ParseId(seriesId);
            return Task.FromResult((IReadOnlyList<SeasonMetadata>)new[] {
                new SeasonMetadata(1, seriesId, "MAL-Animeeintrag – lokale Staffel 1 (Nummerierung prüfen)")
            });
        }

        public async Task<IReadOnlyList<EpisodeMetadata>> GetEpisodesAsync(
            string seriesId, bool includeSpecials, CancellationToken cancellationToken)
        {
            int id = ParseId(seriesId);
            var episodes = new List<EpisodeMetadata>();
            for (int page = 1; page <= 25; page++)
            {
                EpisodeResponse result = await ReadJsonAsync<EpisodeResponse>(
                    "/anime/" + id.ToString(CultureInfo.InvariantCulture) +
                    "/episodes?page=" + page.ToString(CultureInfo.InvariantCulture),
                    cancellationToken).ConfigureAwait(false);
                foreach (EpisodeDto ep in result.Data ?? new EpisodeDto[0])
                {
                    if (ep == null || ep.MalId < 1) continue;
                    string title = null;
                    string actual = "und";
                    if (language == "ja" && !string.IsNullOrWhiteSpace(ep.TitleJapanese))
                    {
                        title = ep.TitleJapanese; actual = "ja";
                    }
                    else if (!string.IsNullOrWhiteSpace(ep.Title))
                    {
                        title = ep.Title; actual = "und"; // source does not guarantee language
                    }
                    else if (!string.IsNullOrWhiteSpace(ep.TitleJapanese))
                    {
                        title = ep.TitleJapanese; actual = "ja";
                    }
                    if (string.IsNullOrWhiteSpace(title)) continue;
                    episodes.Add(new EpisodeMetadata(ProviderKey,
                        id.ToString(CultureInfo.InvariantCulture) + ":" +
                            ep.MalId.ToString(CultureInfo.InvariantCulture),
                        1, ep.MalId, null, title, actual, false));
                }
                if (result.Pagination == null || !result.Pagination.HasNextPage)
                    return episodes;
            }
            // Never silently hand out an incomplete episode catalogue.
            throw new MetadataProviderException(
                "Jikan-Liste übersteigt die sichere Seitenbegrenzung; keine unvollständige Übernahme.");
        }

        internal static int ParseId(string value)
        {
            int id;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) || id <= 0)
                throw new ArgumentException("Ungültige MAL-ID.", "value");
            return id;
        }

        private static int? ParseYear(string input)
        {
            if (string.IsNullOrEmpty(input) || input.Length < 4) return null;
            int year;
            return int.TryParse(input.Substring(0, 4), out year) &&
                year >= 1900 && year <= 2100 ? (int?)year : null;
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20),
                MaxResponseContentBufferSize = 8 * 1024 * 1024 };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Episode-Names/2.0");
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        private async Task<T> ReadJsonAsync<T>(string path, CancellationToken token)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                await Gate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    var delay = nextRequest - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay, token).ConfigureAwait(false);
                    nextRequest = DateTime.UtcNow.AddMilliseconds(450);
                }
                finally { Gate.Release(); }

                using (var response = await client.GetAsync(BaseUrl + path,
                    HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                {
                    if ((response.StatusCode == (HttpStatusCode)429 ||
                         (int)response.StatusCode >= 500) && attempt < 2)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2 * (attempt + 1)), token)
                            .ConfigureAwait(false);
                        continue;
                    }
                    if (!response.IsSuccessStatusCode)
                        throw new MetadataProviderException(
                            "Jikan HTTP " + (int)response.StatusCode + ".");
                    using (Stream stream = await response.Content.ReadAsStreamAsync()
                        .ConfigureAwait(false))
                    {
                        try
                        {
                            return (T)new DataContractJsonSerializer(typeof(T))
                                .ReadObject(stream);
                        }
                        catch (SerializationException ex)
                        {
                            throw new MetadataProviderException(
                                "Jikan lieferte ein unbekanntes Datenformat.", ex);
                        }
                    }
                }
            }
            throw new MetadataProviderException("Jikan ist derzeit nicht erreichbar.");
        }

        [DataContract] private sealed class SearchResponse
        {
            [DataMember(Name = "data")] public AnimeDto[] Data { get; set; }
        }
        [DataContract] private sealed class AnimeDto
        {
            [DataMember(Name = "mal_id")] public int MalId { get; set; }
            [DataMember(Name = "title")] public string Title { get; set; }
            [DataMember(Name = "title_english")] public string TitleEnglish { get; set; }
            [DataMember(Name = "url")] public string Url { get; set; }
            [DataMember(Name = "aired")] public AiredDto Aired { get; set; }
        }
        [DataContract] private sealed class AiredDto
        {
            [DataMember(Name = "from")] public string From { get; set; }
        }
        [DataContract] private sealed class EpisodeResponse
        {
            [DataMember(Name = "data")] public EpisodeDto[] Data { get; set; }
            [DataMember(Name = "pagination")] public PaginationDto Pagination { get; set; }
        }
        [DataContract] private sealed class PaginationDto
        {
            [DataMember(Name = "has_next_page")] public bool HasNextPage { get; set; }
        }
        [DataContract] private sealed class EpisodeDto
        {
            [DataMember(Name = "mal_id")] public int MalId { get; set; }
            [DataMember(Name = "title")] public string Title { get; set; }
            [DataMember(Name = "title_japanese")] public string TitleJapanese { get; set; }
            [DataMember(Name = "title_romanji")] public string TitleRomanji { get; set; }
        }
    }
}
