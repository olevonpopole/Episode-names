using System;
using Episode_Names.Helper;

namespace Episode_Names.Metadata.Tests
{
    internal static class FilenameCharactersChecks
    {
        internal static void Run()
        {
            Assert(FilenameCharacters.Colon == "\u2236", "narrow ratio symbol selected");
            Assert(FilenameCharacters.ReplaceColon("Nichijou: My Ordinary Life")
                == "Nichijou\u2236 My Ordinary Life", "colon is replaced without fullwidth glyph");
            Assert(FilenameCharacters.ReplaceColon("A : B")
                == "A \u2236 B", "spaces remain readable");
            Assert(FilenameCharacters.ReplaceColon("Nichijou： My Ordinary Life")
                == "Nichijou： My Ordinary Life", "existing Unicode title preserved");
            Assert(FilenameCharacters.ReplaceColon("A:B").IndexOf(':') < 0, "ASCII colon removed");
            Console.WriteLine("Windows-safe narrow colon substitution checks passed.");
        }
        private static void Assert(bool ok, string message)
        {
            if (!ok) throw new Exception("FilenameCharacters: " + message);
        }
    }
}
