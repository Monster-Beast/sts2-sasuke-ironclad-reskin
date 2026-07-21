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

function Resolve-Sts2GamePath {
    param([string]$ExplicitPath)

    if ($ExplicitPath -and (Test-Path -LiteralPath $ExplicitPath)) {
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $uninstallKeys = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 2868840",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 2868840"
    )
    foreach ($key in $uninstallKeys) {
        if (Test-Path $key) {
            $candidate = (Get-ItemProperty $key -ErrorAction SilentlyContinue).InstallLocation
            if ($candidate -and (Test-Path -LiteralPath $candidate)) {
                return (Resolve-Path -LiteralPath $candidate).Path
            }
        }
    }

    $steamPath = (Get-ItemProperty "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue).SteamPath
    if ($steamPath) {
        $candidate = Join-Path $steamPath "steamapps\common\Slay the Spire 2"
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "未找到《杀戮尖塔 2》安装目录，请通过 -GamePath 显式指定。"
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
    throw "最新 Beta 校验需要 Python 3，但当前系统未找到 python、py 或 python3。"
}

function Resolve-SteamCmd {
    param([string]$ExplicitPath)

    if ($ExplicitPath) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "SteamCMD 不存在：$ExplicitPath"
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
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "未找到 SteamCMD。请安装 Valve SteamCMD，或通过 -SteamCmdPath / -SteamCmdOutput 指定。"
}

function Invoke-LatestBetaGuard {
    param(
        [string]$ResolvedGamePath,
        [string]$ResolvedOutputRoot
    )

    $python = Resolve-PythonCommand
    $betaDirectory = Join-Path $ResolvedOutputRoot "latest-beta"
    New-Item -ItemType Directory -Force -Path $betaDirectory | Out-Null
    $capturedOutput = Join-Path $betaDirectory "steamcmd-app-info.txt"

    if ($SteamCmdOutput) {
        if (-not (Test-Path -LiteralPath $SteamCmdOutput)) {
            throw "SteamCMD 输出文件不存在：$SteamCmdOutput"
        }
        Copy-Item -LiteralPath $SteamCmdOutput -Destination $capturedOutput -Force
    }
    else {
        $steamCmd = Resolve-SteamCmd -ExplicitPath $SteamCmdPath
        & $steamCmd +login anonymous +app_info_update 1 +app_info_print 2868840 +quit 2>&1 |
            Tee-Object -FilePath $capturedOutput | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "SteamCMD 查询 public-beta 失败，退出码：$LASTEXITCODE"
        }
    }

    $guardScript = Join-Path $PSScriptRoot "latest_beta_guard.py"
    $attestation = Join-Path $betaDirectory "attestation.json"
    $arguments = @($python.Prefix) + @(
        $guardScript,
        "verify",
        "--game-path", $ResolvedGamePath,
        "--steamcmd-output", $capturedOutput,
        "--output", $attestation,
        "--required-branch", "public-beta"
    )
    & $python.Command @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "本地游戏不是 Steam 当前最新 public-beta；请先让 Steam 完成 Beta 更新。"
    }
    Write-Host "已确认当前安装为最新 public-beta：$attestation" -ForegroundColor Green
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
        if (-not (Test-Path -LiteralPath $AssetRoot)) {
            throw "AssetRoot 不存在：$AssetRoot"
        }
        $arguments += @("--asset-root", (Resolve-Path -LiteralPath $AssetRoot).Path)
    }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "本地审计失败，退出码：$LASTEXITCODE"
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
        throw "生成候选审阅表失败，退出码：$LASTEXITCODE"
    }
}

if ($Branch -ne "public-beta") {
    throw "本项目只适配 Steam 最新 public-beta，不再接受 stable 或其他分支。"
}

$resolvedGamePath = Resolve-Sts2GamePath -ExplicitPath $GamePath
$latestBetaAttestation = Invoke-LatestBetaGuard -ResolvedGamePath $resolvedGamePath -ResolvedOutputRoot $OutputRoot
$run1 = Join-Path $OutputRoot "run-1"
$run2 = Join-Path $OutputRoot "run-2"
$comparison = Join-Path $OutputRoot "comparison"
$review = Join-Path $OutputRoot "review"

Invoke-GameAudit -ResolvedGamePath $resolvedGamePath -Session "run-1" -OutputDirectory $run1
Write-Host "第一次审计已完成：$run1" -ForegroundColor Green

if ($SingleRun) {
    exit 0
}

Read-Host "请完整启动并退出一次 public-beta 游戏，确认 Steam 未更新且 Mod 环境未变化后按 Enter"
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
    throw "两次审计结果不一致。请查看：$comparison\audit-comparison.md"
}

Write-Host "两次独立审计一致：$comparison\audit-comparison.md" -ForegroundColor Green
if (-not $SkipReviewWorkbook) {
    Invoke-AuditReviewWorkbook `
        -ReportPath $firstReport `
        -ComparisonPath $comparisonJson `
        -OutputDirectory $review `
        -LatestBetaAttestation $latestBetaAttestation
    if (Test-Path (Join-Path $review "binding-review.md")) {
        Write-Host "候选审阅表已生成：$review\binding-review.md" -ForegroundColor Green
    }
}
Write-Host "不要提交 $OutputRoot；审阅结果在真实游戏验证完成前必须保持 pending_review。" -ForegroundColor Yellow
