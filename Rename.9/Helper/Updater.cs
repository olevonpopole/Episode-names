using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Episode_Names.Helper
{
    /// <summary>
    /// Safe, opt-in updater from GitHub Releases (public or private). Never
    /// writes into the running installation until a verified archive is staged.
    /// </summary>
    internal static class Updater
    {
        private static bool checking;

        internal static async void CheckUpdate(bool automatic, Form owner)
        {
            if (checking || owner == null || owner.IsDisposed) return;
            bool testBuild = string.Equals(
                ConfigurationManager.AppSettings["PortableTestBuild"], "true",
                StringComparison.OrdinalIgnoreCase);
            if (automatic && testBuild) return;
            checking = true;
            try
            {
                string repository = Properties.Settings.Default.GitHubUpdateRepository;
                string token = GitHubUpdateCredentialStore.Unprotect(
                    Properties.Settings.Default.GitHubUpdateToken);
                var client = new GitHubReleaseClient();
                GitHubReleaseInfo release;
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12)))
                    release = await client.LatestAsync(repository, token, timeout.Token);
                if (owner.IsDisposed) return;

                Version installed = Assembly.GetExecutingAssembly().GetName().Version;
                if (!ReleasePackageValidator.IsNewer(release.Tag, installed))
                {
                    if (!automatic)
                        MessageBox.Show(owner,
                            "Kein neueres stabiles Release verfügbar. Aktuelle Version: " +
                            installed + ".",
                            "Episode Names – GitHub Releases", MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    return;
                }
                if (testBuild)
                {
                    MessageBox.Show(owner, "Neues Release: " + release.Tag +
                        "\nDieses portable TESTBUILD installiert absichtlich keine Updates. " +
                        "Die Release-Prüfung und private GitHub-Verbindung wurden ausgeführt.",
                        "Testbuild – Updateprüfung", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                if (MessageBox.Show(owner,
                    "Neues GitHub Release " + release.Tag + " gefunden.\n" +
                    "Repository: " + repository + "\nAsset: " + release.AssetName + "\n" +
                    "ZIP wird heruntergeladen, per SHA-256 geprüft und erst nach Ihrer " +
                    "Bestätigung bei geschlossenem Programm eingespielt. " +
                    "Vorhandene Dateien werden für ein Rollback gesichert.\n" +
                    "Update vorbereiten?",
                    "Episode Names – Update verfügbar", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) !=
                    DialogResult.Yes)
                    return;

                if (string.IsNullOrWhiteSpace(release.Sha256))
                    throw new InvalidDataException(
                        "Das Release-Asset hat keinen SHA-256-Digest auf GitHub. " +
                        "Aus Sicherheitsgründen findet keine automatische Installation statt.");

                string root = Path.Combine(Path.GetTempPath(),
                    "EpisodeNames-Release-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                try
                {
                    string zip = Path.Combine(root, "release.zip");
                    using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
                        await client.DownloadAsync(repository, release.AssetId, token, zip,
                            timeout.Token);
                    ReleasePackageValidator.VerifySha256(zip, release.Sha256);
                    string stage = Path.Combine(root, "new");
                    ReleasePackageValidator.ExtractToStaging(zip, stage);
                    if (owner.IsDisposed) return;

                    string destination = Path.GetDirectoryName(
                        Assembly.GetExecutingAssembly().Location);
                    string probe = Path.Combine(destination,
                        ".episode-names-update-probe-" + Guid.NewGuid().ToString("N"));
                    try
                    {
                        File.WriteAllText(probe, "write access test");
                    }
                    finally
                    {
                        if (File.Exists(probe)) File.Delete(probe);
                    }
                    if (MessageBox.Show(owner, "Update " + release.Tag +
                        " wurde heruntergeladen und geprüft.\n" +
                        "Episode Names jetzt schließen, Sicherung erstellen und " +
                        "Update installieren?\n\nFalls das Starten des Installers " +
                        "durch Windows-Richtlinien blockiert ist, bleibt das geprüfte " +
                        "Paket hier erhalten:\n" + stage,
                        "Geprüftes Release installieren",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                        return;

                    string script = Path.Combine(root, "apply-update.ps1");
                    File.WriteAllText(script,
                        BuildInstallScript(stage, destination, Process.GetCurrentProcess().Id),
                        new UTF8Encoding(false));
                    var process = new Process {
                        StartInfo = new ProcessStartInfo {
                            FileName = "powershell.exe",
                            Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" +
                                script + "\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WorkingDirectory = root
                        }
                    };
                    process.Start();
                    Application.Exit();
                }
                catch
                {
                    // Leave a validated/staged release untouched for troubleshooting;
                    // failed signature checks never modify the running app.
                    throw;
                }
            }
            catch (OperationCanceledException)
            {
                if (!automatic && !owner.IsDisposed)
                    MessageBox.Show(owner, "GitHub-Updateprüfung hat das Zeitlimit erreicht.",
                        "Updateprüfung", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                if (!automatic && !owner.IsDisposed)
                    MessageBox.Show(owner, "GitHub-Releases-Update nicht verfügbar:\n" +
                        ex.Message +
                        "\n\nBei privatem Repo Lesetoken unter Update-Einstellungen prüfen.",
                        "Updateprüfung", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                // No token or HTTP authorization headers are logged or displayed.
            }
            finally { checking = false; }
        }

        private static string B64(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        internal static string BuildInstallScript(string staged, string destination, int oldPid)
        {
            // All paths are Base64 literals, never embedded unescaped in script
            // commands. The script is local and only launched by a user consent.
            string header =
                "$ErrorActionPreference = 'Stop'\r\n" +
                "$stage = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" +
                    B64(staged) + "'))\r\n" +
                "$target = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" +
                    B64(destination) + "'))\r\n" +
                "$oldPid = " + oldPid + "\r\n";
            return header + @"
$root = Split-Path -Parent $stage
$backup = Join-Path $root 'backup'
$changes = New-Object System.Collections.Generic.List[string]
try {
    for ($i=0; $i -lt 30; $i++) {
        if (-not (Get-Process -Id $oldPid -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Seconds 1
    }
    if (Get-Process -Id $oldPid -ErrorAction SilentlyContinue) {
        throw 'Das alte Programm wurde nicht geschlossen.'
    }
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    foreach ($file in (Get-ChildItem -LiteralPath $stage -File -Recurse)) {
        $relative = $file.FullName.Substring($stage.Length).TrimStart([char]'\')
        $dest = Join-Path $target $relative
        $backupFile = Join-Path $backup $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force | Out-Null
        if (Test-Path -LiteralPath $dest) {
            New-Item -ItemType Directory -Path (Split-Path -Parent $backupFile) -Force | Out-Null
            Copy-Item -LiteralPath $dest -Destination $backupFile -Force
        }
        $changes.Add($relative)
        Copy-Item -LiteralPath $file.FullName -Destination $dest -Force
    }
    Start-Process -FilePath (Join-Path $target 'Episode-Names.exe')
}
catch {
    foreach ($relative in $changes) {
        $dest = Join-Path $target $relative
        $backupFile = Join-Path $backup $relative
        if (Test-Path -LiteralPath $backupFile) {
            Copy-Item -LiteralPath $backupFile -Destination $dest -Force
        }
        elseif (Test-Path -LiteralPath $dest) {
            Remove-Item -LiteralPath $dest -Force
        }
    }
    Set-Content -LiteralPath (Join-Path $root 'update-error.txt') -Value $_.Exception.Message
}
";
        }
    }
}
