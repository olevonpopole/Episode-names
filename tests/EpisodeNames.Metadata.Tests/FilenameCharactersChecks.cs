using System;
using System.IO;
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
            // Exercise the actual filesystem, not merely char validation.
            string dir = Path.Combine(Path.GetTempPath(),
                "EpisodeNames-NarrowColon-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string oldName = Path.Combine(dir, "sample.txt");
                string newName = Path.Combine(dir,
                    FilenameCharacters.ReplaceColon("Series: Part One") + ".txt");
                File.WriteAllText(oldName, "safe temporary test");
                File.Move(oldName, newName);
                Assert(File.Exists(newName) && !File.Exists(oldName),
                    "real rename with Unicode colon succeeds on filesystem");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
            Console.WriteLine("Windows-safe narrow colon substitution checks passed.");
        }
        private static void Assert(bool ok, string message)
        {
            if (!ok) throw new Exception("FilenameCharacters: " + message);
        }
    }
}
