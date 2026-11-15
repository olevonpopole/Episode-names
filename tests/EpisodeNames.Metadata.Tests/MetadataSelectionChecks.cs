using System;
using System.Collections.Generic;
using Episode_Names.Metadata;
using Episode_Names.Renaming;

namespace Episode_Names.Metadata.Tests
{
    internal static class MetadataSelectionChecks
    {
        internal static void Run()
        {
            Assert(MetadataSelection.MigrateLegacyProvider(0) == MetadataSelection.TvMaze,
                "Old AniDB migrates to TVmaze");
            Assert(MetadataSelection.MigrateLegacyProvider(1) == MetadataSelection.Anisearch,
                "Old AniSearch remains AniSearch");
            Assert(MetadataSelection.MigrateLegacyProvider(2) == MetadataSelection.TvMaze,
                "Old Fernsehserien migrates to TVmaze");
            Assert(MetadataSelection.MigrateLegacyProvider(3) == MetadataSelection.TvMaze,
                "Old TVDB migrates to TVmaze");
            Assert(MetadataSelection.MigrateLegacyProvider(4) == MetadataSelection.TvMaze,
                "Old TVmaze remains TVmaze");
            Assert(MetadataSelection.MigrateLegacyProvider(5) == MetadataSelection.Tmdb,
                "Old TMDB remains TMDB");
            Assert(MetadataSelection.MigrateLegacyProvider(123) == MetadataSelection.TvMaze,
                "Unknown old index migrates safely");

            Assert(MetadataSelection.MigrateLegacySearchLanguage(5, 1) == "en",
                "Preserve prior TMDB English search");
            Assert(MetadataSelection.MigrateLegacySearchLanguage(1, 2) == "ja",
                "Preserve prior AniSearch Japanese search");
            Assert(MetadataSelection.CanonicalLanguage(" EN ") == "en" &&
                MetadataSelection.CanonicalLanguage("de") == "de" &&
                MetadataSelection.CanonicalLanguage("ja") == "ja",
                "Save last search language by stable language code");

            var tmdbLanguages = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("de", "Deutsch"),
                new KeyValuePair<string, string>("en", "English")
            };
            var tvMazeLanguages = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("en", "English")
            };
            var anisearchLanguages = new List<KeyValuePair<string, string>>(tmdbLanguages) {
                new KeyValuePair<string, string>("ja", "Japanisch")
            };
            Assert(MetadataSelection.FindLanguageIndex(tmdbLanguages, "en") == 1,
                "TMDB defaults to previously searched English");
            Assert(MetadataSelection.FindLanguageIndex(anisearchLanguages, "ja") == 2,
                "AniSearch can restore Japanese");
            Assert(MetadataSelection.FindLanguageIndex(tmdbLanguages, "ja") == 0,
                "TMDB falls back to German when previous language is unsupported");
            Assert(MetadataSelection.FindLanguageIndex(tvMazeLanguages, "de") == 0,
                "TVmaze forces English without overwriting the stored preference");

            var fewerRows = new RenameCountDecision(3, 2);
            Assert(fewerRows.RequiresConfirmation && fewerRows.CanPreview &&
                fewerRows.PairCount == 2 && fewerRows.UntouchedFiles == 1 &&
                fewerRows.IgnoredRows == 0, "Mismatch with too few rows uses first two files");
            var moreRows = new RenameCountDecision(2, 3);
            Assert(moreRows.RequiresConfirmation && moreRows.PairCount == 2 &&
                moreRows.UntouchedFiles == 0 && moreRows.IgnoredRows == 1,
                "Mismatch with too many rows ignores excess rows");
            var equal = new RenameCountDecision(3, 3);
            Assert(!equal.RequiresConfirmation && equal.PairCount == 3,
                "Equal counts do not prompt");
            var empty = new RenameCountDecision(0, 3);
            Assert(!empty.CanPreview, "No file pairs cannot be executed");
            Console.WriteLine("Metadata source/language migration and count-mismatch checks passed.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("Metadata selection regression: " + message);
        }
    }
}
