using System;
using Episode_Names.Renaming;

namespace Episode_Names.Metadata.Tests
{
    internal static class MetadataRowsChecks
    {
        internal static void Run()
        {
            var windows = MetadataRows.Parse("1\tAufbruch\r\n2\tRettung\r\n");
            Check(windows != null && windows.Count == 2 &&
                windows[0] == "1\tAufbruch" && windows[1] == "2\tRettung",
                "Windows CRLF and trailing newline");

            var mixed = MetadataRows.Parse("\uFEFF1\tAlpha\r2\tBeta\n3\tGamma\r\n");
            Check(mixed != null && mixed.Count == 3 &&
                mixed[0] == "1\tAlpha" && mixed[1] == "2\tBeta" &&
                mixed[2] == "3\tGamma", "Mixed endings and BOM");

            var internalBlank = MetadataRows.Parse("A\r\n\r\nB\r\n");
            Check(internalBlank != null && internalBlank.Count == 3 &&
                internalBlank[1] == "", "Interior empty rows remain visible to validation");

            Check(MetadataRows.Parse(" \r\n\t\n") == null, "Only blank text is no data");
            Console.WriteLine("Metadata editor line-ending checks passed.");
        }

        private static void Check(bool condition, string name)
        {
            if (!condition)
                throw new Exception("Metadata row regression: " + name);
        }
    }
}
