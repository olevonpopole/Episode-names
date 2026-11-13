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
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                RunAsync().GetAwaiter().GetResult();
                RenamePlanChecks.Run();
                Console.WriteLine("TVmaze and TMDB provider checks passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static async Task RunAsync()
        {
            string showId;
            Check(TvMazeProvider.TryGetShowId("42", out showId) && showId == "42", "numeric ID");
            Check(TvMazeProvider.TryGetShowId("https://www.tvmaze.com/shows/42/example", out showId)
                && showId == "42", "website URL");
            Check(TvMazeProvider.TryGetShowId("https://api.tvmaze.com/shows/42", out showId)
                && showId == "42", "API URL");
            Check(!TvMazeProvider.TryGetShowId("https://www.tvmaze.com.evil.example/shows/42", out showId),
                "reject disguised host");
            Check(!TvMazeProvider.TryGetShowId("http://www.tvmaze.com/shows/42", out showId),
                "require HTTPS");
            Check(!TvMazeProvider.TryGetShowId("-4", out showId), "reject invalid ID");

            var handler = new FixtureHandler();
            var client = new HttpClient(handler);
            IEpisodeMetadataProvider provider = new TvMazeProvider(client);
            Check(provider.Id == "tvmaze" && !provider.RequiresApiKey, "keyless provider");

            IReadOnlyList<SeriesMatch> shows = await provider.SearchSeriesAsync("Example Anime", CancellationToken.None);
            Check(shows.Count == 1 && shows[0].Name == "Example Anime"
                && shows[0].PremiereYear == 2021 && shows[0].Id == "42", "series mapping");
            Check(handler.LastQuery == "?q=Example%20Anime", "query URL encoding");

            IReadOnlyList<SeasonMetadata> seasons = await provider.GetSeasonsAsync("42", CancellationToken.None);
            Check(seasons.Count == 2 && seasons[0].Number == 0 && seasons[1].Number == 1,
                "season numbering and specials");

            IReadOnlyList<EpisodeMetadata> regular = await provider.GetEpisodesAsync("42", false, CancellationToken.None);
            Check(regular.Count == 2 && regular[0].EpisodeNumber == 1
                && regular[0].AbsoluteNumber == null && !regular[0].IsSpecial,
                "regular episode and unknown absolute numbering");

            IReadOnlyList<EpisodeMetadata> includingSpecials = await provider.GetEpisodesAsync("42", true, CancellationToken.None);
            Check(includingSpecials.Count == 3 && includingSpecials[2].IsSpecial
                && includingSpecials[2].EpisodeNumber == null, "specials and nullable episode number");

            await RunTmdbChecksAsync();

            handler.ReturnNotFound = true;
            try
            {
                await provider.GetSeasonsAsync("42", CancellationToken.None);
                throw new Exception("404 should have failed");
            }
            catch (MetadataProviderException ex)
            {
                Check(ex.Message.Contains("404"), "HTTP error surfaced");
            }
        }


        private static async Task RunTmdbChecksAsync()
        {
            string showId;
            Check(TmdbProvider.TryGetShowId("42", out showId) && showId == "42", "TMDB numeric ID");
            Check(TmdbProvider.TryGetShowId("https://www.themoviedb.org/tv/42-example", out showId)
                && showId == "42", "TMDB show URL");
            Check(!TmdbProvider.TryGetShowId("https://www.themoviedb.org.evil.example/tv/42", out showId),
                "TMDB disguised host rejection");
            Check(!TmdbProvider.TryGetShowId("https://www.themoviedb.org/movie/42", out showId),
                "TMDB movie URL rejection");

            string original = "test-v3-key-value";
            string encrypted = TmdbCredentialStore.Protect(original);
            Check(!encrypted.Contains(original) && encrypted.StartsWith("dpapi:v1:"),
                "key stored only in encrypted form");
            Check(TmdbCredentialStore.Unprotect(encrypted) == original, "DPAPI roundtrip");
            Check(TmdbCredentialStore.Unprotect(null) == string.Empty, "optional empty key");

            var handler = new TmdbFixtureHandler();
            IEpisodeMetadataProvider provider = new TmdbProvider(original, "de", new HttpClient(handler));
            Check(provider.RequiresApiKey && provider.Id == "tmdb", "optional provider identity");
            IReadOnlyList<SeriesMatch> series = await provider.SearchSeriesAsync("Demo Anime", CancellationToken.None);
            Check(series.Count == 1 && series[0].PremiereYear == 2022
                && series[0].Id == "42" && series[0].Name == "Demo Anime", "TMDB TV search");
            Check(handler.LastRequestUri.Contains("api_key=test-v3-key-value")
                && handler.LastRequestUri.Contains("language=de-DE")
                && handler.LastRequestUri.Contains("query=Demo%20Anime"), "TMDB v3 request parameters");

            var seasons = await provider.GetSeasonsAsync("42", CancellationToken.None);
            Check(seasons.Count == 2 && seasons[0].Number == 0 && seasons[1].Number == 1,
                "TMDB seasons and specials");

            var tmdb = (TmdbProvider)provider;
            var episodes = await tmdb.GetSeasonEpisodesAsync("42", 1, CancellationToken.None);
            Check(episodes.Count == 4 && episodes[0].Title == "Aufbruch" && episodes[0].Language == "de",
                "TMDB German translation");
            Check(episodes[1].Title == "Rescue" && episodes[1].Language == "en",
                "TMDB fallback from German placeholder");
            Check(episodes[2].Title == "" && episodes[2].Language == "und",
                "TMDB unknown title not invented");
            Check(episodes[3].Title == "English only" && episodes[3].Language == "en",
                "TMDB episode missing in German listing falls back to English");
            Check(episodes.All(x => x.AbsoluteNumber == null) && episodes.All(x => !x.IsSpecial),
                "no absolute mapping guessed");

            var withoutSpecials = await provider.GetEpisodesAsync("42", false, CancellationToken.None);
            Check(withoutSpecials.Count == 4, "TMDB specials omitted by default");
            var withSpecials = await provider.GetEpisodesAsync("42", true, CancellationToken.None);
            Check(withSpecials.Count == 5 && withSpecials[0].IsSpecial, "TMDB specials opt-in");

            var bearerHandler = new TmdbFixtureHandler();
            var bearerProvider = new TmdbProvider("eyJ.read.access.token", "en", new HttpClient(bearerHandler));
            var en = await bearerProvider.GetSeasonEpisodesAsync("42", 1, CancellationToken.None);
            Check(en[0].Language == "en" && en[0].Title == "Departure", "English preference");
            Check(bearerHandler.LastAuthorization == "Bearer eyJ.read.access.token"
                && !bearerHandler.LastRequestUri.Contains("api_key"), "Bearer token not in URL");

            handler.Forbidden = true;
            try
            {
                await provider.GetSeasonsAsync("42", CancellationToken.None);
                throw new Exception("TMDB invalid credentials should fail");
            }
            catch (MetadataProviderException ex)
            {
                Check(!ex.Message.Contains(original), "No credentials in error messages");
            }
        }

        private sealed class TmdbFixtureHandler : HttpMessageHandler
        {
            public string LastRequestUri { get; private set; }
            public string LastAuthorization { get; private set; }
            public bool Forbidden { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequestUri = request.RequestUri.AbsoluteUri;
                LastAuthorization = request.Headers.Authorization == null ? ""
                    : request.Headers.Authorization.ToString();
                if (Forbidden)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));

                string path = request.RequestUri.AbsolutePath;
                bool german = request.RequestUri.Query.Contains("language=de-DE");
                string json;
                if (path == "/3/search/tv")
                    json = "{\"results\":[{\"id\":42,\"name\":\"Demo Anime\",\"original_language\":\"ja\",\"first_air_date\":\"2022-09-03\"}]}";
                else if (path == "/3/tv/42")
                    json = "{\"seasons\":[{\"id\":900,\"season_number\":0,\"name\":\"Specials\"},{\"id\":901,\"season_number\":1,\"name\":\"Season 1\"}]}";
                else if (path == "/3/tv/42/season/0")
                    json = "{\"episodes\":[{\"id\":999,\"episode_number\":1,\"name\":\"Special\"}]}";
                else if (path == "/3/tv/42/season/1")
                    json = german
                        ? "{\"episodes\":[{\"id\":1001,\"episode_number\":1,\"name\":\"Aufbruch\"},{\"id\":1002,\"episode_number\":2,\"name\":\"Folge 2\"},{\"id\":1003,\"episode_number\":3,\"name\":\"\"}]}"
                        : "{\"episodes\":[{\"id\":1001,\"episode_number\":1,\"name\":\"Departure\"},{\"id\":1002,\"episode_number\":2,\"name\":\"Rescue\"},{\"id\":1003,\"episode_number\":3,\"name\":\"Episode 3\"},{\"id\":1004,\"episode_number\":4,\"name\":\"English only\"}]}";
                else
                    throw new InvalidOperationException("Unexpected TMDB route: " + path);

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            }
        }

        private static void Check(bool condition, string description)
        {
            if (!condition)
                throw new Exception("Failed: " + description);
        }

        private sealed class FixtureHandler : HttpMessageHandler
        {
            public bool ReturnNotFound { get; set; }
            public string LastQuery { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastQuery = request.RequestUri.Query;
                if (ReturnNotFound)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

                string path = request.RequestUri.AbsolutePath;
                string json;
                if (path == "/search/shows")
                    json = "[{\"show\":{\"id\":42,\"name\":\"Example Anime\",\"language\":\"Japanese\",\"premiered\":\"2021-04-02\",\"url\":\"https://www.tvmaze.com/shows/42/example\"}}]";
                else if (path == "/shows/42/seasons")
                    json = "[{\"id\":100,\"number\":0},{\"id\":101,\"number\":1}]";
                else if (path == "/shows/42/episodes")
                    json = request.RequestUri.Query.Contains("specials=1")
                        ? "[{\"id\":200,\"season\":1,\"number\":1,\"name\":\"Pilot\",\"type\":\"regular\"},{\"id\":201,\"season\":1,\"number\":2,\"name\":\"Next\",\"type\":\"regular\"},{\"id\":202,\"season\":0,\"number\":null,\"name\":\"OVA\",\"type\":\"significant_special\"}]"
                        : "[{\"id\":200,\"season\":1,\"number\":1,\"name\":\"Pilot\",\"type\":\"regular\"},{\"id\":201,\"season\":1,\"number\":2,\"name\":\"Next\",\"type\":\"regular\"}]";
                else
                    throw new InvalidOperationException("Unexpected API route: " + request.RequestUri);

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
