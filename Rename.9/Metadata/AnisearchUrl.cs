using System;
using System.Linq;

namespace Episode_Names.Metadata
{
    /// <summary>
    /// Accepts AniSearch anime URLs without trusting external hosts or unrelated paths.
    /// AniSearch uses /anime/2788,naruto (one ID-and-slug segment); old
    /// links can also have /anime/2788/naruto or append /episodes.
    /// </summary>
    internal static class AnisearchUrl
    {
        internal static bool TryEpisodesUrl(string input, string language, out string episodesUrl)
        {
            episodesUrl = null;
            Uri uri;
            if (!Uri.TryCreate((input ?? "").Trim(), UriKind.Absolute, out uri) ||
                uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort ||
                !IsAnisearchHost(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
                return false;

            string[] parts = uri.AbsolutePath.Split(new[] { '/' },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || parts.Length > 4 ||
                !parts[0].Equals("anime", StringComparison.OrdinalIgnoreCase))
                return false;

            string anime = parts[1];
            int comma = anime.IndexOf(',');
            string id = comma < 0 ? anime : anime.Substring(0, comma);
            if (id.Length == 0 || !id.All(c => c >= '0' && c <= '9'))
                return false;
            if (comma >= 0 && (comma == anime.Length - 1 ||
                !IsValidSegment(anime.Substring(comma + 1))))
                return false;

            // /anime/ID,slug, /anime/ID/slug, or either with trailing /episodes.
            string path;
            if (parts.Length == 2)
                path = "/anime/" + anime;
            else if (parts.Length == 3 && parts[2].Equals("episodes", StringComparison.OrdinalIgnoreCase))
                path = "/anime/" + anime;
            else if (parts.Length == 3 && comma < 0 && IsValidSegment(parts[2]))
                path = "/anime/" + anime + "/" + parts[2];
            else if (parts.Length == 4 && comma < 0 &&
                     IsValidSegment(parts[2]) &&
                     parts[3].Equals("episodes", StringComparison.OrdinalIgnoreCase))
                path = "/anime/" + anime + "/" + parts[2];
            else
                return false;

            string host = string.Equals(language, "de", StringComparison.OrdinalIgnoreCase)
                ? "www.anisearch.de" : "www.anisearch.com";
            episodesUrl = "https://" + host + path + "/episodes";
            return true;
        }

        internal static bool TryResolveSearchResult(string baseDomain, string href, out string url)
        {
            url = null;
            Uri source, resolved;
            if (!Uri.TryCreate(baseDomain, UriKind.Absolute, out source) ||
                !IsAnisearchHost(source.Host) || source.Scheme != Uri.UriSchemeHttps ||
                !Uri.TryCreate(source, href, out resolved) ||
                resolved.Scheme != Uri.UriSchemeHttps || !resolved.IsDefaultPort ||
                !IsAnisearchHost(resolved.Host) || !string.IsNullOrEmpty(resolved.UserInfo))
                return false;
            // Do not accept a different AniSearch hostname in a search result.
            if (!string.Equals(source.Host, resolved.Host, StringComparison.OrdinalIgnoreCase))
                return false;
            string[] segments = resolved.AbsolutePath.Split(new[] { '/' },
                StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2 || !segments[0].Equals("anime",
                StringComparison.OrdinalIgnoreCase))
                return false;
            string ignored;
            if (!TryEpisodesUrl(resolved.AbsoluteUri, source.Host.EndsWith(".de",
                StringComparison.OrdinalIgnoreCase) ? "de" : "en", out ignored))
                return false;
            url = resolved.GetLeftPart(UriPartial.Path);
            return true;
        }

        private static bool IsAnisearchHost(string host)
        {
            return host.Equals("anisearch.de", StringComparison.OrdinalIgnoreCase) ||
                host.Equals("www.anisearch.de", StringComparison.OrdinalIgnoreCase) ||
                host.Equals("anisearch.com", StringComparison.OrdinalIgnoreCase) ||
                host.Equals("www.anisearch.com", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidSegment(string segment)
        {
            return segment.Length > 0 && segment != "." && segment != ".." &&
                !segment.Contains("%2f") && !segment.Contains("%2F") &&
                segment.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ||
                    c == ',' || c == '%' || c == '.');
        }
    }
}
