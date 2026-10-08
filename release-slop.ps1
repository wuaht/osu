# Builds slop! for Windows and publishes it as a GitHub release of wuaht/osu,
# which the in-game updater (osu.Desktop/Updater/VelopackUpdateManager.cs) checks for updates.
#
# The first release's "slop-win-Setup.exe" is the installer. It installs to %LocalAppData%\slop,
# independently of an official osu!(lazer) installation.
#
# Usage:
#   ./release-slop.ps1                    # build and publish the next version (e.g. 2026.1008.0)
#   ./release-slop.ps1 -SkipUpload        # only build the installer locally (in .slop-release/)
#   ./release-slop.ps1 -Version 2026.1008.1
#   ./release-slop.ps1 -Force             # skip the checks for uncommitted / unpushed changes
#
# Requirements: .NET SDK, GitHub CLI (gh, logged in), and the osu-framework / osu-resources checkouts next to this repository.

param(
    [string]$Version,
    [switch]$SkipUpload,
    [switch]$Force
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

if (-not $Version)
{
    # Same scheme as official releases: year.monthday.n
    $now = Get-Date
    $prefix = '{0}.{1}{2:00}' -f $now.Year, $now.Month, $now.Day
    $n = 0
    while ($existingTags -contains "$prefix.$n") { $n++ }
    $Version = "$prefix.$n"
}

if ($existingTags -contains $Version) { throw "A release for version $Version already exists." }

Write-Host "Releasing slop! $Version" -ForegroundColor Green

# --- Velopack CLI ---

# The CLI should match the version of the Velopack library used by the game.
$velopackVersion = ([xml](Get-Content (Join-Path $root 'osu.Desktop/osu.Desktop.csproj'))).Project.ItemGroup.PackageReference |
    Where-Object { $_.Include -eq 'Velopack' } |
    Select-Object -ExpandProperty Version -First 1

$installedVpk = dotnet tool list -g | Select-String -Pattern '^vpk\s+(\S+)'
if (-not $installedVpk -or $installedVpk.Matches[0].Groups[1].Value -ne $velopackVersion)
{
    if ($installedVpk) { Invoke-Checked 'Remove mismatching vpk' { dotnet tool uninstall -g vpk } }
    Invoke-Checked "Install vpk $velopackVersion" { dotnet tool install -g vpk --version $velopackVersion }
}

# --- Build ---

if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
New-Item -ItemType Directory -Force $releasesDir | Out-Null

Invoke-Checked 'Publish osu.Desktop' {
    dotnet publish (Join-Path $root 'osu.Desktop/osu.Desktop.csproj') `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $publishDir `
        "-p:Version=$Version" `
        "-p:FileVersion=$Version"
}

$token = $null
if (-not $SkipUpload)
{
    $token = gh auth token

    # Previous releases are required to generate delta updates.
    if ($existingTags.Count -gt 0)
    {
        Invoke-Checked 'Download previous release' {
            vpk download github --repoUrl $repoUrl --token $token --outputDir $releasesDir
        }
    }
}

Invoke-Checked 'Pack' {
    vpk pack `
        --packId $packId `
        --packVersion $Version `
        --packDir $publishDir `
        --mainExe 'osu!.exe' `
        --packTitle $packTitle `
        --packAuthors 'worte' `
        --icon (Join-Path $root 'osu.Desktop/lazer.ico') `
        --outputDir $releasesDir
}

if ($SkipUpload)
{
    Write-Host "Built slop! $Version. The installer is in $releasesDir" -ForegroundColor Green
    return
}

# --- Upload ---

Invoke-Checked 'Upload release' {
    vpk upload github `
        --repoUrl $repoUrl `
        --token $token `
        --outputDir $releasesDir `
        --tag $Version `
        --releaseName "slop! $Version" `
        --targetCommitish (git rev-parse HEAD) `
        --publish
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
    Set-Content -Path $notesFile -Encoding utf8 -Value 'First release of slop!. Run slop-win-Setup.exe to install.'
}

Invoke-Checked 'Set release notes' { gh release edit $Version --repo $repoName --notes-file $notesFile }

Write-Host "Published slop! $Version to $repoUrl/releases/tag/$Version" -ForegroundColor Green
