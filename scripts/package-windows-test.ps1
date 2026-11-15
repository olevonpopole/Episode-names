# Portable test bundle. Run on a Windows GitHub Actions runner AFTER all tests pass.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$release = Join-Path $repoRoot 'Rename.9\bin\Release'
$output = Join-Path $repoRoot 'dist\EpisodeNames-Windows-Test'
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
New-Item -ItemType Directory -Path $output -Force | Out-Null

# Copy executable dependencies only, never keys, user settings or debug files.
$runtimeFiles = @('Episode-Names.exe', 'Episode-Names.exe.config', 'HtmlAgilityPack.dll')
foreach ($name in $runtimeFiles) {
    $source = Join-Path $release $name
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Missing portable runtime dependency: $source"
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $output $name)
}

# The old upstream updater must not overwrite this test build.
# This config flag applies ONLY inside the downloadable artifact.
$configPath = Join-Path $output 'Episode-Names.exe.config'
[xml]$config = Get-Content -LiteralPath $configPath -Raw
$root = $config.DocumentElement
if ($root.LocalName -ne 'configuration') { throw 'Unexpected configuration root' }
$appSettings = $root.SelectSingleNode('appSettings')
if ($null -eq $appSettings) {
    $appSettings = $config.CreateElement('appSettings')
    $root.AppendChild($appSettings) | Out-Null
}
$setting = $config.CreateElement('add')
$setting.SetAttribute('key', 'PortableTestBuild')
$setting.SetAttribute('value', 'true')
$appSettings.AppendChild($setting) | Out-Null
$config.Save($configPath)
if (-not (Select-String -LiteralPath $configPath -Pattern 'PortableTestBuild' -Quiet)) {
    throw 'Missing portable updater guard'
}

Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\WINDOWS_TEST_GUIDE.md') -Destination (Join-Path $output 'ANLEITUNG.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination (Join-Path $output 'PROJEKT_README.md')

$fixtures = @(
    @{ Folder = '01_Nummerierung'; Names = @('Serie-Folge-01.mkv','Serie-Folge-02.mkv','Serie-Folge-03.mkv') },
    @{ Folder = '02_Kollision'; Names = @('Alpha.mkv','Beta.mkv') },
    @{ Folder = '03_Reservierter_Name'; Names = @('Alpha.mkv') },
    @{ Folder = '04_Datenzeilen'; Names = @('Testfolge-01.mkv','Testfolge-02.mkv','Testfolge-03.mkv') }
)
foreach ($fixture in $fixtures) {
    $folder = Join-Path (Join-Path $output 'Testdaten') $fixture.Folder
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    foreach ($name in $fixture.Names) {
        Set-Content -LiteralPath (Join-Path $folder $name) -Value "TESTDATEI (kein Video): $name" -Encoding UTF8
    }
}

$launcher = @'
@echo off
cd /d "%~dp0"
start "" "%~dp0Episode-Names.exe" "%~dp0Testdaten\01_Nummerierung"
'@
Set-Content -LiteralPath (Join-Path $output 'START_HIER.cmd') -Value $launcher -Encoding ASCII

$meta = @(
    'Episode Names - PORTABLE WINDOWS TEST BUILD (not a release)'
    'Repository: https://github.com/olevonpopole/Episode-names'
    "Source branch: $env:GITHUB_REF_NAME"
    "Source commit: $env:GITHUB_SHA"
    "Build URL: https://github.com/olevonpopole/Episode-names/actions/runs/$env:GITHUB_RUN_ID"
    'Runtime: Windows with .NET Framework 4.8; no installer or administrator rights required.'
    'Updater: Disabled in THIS test artifact using PortableTestBuild=true.'
    'No TMDB credentials are included.'
    'Testdaten contains fake .mkv placeholder files, not playable media.'
)
Set-Content -LiteralPath (Join-Path $output 'BUILD_INFO.txt') -Value $meta -Encoding UTF8

$hashes = foreach ($name in $runtimeFiles) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Value $hashes -Encoding ASCII

$required = @('Episode-Names.exe', 'Episode-Names.exe.config', 'HtmlAgilityPack.dll',
    'ANLEITUNG.md', 'START_HIER.cmd', 'SHA256SUMS.txt', 'BUILD_INFO.txt')
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $output $name) -PathType Leaf)) {
        throw "Package validation failed: $name"
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $output 'Testdaten\01_Nummerierung\Serie-Folge-01.mkv'))) {
    throw 'Isolated sample files missing from the package'
}
Write-Host "Portable Episode Names test package validated: $output"
Get-ChildItem -LiteralPath $output | Select-Object Name, Length
