using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Episode_Names.Metadata;

namespace Episode_Names.Metadata.Tests
{
    internal static class AnimeEpisodeSourcesChecks
    {
        internal static void Run()
        {
            Tests().GetAwaiter().GetResult();
            Console.WriteLine("Jikan/AniZip cross-identity and multilingual source checks passed.");
        }

        private static async Task Tests()
        {
            var jikanHttp = new FakeHandler(req =>
            {
                string path = req.RequestUri.AbsolutePath + req.RequestUri.Query;
                if (path.StartsWith("/v4/anime?q="))
                    return "{\"data\":[{\"mal_id\":20,\"title\":\"Naruto\",\"title_english\":\"Naruto\",\"url\":\"https://myanimelist.net/anime/20\",\"aired\":{\"from\":\"2002-10-03T00:00:00+00:00\"}}]}";
                if (path == "/v4/anime/20/episodes?page=1")
                    return "{\"data\":[{\"mal_id\":1,\"title\":\"English one\",\"title_japanese\":\"日本語一\"}],\"pagination\":{\"has_next_page\":true}}";
                if (path == "/v4/anime/20/episodes?page=2")
                    return "{\"data\":[{\"mal_id\":2,\"title\":\"English two\",\"title_japanese\":\"日本語二\"}],\"pagination\":{\"has_next_page\":false}}";
                throw new Exception("Unexpected Jikan route " + path);
            });
            var jikan = new JikanProvider("ja", new HttpClient(jikanHttp));
            var shows = await jikan.SearchSeriesAsync("Naruto", CancellationToken.None);
            Assert(shows.Count == 1 && shows[0].Id == "20" &&
                shows[0].ProviderId == "jikan" && shows[0].PremiereYear == 2002,
                "Jikan search maintains MAL identity");
            var episodes = await jikan.GetEpisodesAsync("20", false, CancellationToken.None);
            Assert(episodes.Count == 2 && episodes.All(ep =>
                    ep.SeasonNumber == 1 && ep.Language == "ja") &&
                episodes[0].EpisodeNumber == 1 && episodes[1].EpisodeNumber == 2 &&
                episodes[0].Title == "日本語一",
                "Jikan paginates and uses explicit episode IDs, not list position");
            var derived = AniZipProvider.FromMalSearch(shows);
            Assert(derived.Count == 1 && derived[0].ProviderId == "anizip" &&
                derived[0].Id == "20", "AniZip candidates tied to matching MAL ID");

            const string anizipJson =
                "{\"mappings\":{\"mal_id\":20},\"episodes\":{" +
                "\"1\":{\"seasonNumber\":1,\"episodeNumber\":1,\"title\":{" +
                "\"de\":\"Deutsch Eins\",\"en\":\"English One\"}}," +
                "\"2\":{\"seasonNumber\":2,\"episodeNumber\":3,\"absoluteEpisodeNumber\":55,\"title\":{" +
                "\"en\":\"English Three\",\"ja\":\"日本語三\"}}," +
                "\"3\":{\"title\":{\"de\":\"Ambiguous Season\"}}," +
                "\"S0\":{\"seasonNumber\":0,\"episodeNumber\":1,\"title\":{" +
                "\"de\":\"Special\"}}}}";
            var aniHttp = new FakeHandler(req =>
            {
                Assert(req.RequestUri.AbsoluteUri ==
                    "https://api.ani.zip/v1/episodes?mal_id=20",
                    "AniZip must use numeric MAL ID, not unchecked title or URL");
                return anizipJson;
            });
            var aniZipDe = new AniZipProvider("de", new HttpClient(aniHttp));
            var zipped = await aniZipDe.GetEpisodesAsync("20", false, CancellationToken.None);
            Assert(zipped.Count == 2 && zipped[0].Title == "Deutsch Eins" &&
                zipped[0].Language == "de" && zipped[0].SeasonNumber == 1 &&
                zipped[1].Title == "English Three" && zipped[1].Language == "en" &&
                zipped[1].SeasonNumber == 2 && zipped[1].EpisodeNumber == 3,
                "AniZip respects provided season/episode and language fallback");
            var matched = EpisodeFileMatcher.Match(new[] {
                EpisodeFileMatcher.Identify("Show.S02E03.mkv", "Show"),
                EpisodeFileMatcher.Identify("Show.S01E01.mkv", "Show")
            }, zipped);
            Assert(matched.All(x => x.IsMatched) &&
                matched[0].Title == "English Three" &&
                matched[1].Title == "Deutsch Eins",
                "AniZip mapping uses explicit season/episode not JSON entry order");

            try
            {
                await new AniZipProvider("de", new HttpClient(new FakeHandler(req =>
                    "{\"mappings\":{\"mal_id\":21},\"episodes\":{}}")))
                    .GetEpisodesAsync("20", false, CancellationToken.None);
                throw new Exception("MAL identity mismatch should block result");
            }
            catch (MetadataProviderException) { }

            var titles = new Dictionary<string, string> {
                { "en", "English title" }, { "fr", "Titre français" },
                { "x-jat", "Romanji" }
            };
            string text, language;
            AniZipProvider.ChooseTitle(titles, "fr", out text, out language);
            Assert(text == "Titre français" && language == "fr",
                "exact language from AniZip preferred");
            AniZipProvider.ChooseTitle(titles, "de", out text, out language);
            Assert(text == "English title" && language == "en",
                "AniZip does not fabricate untranslated German title");
            AniZipProvider.ChooseTitle(new Dictionary<string, string> {
                { "x-jat", "Romaji only" }
            }, "en", out text, out language);
            Assert(text == "Romaji only" && language == "ja-Latn",
                "romanized Japanese accurately labeled");
            AniZipProvider.ChooseTitle(new Dictionary<string, string> {
                { "pt-BR", "Título brasileiro" }, { "en", "English" }
            }, "pt", out text, out language);
            Assert(text == "Título brasileiro" && language == "pt-BR",
                "regional Portuguese title selected ahead of English");
            AniZipProvider.ChooseTitle(new Dictionary<string, string> {
                { "zh-Hans", "简体中文" }, { "en", "English" }
            }, "zh", out text, out language);
            Assert(text == "简体中文" && language == "zh-Hans",
                "Chinese script variant selected ahead of English");

            Assert(MetadataSourcePriority.Rank("tmdb", "de") >
                MetadataSourcePriority.Rank("anizip", "de") &&
                MetadataSourcePriority.Rank("anizip", "de") >
                MetadataSourcePriority.Rank("tvmaze", "de"),
                "source ranking favors potentially localized title data");
            Assert(!MetadataSourcePriority.CanAutoFetch(1, 1, "jikan") &&
                !MetadataSourcePriority.CanAutoFetch(1, 1, "anizip") &&
                !MetadataSourcePriority.CanAutoFetch(2, 2, "tmdb") &&
                MetadataSourcePriority.CanAutoFetch(1, 1, "tmdb"),
                "anime/ambiguous hits always require explicit series selection");
        }

        private static void Assert(bool value, string description)
        {
            if (!value) throw new Exception("Anime sources: " + description);
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, string> responder;
            internal FakeHandler(Func<HttpRequestMessage, string> responder) { this.responder = responder; }
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken token)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(responder(request),
                        Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
