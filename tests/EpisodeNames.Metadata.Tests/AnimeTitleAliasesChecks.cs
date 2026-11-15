using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Episode_Names.Metadata;

namespace Episode_Names.Metadata.Tests
{
    internal static class AnimeTitleAliasesChecks
    {
        internal static void Run()
        {
            Test().GetAwaiter().GetResult();
            Console.WriteLine("AniList title-alias lookup checks passed.");
        }
        private static async Task Test()
        {
            const string fixture = "{\"data\":{\"Page\":{\"media\":[{\"title\":{" +
                "\"romaji\":\"Majo no Tabitabi\",\"english\":\"Wandering Witch: The Journey of Elaina\"," +
                "\"native\":\"魔女の旅々\"},\"synonyms\":[\"Elainas Reise\"]},{" +
                "\"title\":{\"romaji\":\"Different Anime\",\"english\":\"Other Name\",\"native\":null}," +
                "\"synonyms\":[\"Unrelated\"]}]}}}";
            var handler = new FakeHandler(fixture);
            var provider = new AnimeTitleAliases(new HttpClient(handler));
            var aliases = await provider.FindAsync("Elainas Reise", CancellationToken.None);
            Assert(handler.Requests == 1, "one request to AniList");
            Assert(handler.RequestBody.Contains("Elainas Reise"), "query sent as GraphQL variable");
            Assert(aliases.Contains("Majo no Tabitabi") &&
                aliases.Contains("Wandering Witch: The Journey of Elaina"),
                "documented alternate names returned");
            Assert(!aliases.Contains("Other Name"), "unrelated anime not accepted");
            Assert(!(await provider.FindAsync("", CancellationToken.None)).Any(), "blank lookup offline");
            var absent = new AnimeTitleAliases(new HttpClient(
                new FakeHandler(fixture)));
            var none = await absent.FindAsync("Made up Anime", CancellationToken.None);
            Assert(none.Count == 0, "no inferred aliases without matching known title");
        }

        private static void Assert(bool value, string message)
        {
            if (!value) throw new Exception("AnimeTitleAliases: " + message);
        }
        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly string response;
            internal string RequestBody;
            internal int Requests;
            internal FakeHandler(string responseText) { response = responseText; }
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken token)
            {
                Requests++;
                Assert(request.Method == HttpMethod.Post &&
                    request.RequestUri.AbsoluteUri == "https://graphql.anilist.co",
                    "expected POST endpoint");
                RequestBody = await request.Content.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(response, Encoding.UTF8, "application/json")
                };
            }
        }
    }
}
