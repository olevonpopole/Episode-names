using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Episode_Names.Metadata
{
    /// <summary>
    /// AniZip episode titles through a verified MAL identity. Uses ONLY explicit
    /// TVDB seasonNumber/episodeNumber pairs, never assumes the AniDB absolute
    /// key is a matching SxxExx position.
    /// </summary>
    internal sealed class AniZipProvider : IEpisodeMetadataProvider
    {
        internal const string ProviderKey = "anizip";
        private const string BaseUrl = "https://api.ani.zip/v1/episodes?mal_id=";
        private static readonly HttpClient SharedClient = CreateClient();
        private readonly HttpClient client;
        private readonly string requested;

        internal AniZipProvider(string preferredLanguage = "en", HttpClient httpClient = null)
        {
            requested = MetadataSelection.CanonicalLanguage(preferredLanguage);
            client = httpClient ?? SharedClient;
        }

        public string Id { get { return ProviderKey; } }
        public string DisplayName { get { return "AniZip (Anime-Mapping)"; } }
        public bool RequiresApiKey { get { return false; } }

        internal static IReadOnlyList<SeriesMatch> FromMalSearch(IReadOnlyList<SeriesMatch> matches)
        {
            return matches.Where(x => x.ProviderId == JikanProvider.ProviderKey)
                .Select(x => new SeriesMatch(ProviderKey, x.Id, x.Name,
                    x.Language, x.WebUrl, x.PremiereYear)).ToList();
        }

        public async Task<IReadOnlyList<SeriesMatch>> SearchSeriesAsync(
            string query, CancellationToken cancellationToken)
        {
            var jikan = new JikanProvider(requested);
            return FromMalSearch(await jikan.SearchSeriesAsync(
                query, cancellationToken).ConfigureAwait(false));
        }

        public Task<IReadOnlyList<SeasonMetadata>> GetSeasonsAsync(
            string seriesId, CancellationToken cancellationToken)
        {
            JikanProvider.ParseId(seriesId);
            return Task.FromResult((IReadOnlyList<SeasonMetadata>)new[] {
                new SeasonMetadata(1, seriesId,
                    "AniZip – TVDB-Staffelzuordnung (Dateinummern prüfen)")
            });
        }

        public async Task<IReadOnlyList<EpisodeMetadata>> GetEpisodesAsync(
            string seriesId, bool includeSpecials, CancellationToken cancellationToken)
        {
            int malId = JikanProvider.ParseId(seriesId);
            using (var response = await client.GetAsync(BaseUrl +
                malId.ToString(CultureInfo.InvariantCulture),
                HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                    throw new MetadataProviderException(
                        "AniZip HTTP " + (int)response.StatusCode + ".");
                using (Stream stream = await response.Content.ReadAsStreamAsync()
                    .ConfigureAwait(false))
                {
                    Response data;
                    try
                    {
                        data = (Response)new DataContractJsonSerializer(typeof(Response),
                            new DataContractJsonSerializerSettings {
                                UseSimpleDictionaryFormat = true
                            }).ReadObject(stream);
                    }
                    catch (SerializationException ex)
                    {
                        throw new MetadataProviderException(
                            "AniZip lieferte ein unbekanntes Episodenformat.", ex);
                    }
                    if (data == null || data.Mappings == null ||
                        data.Mappings.MalId != malId)
                        throw new MetadataProviderException(
                            "AniZip-MAL-Identität fehlt oder widerspricht der gewählten Serie.");

                    var episodes = new List<EpisodeMetadata>();
                    foreach (var pair in data.Episodes ??
                        new Dictionary<string, EpisodeDto>())
                    {
                        EpisodeDto ep = pair.Value;
                        if (ep == null || !ep.SeasonNumber.HasValue ||
                            !ep.EpisodeNumber.HasValue ||
                            ep.SeasonNumber.Value < 1 || ep.EpisodeNumber.Value < 1)
                            continue;
                        string title, language;
                        ChooseTitle(ep.Title, requested, out title, out language);
                        if (string.IsNullOrWhiteSpace(title))
                            continue;
                        episodes.Add(new EpisodeMetadata(ProviderKey,
                            malId.ToString(CultureInfo.InvariantCulture) + ":" + pair.Key,
                            ep.SeasonNumber.Value, ep.EpisodeNumber.Value,
                            ep.AbsoluteEpisodeNumber, title, language, false));
                    }
                    return episodes;
                }
            }
        }

        internal static void ChooseTitle(Dictionary<string, string> titles,
            string language, out string title, out string actual)
        {
            title = ""; actual = "und";
            if (titles == null) return;
            string requested = MetadataSelection.CanonicalLanguage(language);
            var priorities = new[] { requested, "en", "ja", "x-jat" }
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (string code in priorities)
            {
                string value;
                if (titles.TryGetValue(code, out value) && !string.IsNullOrWhiteSpace(value))
                {
                    title = value.Trim();
                    actual = code == "x-jat" ? "ja-Latn" : code;
                    return;
                }
            }
        }

        private static HttpClient CreateClient()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20),
                MaxResponseContentBufferSize = 8 * 1024 * 1024 };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Episode-Names/2.0");
            http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            return http;
        }

        [DataContract] private sealed class Response
        {
            [DataMember(Name = "mappings")] public MappingDto Mappings { get; set; }
            [DataMember(Name = "episodes")] public Dictionary<string, EpisodeDto> Episodes { get; set; }
        }
        [DataContract] private sealed class MappingDto
        {
            [DataMember(Name = "mal_id")] public int MalId { get; set; }
        }
        [DataContract] private sealed class EpisodeDto
        {
            [DataMember(Name = "seasonNumber")] public int? SeasonNumber { get; set; }
            [DataMember(Name = "episodeNumber")] public int? EpisodeNumber { get; set; }
            [DataMember(Name = "absoluteEpisodeNumber")] public int? AbsoluteEpisodeNumber { get; set; }
            [DataMember(Name = "title")] public Dictionary<string, string> Title { get; set; }
        }
    }
}
