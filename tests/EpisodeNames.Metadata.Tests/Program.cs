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
                Console.WriteLine("TVmaze provider checks passed.");
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
