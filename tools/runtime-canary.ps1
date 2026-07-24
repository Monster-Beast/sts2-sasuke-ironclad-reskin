[CmdletBinding()]
param(
    [ValidateSet("enable", "disable", "status")]
    [string]$Action = "status",

    [string]$GamePath = "",
    [string]$ModDirectory = "",

    [switch]$Combined,
    [switch]$TitlesOnly,
    [switch]$AnimationsOnly,
    [switch]$NoLowFlash,
    [switch]$FastMode,
    [switch]$ReplaceOriginal,

    [ValidateSet("none", "missing_timeline", "forced_playback_failure", "anchor_invalidation")]
    [string]$FailureScenario = "none",

    [ValidateSet("Strike", "Defend", "Bash", "Anger", "Thunderclap", "Flame Barrier", "Whirlwind", "Burning Pact", "Fiend Fire")]
    [string]$FailureCardId = "Strike",

    [ValidateSet("none", "method_signature_mismatch")]
    [string]$StartupFailureScenario = "none",

    [ValidatePattern("^[A-Za-z0-9][A-Za-z0-9._-]{0,47}$")]
    [string]$SessionLabel = "presentation-canary",

    [ValidateRange(0.25, 3.0)]
    [double]$AnchorScale = 1.2,

    [ValidateRange(-1000.0, 1000.0)]
    [double]$AnchorOffsetX = 0.0,

    [ValidateRange(-1000.0, 1000.0)]
    [double]$AnchorOffsetY = -150.0
)

$ErrorActionPreference = "Stop"
$AppId = "2868840"
$RequiredBranch = "public-beta"
$RequiredBuildId = "24251656"
$ReplacementAcknowledgement = "public-beta-24251656-local-ironclad-replacement"
$FailureAcknowledgement = "public-beta-24251656-local-visual-failure-injection"
$StartupFailureAcknowledgement = "public-beta-24251656-local-startup-method-signature-mismatch"
$StartupFailureBindingId = "card_visual_request"
$MarkerFileName = "SasukeIronclad.canary.json"
$StatusFileName = "runtime-canary-status.json"
$AnchorStatusFileName = "runtime-canary-anchor-status.json"
$ReplacementStatusFileName = "runtime-canary-replacement-status.json"
$FailureStatusFileName = "runtime-canary-failure-status.json"
$EventDirectoryName = "canary-output"
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

if (($Combined -and $TitlesOnly) -or
    ($Combined -and $AnimationsOnly) -or
    ($TitlesOnly -and $AnimationsOnly)) {
    throw "-Combined, -TitlesOnly and -AnimationsOnly are mutually exclusive."
}
if ($TitlesOnly -and $ReplaceOriginal) {
    throw "-ReplaceOriginal requires the animation layer and cannot be combined with -TitlesOnly."
}

$enableAnimations = (-not $TitlesOnly.IsPresent)
$enableTitles = (-not $AnimationsOnly.IsPresent)
$presentationMode = if ($enableAnimations -and $enableTitles) {
    "combined"
}
elseif ($enableAnimations) {
    "animations_only"
}
else {
    "titles_only"
}
$failureRequested = -not [string]::Equals($FailureScenario, "none", [System.StringComparison]::Ordinal)
$startupFailureRequested = -not [string]::Equals($StartupFailureScenario, "none", [System.StringComparison]::Ordinal)
if ($failureRequested -and -not $enableAnimations) {
    throw "-FailureScenario requires the animation layer."
}
if ($failureRequested -and -not $ReplaceOriginal.IsPresent) {
    throw "-FailureScenario requires -ReplaceOriginal so original-visual recovery can be verified."
}
if ($startupFailureRequested -and -not $AnimationsOnly.IsPresent) {
    throw "-StartupFailureScenario requires -AnimationsOnly."
}
if ($startupFailureRequested -and $ReplaceOriginal.IsPresent) {
    throw "-StartupFailureScenario cannot be combined with -ReplaceOriginal."
}
if ($startupFailureRequested -and $failureRequested) {
    throw "-StartupFailureScenario cannot be combined with -FailureScenario."
}
$markerMode = if ($startupFailureRequested) {
    "local_startup_failure_only"
}
else {
    "local_visual_only"
}

$resolvedGamePath = Resolve-Sts2GamePath -ExplicitPath $GamePath
$resolvedModDirectory = Resolve-ModDirectory `
    -ResolvedGamePath $resolvedGamePath `
    -ExplicitModDirectory $ModDirectory
$markerPath = Join-Path $resolvedModDirectory $MarkerFileName
$statusPath = Join-Path $resolvedModDirectory $StatusFileName
$anchorStatusPath = Join-Path $resolvedModDirectory $AnchorStatusFileName
$replacementStatusPath = Join-Path $resolvedModDirectory $ReplacementStatusFileName
$failureStatusPath = Join-Path $resolvedModDirectory $FailureStatusFileName
$eventDirectoryPath = Join-Path $resolvedModDirectory $EventDirectoryName
$observationMarkerPath = Join-Path $resolvedModDirectory $ObservationMarkerFileName

Write-Host "Game path: $resolvedGamePath" -ForegroundColor Cyan
Write-Host "Mod path: $resolvedModDirectory" -ForegroundColor Cyan

switch ($Action) {
    "enable" {
        if (Test-Path -LiteralPath $observationMarkerPath -PathType Leaf) {
            throw "Runtime observation is still enabled. Disable it before enabling the presentation canary."
        }
        $replacementAck = ""
        if ($ReplaceOriginal.IsPresent) {
            $replacementAck = $ReplacementAcknowledgement
        }
        $failureAck = ""
        $failureTarget = ""
        if ($failureRequested) {
            $failureAck = $FailureAcknowledgement
            $failureTarget = $FailureCardId
        }
        $startupFailureAck = ""
        $startupFailureBinding = ""
        if ($startupFailureRequested) {
            $startupFailureAck = $StartupFailureAcknowledgement
            $startupFailureBinding = $StartupFailureBindingId
        }
        $marker = [ordered]@{
            schema_version = 1
            enabled = $true
            mode = $markerMode
            expected_branch = $RequiredBranch
            expected_build_id = $RequiredBuildId
            session_label = $SessionLabel
            enable_animations = $enableAnimations
            enable_titles = $enableTitles
            low_flash = (-not $NoLowFlash.IsPresent)
            fast_mode = $FastMode.IsPresent
            anchor_to_local_player = $true
            anchor_scale = $AnchorScale
            anchor_offset_x = $AnchorOffsetX
            anchor_offset_y = $AnchorOffsetY
            hide_original_visual = $ReplaceOriginal.IsPresent
            replacement_acknowledgement = $replacementAck
            failure_injection_scenario = $FailureScenario
            failure_injection_card_id = $failureTarget
            failure_injection_once = $true
            failure_injection_acknowledgement = $failureAck
            startup_failure_injection_scenario = $StartupFailureScenario
            startup_failure_injection_binding_id = $startupFailureBinding
            startup_failure_injection_once = $true
            startup_failure_injection_acknowledgement = $startupFailureAck
        }
        $json = ($marker | ConvertTo-Json -Depth 4) + [Environment]::NewLine
        Write-Utf8NoBom -Path $markerPath -Content $json
        foreach ($oldStatus in @($statusPath, $anchorStatusPath, $replacementStatusPath, $failureStatusPath)) {
            if (Test-Path -LiteralPath $oldStatus -PathType Leaf) {
                Remove-Item -LiteralPath $oldStatus -Force
            }
        }
        Write-Host "Runtime presentation canary enabled for the next game launch." -ForegroundColor Green
        Write-Host "Marker: $markerPath"
        Write-Host "Expected build: $RequiredBranch / $RequiredBuildId"
        Write-Host "Session label: $SessionLabel"
        Write-Host "Presentation mode: $presentationMode"
        Write-Host "Animations: $enableAnimations"
        Write-Host "Titles: $enableTitles"
        Write-Host "Low flash: $(-not $NoLowFlash.IsPresent)"
        Write-Host "Fast mode: $($FastMode.IsPresent)"
        Write-Host "Anchor to local player: True"
        Write-Host "Anchor scale: $AnchorScale"
        Write-Host "Anchor offset: ($AnchorOffsetX, $AnchorOffsetY)"
        Write-Host "Replace original Ironclad visual: $($ReplaceOriginal.IsPresent)"
        Write-Host "Failure injection scenario: $FailureScenario"
        Write-Host "Failure injection target card: $(if ($failureRequested) { $FailureCardId } else { 'none' })"
        Write-Host "Startup failure injection scenario: $StartupFailureScenario"
        Write-Host "Startup failure injection binding: $(if ($startupFailureRequested) { $StartupFailureBindingId } else { 'none' })"
        Write-Host "Previous canary startup, anchor, replacement and failure status were cleared."
        Write-Host "Existing JSONL journals were preserved at: $eventDirectoryPath"
        if ($enableAnimations -and -not $startupFailureRequested) {
            Write-Host "The overlay remains hidden until a unique local-player combat anchor is found."
        }
        if ($ReplaceOriginal.IsPresent -and -not $startupFailureRequested) {
            Write-Host "The exact local Ironclad NCreatureVisuals node will be hidden only after a reviewed Sasuke timeline starts." -ForegroundColor Yellow
            Write-Host "Playback fallback, anchor loss, combat end and Mod disposal request restoration of the captured original visibility." -ForegroundColor Yellow
        }
        elseif ($enableAnimations -and -not $startupFailureRequested) {
            Write-Host "This overlay canary leaves the original Ironclad visual visible and unchanged."
        }
        if ($failureRequested) {
            Write-Host "The selected failure is injected once in memory; no game or PCK file is modified." -ForegroundColor Yellow
            Write-Host "The current combat must restore the original Ironclad and remain on original presentation after the injected failure." -ForegroundColor Yellow
        }
        if ($startupFailureRequested) {
            Write-Host "The startup mismatch changes only an in-memory signature comparison after the real reviewed signature matches." -ForegroundColor Yellow
            Write-Host "No runtime session or Harmony patch may be installed; the game must remain on original presentation." -ForegroundColor Yellow
        }
        Write-Host "Changing the marker requires a complete game restart."
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
        Write-Host "Existing replacement status was preserved at: $replacementStatusPath"
        Write-Host "Existing failure status was preserved at: $failureStatusPath"
        Write-Host "Existing JSONL journals were preserved at: $eventDirectoryPath"
        Write-Host "A running game must be exited before the installed patches are reset."
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

        Write-Host "Last original-visual replacement status: $replacementStatusPath"
        if (Test-Path -LiteralPath $replacementStatusPath -PathType Leaf) {
            Get-Content -LiteralPath $replacementStatusPath -Raw
        }
        else {
            Write-Host "No replacement status has been written. Use -ReplaceOriginal, restart the game and play a reviewed local Ironclad card." -ForegroundColor Yellow
        }

        Write-Host "Last injected-failure recovery status: $failureStatusPath"
        if (Test-Path -LiteralPath $failureStatusPath -PathType Leaf) {
            Get-Content -LiteralPath $failureStatusPath -Raw
        }
        else {
            Write-Host "No failure status has been written. Use -FailureScenario with -ReplaceOriginal, restart the game and play the target card." -ForegroundColor Yellow
        }

        Write-Host "Runtime presentation event journals: $eventDirectoryPath"
        if (Test-Path -LiteralPath $eventDirectoryPath -PathType Container) {
            Get-ChildItem `
                -LiteralPath $eventDirectoryPath `
                -Filter "runtime-canary-*.jsonl" `
                -File |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 10 Name, Length, LastWriteTime
        }
        else {
            Write-Host "No event journal directory has been created yet." -ForegroundColor Yellow
        }
    }
}
