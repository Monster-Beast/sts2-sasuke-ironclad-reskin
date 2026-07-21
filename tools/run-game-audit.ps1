[CmdletBinding()]
param(
    [string]$GamePath = "",
    [string]$AssetRoot = "",
    [ValidateSet("public-beta")]
    [string]$Branch = "public-beta",
    [string]$OutputRoot = "local-audit",
    [string]$MegaDotVersion = "4.5.1",
    [string]$SteamCmdPath = "",
    [string]$SteamCmdOutput = "",
    [switch]$SingleRun,
    [switch]$SkipReviewWorkbook
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "steamcmd-query.ps1")

function Resolve-Sts2GamePath {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath -PathType Container)) {
            throw "GamePath does not exist or is not a directory: $ExplicitPath. An invalid explicit path will not fall back to another Steam installation."
        }
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $uninstallKeys = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 2868840",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 2868840"
    )
    foreach ($key in $uninstallKeys) {
        if (Test-Path $key) {
            $candidate = (Get-ItemProperty $key -ErrorAction SilentlyContinue).InstallLocation
            if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Container)) {
                return (Resolve-Path -LiteralPath $candidate).Path
            }
        }
    }

    $steamPath = (Get-ItemProperty "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue).SteamPath
    if ($steamPath) {
        $candidate = Join-Path $steamPath "steamapps\common\Slay the Spire 2"
        if (Test-Path -LiteralPath $candidate -PathType Container) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "Slay the Spire 2 was not found. Supply the installation directory with -GamePath."
}

function Resolve-PythonCommand {
    if (Get-Command python -ErrorAction SilentlyContinue) {
        return @{ Command = "python"; Prefix = @() }
    }
    if (Get-Command py -ErrorAction SilentlyContinue) {
        return @{ Command = "py"; Prefix = @("-3") }
    }
    if (Get-Command python3 -ErrorAction SilentlyContinue) {
        return @{ Command = "python3"; Prefix = @() }
    }
    throw "Latest-beta validation requires Python 3, but python, py, and python3 were not found."
}

function Resolve-SteamCmd {
    param([string]$ExplicitPath)

    if ($ExplicitPath) {
        if (-not (Test-Path -LiteralPath $ExplicitPath -PathType Leaf)) {
            throw "SteamCMD does not exist: $ExplicitPath"
        }
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    foreach ($name in @("steamcmd.exe", "steamcmd")) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($command) {
            return $command.Source
        }
    }

    $candidates = @(
        "C:\steamcmd\steamcmd.exe",
        "C:\Program Files (x86)\Steam\steamcmd.exe",
        "C:\Program Files\Steam\steamcmd.exe"
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "SteamCMD was not found. Install Valve SteamCMD or supply -SteamCmdPath / -SteamCmdOutput."
}

function Invoke-LatestBetaGuard {
    param(
        [string]$ResolvedGamePath,
        [string]$ResolvedOutputRoot
    )

    $python = Resolve-PythonCommand
    $guardScript = Join-Path $PSScriptRoot "latest_beta_guard.py"
    $betaDirectory = Join-Path $ResolvedOutputRoot "latest-beta"
    New-Item -ItemType Directory -Force -Path $betaDirectory | Out-Null
    $capturedOutput = Join-Path $betaDirectory "steamcmd-app-info.txt"

    if ($SteamCmdOutput) {
        if (-not (Test-Path -LiteralPath $SteamCmdOutput -PathType Leaf)) {
            throw "SteamCMD output file does not exist: $SteamCmdOutput"
        }
        Copy-Item -LiteralPath $SteamCmdOutput -Destination $capturedOutput -Force
    }
    else {
        $steamCmd = Resolve-SteamCmd -ExplicitPath $SteamCmdPath
        $queryResult = Invoke-SteamCmdBetaQuery `
            -SteamCmdPath $steamCmd `
            -OutputPath $capturedOutput `
            -Branch "public-beta"
        Write-Host "SteamCMD parsed public-beta buildid=$($queryResult.RemoteBuildId)" -ForegroundColor Cyan
    }

    $attestation = Join-Path $betaDirectory "attestation.json"
    $arguments = @($python.Prefix) + @(
        $guardScript,
        "verify",
        "--game-path", $ResolvedGamePath,
        "--steamcmd-output", $capturedOutput,
        "--output", $attestation,
        "--required-branch", "public-beta"
    )
    $guardOutput = @(& $python.Command @arguments 2>&1)
    $guardExitCode = $LASTEXITCODE
    foreach ($line in $guardOutput) {
        Write-Host ([string]$line)
    }
    if ($guardExitCode -ne 0) {
        $detail = ""
        if (Test-Path -LiteralPath $attestation -PathType Leaf) {
            try {
                $state = Get-Content -LiteralPath $attestation -Raw | ConvertFrom-Json
                $detail = (
                    "installed_branch=$($state.installed_branch), " +
                    "installed_build=$($state.installed_build_id), " +
                    "remote_build=$($state.remote_build_id)"
                )
            }
            catch {
                $detail = "Could not read attestation.json: $($_.Exception.Message)"
            }
        }
        if (-not $detail) {
            $detail = ($guardOutput | ForEach-Object { [string]$_ }) -join " | "
        }
        throw (
            "The local game is not the current Steam public-beta. " +
            "Audit path: $ResolvedGamePath; $detail. Check the Steam beta branch and local buildid."
        )
    }
    Write-Host "Confirmed latest public-beta: $attestation" -ForegroundColor Green
    return $attestation
}

function Invoke-GameAudit {
    param(
        [string]$ResolvedGamePath,
        [string]$Session,
        [string]$OutputDirectory
    )

    $project = Join-Path $PSScriptRoot "game_audit\SasukeIronclad.GameAudit.csproj"
    $arguments = @(
        "run", "--project", $project, "--configuration", "Release", "--",
        "scan",
        "--game-path", $ResolvedGamePath,
        "--output", $OutputDirectory,
        "--branch", $Branch,
        "--session", $Session,
        "--megadot-version", $MegaDotVersion
    )
    if ($AssetRoot) {
        $arguments += @("--asset-root", $AssetRoot)
    }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Local audit failed with exit code $LASTEXITCODE."
    }
}

function Invoke-AuditReviewWorkbook {
    param(
        [string]$ReportPath,
        [string]$ComparisonPath,
        [string]$OutputDirectory,
        [string]$LatestBetaAttestation
    )

    $python = Resolve-PythonCommand
    $reviewScript = Join-Path $PSScriptRoot "build_audit_review.py"
    $arguments = @($python.Prefix) + @(
        $reviewScript,
        "--report", $ReportPath,
        "--comparison", $ComparisonPath,
        "--latest-beta-attestation", $LatestBetaAttestation,
        "--output", $OutputDirectory
    )
    & $python.Command @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Audit review workbook generation failed with exit code $LASTEXITCODE."
    }
}

if ($Branch -ne "public-beta") {
    throw "This project only supports the current Steam public-beta branch."
}

$resolvedGamePath = Resolve-Sts2GamePath -ExplicitPath $GamePath
if (-not [string]::IsNullOrWhiteSpace($AssetRoot)) {
    if (-not (Test-Path -LiteralPath $AssetRoot -PathType Container)) {
        throw "AssetRoot does not exist or is not a directory: $AssetRoot. Omit -AssetRoot when no recovered asset directory is available."
    }
    $AssetRoot = (Resolve-Path -LiteralPath $AssetRoot).Path
}
Write-Host "Actual game audit path: $resolvedGamePath" -ForegroundColor Cyan
if ($AssetRoot) {
    Write-Host "Recovered asset path: $AssetRoot" -ForegroundColor Cyan
}
else {
    Write-Host "No AssetRoot supplied; only resources visible in the installed game directory will be scanned." -ForegroundColor Yellow
}

$latestBetaAttestation = Invoke-LatestBetaGuard -ResolvedGamePath $resolvedGamePath -ResolvedOutputRoot $OutputRoot
$run1 = Join-Path $OutputRoot "run-1"
$run2 = Join-Path $OutputRoot "run-2"
$comparison = Join-Path $OutputRoot "comparison"
$review = Join-Path $OutputRoot "review"

Invoke-GameAudit -ResolvedGamePath $resolvedGamePath -Session "run-1" -OutputDirectory $run1
Write-Host "First audit completed: $run1" -ForegroundColor Green

if ($SingleRun) {
    exit 0
}

Read-Host "Launch and fully exit the public-beta game once. Confirm Steam and the Mod environment did not change, then press Enter"
Invoke-LatestBetaGuard -ResolvedGamePath $resolvedGamePath -ResolvedOutputRoot $OutputRoot | Out-Null
Invoke-GameAudit -ResolvedGamePath $resolvedGamePath -Session "run-2" -OutputDirectory $run2

$project = Join-Path $PSScriptRoot "game_audit\SasukeIronclad.GameAudit.csproj"
$firstReport = Join-Path $run1 "audit-report.json"
$secondReport = Join-Path $run2 "audit-report.json"
$comparisonJson = Join-Path $comparison "audit-comparison.json"
& dotnet run --project $project --configuration Release -- compare `
    --first $firstReport `
    --second $secondReport `
    --output $comparison

if ($LASTEXITCODE -ne 0) {
    throw "The two audit runs differ. Review: $comparison\audit-comparison.md"
}

Write-Host "Two independent audit runs match: $comparison\audit-comparison.md" -ForegroundColor Green
if (-not $SkipReviewWorkbook) {
    Invoke-AuditReviewWorkbook `
        -ReportPath $firstReport `
        -ComparisonPath $comparisonJson `
        -OutputDirectory $review `
        -LatestBetaAttestation $latestBetaAttestation
    if (Test-Path (Join-Path $review "binding-review.md")) {
        Write-Host "Candidate review workbook generated: $review\binding-review.md" -ForegroundColor Green
    }
}
Write-Host "Do not commit $OutputRoot. Review outputs must remain pending_review until real-game validation is complete." -ForegroundColor Yellow
