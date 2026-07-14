# Publishes v1.0.1-v1.0.3 to https://github.com/bslie/Multicrew-Nuclear-Option/releases
# Requires: gh auth login (or GH_TOKEN with repo scope)

$ErrorActionPreference = "Stop"
$Repo = "bslie/Multicrew-Nuclear-Option"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

gh auth status | Out-Null

$releases = @(
    @{
        Tag    = "v1.0.1"
        Title  = "SimpleWSO v1.0.1"
        Asset  = "releases/SimpleWSO-v1.0.1.zip"
        Notes  = "releases/v1.0.1-notes.md"
        Latest = $false
    },
    @{
        Tag    = "v1.0.2"
        Title  = "Multicrew Nuclear Option v1.0.2"
        Asset  = "releases/MulticrewNuclearOption-v1.0.2.zip"
        Notes  = "releases/v1.0.2-notes.md"
        Latest = $false
    },
    @{
        Tag    = "v1.0.3"
        Title  = "Multicrew Nuclear Option v1.0.3"
        Asset  = "releases/MulticrewNuclearOption-v1.0.3.zip"
        Notes  = "releases/v1.0.3-notes.md"
        Latest = $true
    }
)

foreach ($r in $releases) {
    if (-not (Test-Path $r.Asset)) { throw "Missing asset: $($r.Asset)" }
    if (-not (Test-Path $r.Notes)) { throw "Missing notes: $($r.Notes)" }

    $exists = $false
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    gh release view $r.Tag --repo $Repo 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) { $exists = $true }
    $ErrorActionPreference = $prevEap

    if ($exists) {
        Write-Host "Release $($r.Tag) already exists - uploading asset if needed..."
        gh release upload $r.Tag $r.Asset --repo $Repo --clobber
        continue
    }

    $ghArgs = @(
        "release", "create", $r.Tag, $r.Asset,
        "--repo", $Repo,
        "--title", $r.Title,
        "--notes-file", $r.Notes
    )
    if ($r.Latest) { $ghArgs += "--latest" }

    & gh @ghArgs
    if ($LASTEXITCODE -ne 0) { throw "Failed to create release $($r.Tag)" }
    Write-Host "Created $($r.Tag)"
}

Write-Host "Done: https://github.com/$Repo/releases"
