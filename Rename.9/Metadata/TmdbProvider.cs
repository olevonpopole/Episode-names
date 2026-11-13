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
    /// <summary>Optional TMDB v3 TV API provider. Keeps all credentials out of log/error messages.</summary>
    internal sealed class TmdbProvider : IEpisodeMetadataProvider
    {
        public const string ProviderKey = "tmdb";
        private const string BaseUrl = "https://api.themoviedb.org/3";
        private static readonly HttpClient SharedClient = CreateClient();
        private readonly HttpClient client;
        private readonly string credential;
        private readonly bool bearer;
        private readonly string language;

        public string Id { get { return ProviderKey; } }
        public string DisplayName { get { return "TMDB"; } }
        public bool RequiresApiKey { get { return true; } }
        public string PreferredLanguage { get { return language; } }

        /// <param name="apiKeyOrReadToken">A user-supplied v3 API key or API Read Access Token (JWT).</param>
        /// <param name="preferredLanguage">Either de or en, defaults to German.</param>
        public TmdbProvider(string apiKeyOrReadToken, string preferredLanguage = "de", HttpClient httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(apiKeyOrReadToken))
                throw new MetadataProviderException("TMDB benötigt einen eigenen kostenlosen API-Key oder API Read Access Token.");
            credential = apiKeyOrReadToken.Trim();
            bearer = credential.StartsWith("eyJ", StringComparison.Ordinal);
            language = string.Equals(preferredLanguage, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "de";
            client = httpClient ?? SharedClient;
        }

        public async Task<IReadOnlyList<SeriesMatch>> SearchSeriesAsync(string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Bitte einen Serientitel eingeben.", "query");

            SearchDto response = await ReadJsonAsync<SearchDto>(
                "/search/tv?language=" + Locale + "&query=" + Uri.EscapeDataString(query.Trim()) + "&page=1",
                cancellationToken).ConfigureAwait(false);

            return (response.Results ?? new ShowDto[0])
                .Where(show => show != null && show.Id > 0)
                .Select(show => new SeriesMatch(ProviderKey,
                    show.Id.ToString(CultureInfo.InvariantCulture),
                    !string.IsNullOrWhiteSpace(show.Name) ? show.Name : (show.OriginalName ?? string.Empty),
                    show.OriginalLanguage ?? string.Empty,
                    "https://www.themoviedb.org/tv/" + show.Id.ToString(CultureInfo.InvariantCulture),
                    ParseYear(show.FirstAirDate))).ToList();
        }

        public async Task<IReadOnlyList<SeasonMetadata>> GetSeasonsAsync(string seriesId, CancellationToken cancellationToken)
        {
            int id = ParseId(seriesId);
            SeriesDto response = await ReadJsonAsync<SeriesDto>(
                "/tv/" + id.ToString(CultureInfo.InvariantCulture) + "?language=" + Locale,
                cancellationToken).ConfigureAwait(false);

            return (response.Seasons ?? new SeasonDto[0])
                .Where(season => season != null && season.SeasonNumber.HasValue)
                .OrderBy(season => season.SeasonNumber.Value)
                .Select(season => new SeasonMetadata(season.SeasonNumber.Value,
                    season.Id.ToString(CultureInfo.InvariantCulture),
                    string.IsNullOrWhiteSpace(season.Name)
                        ? (season.SeasonNumber.Value == 0 ? "Specials" : "Staffel " + season.SeasonNumber.Value)
                        : season.Name)).ToList();
        }

        public async Task<IReadOnlyList<EpisodeMetadata>> GetEpisodesAsync(
            string seriesId, bool includeSpecials, CancellationToken cancellationToken)
        {
            IReadOnlyList<SeasonMetadata> seasons = await GetSeasonsAsync(seriesId, cancellationToken).ConfigureAwait(false);
            var result = new List<EpisodeMetadata>();
            foreach (SeasonMetadata season in seasons)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!includeSpecials && season.Number == 0)
                    continue;
                result.AddRange(await GetSeasonEpisodesAsync(seriesId, season.Number, cancellationToken).ConfigureAwait(false));
            }
            return result;
        }

        /// <summary>Fetch just one season. One localized request plus an English request only when needed.</summary>
        public async Task<IReadOnlyList<EpisodeMetadata>> GetSeasonEpisodesAsync(
            string seriesId, int seasonNumber, CancellationToken cancellationToken)
        {
            int id = ParseId(seriesId);
            if (seasonNumber < 0)
                throw new ArgumentOutOfRangeException("seasonNumber");

            string route = "/tv/" + id.ToString(CultureInfo.InvariantCulture) + "/season/"
                + seasonNumber.ToString(CultureInfo.InvariantCulture);

            SeasonDetailsDto localized = await ReadJsonAsync<SeasonDetailsDto>(
                route + "?language=" + Locale, cancellationToken).ConfigureAwait(false);
            SeasonDetailsDto english = language == "en" ? localized : await ReadJsonAsync<SeasonDetailsDto>(
                route + "?language=en-US", cancellationToken).ConfigureAwait(false);

            var enByNumber = (english.Episodes ?? new EpisodeDto[0])
                .Where(ep => ep != null && ep.EpisodeNumber.HasValue)
                .GroupBy(ep => ep.EpisodeNumber.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var localByNumber = (localized.Episodes ?? new EpisodeDto[0])
                .Where(ep => ep != null && ep.EpisodeNumber.HasValue && ep.EpisodeNumber.Value > 0)
                .GroupBy(ep => ep.EpisodeNumber.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var result = new List<EpisodeMetadata>();
            // An English episode may exist even when it is absent from the localized listing.
            foreach (int number in localByNumber.Keys.Union(enByNumber.Keys).Where(n => n > 0).OrderBy(n => n))
            {
                EpisodeDto local;
                EpisodeDto eng;
                localByNumber.TryGetValue(number, out local);
                enByNumber.TryGetValue(number, out eng);
                string engTitle = NormalizeTitle(eng == null ? null : eng.Name, number);
                string localTitle = NormalizeTitle(local == null ? null : local.Name, number);

                string chosen;
                string actualLanguage;
                if (language == "en")
                {
                    chosen = engTitle ?? localTitle;
                    actualLanguage = string.IsNullOrEmpty(chosen) ? "und" : "en";
                }
                else if (!string.IsNullOrEmpty(localTitle)
                    && (string.IsNullOrEmpty(engTitle) || !string.Equals(localTitle, engTitle, StringComparison.Ordinal)))
                {
                    chosen = localTitle;
                    actualLanguage = "de";
                }
                else
                {
                    chosen = engTitle ?? localTitle;
                    actualLanguage = string.IsNullOrEmpty(chosen) ? "und" : "en";
                }

                EpisodeDto source = local ?? eng;
                result.Add(new EpisodeMetadata(ProviderKey,
                    source.Id.ToString(CultureInfo.InvariantCulture),
                    seasonNumber, number, null, chosen ?? string.Empty,
                    actualLanguage, seasonNumber == 0));
            }

            return result.OrderBy(ep => ep.EpisodeNumber.Value).ToList();
        }

        /// <summary>Accept only a positive TV ID or HTTPS TMDB TV URL. Never accept an untrusted host.</summary>
        public static bool TryGetShowId(string input, out string showId)
        {
            showId = null;
            if (string.IsNullOrWhiteSpace(input))
                return false;
            int id;
            if (int.TryParse(input.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0)
            {
                showId = id.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            Uri uri;
            if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !(string.Equals(uri.Host, "www.themoviedb.org", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Host, "themoviedb.org", StringComparison.OrdinalIgnoreCase))
                || !uri.IsDefaultPort)
                return false;

            string[] parts = uri.AbsolutePath.Trim('/').Split('/');
            if (parts.Length < 2 || parts[0] != "tv")
                return false;
            string segment = parts[1].Split('-')[0];
            if (!int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out id) || id <= 0)
                return false;
            showId = id.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        private string Locale { get { return language == "en" ? "en-US" : "de-DE"; } }

        private static int ParseId(string id)
        {
            int number;
            if (!int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number <= 0)
                throw new ArgumentException("Ungültige TMDB-Serien-ID.", "id");
            return number;
        }

        private static int? ParseYear(string date)
        {
            DateTime parsed;
            return DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out parsed) ? parsed.Year : (int?)null;
        }

        private static string NormalizeTitle(string value, int episodeNumber)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            string title = value.Trim();
            string number = episodeNumber.ToString(CultureInfo.InvariantCulture);
            if (string.Equals(title, "Episode " + number, StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, "Folge " + number, StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, "Episode #" + number, StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, "TBA", StringComparison.OrdinalIgnoreCase))
                return null;
            return title;
        }

        private static HttpClient CreateClient()
        {
            var result = new HttpClient();
            result.Timeout = TimeSpan.FromSeconds(20);
            result.MaxResponseContentBufferSize = 16 * 1024 * 1024;
            result.DefaultRequestHeaders.UserAgent.ParseAdd("Episode-Names/2.0 (https://github.com/olevonpopole/Episode-names)");
            result.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return result;
        }

        private async Task<T> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
        {
            // For legacy v3 keys the official TMDB API uses an api_key query argument.
            // Never include that URL or the credential in any user-visible errors.
            string uri = BaseUrl + path + (bearer ? "" : (path.Contains("?") ? "&" : "?")
                + "api_key=" + Uri.EscapeDataString(credential));

            for (int attempt = 0; attempt < 3; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                    {
                        if (bearer)
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);

                        using (HttpResponseMessage response = await client.SendAsync(
                            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                        {
                            if ((response.StatusCode == (HttpStatusCode)429 || (int)response.StatusCode >= 500) && attempt < 2)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(2 * (attempt + 1)), cancellationToken)
                                    .ConfigureAwait(false);
                                continue;
                            }

                            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                                throw new MetadataProviderException("TMDB hat den API-Key abgelehnt. Bitte TMDB-Anmeldedaten überprüfen.");
                            if (!response.IsSuccessStatusCode)
                                throw new MetadataProviderException("TMDB-Anfrage fehlgeschlagen (HTTP "
                                    + (int)response.StatusCode + ").");

                            using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            {
                                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new MetadataProviderException("TMDB hat nicht rechtzeitig geantwortet.");
                }
                catch (HttpRequestException)
                {
                    throw new MetadataProviderException("TMDB ist über das Netzwerk nicht erreichbar.");
                }
                catch (SerializationException)
                {
                    throw new MetadataProviderException("TMDB hat ein unerwartetes Datenformat geliefert.");
                }
            }

            throw new MetadataProviderException("TMDB konnte nach mehreren Versuchen nicht erreicht werden.");
        }

        [DataContract]
        private sealed class SearchDto
        {
            [DataMember(Name = "results")] public ShowDto[] Results { get; set; }
        }

        [DataContract]
        private sealed class ShowDto
        {
            [DataMember(Name = "id")] public int Id { get; set; }
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "original_name")] public string OriginalName { get; set; }
            [DataMember(Name = "original_language")] public string OriginalLanguage { get; set; }
            [DataMember(Name = "first_air_date")] public string FirstAirDate { get; set; }
        }

        [DataContract]
        private sealed class SeriesDto
        {
            [DataMember(Name = "seasons")] public SeasonDto[] Seasons { get; set; }
        }

        [DataContract]
        private sealed class SeasonDto
        {
            [DataMember(Name = "id")] public int Id { get; set; }
            [DataMember(Name = "season_number")] public int? SeasonNumber { get; set; }
            [DataMember(Name = "name")] public string Name { get; set; }
        }

        [DataContract]
        private sealed class SeasonDetailsDto
        {
            [DataMember(Name = "episodes")] public EpisodeDto[] Episodes { get; set; }
        }

        [DataContract]
        private sealed class EpisodeDto
        {
            [DataMember(Name = "id")] public int Id { get; set; }
            [DataMember(Name = "episode_number")] public int? EpisodeNumber { get; set; }
            [DataMember(Name = "name")] public string Name { get; set; }
        }
    }
}
