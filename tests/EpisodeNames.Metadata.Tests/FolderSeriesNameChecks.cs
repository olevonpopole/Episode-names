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
            // Real-world user-supplied anime and release folder examples.
            Check(@"C:\Anime\[AO]Elainas.Reise.S01E01.DTS.1080p.BD.10bit.AV1", "Elainas Reise");
            Check(@"C:\Anime\Goodbye.Lara.S01E01.English.DL.AAC.1080p.WebDL.x264-PuddingSama", "Goodbye Lara");
            Check(@"C:\Anime\Daemons of the Shadow Realm [1080p][x264][Sub][AAC 2.0][Crunchyroll WebDL]", "Daemons of the Shadow Realm");
            Check(@"C:\Anime\Saga.of.Tanya.the.Evil.2017.S02E01.Ger.Eng.Sub.AAC.1080p.WebDL.x264-PuddingSama", "Saga of Tanya the Evil");
            Check(@"C:\Anime\The.Daily.Life.of.a.Middle.Aged.Online.Shopper.in.Another.World.S01E01.Ger.Eng.Sub.AAC.1080p.CR.WEB.h264-DRiFTKiNG", "The Daily Life of a Middle Aged Online Shopper in Another World");
            Check(@"C:\Anime\Nichijou： My Ordinary Life [1080p][Sub][FLAC 2.0][x264]", "Nichijou： My Ordinary Life");
            // Words in titles are not release metadata; preserve punctuation.
            Check(@"C:\Anime\The English [2022]", "The English");
            Check(@"C:\Anime\Sub.Mariner", "Sub Mariner");

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
