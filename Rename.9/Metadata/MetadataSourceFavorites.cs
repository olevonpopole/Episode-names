using System;
using System.Collections.Generic;
using System.Linq;

namespace Episode_Names.Metadata
{
    /// <summary>Persist stable provider IDs, never UI indices or search results.</summary>
    internal static class MetadataSourceFavorites
    {
        private static readonly string[] Known = { "tvmaze", "jikan", "anizip", "tmdb" };

        internal static IReadOnlyList<string> Load(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored))
                return new[] { "tvmaze" }; // fast, keyless default
            return stored.Split(';').Where(x => Known.Contains(x))
                .Distinct(StringComparer.Ordinal).ToArray();
        }

        internal static string Save(IEnumerable<string> choices)
        {
            if (choices == null) return "tvmaze";
            return string.Join(";", choices.Where(x => Known.Contains(x))
                .Distinct(StringComparer.Ordinal));
        }

        internal static bool UsesMalLookup(IEnumerable<string> providers)
        {
            return providers.Contains("jikan") || providers.Contains("anizip");
        }
    }
}
