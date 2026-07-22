[CmdletBinding()]
param(
    [string]$GamePath = "",
    [string]$ModDirectory = "",

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    [string]$Name,

    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$AppId = "2868840"
$StatusFileNames = @(
    "runtime-canary-status.json",
    "runtime-canary-anchor-status.json",
    "runtime-canary-replacement-status.json"
)
$MarkerFileName = "SasukeIronclad.canary.json"
$ObservationMarkerFileName = "SasukeIronclad.observe.json"
$EventDirectoryName = "canary-output"

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
    throw "The installed SasukeIronclad mod directory was not found at $candidate."
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
$resolvedModDirectory = Resolve-ModDirectory `
    -ResolvedGamePath $resolvedGamePath `
    -ExplicitModDirectory $ModDirectory

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path (Get-Location) "runtime-canary-checkpoints"
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$resolvedOutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$checkpointDirectory = Join-Path $resolvedOutputDirectory $Name
New-Item -ItemType Directory -Force -Path $checkpointDirectory | Out-Null

$copiedFiles = New-Object System.Collections.Generic.List[string]
foreach ($fileName in $StatusFileNames) {
    $source = Join-Path $resolvedModDirectory $fileName
    if (Test-Path -LiteralPath $source -PathType Leaf) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $checkpointDirectory $fileName) -Force
        [void]$copiedFiles.Add($fileName)
    }
}

$statusPath = Join-Path $resolvedModDirectory "runtime-canary-status.json"
$eventFileName = $null
if (Test-Path -LiteralPath $statusPath -PathType Leaf) {
    try {
        $startupStatus = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
        $eventFileName = [string]$startupStatus.event_file
    }
    catch {
        $eventFileName = $null
    }
}

$eventPath = $null
$journalEventCount = $null
$journalLastSequence = $null
$journalLastEventType = $null
$journalSessionId = $null
if (-not [string]::IsNullOrWhiteSpace($eventFileName)) {
    $eventPath = Join-Path (Join-Path $resolvedModDirectory $EventDirectoryName) $eventFileName
    if (Test-Path -LiteralPath $eventPath -PathType Leaf) {
        try {
            $journalLines = @(
                Get-Content -LiteralPath $eventPath |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
            )
            $journalEventCount = $journalLines.Count
            if ($journalLines.Count -gt 0) {
                $lastEvent = $journalLines[-1] | ConvertFrom-Json
                $journalLastSequence = [int]$lastEvent.sequence
                $journalLastEventType = [string]$lastEvent.event_type
                $journalSessionId = [string]$lastEvent.session_id
            }
        }
        catch {
            $journalEventCount = $null
            $journalLastSequence = $null
            $journalLastEventType = $null
            $journalSessionId = $null
        }
        Copy-Item -LiteralPath $eventPath -Destination (Join-Path $checkpointDirectory $eventFileName) -Force
        [void]$copiedFiles.Add($eventFileName)
    }
}

$controlPath = Join-Path $PSScriptRoot "runtime-canary.ps1"
$statusText = (& powershell -NoProfile -ExecutionPolicy Bypass `
    -File $controlPath `
    -Action status `
    -GamePath $resolvedGamePath `
    -ModDirectory $resolvedModDirectory 2>&1 | Out-String)
# Redact the longer Mod path before the containing game path. Checkpoint
# evidence may be shared for review without exposing local installation roots.
$statusText = $statusText.Replace($resolvedModDirectory, "<MOD_PATH>")
$statusText = $statusText.Replace($resolvedGamePath, "<GAME_PATH>")
Write-Utf8NoBom `
    -Path (Join-Path $checkpointDirectory "status.txt") `
    -Content $statusText
[void]$copiedFiles.Add("status.txt")

$processes = @()
$processQuerySucceeded = $false
try {
    $processes = @(
        Get-CimInstance Win32_Process -ErrorAction Stop |
        Where-Object {
            $_.ExecutablePath -and
            $_.ExecutablePath.StartsWith(
                $resolvedGamePath,
                [System.StringComparison]::OrdinalIgnoreCase
            )
        } |
        Select-Object Name, ProcessId, WorkingSetSize
    )
    $processQuerySucceeded = $true
}
catch {
    $processes = @()
    $processQuerySucceeded = $false
}
$gameProcesses = @(
    $processes |
    Where-Object { [string]::Equals($_.Name, "SlayTheSpire2.exe", [System.StringComparison]::OrdinalIgnoreCase) }
)
$auxiliaryProcesses = @(
    $processes |
    Where-Object { -not [string]::Equals($_.Name, "SlayTheSpire2.exe", [System.StringComparison]::OrdinalIgnoreCase) }
)
$processJson = if ($processes.Count -eq 0) {
    "[]" + [Environment]::NewLine
}
else {
    ($processes | ConvertTo-Json -Depth 4) + [Environment]::NewLine
}
Write-Utf8NoBom `
    -Path (Join-Path $checkpointDirectory "process.json") `
    -Content $processJson
[void]$copiedFiles.Add("process.json")

$checkpoint = [ordered]@{
    schema_version = 1
    generated_at_utc = [DateTimeOffset]::UtcNow.ToString("O")
    name = $Name
    canary_marker_present = Test-Path -LiteralPath (Join-Path $resolvedModDirectory $MarkerFileName) -PathType Leaf
    observation_marker_present = Test-Path -LiteralPath (Join-Path $resolvedModDirectory $ObservationMarkerFileName) -PathType Leaf
    process_query_succeeded = $processQuerySucceeded
    process_count = $processes.Count
    game_process_count = $gameProcesses.Count
    auxiliary_process_count = $auxiliaryProcesses.Count
    process_absent_at_capture = ($processQuerySucceeded -and $gameProcesses.Count -eq 0)
    event_file = $eventFileName
    journal_session_id = $journalSessionId
    journal_event_count = $journalEventCount
    journal_last_sequence = $journalLastSequence
    journal_last_event_type = $journalLastEventType
    copied_files = @($copiedFiles)
}
Write-Utf8NoBom `
    -Path (Join-Path $checkpointDirectory "checkpoint.json") `
    -Content (($checkpoint | ConvertTo-Json -Depth 5) + [Environment]::NewLine)

Write-Host "Runtime canary checkpoint saved." -ForegroundColor Green
Write-Host "Checkpoint: $checkpointDirectory"
Write-Host "Process query succeeded: $processQuerySucceeded"
Write-Host "Process count: $($processes.Count)"
Write-Host "Game process count: $($gameProcesses.Count)"
Write-Host "Event file: $(if ($eventFileName) { $eventFileName } else { 'none' })"
Write-Host "Journal last event: $(if ($journalLastEventType) { $journalLastEventType } else { 'none' })"
Write-Host "Files: $([string]::Join(', ', @($copiedFiles)))"
