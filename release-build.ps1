# Builds slop! and publishes it as a GitHub release of wuaht/osu,
# which the in-game updater (osu.Desktop/Updater/VelopackUpdateManager.cs) checks for updates.
#
# The first release's "slop-win-Setup.exe" is the installer. It installs to %LocalAppData%\slop,
# independently of an official osu!(lazer) installation.
#
# osu!fps (the osu-fps submodule) is released alongside it: its commit is tagged with the same version on wuaht/osu-fps,
# whose CI then publishes the ruleset DLL for official osu!lazer. This is skipped if the ruleset hasn't changed since its last release.
#
# Update channels: stable releases are normal GitHub releases, nightly builds (-Nightly) are published as prereleases
# with a "-nightly" version suffix. The in-game "Release stream" setting picks which of them the updater installs:
# "Stable" only sees stable releases, "Nightly" sees both (whichever is newer). Installing a nightly build switches the setting to "Nightly".
# osu!fps is only released alongside stable releases.
#
# Branches: new features are developed on "slop-nightly", which nightly builds are made from. "slop" only contains what has been
# released as stable. Running a stable release on "slop-nightly" brings everything on it over to "slop" (fast-forward, after the build
# succeeded) and releases it from there. Running it on "slop" releases "slop" as it is (e.g. for a hotfix, which then has to be
# merged into "slop-nightly" before the next stable release from it).
#
# Platforms (-Platforms, default: win):
#   win    Velopack installer + updates (channel "win").
#   osx    Portable, unsigned zips for Apple Silicon (osx-arm64) and Intel (osx-x64), without auto-updates.
#          Velopack can only pack (and sign / notarize) macOS packages on macOS.
# Linux builds aren't released anymore (building them lags the whole PC), the README explains how to build one yourself.
# Android and iOS are not supported: iOS can only be built on a Mac (Xcode + Apple signing), and Android
# requires the .NET Android workload, Android SDK / JDK and a signing keystore.
#
# Usage:
#   ./release-build.ps1                    # build and publish the next version (e.g. 2026.1008.0), on slop-nightly: bring it over to slop
#   ./release-build.ps1 -Nightly           # build and publish the next nightly build (e.g. 2026.1008.1-nightly)
#   ./release-build.ps1 -SkipUpload        # only build the installer locally (in .slop-release/), without releasing osu!fps
#   ./release-build.ps1 -Platforms win,osx
#   ./release-build.ps1 -Version 2026.1008.1
#   ./release-build.ps1 -Force             # skip the checks for uncommitted / unpushed changes
#   ./release-build.ps1 -OfficialSchemaVersion 52   # don't look up the realm schema version of the latest official release
#
# Requirements: .NET SDK, GitHub CLI (gh, logged in), the osu-framework / osu-resources checkouts next to this repository,
# the osu-fps submodule, and push access to wuaht/osu-fps.

param(
    [string]$Version,
    [string[]]$Platforms = @('win'),
    [switch]$SkipUpload,
    [switch]$Force,
    [switch]$Nightly,
    [int]$OfficialSchemaVersion
)

$ErrorActionPreference = 'Stop'

$repoUrl = 'https://github.com/wuaht/osu'
$nightlySuffix = '-nightly'
$repoName = 'wuaht/osu'
$fpsRepoName = 'wuaht/osu-fps'
$fpsBranch = 'master'
$packId = 'slop'
$packTitle = 'slop!'
$stableBranch = 'slop'
$nightlyBranch = 'slop-nightly'

$root = $PSScriptRoot
$fpsDir = Join-Path $root 'osu-fps'
$outputDir = Join-Path $root '.slop-release'
$publishDir = Join-Path $outputDir 'publish'
$releasesDir = Join-Path $outputDir 'releases'

# Commands run at below normal priority, so that building and packing (which use every core) don't make the whole PC lag.
# Windows passes a below normal priority class on to child processes, so this covers everything the command starts.
# It's restored afterwards, so that the shell which ran this script isn't left at a lower priority.
function Invoke-Checked([string]$description, [scriptblock]$command)
{
    Write-Host "==> $description" -ForegroundColor Cyan

    $process = [System.Diagnostics.Process]::GetCurrentProcess()
    $originalPriority = $process.PriorityClass
    $process.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::BelowNormal

    try
    {
        & $command
    }
    finally
    {
        $process.PriorityClass = $originalPriority
    }

    if ($LASTEXITCODE -ne 0) { throw "$description failed (exit code $LASTEXITCODE)." }
}

# Changes to IDE files don't affect the build.
function Get-RelevantChanges([string]$path)
{
    git -C $path status --porcelain | Where-Object { $_ -notmatch '\.idea/' }
}

function Assert-Pushed([string]$path, [string]$name, [string]$remoteBranch = $branch)
{
    if (Get-RelevantChanges $path)
    {
        throw "$name has uncommitted changes. Commit them first, or use -Force."
    }

    git -C $path fetch origin --quiet
    $unpushed = git -C $path log --oneline "origin/$remoteBranch..HEAD"
    if ($unpushed)
    {
        throw "$name has commits which are not pushed to origin/$remoteBranch. Push them first, or use -Force."
    }
}

Set-Location $root

# --- Checks ---

# "-Platforms win,osx" arrives as a single string when the script is run with "powershell -File".
$Platforms = @($Platforms | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ } | Select-Object -Unique)
foreach ($platform in $Platforms)
{
    if ($platform -eq 'linux') { throw "Linux builds aren't released anymore, see the README on how to build one yourself." }
    if ($platform -notin @('win', 'osx')) { throw "Unknown platform '$platform'. Supported platforms: win, osx." }
}
if ($Platforms.Count -eq 0) { throw 'No platforms given.' }

# The branch which is released, and which therefore must be pushed.
$branch = git rev-parse --abbrev-ref HEAD
$promote = $false

if ($Nightly)
{
    if ($branch -ne $nightlyBranch) { throw "Nightly builds must be made from the '$nightlyBranch' branch." }
}
elseif ($branch -eq $nightlyBranch)
{
    # A stable release from slop-nightly brings it over to slop. slop must not have anything which slop-nightly doesn't,
    # otherwise it couldn't be fast-forwarded (and that would be left out of the release).
    git merge-base --is-ancestor $stableBranch HEAD
    if ($LASTEXITCODE -ne 0) { throw "'$stableBranch' has commits which are not on '$nightlyBranch'. Merge '$stableBranch' into '$nightlyBranch' first." }

    # A local build doesn't change any branches.
    $promote = -not $SkipUpload
}
elseif ($branch -ne $stableBranch)
{
    throw "Stable releases must be made from the '$nightlyBranch' branch (which brings it over to '$stableBranch'), or from '$stableBranch'."
}

foreach ($dependency in @('osu-framework', 'osu-resources'))
{
    if (-not (Test-Path (Join-Path $root "../$dependency"))) { throw "../$dependency is missing (see README)." }
}

if (-not (Test-Path (Join-Path $fpsDir 'osu.Game.Rulesets.Fposu'))) { throw "The osu-fps submodule is not checked out. Run 'git submodule update --init' first." }

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
        # osu!fps is released by tagging the submodule's commit, so it must be on GitHub as well.
        Assert-Pushed $fpsDir 'osu-fps' $fpsBranch
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
        # Stable releases and nightly builds share the counter, so that their versions never collide.
        if ($lastVersion -match "^$([regex]::Escape($prefix))\.(\d+)(-.*)?$") { $n = [int]$Matches[1] + 1 }
    }

    $Version = "$prefix.$n"
    if ($Nightly) { $Version += $nightlySuffix }
}

if ($Nightly -and -not $Version.EndsWith($nightlySuffix)) { throw "Nightly versions must end with '$nightlySuffix'." }
if (-not $Nightly -and $Version.Contains('-')) { throw "Stable versions can't have a suffix. Use -Nightly for nightly builds." }

# The file version can't have a suffix.
$fileVersion = ($Version -split '-')[0]
$releaseKind = if ($Nightly) { 'nightly build' } else { 'release' }

if ($existingTags -contains $Version) { throw "A release for version $Version already exists." }

New-Item -ItemType Directory -Force $outputDir | Out-Null
Set-Content -Path $lastVersionFile -Encoding utf8 -Value $Version

Write-Host "Releasing slop! $Version ($releaseKind)" -ForegroundColor Green

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
    # -lowPriority makes MSBuild use its own low priority nodes instead of reusing normal priority ones (e.g. from the IDE).
    Invoke-Checked "Publish osu.Desktop ($runtime)" {
        dotnet publish (Join-Path $root 'osu.Desktop/osu.Desktop.csproj') `
            -lowPriority `
            --configuration Release `
            --runtime $runtime `
            --self-contained true `
            --output $outputPath `
            "-p:Version=$Version" `
            "-p:FileVersion=$fileVersion" `
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
        # Deltas are generated against the previous build of the same update channel, as that's what its users update from.
        $downloadArgs = @('download', 'github', '--repoUrl', $repoUrl, '--token', $token, '--channel', $platform, '--outputDir', $releasesDir)
        if ($Nightly) { $downloadArgs += '--pre' }
        vpk @downloadArgs
        # The first release of a platform has no previous release, so this is not an error.
        if ($LASTEXITCODE -ne 0) { Write-Host "No previous release found for $platform, skipping delta updates." -ForegroundColor Yellow }
    }

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

if ($SkipUpload)
{
    Write-Host "Built slop! $Version for $($Platforms -join ', '). The packages are in $releasesDir" -ForegroundColor Green
    return
}

# --- Bring slop-nightly over to slop ---

# Only done once the build succeeded, so that slop never contains anything which failed to build.
if ($promote)
{
    Invoke-Checked "Fast-forward $stableBranch to $nightlyBranch" { git push . "HEAD:refs/heads/$stableBranch" }
    Invoke-Checked "Push $stableBranch" { git push origin "refs/heads/${stableBranch}:refs/heads/$stableBranch" }
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
    if ($Nightly) { $uploadArgs += '--pre' }

    Invoke-Checked "Upload release ($channel)" { vpk @uploadArgs }
    $releaseCreated = $true
}

if (-not $releaseCreated)
{
    Invoke-Checked 'Create release' {
        $createArgs = @('release', 'create', $Version, '--repo', $repoName, '--title', "slop! $Version", '--target', (git rev-parse HEAD), '--notes', '')
        if ($Nightly) { $createArgs += '--prerelease' }
        gh @createArgs
    }
}

if ($extraAssets.Count -gt 0)
{
    Invoke-Checked 'Upload additional files' { gh release upload $Version --repo $repoName --clobber @extraAssets }
}

# Release notes: the commits since the previous build of the same update channel (nightly builds also follow stable releases).
$previousTag = $existingTags | Where-Object { $_ -ne $Version -and ($Nightly -or -not $_.Contains('-')) } | Select-Object -First 1
$notesFile = Join-Path $outputDir 'notes.md'

if ($previousTag)
{
    git fetch origin "refs/tags/${previousTag}:refs/tags/$previousTag" --quiet
    $commits = git log --no-merges --pretty=format:'- %s' "$previousTag..HEAD"
    Set-Content -Path $notesFile -Encoding utf8 -Value (@("Changes since $previousTag", '') + $commits)
}
else
{
    Set-Content -Path $notesFile -Encoding utf8 -Value 'First release of slop!. Run slop-win-Setup.exe to install on Windows.'
}

Invoke-Checked 'Set release notes' { gh release edit $Version --repo $repoName --notes-file $notesFile }

Write-Host "Published slop! $Version ($releaseKind) to $repoUrl/releases/tag/$Version" -ForegroundColor Green

if ($Nightly)
{
    Write-Host 'Skipping the osu!fps release, as it is only released alongside stable releases.' -ForegroundColor Yellow
    return
}

# --- osu!fps ---

# osu!fps is also released as a standalone ruleset for official osu!lazer. Pushing a tag to its repository makes its CI
# (osu-fps/.github/workflows/build.yml) build the DLL against the official packages and publish it as a GitHub release.
# It can't be built here, because inside this repository it's built against the client's source instead.
Invoke-Checked 'Fetch osu-fps tags' { git -C $fpsDir fetch origin --tags --quiet }

$fpsCommit = git -C $fpsDir rev-parse HEAD
# Only changes to what goes into the DLL count, not e.g. tests, CI or the readme.
$fpsLastRelease = git -C $fpsDir tag --merged $fpsCommit --sort=-v:refname | Select-Object -First 1
$fpsChanges = $null
if ($fpsLastRelease) { $fpsChanges = git -C $fpsDir diff --name-only $fpsLastRelease $fpsCommit -- osu.Game.Rulesets.Fposu Directory.Build.props }

if ($fpsLastRelease -and -not $fpsChanges)
{
    Write-Host "osu!fps hasn't changed since its release $fpsLastRelease, skipping its release." -ForegroundColor Yellow
}
elseif (git -C $fpsDir tag --list $Version)
{
    Write-Host "osu-fps already has a tag $Version on another commit, skipping its release." -ForegroundColor Yellow
}
else
{
    Invoke-Checked "Tag osu-fps $Version" { git -C $fpsDir tag $Version $fpsCommit }
    Invoke-Checked "Push osu-fps tag $Version" { git -C $fpsDir push origin "refs/tags/$Version" }
    Write-Host "Tagged osu!fps $Version, its release will be published to https://github.com/$fpsRepoName/releases/tag/$Version by GitHub Actions." -ForegroundColor Green
}
