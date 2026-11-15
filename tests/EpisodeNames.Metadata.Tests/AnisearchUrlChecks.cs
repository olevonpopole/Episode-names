using System;
using Episode_Names.Metadata;

namespace Episode_Names.Metadata.Tests
{
    internal static class AnisearchUrlChecks
    {
        internal static void Run()
        {
            CheckUrl("https://www.anisearch.de/anime/2788,naruto",
                "de", "https://www.anisearch.de/anime/2788,naruto/episodes");
            CheckUrl("https://www.anisearch.de/anime/2788,naruto/episodes",
                "de", "https://www.anisearch.de/anime/2788,naruto/episodes");
            CheckUrl("https://www.anisearch.com/anime/2788,naruto?ref=search",
                "en", "https://www.anisearch.com/anime/2788,naruto/episodes");
            CheckUrl("https://www.anisearch.de/anime/2788/naruto",
                "de", "https://www.anisearch.de/anime/2788/naruto/episodes");
            CheckUrl("https://anisearch.de/anime/2788",
                "de", "https://www.anisearch.de/anime/2788/episodes");
            string url;
            Assert(AnisearchUrl.TryResolveSearchResult("https://www.anisearch.de",
                "/anime/2788,naruto", out url) &&
                url == "https://www.anisearch.de/anime/2788,naruto",
                "Root-relative search result");
            Assert(AnisearchUrl.TryResolveSearchResult("https://www.anisearch.de",
                "anime/2788,naruto", out url) &&
                url == "https://www.anisearch.de/anime/2788,naruto",
                "Relative search result");
            Assert(AnisearchUrl.TryResolveSearchResult("https://www.anisearch.de",
                "https://www.anisearch.de/anime/2788,naruto", out url),
                "Absolute search result");
            foreach (string invalid in new[] {
                "http://www.anisearch.de/anime/2788,naruto",
                "https://www.anisearch.de.evil.example/anime/2788,naruto",
                "https://evil.example/anime/2788,naruto",
                "https://www.anisearch.de/manga/2788,naruto",
                "https://www.anisearch.de/anime/index",
                "https://www.anisearch.de/anime/foo,naruto",
                "https://www.anisearch.de/anime/2788,naruto/random",
                "https://www.anisearch.de/anime/2788,naruto/episodes/extra",
                "https://www.anisearch.de:8443/anime/2788,naruto",
                "https://hacker@www.anisearch.de/anime/2788,naruto",
                "https://www.anisearch.de/anime/2788,"
            }) {
                Assert(!AnisearchUrl.TryEpisodesUrl(invalid, "de", out url),
                    "Reject invalid URL: " + invalid);
            }
            Assert(!AnisearchUrl.TryResolveSearchResult("https://www.anisearch.de",
                "https://evil.example/anime/2788,naruto", out url),
                "Reject external search-result redirect");
            Console.WriteLine("AniSearch anime URL compatibility and security checks passed.");
        }

        private static void CheckUrl(string input, string language, string expected)
        {
            string output;
            Assert(AnisearchUrl.TryEpisodesUrl(input, language, out output) &&
                output == expected, "AniSearch URL: " + input);
        }

        private static void Assert(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
        }
    }
}
