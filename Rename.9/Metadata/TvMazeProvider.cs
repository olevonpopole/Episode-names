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
    /// <summary>Public TVmaze JSON API. No registration or API key is required.</summary>
    internal sealed class TvMazeProvider : IEpisodeMetadataProvider
    {
        public const string ProviderKey = "tvmaze";
        private const string BaseUrl = "https://api.tvmaze.com";
        private static readonly HttpClient SharedClient = CreateClient();
        private static readonly SemaphoreSlim RequestGate = new SemaphoreSlim(1, 1);
        private static DateTime nextRequestUtc = DateTime.MinValue;

        private readonly HttpClient client;

        public string Id { get { return ProviderKey; } }
        public string DisplayName { get { return "TVmaze"; } }
        public bool RequiresApiKey { get { return false; } }

        // Supplying an HttpClient also makes the transport testable without real HTTP requests.
        public TvMazeProvider(HttpClient httpClient = null)
        {
            client = httpClient ?? SharedClient;
        }

        public async Task<IReadOnlyList<SeriesMatch>> SearchSeriesAsync(string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Bitte einen Suchbegriff eingeben.", "query");

            SearchResultDto[] results = await ReadJsonAsync<SearchResultDto[]>(
                "/search/shows?q=" + Uri.EscapeDataString(query.Trim()), cancellationToken).ConfigureAwait(false);

            return (results ?? new SearchResultDto[0])
                .Where(entry => entry != null && entry.Show != null && entry.Show.Id > 0)
                .Select(entry => new SeriesMatch(ProviderKey,
                    entry.Show.Id.ToString(CultureInfo.InvariantCulture),
                    entry.Show.Name ?? string.Empty,
                    entry.Show.Language ?? string.Empty,
                    entry.Show.Url,
                    ParseYear(entry.Show.Premiered)))
                .ToList();
        }

        public async Task<IReadOnlyList<SeasonMetadata>> GetSeasonsAsync(string seriesId, CancellationToken cancellationToken)
        {
            int id = ParseId(seriesId);
            SeasonDto[] seasons = await ReadJsonAsync<SeasonDto[]>(
                "/shows/" + id.ToString(CultureInfo.InvariantCulture) + "/seasons", cancellationToken).ConfigureAwait(false);

            return (seasons ?? new SeasonDto[0])
                .Where(season => season != null && season.Number.HasValue)
                .Select(season => new SeasonMetadata(season.Number.Value,
                    season.Id.ToString(CultureInfo.InvariantCulture),
                    season.Name ?? (season.Number.Value == 0 ? "Specials" :
                        "Staffel " + season.Number.Value.ToString(CultureInfo.InvariantCulture))))
                .OrderBy(season => season.Number)
                .ToList();
        }

        public async Task<IReadOnlyList<EpisodeMetadata>> GetEpisodesAsync(string seriesId,
            bool includeSpecials, CancellationToken cancellationToken)
        {
            int id = ParseId(seriesId);
            string path = "/shows/" + id.ToString(CultureInfo.InvariantCulture) + "/episodes";
            if (includeSpecials)
                path += "?specials=1";

            EpisodeDto[] episodes = await ReadJsonAsync<EpisodeDto[]>(path, cancellationToken).ConfigureAwait(false);
            return (episodes ?? new EpisodeDto[0])
                .Where(episode => episode != null && episode.Id > 0)
                .Select(episode => new EpisodeMetadata(
                    ProviderKey, episode.Id.ToString(CultureInfo.InvariantCulture),
                    episode.Season, episode.Number, null,
                    episode.Name ?? string.Empty, "und",
                    episode.Season == 0 || !string.IsNullOrEmpty(episode.Type)
                        && !string.Equals(episode.Type, "regular", StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        /// <summary>Accept only TVmaze show URLs or a positive numeric show ID.</summary>
        public static bool TryGetShowId(string input, out string showId)
        {
            showId = null;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            int numericId;
            if (int.TryParse(input.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out numericId) && numericId > 0)
            {
                showId = numericId.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            Uri uri;
            if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !(string.Equals(uri.Host, "tvmaze.com", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Host, "www.tvmaze.com", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Host, "api.tvmaze.com", StringComparison.OrdinalIgnoreCase)))
                return false;

            string[] segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length < 2 || !string.Equals(segments[0], "shows", StringComparison.OrdinalIgnoreCase)
                || !int.TryParse(segments[1], NumberStyles.None, CultureInfo.InvariantCulture, out numericId)
                || numericId <= 0)
                return false;

            showId = numericId.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        private static int ParseId(string value)
        {
            int id;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) || id <= 0)
                throw new ArgumentException("Ungültige TVmaze-Serien-ID.", "value");
            return id;
        }

        private static int? ParseYear(string premiered)
        {
            DateTime result;
            if (DateTime.TryParseExact(premiered, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out result))
                return result.Year;
            return null;
        }

        private static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            client.MaxResponseContentBufferSize = 16 * 1024 * 1024;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Episode-Names/2.0 (https://github.com/olevonpopole/Episode-names)");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        private static async Task WaitForRequestSlotAsync(CancellationToken cancellationToken)
        {
            // Conservative shared cap: ~18 requests per 10 seconds (TVmaze publishes 20/10s/IP).
            await RequestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                TimeSpan delay = nextRequestUtc - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                nextRequestUtc = DateTime.UtcNow.AddMilliseconds(550);
            }
            finally
            {
                RequestGate.Release();
            }
        }

        private async Task<T> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                await WaitForRequestSlotAsync(cancellationToken).ConfigureAwait(false);
                using (HttpResponseMessage response = await client.GetAsync(BaseUrl + path,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    if ((response.StatusCode == (HttpStatusCode)429 || (int)response.StatusCode >= 500) && attempt < 2)
                    {
                        TimeSpan wait = response.Headers.RetryAfter != null && response.Headers.RetryAfter.Delta.HasValue
                            ? response.Headers.RetryAfter.Delta.Value
                            : TimeSpan.FromSeconds(2 * (attempt + 1));
                        if (wait < TimeSpan.Zero)
                            wait = TimeSpan.FromSeconds(2);
                        await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                        throw new MetadataProviderException("TVmaze-Anfrage fehlgeschlagen (HTTP " +
                            (int)response.StatusCode + ").");

                    try
                    {
                        using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        {
                            return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
                        }
                    }
                    catch (SerializationException ex)
                    {
                        throw new MetadataProviderException("TVmaze hat unerwartete Episodendaten geliefert.", ex);
                    }
                    catch (InvalidDataContractException ex)
                    {
                        throw new MetadataProviderException("TVmaze-Datenformat konnte nicht gelesen werden.", ex);
                    }
                }
            }

            throw new MetadataProviderException("TVmaze konnte nach mehreren Versuchen nicht erreicht werden.");
        }

        [DataContract]
        private sealed class SearchResultDto
        {
            [DataMember(Name = "show")] public ShowDto Show { get; set; }
        }

        [DataContract]
        private sealed class ShowDto
        {
            [DataMember(Name = "id")] public int Id { get; set; }
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "language")] public string Language { get; set; }
            [DataMember(Name = "url")] public string Url { get; set; }
            [DataMember(Name = "premiered")] public string Premiered { get; set; }
        }

        [DataContract]
        private sealed class SeasonDto
        {
            [DataMember(Name = "id")] public int Id { get; set; }
            [DataMember(Name = "number")] public int? Number { get; set; }
            [DataMember(Name = "name")] public string Name { get; set; }
        }

        [DataContract]
        private sealed class EpisodeDto
        {
            [DataMember(Name = "id")] public int Id { get; set; }
            [DataMember(Name = "season")] public int Season { get; set; }
            [DataMember(Name = "number")] public int? Number { get; set; }
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "type")] public string Type { get; set; }
        }
    }
}
