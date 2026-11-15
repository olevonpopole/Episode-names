using System;
using System.Collections.Generic;
using System.Linq;
using Episode_Names.Metadata;

namespace Episode_Names.Metadata.Tests
{
    internal static class EpisodeFileMatcherChecks
    {
        internal static void Run()
        {
            Assert(EpisodeFileMatcher.Identify("[AO]Elainas.Reise.S01E01.DTS.1080p.AV1.mkv",
                "Elainas Reise").IsUnambiguous, "SxxExx recognized");
            var two = EpisodeFileMatcher.Identify("Show.02x03.1080p.mkv", "Show");
            Assert(two.IsUnambiguous && two.Season == 2 && two.Episode == 3, "2x03 recognized");
            var seasonFolder = EpisodeFileMatcher.Identify("E04.mkv", "Staffel 2");
            Assert(seasonFolder.IsUnambiguous && seasonFolder.Season == 2 &&
                seasonFolder.Episode == 4, "season folder and episode");
            Assert(!EpisodeFileMatcher.Identify("Anime - 01 [1080p].mkv", "Anime").IsUnambiguous,
                "anime absolute episode requires explicit mapping");
            Assert(!EpisodeFileMatcher.Identify("Anime.S00E01.mkv", "Anime").IsUnambiguous,
                "specials never automatically matched");
            Assert(!EpisodeFileMatcher.Identify("Anime.S01E01.S02E03.mkv", "Anime").IsUnambiguous,
                "ambiguous double episode marker");
            var files = new[] {
                EpisodeFileMatcher.Identify("Anime.S02E03.mkv", "Anime"),
                EpisodeFileMatcher.Identify("Anime.S01E02.mkv", "Anime")
            };
            var catalog = new[] {
                new EpisodeMetadata("tvmaze","1",1,2,null,"Second", "en",false),
                new EpisodeMetadata("tvmaze","2",2,3,null,"Third", "en",false),
                new EpisodeMetadata("tvmaze","3",0,1,null,"Special", "en",true)
            };
            var suggestions = EpisodeFileMatcher.Match(files, catalog);
            Assert(suggestions.Count == 2 && suggestions[0].Title == "Third" &&
                suggestions[1].Title == "Second" && suggestions.All(x => x.IsMatched),
                "match by key not metadata list order");
            var duplicate = EpisodeFileMatcher.Match(
                new[] { files[0], files[0] }, catalog);
            Assert(duplicate.All(x => !x.IsMatched), "duplicate local ID blocks suggestions");
            var incomplete = EpisodeFileMatcher.Match(
                new[] { files[0], EpisodeFileMatcher.Identify("random.mkv", "Anime") }, catalog);
            Assert(incomplete[0].IsMatched && !incomplete[1].IsMatched,
                "unidentified file never auto-approves");
            var ambiguousProvider = EpisodeFileMatcher.Match(files,
                catalog.Concat(new [] {
                    new EpisodeMetadata("tvmaze","4",2,3,null,"Another title", "en",false)
                }).ToList());
            Assert(!ambiguousProvider[0].IsMatched, "duplicate provider episode blocks mapping");
            Console.WriteLine("Episode file-to-metadata matching checks passed.");
        }
        private static void Assert(bool condition, string label)
        {
            if (!condition) throw new Exception("EpisodeFileMatcher: " + label);
        }
    }
}
