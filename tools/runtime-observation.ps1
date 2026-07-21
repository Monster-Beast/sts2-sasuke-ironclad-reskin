[CmdletBinding()]
param(
    [ValidateSet("enable", "disable", "status")]
    [string]$Action = "status",

    [string]$GamePath = "",
    [string]$ModDirectory = "",

    [ValidatePattern("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    [string]$SessionLabel = "observed-run-1",

    [ValidateRange(100, 50000)]
    [int]$MaxEvents = 20000,

    [switch]$NoStacks
)

$ErrorActionPreference = "Stop"
$AppId = "2868840"
$RequiredBranch = "public-beta"
$RequiredBuildId = "24251656"
$MarkerFileName = "SasukeIronclad.observe.json"
$OutputDirectoryName = "observation-output"

function Resolve-Sts2GamePath {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath -PathType Container)) {
            throw "GamePath does not exist or is not a directory: $ExplicitPath"
        }
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $uninstallKeys = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App $AppId",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App $AppId"
    )
    foreach ($key in $uninstallKeys) {
        if (-not (Test-Path $key)) {
            continue
        }
        $candidate = (Get-ItemProperty $key -ErrorAction SilentlyContinue).InstallLocation
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Container)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    $steamPath = (Get-ItemProperty "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue).SteamPath
    if ($steamPath) {
        $candidate = Join-Path $steamPath "steamapps\common\Slay the Spire 2"
        if (Test-Path -LiteralPath $candidate -PathType Container) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "Slay the Spire 2 was not found. Supply -GamePath."
}

function Resolve-ObservationModDirectory {
    param(
        [string]$ResolvedGamePath,
        [string]$ExplicitModDirectory
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitModDirectory)) {
        if (-not (Test-Path -LiteralPath $ExplicitModDirectory -PathType Container)) {
            throw "ModDirectory does not exist or is not a directory: $ExplicitModDirectory"
        }
        return (Resolve-Path -LiteralPath $ExplicitModDirectory).Path
    }

    $candidate = Join-Path $ResolvedGamePath "mods\SasukeIronclad"
    if (Test-Path -LiteralPath $candidate -PathType Container) {
        return (Resolve-Path -LiteralPath $candidate).Path
    }

    throw "The installed SasukeIronclad mod directory was not found at $candidate. Build/copy the mod first or supply -ModDirectory."
}

function Write-Utf8NoBom {
    param(
        [string]$Path,
        [string]$Content
    )

    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        (New-Object System.Text.UTF8Encoding($false))
    )
}

$resolvedGamePath = Resolve-Sts2GamePath -ExplicitPath $GamePath
$resolvedModDirectory = Resolve-ObservationModDirectory `
    -ResolvedGamePath $resolvedGamePath `
    -ExplicitModDirectory $ModDirectory
$markerPath = Join-Path $resolvedModDirectory $MarkerFileName
$outputDirectory = Join-Path $resolvedModDirectory $OutputDirectoryName

Write-Host "Game path: $resolvedGamePath" -ForegroundColor Cyan
Write-Host "Mod path: $resolvedModDirectory" -ForegroundColor Cyan

switch ($Action) {
    "enable" {
        $marker = [ordered]@{
            schema_version = 1
            enabled = $true
            mode = "read_only"
            expected_branch = $RequiredBranch
            expected_build_id = $RequiredBuildId
            session_label = $SessionLabel
            max_events = $MaxEvents
            capture_stacks = (-not $NoStacks.IsPresent)
        }
        $json = ($marker | ConvertTo-Json -Depth 4) + [Environment]::NewLine
        Write-Utf8NoBom -Path $markerPath -Content $json
        Write-Host "Runtime observation enabled for the next game launch." -ForegroundColor Green
        Write-Host "Marker: $markerPath"
        Write-Host "Expected build: $RequiredBranch / $RequiredBuildId"
        Write-Host "Session label: $SessionLabel"
        Write-Host "No gameplay values, method arguments or return values are modified."
    }
    "disable" {
        if (Test-Path -LiteralPath $markerPath -PathType Leaf) {
            Remove-Item -LiteralPath $markerPath -Force
            Write-Host "Runtime observation marker removed." -ForegroundColor Green
        }
        else {
            Write-Host "Runtime observation was already disabled." -ForegroundColor Yellow
        }
        Write-Host "Existing JSONL evidence was preserved at: $outputDirectory"
    }
    "status" {
        if (Test-Path -LiteralPath $markerPath -PathType Leaf) {
            Write-Host "Runtime observation marker: ENABLED" -ForegroundColor Yellow
            Get-Content -LiteralPath $markerPath -Raw
        }
        else {
            Write-Host "Runtime observation marker: DISABLED" -ForegroundColor Green
        }

        Write-Host "Evidence directory: $outputDirectory"
        if (Test-Path -LiteralPath $outputDirectory -PathType Container) {
            Get-ChildItem -LiteralPath $outputDirectory -Filter "runtime-observation-*.jsonl" -File |
                Sort-Object LastWriteTime -Descending |
                Select-Object Name, Length, LastWriteTime
        }
        else {
            Write-Host "No runtime observation evidence has been written yet."
        }
    }
}
