using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Episode_Names.Helper
{
    /// <summary>Pure, offline-testable GitHub release package validation.</summary>
    internal static class ReleasePackageValidator
    {
        internal static bool ValidRepository(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                Regex.IsMatch(value, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$") &&
                !value.Contains("..");
        }

        internal static bool IsNewer(string releaseTag, Version current)
        {
            string normalized = (releaseTag ?? "").Trim().TrimStart('v', 'V');
            int cut = normalized.IndexOf('-');
            if (cut >= 0) return false; // no accidental prerelease upgrade
            Version incoming;
            if (!Version.TryParse(normalized, out incoming)) return false;
            Version ours = current ?? new Version(0, 0);
            return incoming > ours;
        }

        internal static void VerifySha256(string filePath, string expected)
        {
            if (string.IsNullOrEmpty(expected) ||
                !Regex.IsMatch(expected, @"^[0-9a-fA-F]{64}$"))
                throw new InvalidDataException(
                    "Release-Asset enthält keinen verwendbaren SHA-256-Digest.");
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                string digest = BitConverter.ToString(hash.ComputeHash(stream))
                    .Replace("-", "");
                if (!string.Equals(digest, expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("SHA-256-Prüfung des Release-Pakets fehlgeschlagen.");
            }
        }

        internal static IReadOnlyList<string> ExtractToStaging(string zipPath, string stagingFolder)
        {
            if (Directory.Exists(stagingFolder) &&
                Directory.EnumerateFileSystemEntries(stagingFolder).Any())
                throw new InvalidOperationException("Update-Staging muss leer sein.");
            Directory.CreateDirectory(stagingFolder);
            string root = Path.GetFullPath(stagingFolder).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var extracted = new List<string>();
            long totalSize = 0;
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                if (archive.Entries.Count > 300)
                    throw new InvalidDataException("Release enthält zu viele Dateien.");
                foreach (var entry in archive.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    if (string.IsNullOrWhiteSpace(name) || name.StartsWith("/") ||
                        name.Contains(":") || name.Split('/').Any(part =>
                            part == "." || part == ".."))
                        throw new InvalidDataException("Ungültiger Pfad im Release-Paket.");
                    string output = Path.GetFullPath(Path.Combine(stagingFolder,
                        name.Replace('/', Path.DirectorySeparatorChar)));
                    if (!output.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("ZIP-Pfad verlässt den Zielordner.");
                    // Unix symlinks can redirect extraction outside staging.
                    int mode = (entry.ExternalAttributes >> 16) & 0xF000;
                    if (mode == 0xA000)
                        throw new InvalidDataException("ZIP-Symlinks sind nicht erlaubt.");
                    if (name.EndsWith("/", StringComparison.Ordinal))
                        continue;
                    if (!seen.Add(output))
                        throw new InvalidDataException("Doppelte Dateipfade im Update.");
                    totalSize += entry.Length;
                    if (entry.Length > 128L * 1024 * 1024 ||
                        totalSize > 256L * 1024 * 1024)
                        throw new InvalidDataException("Release-Paket überschreitet Größenlimit.");
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    entry.ExtractToFile(output, false);
                    extracted.Add(output);
                }
            }
            if (!File.Exists(Path.Combine(stagingFolder, "Episode-Names.exe")))
                throw new InvalidDataException(
                    "Das Release-Paket enthält keine Episode-Names.exe im Stammordner.");
            return extracted;
        }
    }
}
