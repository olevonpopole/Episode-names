using System;

namespace Episode_Names.Metadata
{
    /// <summary>
    /// Presentation-only source priority. Every provider keeps its own series
    /// identity and numbering; this never silently joins episodes between APIs.
    /// </summary>
    internal static class MetadataSourcePriority
    {
        internal static int Rank(string providerId, string preferredLanguage)
        {
            string language = MetadataSelection.CanonicalLanguage(preferredLanguage);
            switch (providerId ?? "")
            {
                case "tmdb": return 100; // localized episodes with fallback
                case "anizip": return 80; // multilingual titles, MAL ID verified
                case "jikan": return language == "ja" ? 60 : 40;
                case "tvmaze": return language == "en" ? 55 : 20;
                default: return 0;
            }
        }

        internal static bool CanAutoFetch(int matchingTitleResults, int exactMatches,
            string providerId)
        {
            // Anime sources commonly represent separate seasons as separate MAL
            // works; they must be explicitly selected, even on an exact name.
            return matchingTitleResults == 1 && exactMatches == 1 &&
                (providerId == "tmdb" || providerId == "tvmaze");
        }
    }
}
