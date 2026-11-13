using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Episode_Names.Renaming
{
    /// <summary>One proposed move. The paths are fixed when the preview is created.</summary>
    internal sealed class RenameRequest
    {
        public string SourcePath { get; private set; }
        public string DestinationPath { get; private set; }

        public RenameRequest(string sourcePath, string destinationPath)
        {
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
        }
    }

    internal sealed class RenameEntry
    {
        public string SourcePath { get; private set; }
        public string DestinationPath { get; private set; }
        public string Problem { get; internal set; }
        public bool IsUnchanged { get; internal set; }

        public RenameEntry(string sourcePath, string destinationPath)
        {
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
        }
    }

    /// <summary>
    /// A conservative same-folder Windows rename plan.
    /// Does not modify files. Existing targets (including swaps and case-only renames) are blocked.
    /// </summary>
    internal sealed class RenamePlan
    {
        private static readonly StringComparer WindowsPaths = StringComparer.OrdinalIgnoreCase;
        private static readonly HashSet<string> WindowsDevices = new HashSet<string>(
            new[] { "CON", "PRN", "AUX", "NUL", "CLOCK$", "COM1", "COM2", "COM3",
                "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2",
                "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" },
            StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<RenameEntry> Entries { get; private set; }
        public int ChangeCount { get { return Entries.Count(x => !x.IsUnchanged); } }
        public int ErrorCount { get { return Entries.Count(x => !string.IsNullOrEmpty(x.Problem)); } }
        public bool CanExecute { get { return ChangeCount > 0 && ErrorCount == 0; } }

        private RenamePlan(List<RenameEntry> entries)
        {
            Entries = entries.AsReadOnly();
        }

        public static RenamePlan Create(IEnumerable<RenameRequest> requests)
        {
            if (requests == null)
                throw new ArgumentNullException("requests");

            var entries = requests.Select(request =>
                request == null ? new RenameEntry(null, null) :
                    new RenameEntry(request.SourcePath, request.DestinationPath)).ToList();

            foreach (RenameEntry entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.SourcePath) ||
                    string.IsNullOrWhiteSpace(entry.DestinationPath))
                {
                    AddProblem(entry, "Quell- oder Zielpfad fehlt.");
                    continue;
                }

                string source, destination;
                try
                {
                    source = Path.GetFullPath(entry.SourcePath);
                    destination = Path.GetFullPath(entry.DestinationPath);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException ||
                    ex is PathTooLongException)
                {
                    AddProblem(entry, "Ungültiger Quell- oder Zielpfad.");
                    continue;
                }

                string filename = Path.GetFileName(destination);
                if (!WindowsPaths.Equals(Path.GetDirectoryName(source), Path.GetDirectoryName(destination)))
                    AddProblem(entry, "Ziel liegt außerhalb des Quellordners.");
                if (string.IsNullOrWhiteSpace(filename) || filename.Length > 255)
                    AddProblem(entry, "Dateiname fehlt oder ist länger als 255 Zeichen.");
                else
                {
                    if (filename.Any(ch => ch < 32 || "<>:\"/\\|?*".IndexOf(ch) >= 0))
                        AddProblem(entry, "Dateiname enthält unter Windows ungültige Zeichen.");
                    if (filename.EndsWith(".", StringComparison.Ordinal) ||
                        filename.EndsWith(" ", StringComparison.Ordinal))
                        AddProblem(entry, "Dateiname endet auf Punkt oder Leerzeichen.");
                    if (WindowsDevices.Contains(filename.Split('.')[0]))
                        AddProblem(entry, "Reservierter Windows-Dateiname (z. B. CON oder LPT1).");
                }

                if (destination.Length >= 260)
                    AddProblem(entry, "Zielpfad ist für die Legacy-Windows-Anwendung zu lang.");
                if (!File.Exists(source))
                    AddProblem(entry, "Quelldatei existiert nicht mehr.");
                if (string.Equals(source, destination, StringComparison.Ordinal))
                {
                    entry.IsUnchanged = true;
                    continue;
                }
                if (WindowsPaths.Equals(source, destination))
                {
                    AddProblem(entry, "Nur Groß-/Kleinschreibung geändert. Dafür ist ein separater sicherer Ablauf nötig.");
                    continue;
                }

                if (File.Exists(destination) || Directory.Exists(destination))
                    AddProblem(entry, "Ziel existiert bereits (auch belegte Namen oder Vertauschungen werden blockiert).");
            }

            var sourceGroups = entries.Where(x => !string.IsNullOrWhiteSpace(x.SourcePath))
                .GroupBy(x => SafeFullPath(x.SourcePath), WindowsPaths)
                .Where(g => g.Key != null && g.Count() > 1);
            foreach (var group in sourceGroups)
                foreach (RenameEntry entry in group)
                    AddProblem(entry, "Quelldatei mehrfach eingeplant.");

            var destGroups = entries.Where(x => !string.IsNullOrWhiteSpace(x.DestinationPath))
                .GroupBy(x => SafeFullPath(x.DestinationPath), WindowsPaths)
                .Where(g => g.Key != null && g.Count() > 1);
            foreach (var group in destGroups)
                foreach (RenameEntry entry in group)
                    AddProblem(entry, "Mehrere Dateien würden denselben Zielnamen erhalten.");

            return new RenamePlan(entries);
        }

        private static string SafeFullPath(string path)
        {
            try { return Path.GetFullPath(path); }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException ||
                ex is PathTooLongException)
            {
                return null;
            }
        }

        private static void AddProblem(RenameEntry entry, string message)
        {
            entry.Problem = string.IsNullOrEmpty(entry.Problem) ? message : entry.Problem + " " + message;
        }

        /// <summary>Recheck disk state immediately before the first move, then best-effort rollback on I/O failure.</summary>
        public IReadOnlyList<RenameRequest> Execute()
        {
            return Execute(null);
        }

        // Internal test seam for simulating a failure between successive file moves.
        internal IReadOnlyList<RenameRequest> Execute(Action<int> beforeMove)
        {
            RenamePlan fresh = Create(Entries.Select(x => new RenameRequest(x.SourcePath, x.DestinationPath)));
            if (!fresh.CanExecute)
                throw new IOException("Dateizustand hat sich seit der Vorschau geändert oder es bestehen Konflikte. Bitte neu prüfen.");

            var completed = new List<RenameRequest>();
            try
            {
                foreach (RenameEntry entry in fresh.Entries.Where(x => !x.IsUnchanged))
                {
                    if (beforeMove != null)
                        beforeMove(completed.Count);
                    // Keep the no-overwrite invariant even if another process created a target after validation.
                    if (!File.Exists(entry.SourcePath) ||
                        File.Exists(entry.DestinationPath) || Directory.Exists(entry.DestinationPath))
                        throw new IOException("Quelldatei oder Ziel hat sich geändert: " +
                            Path.GetFileName(entry.DestinationPath));

                    File.Move(entry.SourcePath, entry.DestinationPath);
                    completed.Add(new RenameRequest(entry.SourcePath, entry.DestinationPath));
                }

                return completed.AsReadOnly();
            }
            catch (Exception failure)
            {
                var rollbackErrors = new List<string>();
                for (int i = completed.Count - 1; i >= 0; i--)
                {
                    RenameRequest move = completed[i];
                    try
                    {
                        if (File.Exists(move.SourcePath) || Directory.Exists(move.SourcePath))
                            throw new IOException("Alter Dateiname ist erneut belegt.");
                        File.Move(move.DestinationPath, move.SourcePath);
                    }
                    catch (Exception ex)
                    {
                        rollbackErrors.Add(Path.GetFileName(move.DestinationPath) + ": " + ex.Message);
                    }
                }

                string detail = rollbackErrors.Count == 0
                    ? "Bereits ausgeführte Schritte wurden zurückgerollt."
                    : "ACHTUNG: Rücknahme unvollständig. Bitte Dateien manuell kontrollieren: " +
                        string.Join("; ", rollbackErrors);
                throw new IOException("Umbenennung abgebrochen. " + detail + " Grund: " + failure.Message, failure);
            }
        }
    }
}
