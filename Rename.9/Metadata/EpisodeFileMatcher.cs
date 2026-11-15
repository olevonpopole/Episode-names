using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Episode_Names.Metadata
{
    internal sealed class EpisodeFileIdentity
    {
        internal string FileName { get; private set; }
        internal int? Season { get; private set; }
        internal int? Episode { get; private set; }
        internal string Warning { get; private set; }
        internal EpisodeFileIdentity(string name, int? season, int? episode, string warning)
        {
            FileName = name; Season = season; Episode = episode; Warning = warning;
        }
        internal bool IsUnambiguous { get { return Season.HasValue && Episode.HasValue && Warning == null; } }
    }

    internal sealed class EpisodeSuggestion
    {
        internal EpisodeFileIdentity File { get; private set; }
        internal string Title { get; private set; }
        internal string Language { get; private set; }
        internal string Status { get; private set; }
        internal bool IsMatched { get { return Status == "OK"; } }
        internal EpisodeSuggestion(EpisodeFileIdentity file, string title, string language, string status)
        {
            File = file; Title = title; Language = language; Status = status;
        }
    }

    /// <summary>
    /// File-oriented matching, never position based. Unnumbered episodes, specials,
    /// duplicate keys and ambiguous local files are not auto-approved.
    /// </summary>
    internal static class EpisodeFileMatcher
    {
        private static readonly Regex SeasonEpisode = new Regex(
            @"(?<![A-Za-z0-9])S(?<s>\d{1,3})[\s._-]*E(?<e>\d{1,4})(?!\d)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex XEpisode = new Regex(
            @"(?<![A-Za-z0-9])(?<s>\d{1,3})x(?<e>\d{1,4})(?!\d)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex BareEpisode = new Regex(
            @"(?<![A-Za-z0-9])(?:E|EP|Episode|Folge)[\s._-]*(?<e>\d{1,4})(?!\d)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex ParentSeason = new Regex(
            @"^(?:Staffel|Season|Saison|S)[\s._-]*(?<s>\d{1,3})$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static EpisodeFileIdentity Identify(string fileName, string parentFolderName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return new EpisodeFileIdentity(fileName, null, null, "Kein Dateiname");
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            MatchCollection se = SeasonEpisode.Matches(baseName);
            MatchCollection x = XEpisode.Matches(baseName);
            if (se.Count + x.Count > 1)
                return new EpisodeFileIdentity(fileName, null, null, "Mehrdeutige Staffel-/Folgenangabe");

            Match found = se.Count == 1 ? se[0] : x.Count == 1 ? x[0] : null;
            int season, episode;
            if (found != null && found.Success)
            {
                if (!int.TryParse(found.Groups["s"].Value, out season) ||
                    !int.TryParse(found.Groups["e"].Value, out episode) ||
                    episode < 1 || season < 0)
                    return new EpisodeFileIdentity(fileName, null, null, "Ungültige Staffel-/Folgenangabe");
                if (season == 0)
                    return new EpisodeFileIdentity(fileName, season, episode, "Special: manuell prüfen");
                return new EpisodeFileIdentity(fileName, season, episode, null);
            }

            Match bare = BareEpisode.Match(baseName);
            Match parent = ParentSeason.Match(parentFolderName ?? "");
            if (bare.Success && BareEpisode.Matches(baseName).Count == 1 && parent.Success &&
                int.TryParse(parent.Groups["s"].Value, out season) &&
                int.TryParse(bare.Groups["e"].Value, out episode) && episode > 0 && season > 0)
                return new EpisodeFileIdentity(fileName, season, episode, null);

            return new EpisodeFileIdentity(fileName, null, null,
                "Staffel und Folge nicht eindeutig erkennbar");
        }

        internal static IReadOnlyList<EpisodeSuggestion> Match(
            IReadOnlyList<EpisodeFileIdentity> files, IReadOnlyList<EpisodeMetadata> episodes)
        {
            var eligible = (episodes ?? new EpisodeMetadata[0])
                .Where(ep => ep.EpisodeNumber.HasValue && ep.EpisodeNumber.Value > 0 &&
                    !ep.IsSpecial && ep.SeasonNumber > 0 && !string.IsNullOrWhiteSpace(ep.Title))
                .GroupBy(ep => Tuple.Create(ep.SeasonNumber, ep.EpisodeNumber.Value))
                .ToDictionary(g => g.Key, g => g.ToList());

            var localDuplicates = (files ?? new EpisodeFileIdentity[0])
                .Where(f => f.IsUnambiguous).GroupBy(f => Tuple.Create(f.Season.Value, f.Episode.Value))
                .Where(g => g.Count() > 1).Select(g => g.Key).ToList();

            return (files ?? new EpisodeFileIdentity[0]).Select(file =>
            {
                if (!file.IsUnambiguous)
                    return new EpisodeSuggestion(file, "", "", file.Warning);
                var key = Tuple.Create(file.Season.Value, file.Episode.Value);
                if (localDuplicates.Contains(key))
                    return new EpisodeSuggestion(file, "", "", "Doppelte lokale Episodennummer");
                List<EpisodeMetadata> matches;
                if (!eligible.TryGetValue(key, out matches))
                    return new EpisodeSuggestion(file, "", "", "Keine Folge in der Quelle");
                if (matches.Count != 1)
                    return new EpisodeSuggestion(file, "", "", "Mehrdeutiger Metadatentreffer");
                return new EpisodeSuggestion(file, matches[0].Title, matches[0].Language, "OK");
            }).ToList();
        }
    }
}
