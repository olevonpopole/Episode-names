using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Episode_Names.Helper
{
    internal sealed class GitHubReleaseInfo
    {
        internal string Tag;
        internal string Page;
        internal long AssetId;
        internal string AssetName;
        internal string Sha256;
    }

    /// <summary>
    /// GitHub REST releases/latest for public or private repos. Token is supplied
    /// at runtime only; URLs are always pinned to api.github.com.
    /// </summary>
    internal sealed class GitHubReleaseClient
    {
        private readonly HttpClient client;
        internal GitHubReleaseClient(HttpClient http = null)
        {
            client = http ?? new HttpClient {
                Timeout = TimeSpan.FromSeconds(20),
                MaxResponseContentBufferSize = 4 * 1024 * 1024
            };
        }

        private static HttpRequestMessage Request(string endpoint, string token,
            string accept = "application/vnd.github+json")
        {
            var req = new HttpRequestMessage(HttpMethod.Get, endpoint);
            req.Headers.UserAgent.ParseAdd("Episode-Names-Updater/2.0");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
            req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            if (!string.IsNullOrWhiteSpace(token))
                req.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token.Trim());
            return req;
        }

        internal async Task<GitHubReleaseInfo> LatestAsync(
            string repository, string token, CancellationToken ct)
        {
            if (!ReleasePackageValidator.ValidRepository(repository))
                throw new InvalidDataException(
                    "Repository bitte im Format Besitzer/Repository eintragen.");
            string endpoint = "https://api.github.com/repos/" + repository +
                "/releases/latest";
            using (var req = Request(endpoint, token))
            using (var response = await client.SendAsync(req, ct).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                    throw new InvalidOperationException(
                        "Kein öffentliches/zugängliches GitHub Release gefunden. " +
                        "Bei privatem Repo einen Lesetoken einrichten.");
                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.Forbidden)
                    throw new InvalidOperationException(
                        "GitHub-Zugriff verweigert. Token-/Repositoryberechtigung prüfen.");
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        "GitHub-Releases-Abfrage fehlgeschlagen (HTTP " +
                        (int)response.StatusCode + ").");
                using (var stream = await response.Content.ReadAsStreamAsync()
                    .ConfigureAwait(false))
                {
                    var data = (ReleaseDto)new DataContractJsonSerializer(
                        typeof(ReleaseDto)).ReadObject(stream);
                    if (data == null || data.Draft || data.Prerelease ||
                        string.IsNullOrWhiteSpace(data.Tag))
                        throw new InvalidDataException(
                            "Kein stabiles GitHub Release vorhanden.");
                    var asset = (data.Assets ?? new AssetDto[0])
                        .Where(a => a.Id > 0 && a.Size > 0 &&
                            a.Size <= 128L * 1024 * 1024 &&
                            (string.Equals(a.Name, "EpisodeNames-Windows-Portable.zip",
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(a.Name, "Anwendung.zip",
                                StringComparison.OrdinalIgnoreCase) ||
                            (a.Name != null &&
                            a.Name.StartsWith("EpisodeNames-Windows-", StringComparison.OrdinalIgnoreCase) &&
                            a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))))
                        .OrderBy(a => a.Name == "EpisodeNames-Windows-Portable.zip" ? 0 : 1)
                        .FirstOrDefault();
                    if (asset == null)
                        throw new InvalidDataException(
                            "Im Release fehlt ein Windows-Portable-ZIP-Asset.");
                    return new GitHubReleaseInfo {
                        Tag = data.Tag, Page = data.HtmlUrl,
                        AssetId = asset.Id, AssetName = asset.Name,
                        Sha256 = asset.Digest != null &&
                            asset.Digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                            ? asset.Digest.Substring(7) : ""
                    };
                }
            }
        }

        internal async Task DownloadAsync(string repository, long assetId,
            string token, string output, CancellationToken ct)
        {
            if (!ReleasePackageValidator.ValidRepository(repository) || assetId <= 0)
                throw new ArgumentException("Ungültiger Release-Asset-Bezug.");
            string url = "https://api.github.com/repos/" + repository +
                "/releases/assets/" + assetId;
            using (var req = Request(url, token, "application/octet-stream"))
            using (var response = await client.SendAsync(req,
                HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        "Download des GitHub-Release-Assets fehlgeschlagen (HTTP " +
                        (int)response.StatusCode + ").");
                // Some proxy/API variants may return JSON metadata instead
                // of the requested asset bytes. ZIP signature is verified too.
                using (var source = await response.Content.ReadAsStreamAsync()
                    .ConfigureAwait(false))
                using (var dest = File.Create(output))
                {
                    byte[] buffer = new byte[65536];
                    long written = 0;
                    int n;
                    while ((n = await source.ReadAsync(buffer, 0, buffer.Length, ct)
                        .ConfigureAwait(false)) > 0)
                    {
                        written += n;
                        if (written > 128L * 1024 * 1024)
                            throw new InvalidDataException("Release-Download zu groß.");
                        await dest.WriteAsync(buffer, 0, n, ct).ConfigureAwait(false);
                    }
                }
                using (var f = File.OpenRead(output))
                {
                    byte[] magic = new byte[4];
                    if (f.Read(magic, 0, 4) != 4 ||
                        magic[0] != 0x50 || magic[1] != 0x4b ||
                        magic[2] != 0x03 || magic[3] != 0x04)
                        throw new InvalidDataException(
                            "GitHub lieferte kein ZIP-Asset (Token/Proxy prüfen).");
                }
            }
        }

        [DataContract] private sealed class ReleaseDto
        {
            [DataMember(Name = "tag_name")] public string Tag { get; set; }
            [DataMember(Name = "html_url")] public string HtmlUrl { get; set; }
            [DataMember(Name = "draft")] public bool Draft { get; set; }
            [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
            [DataMember(Name = "assets")] public AssetDto[] Assets { get; set; }
        }
        [DataContract] private sealed class AssetDto
        {
            [DataMember(Name = "id")] public long Id { get; set; }
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "size")] public long Size { get; set; }
            [DataMember(Name = "digest")] public string Digest { get; set; }
        }
    }
}
