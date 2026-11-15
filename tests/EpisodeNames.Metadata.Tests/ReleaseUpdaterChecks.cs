using Episode_Names.Helper;
using Episode_Names.Metadata;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Episode_Names.Metadata.Tests
{
    internal static class ReleaseUpdaterChecks
    {
        internal static void Run()
        {
            Assert(ReleasePackageValidator.ValidRepository("olevonpopole/Episode-names"),
                "legitimate GitHub slug");
            foreach (string invalid in new[] { "../repo", "a/b/c", "https://github.com/x/y",
                "a//b", "evil.com/repo?x=1", "user/..", "a/b.." })
                Assert(!ReleasePackageValidator.ValidRepository(invalid),
                    "reject malformed repo " + invalid);
            Assert(ReleasePackageValidator.IsNewer("v1.5.0", new Version(1, 4, 19)),
                "allow newer stable semver");
            Assert(!ReleasePackageValidator.IsNewer("v1.4.0", new Version(1, 4, 19)),
                "reject downgrades");
            Assert(!ReleasePackageValidator.IsNewer("v2.0.0-beta", new Version(1, 4, 19)),
                "prerelease never installed");
            Assert(MetadataSourceFavorites.Load("").SequenceEqual(new[] { "tvmaze" }),
                "fast default search when no favorites stored");
            Assert(MetadataSourceFavorites.Save(new[] {
                "jikan", "anizip", "jikan", "unknown"
            }) == "jikan;anizip", "stable favorites exclude duplicates/unknown");
            Assert(MetadataSourceFavorites.UsesMalLookup(new[] { "anizip" }),
                "AniZip favorites route via MAL identity");

            string folder = Path.Combine(Path.GetTempPath(),
                "EpisodeNames-ReleaseUpdaterTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string good = Path.Combine(folder, "good.zip");
                using (var archive = ZipFile.Open(good, ZipArchiveMode.Create))
                {
                    Write(archive, "Episode-Names.exe", "fixture executable");
                    Write(archive, "bin/fixture.dll", "safe library");
                }
                byte[] expected;
                using (var sha = SHA256.Create())
                using (var file = File.OpenRead(good))
                    expected = sha.ComputeHash(file);
                ReleasePackageValidator.VerifySha256(good,
                    BitConverter.ToString(expected).Replace("-", ""));
                Reject(() => ReleasePackageValidator.VerifySha256(good,
                    new string('0', 64)), "tampered digest");
                string extracted = Path.Combine(folder, "stage");
                var files = ReleasePackageValidator.ExtractToStaging(good, extracted);
                Assert(files.Count == 2 &&
                    File.Exists(Path.Combine(extracted, "Episode-Names.exe")),
                    "valid release extracted only to isolated staging");

                string malicious = Path.Combine(folder, "malicious.zip");
                using (var archive = ZipFile.Open(malicious, ZipArchiveMode.Create))
                {
                    Write(archive, "Episode-Names.exe", "fake");
                    Write(archive, "../outside.txt", "must not escape");
                }
                Reject(() => ReleasePackageValidator.ExtractToStaging(malicious,
                    Path.Combine(folder, "unsafe")), "zip-slip");
                Assert(!File.Exists(Path.Combine(folder, "outside.txt")),
                    "zip traversal cannot write outside staging");

                string bad = Path.Combine(folder, "withoutexe.zip");
                using (var archive = ZipFile.Open(bad, ZipArchiveMode.Create))
                    Write(archive, "readme.txt", "not an application");
                Reject(() => ReleasePackageValidator.ExtractToStaging(bad,
                    Path.Combine(folder, "missingexe")), "missing app payload");

                CheckPrivateGithubAsync().GetAwaiter().GetResult();
                string protectedToken = GitHubUpdateCredentialStore.Protect("fake-fixture-token");
                Assert(!protectedToken.Contains("fake-fixture-token") &&
                    GitHubUpdateCredentialStore.Unprotect(protectedToken) ==
                        "fake-fixture-token",
                    "private access token is encrypted with user DPAPI");
            }
            finally { Directory.Delete(folder, true); }
            Console.WriteLine("GitHub release authentication, validation, ZIP safety and favorites checks passed.");
        }

        private static async Task CheckPrivateGithubAsync()
        {
            const string digest = "sha256:0123456789012345678901234567890123456789012345678901234567890123";
            string fixture = "{\"tag_name\":\"v2.0.0\",\"draft\":false,\"prerelease\":false," +
                "\"html_url\":\"https://github.com/olevonpopole/Episode-names/releases/tag/v2.0.0\"," +
                "\"assets\":[{\"id\":42,\"name\":\"EpisodeNames-Windows-Portable.zip\"," +
                "\"size\":100,\"digest\":\"" + digest + "\"}]}";
            var fake = new FakeHandler(fixture);
            var release = await new GitHubReleaseClient(new HttpClient(fake))
                .LatestAsync("olevonpopole/Episode-names", "not-a-real-token",
                    CancellationToken.None);
            Assert(release.Tag == "v2.0.0" && release.AssetId == 42 &&
                release.Sha256 == digest.Substring(7), "stable release digest parsed");
            Assert(fake.CheckedBearer, "private repo token is sent only to GitHub API");
        }

        private static void Write(ZipArchive archive, string name, string value)
        {
            var entry = archive.CreateEntry(name);
            using (var writer = new StreamWriter(entry.Open()))
                writer.Write(value);
        }

        private static void Reject(Action action, string label)
        {
            bool threw = false;
            try { action(); }
            catch (InvalidDataException) { threw = true; }
            Assert(threw, "expected rejection: " + label);
        }

        private static void Assert(bool ok, string label)
        {
            if (!ok) throw new Exception("ReleaseUpdater: " + label);
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly string response;
            internal bool CheckedBearer;
            internal FakeHandler(string response) { this.response = response; }
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CheckedBearer = request.RequestUri.Host == "api.github.com" &&
                    request.Headers.Authorization != null &&
                    request.Headers.Authorization.Scheme == "Bearer" &&
                    request.Headers.Authorization.Parameter == "not-a-real-token";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(response, Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
