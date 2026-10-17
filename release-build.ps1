# Builds slop! and publishes it as a GitHub release of wuaht/osu,
# which the in-game updater (osu.Desktop/Updater/VelopackUpdateManager.cs) checks for updates.
#
# The first release's "slop-win-Setup.exe" is the installer. It installs to %LocalAppData%\slop,
# independently of an official osu!(lazer) installation.
#
# Platforms (-Platforms, default: win):
#   win    Velopack installer + updates (channel "win").
#   linux  Velopack AppImage (x64) + updates (channel "linux"). Velopack can cross-pack Linux packages on Windows.
#   osx    Portable, unsigned zips for Apple Silicon (osx-arm64) and Intel (osx-x64), without auto-updates.
#          Velopack can only pack (and sign / notarize) macOS packages on macOS.
# Android and iOS are not supported: iOS can only be built on a Mac (Xcode + Apple signing), and Android
# requires the .NET Android workload, Android SDK / JDK and a signing keystore.
#
# Usage:
#   ./release-build.ps1                    # build and publish the next version (e.g. 2026.1008.0)
#   ./release-build.ps1 -SkipUpload        # only build the installer locally (in .slop-release/)
#   ./release-build.ps1 -Platforms win,linux,osx
#   ./release-build.ps1 -Version 2026.1008.1
#   ./release-build.ps1 -Force             # skip the checks for uncommitted / unpushed changes
#   ./release-build.ps1 -OfficialSchemaVersion 52   # don't look up the realm schema version of the latest official release
#
# Requirements: .NET SDK, GitHub CLI (gh, logged in), and the osu-framework / osu-resources checkouts next to this repository.

param(
    [string]$Version,
    [string[]]$Platforms = @('win'),
    [switch]$SkipUpload,
    [switch]$Force,
    [int]$OfficialSchemaVersion
)

$ErrorActionPreference = 'Stop'

$repoUrl = 'https://github.com/wuaht/osu'
$repoName = 'wuaht/osu'
$packId = 'slop'
$packTitle = 'slop!'
$branch = 'slop'

$root = $PSScriptRoot
$outputDir = Join-Path $root '.slop-release'
$publishDir = Join-Path $outputDir 'publish'
$releasesDir = Join-Path $outputDir 'releases'

function Invoke-Checked([string]$description, [scriptblock]$command)
{
    Write-Host "==> $description" -ForegroundColor Cyan
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$description failed (exit code $LASTEXITCODE)." }
}

# Changes to IDE files don't affect the build.
function Get-RelevantChanges([string]$path)
{
    git -C $path status --porcelain | Where-Object { $_ -notmatch '\.idea/' }
}

function Assert-Pushed([string]$path, [string]$name)
{
    if (Get-RelevantChanges $path)
    {
        throw "$name has uncommitted changes. Commit them first, or use -Force."
    }

    git -C $path fetch origin --quiet
    $unpushed = git -C $path log --oneline "origin/$branch..HEAD"
    if ($unpushed)
    {
        throw "$name has commits which are not pushed to origin/$branch. Push them first, or use -Force."
    }
}

Set-Location $root

# --- Checks ---

# "-Platforms win,linux" arrives as a single string when the script is run with "powershell -File".
$Platforms = @($Platforms | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ } | Select-Object -Unique)
foreach ($platform in $Platforms)
{
    if ($platform -notin @('win', 'linux', 'osx')) { throw "Unknown platform '$platform'. Supported platforms: win, linux, osx." }
}
if ($Platforms.Count -eq 0) { throw 'No platforms given.' }

if ((git rev-parse --abbrev-ref HEAD) -ne $branch) { throw "Releases must be made from the '$branch' branch." }

foreach ($dependency in @('osu-framework', 'osu-resources'))
{
    if (-not (Test-Path (Join-Path $root "../$dependency"))) { throw "../$dependency is missing (see README)." }
}

if (-not $SkipUpload)
{
    gh auth status *> $null
    if ($LASTEXITCODE -ne 0) { throw "The GitHub CLI is not logged in. Run 'gh auth login' first." }

    if (-not $Force)
    {
        # The release is built from the local checkouts, so they must match what is on GitHub.
        Assert-Pushed $root 'osu'
        Assert-Pushed (Join-Path $root '../osu-framework') 'osu-framework'
        Assert-Pushed (Join-Path $root '../osu-resources') 'osu-resources'
    }
}

# --- Version ---

$existingTags = @()
if (-not $SkipUpload)
{
    $existingTags = @(gh release list --repo $repoName --limit 1000 --json tagName --jq '.[].tagName')
}

# The last version is stored locally, so that each run of this script uses the next number.
$lastVersionFile = Join-Path $outputDir 'last-version.txt'

if (-not $Version)
{
    # Same scheme as official releases: year.monthday.n, where n counts up with each run on the same day.
    $now = Get-Date
    $prefix = '{0}.{1}{2:00}' -f $now.Year, $now.Month, $now.Day
    $n = 0

    if (Test-Path $lastVersionFile)
    {
        $lastVersion = (Get-Content -Raw $lastVersionFile).Trim()
        if ($lastVersion -match "^$([regex]::Escape($prefix))\.(\d+)$") { $n = [int]$Matches[1] + 1 }
    }

    $Version = "$prefix.$n"
}

if ($existingTags -contains $Version) { throw "A release for version $Version already exists." }

New-Item -ItemType Directory -Force $outputDir | Out-Null
Set-Content -Path $lastVersionFile -Encoding utf8 -Value $Version

Write-Host "Releasing slop! $Version" -ForegroundColor Green

# --- Official database compatibility ---

# The client shares its data folder with official osu!(lazer). If it uses a newer realm schema version than the latest official release,
# it uses a separate copy of the database, so that the official release can still open the shared one (see osu.Game/Database/OfficialDatabaseCompatibility.cs).

function Get-SchemaVersion([string]$source, [string]$content)
{
    $match = [regex]::Match($content, 'private const int schema_version = (\d+);')
    if (-not $match.Success) { throw "Could not find the realm schema version in $source." }
    [int]$match.Groups[1].Value
}

if (-not $OfficialSchemaVersion)
{
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw "The GitHub CLI is required to look up the latest official release. Alternatively, use -OfficialSchemaVersion." }

    $officialTag = gh release list --repo ppy/osu --exclude-pre-releases --exclude-drafts --limit 1 --json tagName --jq '.[0].tagName'
    if ($LASTEXITCODE -ne 0 -or -not $officialTag) { throw "Failed to look up the latest official release. Alternatively, use -OfficialSchemaVersion." }

    Invoke-Checked "Fetch official release $officialTag" { git fetch upstream "refs/tags/${officialTag}:refs/tags/$officialTag" --quiet }

    $officialRealmAccess = git show "${officialTag}:osu.Game/Database/RealmAccess.cs" | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Failed to read RealmAccess.cs of $officialTag." }

    $OfficialSchemaVersion = Get-SchemaVersion $officialTag $officialRealmAccess
}

$clientSchemaVersion = Get-SchemaVersion 'osu.Game/Database/RealmAccess.cs' (Get-Content -Raw (Join-Path $root 'osu.Game/Database/RealmAccess.cs'))

Write-Host "Realm schema version: $clientSchemaVersion (latest official release: $OfficialSchemaVersion)" -ForegroundColor Green
if ($clientSchemaVersion -gt $OfficialSchemaVersion)
{
    Write-Host "This build will use a separate database (client_$clientSchemaVersion.realm) until official osu!(lazer) supports schema version $clientSchemaVersion." -ForegroundColor Yellow
}

# --- Velopack CLI ---

# The CLI should match the major / minor version of the Velopack library used by the game.
$velopackVersion = ([xml](Get-Content (Join-Path $root 'osu.Desktop/osu.Desktop.csproj'))).Project.ItemGroup.PackageReference |
    Where-Object { $_.Include -eq 'Velopack' } |
    Select-Object -ExpandProperty Version -First 1

function Get-MajorMinor([string]$v) { ($v -split '\.')[0..1] -join '.' }

$installedVpk = dotnet tool list -g | Select-String -Pattern '^vpk\s+(\S+)'
if (-not $installedVpk -or (Get-MajorMinor $installedVpk.Matches[0].Groups[1].Value) -ne (Get-MajorMinor $velopackVersion))
{
    if ($installedVpk) { Invoke-Checked 'Remove mismatching vpk' { dotnet tool uninstall -g vpk } }
    Invoke-Checked "Install vpk $velopackVersion" { dotnet tool install -g vpk --version $velopackVersion }
}

# --- Build ---

if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
New-Item -ItemType Directory -Force $releasesDir | Out-Null

# Files which aren't Velopack packages (and therefore aren't uploaded by vpk), such as the portable macOS builds.
$extraAssets = @()

function Publish-Desktop([string]$runtime, [string]$outputPath)
{
    Invoke-Checked "Publish osu.Desktop ($runtime)" {
        dotnet publish (Join-Path $root 'osu.Desktop/osu.Desktop.csproj') `
            --configuration Release `
            --runtime $runtime `
            --self-contained true `
            --output $outputPath `
            "-p:Version=$Version" `
            "-p:FileVersion=$Version" `
            "-p:OfficialRealmSchemaVersion=$OfficialSchemaVersion"
    }
}

# Zips a folder for unix systems. Archives created by Compress-Archive have no unix permissions,
# so the executable couldn't be started after extracting it.
function New-UnixZip([string]$sourceDir, [string]$zipPath, [string]$entryPrefix)
{
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

    $zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try
    {
        foreach ($file in Get-ChildItem -Recurse -File $sourceDir)
        {
            $relativePath = $file.FullName.Substring($sourceDir.Length).TrimStart('\', '/').Replace('\', '/')
            $entry = [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, "$entryPrefix/$relativePath")
            # -rwxr-xr-x, stored in the upper 16 bits of the external attributes.
            $entry.ExternalAttributes = 0x81ED -shl 16
        }
    }
    finally
    {
        $zip.Dispose()
    }
}

$token = $null
if (-not $SkipUpload) { $token = gh auth token }

foreach ($platform in $Platforms)
{
    switch ($platform)
    {
        'win'
        {
            $platformPublishDir = Join-Path $publishDir 'win-x64'
            Publish-Desktop 'win-x64' $platformPublishDir
        }

        'linux'
        {
            $platformPublishDir = Join-Path $publishDir 'linux-x64'
            Publish-Desktop 'linux-x64' $platformPublishDir
        }

        'osx'
        {
            foreach ($runtime in @('osx-arm64', 'osx-x64'))
            {
                $platformPublishDir = Join-Path $publishDir $runtime
                Publish-Desktop $runtime $platformPublishDir

                $zipPath = Join-Path $releasesDir "$packId-$runtime-portable.zip"
                Write-Host "==> Zip $runtime" -ForegroundColor Cyan
                New-UnixZip $platformPublishDir $zipPath $packId
                $extraAssets += $zipPath
            }
        }
    }

    # The macOS builds aren't Velopack packages.
    if ($platform -eq 'osx') { continue }

    # Previous releases are required to generate delta updates.
    if (-not $SkipUpload -and $existingTags.Count -gt 0)
    {
        Write-Host "==> Download previous release ($platform)" -ForegroundColor Cyan
        vpk download github --repoUrl $repoUrl --token $token --channel $platform --outputDir $releasesDir
        # The first release of a platform has no previous release, so this is not an error.
        if ($LASTEXITCODE -ne 0) { Write-Host "No previous release found for $platform, skipping delta updates." -ForegroundColor Yellow }
    }

    if ($platform -eq 'win')
    {
        Invoke-Checked 'Pack (win)' {
            vpk pack `
                --packId $packId `
                --packVersion $Version `
                --packDir $platformPublishDir `
                --mainExe 'osu!.exe' `
                --packTitle $packTitle `
                --packAuthors 'worte' `
                --icon (Join-Path $root 'osu.Desktop/lazer.ico') `
                --outputDir $releasesDir
        }
    }
    else
    {
        # The "[linux]" directive makes vpk cross-pack for Linux when run on Windows.
        Invoke-Checked 'Pack (linux)' {
            vpk '[linux]' pack `
                --packId $packId `
                --packVersion $Version `
                --packDir $platformPublishDir `
                --mainExe 'osu!' `
                --packTitle $packTitle `
                --packAuthors 'worte' `
                --icon (Join-Path $root 'assets/lazer.png') `
                --categories 'Game' `
                --outputDir $releasesDir
        }
    }
}

if ($SkipUpload)
{
    Write-Host "Built slop! $Version for $($Platforms -join ', '). The packages are in $releasesDir" -ForegroundColor Green
    return
}

# --- Upload ---

# Every Velopack channel is uploaded to the same GitHub release, the first one creates it.
$releaseCreated = $false

foreach ($channel in @($Platforms | Where-Object { $_ -ne 'osx' }))
{
    $uploadArgs = @(
        'upload', 'github',
        '--repoUrl', $repoUrl,
        '--token', $token,
        '--channel', $channel,
        '--outputDir', $releasesDir,
        '--tag', $Version,
        '--releaseName', "slop! $Version",
        '--targetCommitish', (git rev-parse HEAD),
        '--publish'
    )
    if ($releaseCreated) { $uploadArgs += '--merge' }

    Invoke-Checked "Upload release ($channel)" { vpk @uploadArgs }
    $releaseCreated = $true
}

if (-not $releaseCreated)
{
    Invoke-Checked 'Create release' {
        gh release create $Version --repo $repoName --title "slop! $Version" --target (git rev-parse HEAD) --notes ''
    }
}

if ($extraAssets.Count -gt 0)
{
    Invoke-Checked 'Upload additional files' { gh release upload $Version --repo $repoName --clobber @extraAssets }
}

# Release notes: the commits since the previous release.
$previousTag = $existingTags | Where-Object { $_ -ne $Version } | Select-Object -First 1
$notesFile = Join-Path $outputDir 'notes.md'

if ($previousTag)
{
    git fetch origin "refs/tags/${previousTag}:refs/tags/$previousTag" --quiet
    $commits = git log --no-merges --pretty=format:'- %s' "$previousTag..HEAD"
    Set-Content -Path $notesFile -Encoding utf8 -Value (@("Changes since $previousTag", '') + $commits)
}
else
{
    Set-Content -Path $notesFile -Encoding utf8 -Value 'First release of slop!. Run slop-win-Setup.exe to install on Windows, or slop.AppImage on Linux.'
}

Invoke-Checked 'Set release notes' { gh release edit $Version --repo $repoName --notes-file $notesFile }

Write-Host "Published slop! $Version to $repoUrl/releases/tag/$Version" -ForegroundColor Green
