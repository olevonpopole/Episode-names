using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Episode_Names.Metadata
{
    /// <summary>
    /// Public AniList title index. Resolves alternate titles for searching
    /// other metadata providers. Does not supply or translate episode names.
    /// </summary>
    internal sealed class AnimeTitleAliases
    {
        private static readonly HttpClient SharedClient = CreateClient();
        private readonly HttpClient client;

        internal AnimeTitleAliases(HttpClient httpClient = null)
        {
            client = httpClient ?? SharedClient;
        }

        internal async Task<IReadOnlyList<string>> FindAsync(string search, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(search))
                return new string[0];

            // Only known, published AniList title aliases. No generated translations.
            const string graph = "query ($search: String!) { Page(page: 1, perPage: 5) { " +
                "media(search: $search, type: ANIME, isAdult: false) { " +
                "title { romaji english native } synonyms } } }";
            string body = "{\"query\":\"" + EscapeJson(graph) +
                "\",\"variables\":{\"search\":\"" + EscapeJson(search.Trim()) + "\"}}";
            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://graphql.anilist.co"))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                using (var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new MetadataProviderException(
                            "AniList-Titelindex momentan nicht verfügbar (HTTP " +
                            (int)response.StatusCode + ").");
                    using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        var obj = (Root)new DataContractJsonSerializer(typeof(Root)).ReadObject(stream);
                        var media = obj == null || obj.Data == null || obj.Data.Page == null
                            ? new Media[0] : obj.Data.Page.Media ?? new Media[0];
                        string normalizedSearch = Normalize(search);
                        return media.Where(m => m != null)
                            .Select(m => new {
                                Titles = (new[] {
                                    m.Title == null ? null : m.Title.Romaji,
                                    m.Title == null ? null : m.Title.English,
                                    m.Title == null ? null : m.Title.Native
                                }).Concat(m.Synonyms ?? new string[0]).Where(Valid).Distinct().ToArray()
                            })
                            // An exact matching alias is required to trust this anime
                            // identity; otherwise keep the original provider results.
                            .Where(m => m.Titles.Any(t => Normalize(t) == normalizedSearch))
                            .SelectMany(m => m.Titles)
                            .Where(t => Normalize(t) != normalizedSearch)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Take(5).ToList();
                    }
                }
            }
        }

        private static string Normalize(string value)
        {
            return Regex.Replace((value ?? "").ToLowerInvariant(),
                @"[^\p{L}\p{Nd}]+", "");
        }

        private static bool Valid(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= 90 &&
                !value.Any(char.IsControl);
        }

        private static string EscapeJson(string value)
        {
            var sb = new StringBuilder();
            foreach (char c in value)
            {
                if (c == '\\' || c == '"') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static HttpClient CreateClient()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12),
                MaxResponseContentBufferSize = 512 * 1024 };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Episode-Names/2.0");
            return http;
        }

        [DataContract] private sealed class Root
        {
            [DataMember(Name = "data")] public ResponseData Data { get; set; }
        }
        [DataContract] private sealed class ResponseData
        {
            [DataMember(Name = "Page")] public PageData Page { get; set; }
        }
        [DataContract] private sealed class PageData
        {
            [DataMember(Name = "media")] public Media[] Media { get; set; }
        }
        [DataContract] private sealed class Media
        {
            [DataMember(Name = "title")] public TitleData Title { get; set; }
            [DataMember(Name = "synonyms")] public string[] Synonyms { get; set; }
        }
        [DataContract] private sealed class TitleData
        {
            [DataMember(Name = "romaji")] public string Romaji { get; set; }
            [DataMember(Name = "english")] public string English { get; set; }
            [DataMember(Name = "native")] public string Native { get; set; }
        }
    }
}
