using System;
using Episode_Names.Metadata;

namespace Episode_Names.Metadata.Tests
{
    internal static class FolderSeriesNameChecks
    {
        internal static void Run()
        {
            Check(@"C:\Serien\Breaking.Bad.S01E01.1080p.WEB-DL.x264", "Breaking Bad");
            Check(@"D:\TV\The.Last.of.Us.S01.1080p", "The Last of Us");
            Check(@"C:\Anime\[SubsPlease] Frieren - 01 [1080p]", "Frieren");
            Check(@"C:\Anime\[Erai-raws] One.Piece.S21E01.1080p", "One Piece");
            Check(@"C:\Shows\Better_Call_Saul_2015_1080p", "Better Call Saul");
            Check(@"C:\Shows\Fate-Zero", "Fate-Zero");
            Check(@"C:\Shows\Spider-Man", "Spider-Man");
            Check(@"C:\Shows\House.of.the.Dragon\Season 02", "House of the Dragon");
            Check(@"C:\Shows\Dark\Staffel 1", "Dark");
            Check(@"C:\Shows\The.Expanse.WEB-DL.1080p", "The Expanse");
            Check(@"C:\Shows\Dr.Who", "Dr Who");
            Check(@"C:\Shows\Naruto", "Naruto");
            Check(@"C:\", "");
            Check(null, "");
            Console.WriteLine("Folder-name series search suggestion checks passed.");
        }

        private static void Check(string source, string expected)
        {
            string actual = FolderSeriesName.Suggest(source);
            if (actual != expected)
                throw new Exception("Folder name " + source + ": expected '" +
                    expected + "', got '" + actual + "'");
        }
    }
}
