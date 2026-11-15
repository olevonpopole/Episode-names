using System;
using System.Collections.Generic;
using System.Linq;

namespace Episode_Names.Metadata
{
    // Stable provider IDs are saved only after migrating the legacy index once.
    internal static class MetadataSelection
    {
        internal const int Anisearch = 0;
        internal const int TvMaze = 1;
        internal const int Tmdb = 2;

        internal static int MigrateLegacyProvider(int oldIndex)
        {
            if (oldIndex == 1) return Anisearch;
            if (oldIndex == 5) return Tmdb;
            // Old AniDB (0), Fernsehserien (2), TVDB (3), TVmaze (4),
            // or unknown selections switch safely to keyless TVmaze.
            return TvMaze;
        }

        internal static string MigrateLegacySearchLanguage(int oldProvider, int oldIndex)
        {
            if (oldProvider == 1 && oldIndex == 2) return "ja";
            if ((oldProvider == 1 || oldProvider == 5) && oldIndex == 1) return "en";
            if (oldProvider == 4 || oldProvider == 0) return "en";
            return "de";
        }

        internal static string CanonicalLanguage(string code)
        {
            switch ((code ?? "").Trim().ToLowerInvariant())
            {
                case "de": return "de";
                case "en": return "en";
                case "ja": return "ja";
                case "fr": return "fr";
                case "es": return "es";
                case "it": return "it";
                case "pt": return "pt";
                case "pl": return "pl";
                case "ko": return "ko";
                case "zh": return "zh";
                case "tr": return "tr";
                default: return "de";
            }
        }

        internal static IReadOnlyList<KeyValuePair<string, string>> SupportedLanguages()
        {
            return new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("de", "Deutsch"),
                new KeyValuePair<string, string>("en", "English"),
                new KeyValuePair<string, string>("ja", "日本語"),
                new KeyValuePair<string, string>("fr", "Français"),
                new KeyValuePair<string, string>("es", "Español"),
                new KeyValuePair<string, string>("it", "Italiano"),
                new KeyValuePair<string, string>("pt", "Português"),
                new KeyValuePair<string, string>("pl", "Polski"),
                new KeyValuePair<string, string>("ko", "한국어"),
                new KeyValuePair<string, string>("zh", "中文"),
                new KeyValuePair<string, string>("tr", "Türkçe")
            };
        }

        internal static string DescribeResultLanguage(string requestedLanguage, string actualLanguage)
        {
            string requested = CanonicalLanguage(requestedLanguage);
            string actual = string.IsNullOrWhiteSpace(actualLanguage) ? "und" :
                actualLanguage.Trim().ToLowerInvariant();
            if (actual == requested) return actual;
            if (actual == "und") return "und (Original/unbekannt)";
            return actual + " (Fallback für " + requested + ")";
        }

        internal static int FindLanguageIndex(IEnumerable<KeyValuePair<string, string>> choices,
            string preferredLanguage)
        {
            var values = choices.ToList();
            string language = CanonicalLanguage(preferredLanguage);
            int index = values.FindIndex(item => item.Key == language);
            if (index >= 0) return index;
            index = values.FindIndex(item => item.Key == "de");
            return index >= 0 ? index : 0;
        }
    }
}
