using System;
using Episode_Names.Metadata;

namespace Episode_Names.Metadata.Tests
{
    internal static class SeasonNumberChecks
    {
        internal static void Run()
        {
            Check("Show.S02E03.mkv", "Show", "S02E03\tThird", "02");
            Check("Show.1x06.mkv", "Show", "1\tSixth", "01");
            Check("E07.mkv", "Staffel 3", "7\tSeventh", "03");
            Check("unnumbered.mkv", "Season 12", "12\tTitle", "12");
            Check("unnumbered.mkv", "Show", "S04E02\tName", "04");
            Check("Show.S01E03.mkv", "Show", "S01E03\tName", "01");
            Check("Show.S00E01.mkv", "Show", "S00E01\tSpecial", "00");
            Fail("Show.S01E03.mkv", "Show", "S02E03\tWrong", "Staffel widerspricht");
            Fail("Show.S01E03.mkv", "Show", "S01E04\tWrong", "Episodennummer widerspricht");
            Fail("Show.S01E03.S02E02.mkv", "Show", "S01E03\tWrong", "Mehrdeutige");
            Fail("Show.S02E03.mkv", "Staffel 1", "2\tTitle", "Staffel widerspricht");
            Fail("unnumbered.mkv", "Show", "1\tTitle", "Keine eindeutige Staffelnummer");
            Console.WriteLine("Season number formatting and ambiguity checks passed.");
        }
        private static void Check(string file, string folder, string data, string expected)
        {
            string actual = SeasonNumberResolver.Resolve(file, folder, data);
            if (actual != expected)
                throw new Exception("%season expected " + expected + " but got " + actual);
        }
        private static void Fail(string file, string folder, string data, string message)
        {
            try
            {
                SeasonNumberResolver.Resolve(file, folder, data);
                throw new Exception("%season should reject ambiguous input: " + file);
            }
            catch (InvalidOperationException ex)
            {
                if (!ex.Message.Contains(message)) throw;
            }
        }
    }
}
