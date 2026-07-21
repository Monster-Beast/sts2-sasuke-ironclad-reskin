[CmdletBinding()]
param(
    [ValidateSet("enable", "disable", "status")]
    [string]$Action = "status",

    [string]$GamePath = "",
    [string]$ModDirectory = "",

    [switch]$TitlesOnly,
    [switch]$AnimationsOnly,
    [switch]$NoLowFlash,
    [switch]$FastMode,

    [ValidateRange(0.25, 3.0)]
    [double]$AnchorScale = 1.0,

    [ValidateRange(-1000.0, 1000.0)]
    [double]$AnchorOffsetX = 0.0,

    [ValidateRange(-1000.0, 1000.0)]
    [double]$AnchorOffsetY = 0.0
)

$ErrorActionPreference = "Stop"
$AppId = "2868840"
$RequiredBranch = "public-beta"
$RequiredBuildId = "24251656"
$MarkerFileName = "SasukeIronclad.canary.json"
$StatusFileName = "runtime-canary-status.json"
$AnchorStatusFileName = "runtime-canary-anchor-status.json"
$ObservationMarkerFileName = "SasukeIronclad.observe.json"

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

function Resolve-ModDirectory {
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

if ($TitlesOnly -and $AnimationsOnly) {
    throw "-TitlesOnly and -AnimationsOnly cannot be used together."
}

$resolvedGamePath = Resolve-Sts2GamePath -ExplicitPath $GamePath
$resolvedModDirectory = Resolve-ModDirectory `
    -ResolvedGamePath $resolvedGamePath `
    -ExplicitModDirectory $ModDirectory
$markerPath = Join-Path $resolvedModDirectory $MarkerFileName
$statusPath = Join-Path $resolvedModDirectory $StatusFileName
$anchorStatusPath = Join-Path $resolvedModDirectory $AnchorStatusFileName
$observationMarkerPath = Join-Path $resolvedModDirectory $ObservationMarkerFileName

Write-Host "Game path: $resolvedGamePath" -ForegroundColor Cyan
Write-Host "Mod path: $resolvedModDirectory" -ForegroundColor Cyan

switch ($Action) {
    "enable" {
        if (Test-Path -LiteralPath $observationMarkerPath -PathType Leaf) {
            throw "Runtime observation is still enabled. Disable it before enabling the presentation canary."
        }
        $marker = [ordered]@{
            schema_version = 1
            enabled = $true
            mode = "local_visual_only"
            expected_branch = $RequiredBranch
            expected_build_id = $RequiredBuildId
            enable_animations = (-not $TitlesOnly.IsPresent)
            enable_titles = (-not $AnimationsOnly.IsPresent)
            low_flash = (-not $NoLowFlash.IsPresent)
            fast_mode = $FastMode.IsPresent
            anchor_to_local_player = $true
            anchor_scale = $AnchorScale
            anchor_offset_x = $AnchorOffsetX
            anchor_offset_y = $AnchorOffsetY
        }
        $json = ($marker | ConvertTo-Json -Depth 4) + [Environment]::NewLine
        Write-Utf8NoBom -Path $markerPath -Content $json
        foreach ($oldStatus in @($statusPath, $anchorStatusPath)) {
            if (Test-Path -LiteralPath $oldStatus -PathType Leaf) {
                Remove-Item -LiteralPath $oldStatus -Force
            }
        }
        Write-Host "Runtime presentation canary enabled for the next game launch." -ForegroundColor Green
        Write-Host "Marker: $markerPath"
        Write-Host "Expected build: $RequiredBranch / $RequiredBuildId"
        Write-Host "Animations: $(-not $TitlesOnly.IsPresent)"
        Write-Host "Titles: $(-not $AnimationsOnly.IsPresent)"
        Write-Host "Low flash: $(-not $NoLowFlash.IsPresent)"
        Write-Host "Fast mode: $($FastMode.IsPresent)"
        Write-Host "Anchor to local player: True"
        Write-Host "Anchor scale: $AnchorScale"
        Write-Host "Anchor offset: ($AnchorOffsetX, $AnchorOffsetY)"
        Write-Host "Previous canary startup and anchor status were cleared."
        Write-Host "The overlay remains hidden until a unique local-player combat anchor is found."
        Write-Host "This canary leaves the original Ironclad visual visible and unchanged."
    }
    "disable" {
        if (Test-Path -LiteralPath $markerPath -PathType Leaf) {
            Remove-Item -LiteralPath $markerPath -Force
            Write-Host "Runtime presentation canary marker removed." -ForegroundColor Green
        }
        else {
            Write-Host "Runtime presentation canary was already disabled." -ForegroundColor Yellow
        }
        Write-Host "Existing startup status was preserved at: $statusPath"
        Write-Host "Existing anchor status was preserved at: $anchorStatusPath"
    }
    "status" {
        if (Test-Path -LiteralPath $markerPath -PathType Leaf) {
            Write-Host "Runtime presentation canary marker: ENABLED" -ForegroundColor Yellow
            Get-Content -LiteralPath $markerPath -Raw
        }
        else {
            Write-Host "Runtime presentation canary marker: DISABLED" -ForegroundColor Green
        }

        Write-Host "Last game-start canary status: $statusPath"
        if (Test-Path -LiteralPath $statusPath -PathType Leaf) {
            Get-Content -LiteralPath $statusPath -Raw
        }
        else {
            Write-Host "No canary startup status has been written. The Mod initializer has not run since the marker changed, or the DLL was not loaded." -ForegroundColor Yellow
        }

        Write-Host "Last local-player anchor status: $anchorStatusPath"
        if (Test-Path -LiteralPath $anchorStatusPath -PathType Leaf) {
            Get-Content -LiteralPath $anchorStatusPath -Raw
        }
        else {
            Write-Host "No anchor status has been written. Enable animations and launch the game, then play a reviewed local Ironclad card." -ForegroundColor Yellow
        }
    }
}