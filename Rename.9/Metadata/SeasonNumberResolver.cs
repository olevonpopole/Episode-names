using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Episode_Names.Metadata
{
    /// <summary>
    /// Resolves the season per file. Never guesses from row order or a mere
    /// episode number; disagreements abort the rename preview.
    /// </summary>
    internal static class SeasonNumberResolver
    {
        private static readonly Regex DataIdentity = new Regex(
            @"^\s*S(?<season>\d{1,3})E(?<episode>\d{1,4})(?=\s|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex SeasonFolder = new Regex(
            @"^(?:Staffel|Season|Saison|S)[\s._-]*(?<season>\d{1,3})$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static string Resolve(string fileName, string folderName, string dataRow)
        {
            var file = EpisodeFileMatcher.Identify(fileName, folderName);
            if (file.Warning != null && file.Warning.StartsWith("Mehrdeutige",
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Mehrdeutige Episodenangaben in Datei " + fileName + ".");
            int? fileSeason = file.Season;
            int? rowSeason = null;
            Match row = DataIdentity.Match(dataRow ?? "");
            int number;
            if (row.Success && int.TryParse(row.Groups["season"].Value, out number))
                rowSeason = number;
            int? parentSeason = null;
            Match parent = SeasonFolder.Match(folderName ?? "");
            if (parent.Success && int.TryParse(parent.Groups["season"].Value, out number))
                parentSeason = number;

            if (file.Episode.HasValue && rowSeason.HasValue && row.Success &&
                int.TryParse(row.Groups["episode"].Value, out number) &&
                file.Episode.Value != number)
                throw new InvalidOperationException(
                    "Episodennummer widerspricht den Daten: " + fileName + ".");
            if (fileSeason.HasValue && rowSeason.HasValue && fileSeason != rowSeason)
                throw new InvalidOperationException(
                    "Staffel widerspricht den Daten: Datei " + fileName +
                    " (S" + fileSeason.Value.ToString("00") +
                    ") und Datenzeile (S" + rowSeason.Value.ToString("00") + ").");
            if (fileSeason.HasValue && parentSeason.HasValue && fileSeason != parentSeason)
                throw new InvalidOperationException(
                    "Staffel widerspricht dem Ordner: Datei " + fileName + ".");
            if (rowSeason.HasValue && parentSeason.HasValue && rowSeason != parentSeason)
                throw new InvalidOperationException(
                    "Staffel widerspricht dem Ordner: Datenzeile " + dataRow + ".");

            int? season = fileSeason ?? rowSeason ?? parentSeason;
            if (!season.HasValue)
                throw new InvalidOperationException(
                    "Keine eindeutige Staffelnummer für " + fileName +
                    ". Bitte S01E01 in den Dateinamen/Daten oder Staffel 01 als Ordner verwenden.");
            return season.Value.ToString("00");
        }
    }
}
