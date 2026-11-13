using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Episode_Names.Renaming;

namespace Episode_Names.Metadata.Tests
{
    internal static class RenamePlanChecks
    {
        public static void Run()
        {
            string dir = Path.Combine(Path.GetTempPath(), "EpisodeNames-RenameTests-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string a = Path.Combine(dir, "A.mkv");
                string b = Path.Combine(dir, "B.mkv");
                string exists = Path.Combine(dir, "Existing.mkv");
                File.WriteAllText(a, "A");
                File.WriteAllText(b, "B");
                File.WriteAllText(exists, "Existing");

                CheckBlocked(new[] { new RenameRequest(a, exists) }, "occupied destination");
                CheckBlocked(new[]
                {
                    new RenameRequest(a, Path.Combine(dir, "Same.mkv")),
                    new RenameRequest(b, Path.Combine(dir, "same.MKV"))
                }, "case-insensitive duplicate target");
                CheckBlocked(new[]
                {
                    new RenameRequest(a, b), new RenameRequest(b, a)
                }, "unsafe swap");
                CheckBlocked(new[] { new RenameRequest(a, Path.Combine(dir, "A.MKV")) },
                    "case-only rename");
                CheckBlocked(new[] { new RenameRequest(a, Path.Combine(dir, "CON.txt")) },
                    "reserved Windows device name");
                CheckBlocked(new[] { new RenameRequest(a, Path.Combine(dir, "bad?.mkv")) },
                    "Windows-invalid character");
                CheckBlocked(new[] { new RenameRequest(a, Path.Combine(dir, "name .")) },
                    "trailing period");
                CheckBlocked(new[] { new RenameRequest(a, Path.Combine(dir, "x" + new string('x', 255))) },
                    "filename longer than 255 characters");
                CheckBlocked(new[] { new RenameRequest(a, Path.Combine(dir, "nested", "A.mkv")) },
                    "escaping source folder");
                CheckBlocked(new[] { new RenameRequest(Path.Combine(dir, "Missing.mkv"), a) },
                    "missing source");
                CheckBlocked(new[]
                {
                    new RenameRequest(a, Path.Combine(dir, "Moved.mkv")),
                    new RenameRequest(a, Path.Combine(dir, "Other.mkv"))
                }, "duplicate source");

                string directoryTarget = Path.Combine(dir, "Folder.mkv");
                Directory.CreateDirectory(directoryTarget);
                CheckBlocked(new[] { new RenameRequest(a, directoryTarget) }, "existing directory");
                Check(File.ReadAllText(a) == "A" && File.ReadAllText(b) == "B",
                    "blocked plans never modify existing files");

                string changed = Path.Combine(dir, "Changed.mkv");
                var plan = RenamePlan.Create(new[]
                {
                    new RenameRequest(a, changed), new RenameRequest(b, b)
                });
                Check(plan.CanExecute && plan.ChangeCount == 1 && plan.ErrorCount == 0,
                    "valid plan with unchanged file");
                Check(File.Exists(a) && !File.Exists(changed), "plan alone never renames");
                var moved = plan.Execute();
                Check(moved.Count == 1 && moved[0].SourcePath == a &&
                    File.Exists(changed) && !File.Exists(a), "execute does only planned move");
                var undo = RenamePlan.Create(moved.Select(m =>
                    new RenameRequest(m.DestinationPath, m.SourcePath)));
                Check(undo.CanExecute, "undo mapping is safe");
                undo.Execute();
                Check(File.Exists(a) && !File.Exists(changed), "restore uses the same safe planner");

                string late = Path.Combine(dir, "Late.mkv");
                var stale = RenamePlan.Create(new[] { new RenameRequest(a, late) });
                File.WriteAllText(late, "third party");
                Check(stale.CanExecute, "preview reflected the earlier snapshot");
                try
                {
                    stale.Execute();
                    throw new Exception("stale plan should have failed");
                }
                catch (IOException) { }
                Check(File.ReadAllText(a) == "A" && File.ReadAllText(late) == "third party",
                    "revalidation prevents overwrite when target appeared after preview");

                var failPlan = RenamePlan.Create(new[] {
                    new RenameRequest(a, Path.Combine(dir, "One.mkv")),
                    new RenameRequest(b, Path.Combine(dir, "Two.mkv"))
                });
                try
                {
                    failPlan.Execute(step =>
                    {
                        if (step == 1)
                            throw new IOException("simulated second move failure");
                    });
                    throw new Exception("injected second move failure was not raised");
                }
                catch (IOException) { }
                Check(File.Exists(a) && File.Exists(b) &&
                    !File.Exists(Path.Combine(dir, "One.mkv")) &&
                    !File.Exists(Path.Combine(dir, "Two.mkv")),
                    "rollback restores the first rename after a later failure");

                var noOp = RenamePlan.Create(new[] { new RenameRequest(a, a) });
                Check(!noOp.CanExecute && noOp.ErrorCount == 0,
                    "unchanged-only plan has nothing to execute");
                Console.WriteLine("Safe rename plan checks passed.");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        private static void CheckBlocked(IEnumerable<RenameRequest> requests, string description)
        {
            RenamePlan plan = RenamePlan.Create(requests);
            Check(!plan.CanExecute && plan.ErrorCount > 0, "blocked: " + description);
        }

        private static void Check(bool condition, string description)
        {
            if (!condition)
                throw new Exception("Failed: " + description);
        }
    }
}
