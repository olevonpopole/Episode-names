using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Episode_Names.Metadata
{
    /// <summary>
    /// Best-effort search suggestion from a folder name. This never selects a
    /// provider result and never renames any file automatically.
    /// </summary>
    internal static class FolderSeriesName
    {
        private static readonly Regex SeasonFolder = new Regex(
            @"^(?:season|staffel|series|saison)[\s._-]*\d{1,3}$|^s\d{1,3}$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex LeadingReleaseGroup = new Regex(
            @"^\s*(?:\[[^\]]+\]|\([^\)]+\))\s*",
            RegexOptions.CultureInvariant);
        private static readonly Regex Extension = new Regex(
            @"\.(?:mkv|mp4|avi|wmv|mov)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex EpisodeMarker = new Regex(
            @"(?:^|[\s._-])(?:s\d{1,3}[\s._-]*e\d{1,4}|\d{1,3}x\d{1,4}|(?:episode|ep|folge)[\s._-]*\d{1,4})(?=$|[\s._\-\[(])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex ReleaseTag = new Regex(
            @"^(?:\d{3,4}p|4k|uhd|hdr(?:10|10\+)?|dv|dolbyvision|bluray|blu-ray|bdrip|brrip|webrip|web-dl|webdl|hdtv|dvdrip|remux|x264|x265|h264|h265|hevc|av1|avc|aac(?:2\.0)?|ac3|eac3|dts(?:-hd)?|flac|10bit|8bit|dual|multi|multisub|subbed|dubbed|proper|repack|complete|vostfr|nf|amzn|dsnp|cr|subsplease|erai-raws|batch)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Year = new Regex(
            @"^(?:19\d{2}|20\d{2})$", RegexOptions.CultureInvariant);

        internal static string Suggest(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return "";

            // Path.GetFileName does not recognize Windows backslashes when unit
            // tests are run on a non-Windows development machine.
            string path = folderPath.Trim().TrimEnd('\\', '/');
            if (path.Length == 0 || Regex.IsMatch(path, @"^[A-Za-z]:$")) return "";
            string[] parts = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "";
            string leaf = parts[parts.Length - 1];
            if (SeasonFolder.IsMatch(leaf) && parts.Length >= 2)
                leaf = parts[parts.Length - 2];

            leaf = Extension.Replace(leaf, "");
            // Release groups are metadata only when present as leading brackets.
            while (LeadingReleaseGroup.IsMatch(leaf))
                leaf = LeadingReleaseGroup.Replace(leaf, "", 1);
            // Bracketed technical tags often occur without spaces:
            // [1080p][x264][Sub][AAC 2.0][Crunchyroll WebDL].
            // Truncate only after a recognizably technical first bracket;
            // never treat arbitrary title brackets as release groups.
            Match bracketedTag = Regex.Match(leaf,
                @"\[(?:\d{3,4}p|4k|x26[45]|h26[45]|hevc|av1|sub|dub|aac(?:\s*\d+(?:\.\d+)?)?|flac(?:\s*\d+(?:\.\d+)?)?|web[- ]?dl|bluray|bd|crunchyroll(?:\s+web[- ]?dl)?)\]",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (bracketedTag.Success)
                leaf = leaf.Substring(0, bracketedTag.Index);
            // A year in trailing square brackets is release metadata, not title.
            leaf = Regex.Replace(leaf, @"\s*\[(?:19|20)\d{2}\]\s*$", "");
            leaf = Regex.Replace(leaf, @"\s*\[[^\]]*\]\s*$", "");

            leaf = leaf.TrimEnd();
            // Stop at explicit season/episode indicators; the prefix is the
            // series name. Never try to guess anime absolute numbering.
            Match episode = EpisodeMarker.Match(leaf);
            if (episode.Success && episode.Index > 0)
                leaf = leaf.Substring(0, episode.Index);
            // The year before an explicit SxxExx marker identifies the release,
            // not the series name: Saga.of.Tanya.the.Evil.2017.S02E01.
            leaf = Regex.Replace(leaf, @"[._\s-]+(?:19\d{2}|20\d{2})$",
                "", RegexOptions.CultureInvariant);
            // Anime groups often put a bare episode number after " - ".
            leaf = Regex.Replace(leaf, @"\s+-\s+\d{1,4}$", "",
                RegexOptions.CultureInvariant);
            // Remove standalone season markers even when they have no episode.
            leaf = Regex.Replace(leaf,
                @"(?:[._\s-]+)(?:season|staffel|saison|s)\s*[._-]*\d{1,3}(?:[._\s-]|$).*$",
                "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            // Separator-delimited release tokens after the title (e.g. 1080p).
            // Keep ordinary words, even if they look like short abbreviations
            // within the real title.
            leaf = Regex.Replace(leaf, @"[._]+", " ");
            leaf = Regex.Replace(leaf, @"\s+-\s+", " ");
            leaf = Regex.Replace(leaf, @"\s+", " ").Trim();

            var tokens = leaf.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            // Remove the first recognizably technical token and all suffix after it.
            for (int i = 1; i < tokens.Count; i++)
            {
                string token = tokens[i].Trim('(', ')', '[', ']', '-');
                if (ReleaseTag.IsMatch(token) ||
                    (Year.IsMatch(token) && (i == tokens.Count - 1 ||
                        tokens.Skip(i + 1).All(t => ReleaseTag.IsMatch(t)))))
                {
                    tokens = tokens.Take(i).ToList();
                    break;
                }
            }
            string result = string.Join(" ", tokens).Trim(' ', '-', '.', '_');
            return result.Length >= 2 && result.Any(char.IsLetter) ? result : "";
        }
    }
}
