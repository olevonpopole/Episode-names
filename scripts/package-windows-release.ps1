# Produce a portable release CANDIDATE, not a GitHub release.
# No release/tag is created by this script. Run only after Windows tests.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$build = Join-Path $root 'Rename.9\bin\Release'
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist 'EpisodeNames-Windows-Portable'
$zip = Join-Path $dist 'EpisodeNames-Windows-Portable.zip'

if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null

$runtime = @('Episode-Names.exe', 'Episode-Names.exe.config', 'HtmlAgilityPack.dll')
foreach ($name in $runtime) {
    $src = Join-Path $build $name
    if (-not (Test-Path -LiteralPath $src -PathType Leaf)) {
        throw "Missing build dependency: $name"
    }
    Copy-Item -LiteralPath $src -Destination (Join-Path $stage $name)
}

$configPath = Join-Path $stage 'Episode-Names.exe.config'
[xml]$config = Get-Content -LiteralPath $configPath -Raw
$node = $config.SelectSingleNode('/configuration/appSettings/add[@key="PortableTestBuild"]')
if ($null -ne $node) { throw 'Release candidate must never contain PortableTestBuild flag' }

Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $stage 'README.md')
Copy-Item -LiteralPath (Join-Path $root 'docs\GITHUB_RELEASE_UPDATER.md') -Destination (Join-Path $stage 'UPDATE_INFO.md')

$hashes = foreach ($name in $runtime) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $stage $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
Set-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Value $hashes -Encoding ASCII

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal -Force
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    if (-not (@($archive.Entries.FullName) -contains 'Episode-Names.exe')) {
        throw 'Release ZIP is missing root Episode-Names.exe'
    }
} finally { $archive.Dispose() }

$sha = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $dist 'EpisodeNames-Windows-Portable.zip.sha256') -Encoding ASCII -Value "$sha  EpisodeNames-Windows-Portable.zip"
Write-Host "Prepared portable release candidate, not published: $zip"
Write-Host "SHA256: $sha"
