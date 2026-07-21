[CmdletBinding()]
param(
    [string]$GamePath = "",
    [string]$AssetRoot = "",
    [string]$Branch = "stable",
    [string]$OutputRoot = "local-audit",
    [string]$MegaDotVersion = "4.5.1",
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
        [string]$OutputDirectory
    )

    $reviewScript = Join-Path $PSScriptRoot "build_audit_review.py"
    $pythonCommand = $null
    $pythonPrefix = @()
    if (Get-Command python -ErrorAction SilentlyContinue) {
        $pythonCommand = "python"
    }
    elseif (Get-Command py -ErrorAction SilentlyContinue) {
        $pythonCommand = "py"
        $pythonPrefix = @("-3")
    }
    elseif (Get-Command python3 -ErrorAction SilentlyContinue) {
        $pythonCommand = "python3"
    }

    if (-not $pythonCommand) {
        Write-Warning "未检测到 Python；跳过候选审阅表。可稍后手动运行 tools/build_audit_review.py。"
        return
    }

    $arguments = @($pythonPrefix) + @(
        $reviewScript,
        "--report", $ReportPath,
        "--comparison", $ComparisonPath,
        "--output", $OutputDirectory
    )
    & $pythonCommand @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "生成候选审阅表失败，退出码：$LASTEXITCODE"
    }
}

$resolvedGamePath = Resolve-Sts2GamePath -ExplicitPath $GamePath
$run1 = Join-Path $OutputRoot "run-1"
$run2 = Join-Path $OutputRoot "run-2"
$comparison = Join-Path $OutputRoot "comparison"
$review = Join-Path $OutputRoot "review"

Invoke-GameAudit -ResolvedGamePath $resolvedGamePath -Session "run-1" -OutputDirectory $run1
Write-Host "第一次审计已完成：$run1" -ForegroundColor Green

if ($SingleRun) {
    exit 0
}

Read-Host "请完整启动并退出一次游戏，确认相同 Mod/分支环境后按 Enter 进行第二次审计"
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
        -OutputDirectory $review
    if (Test-Path (Join-Path $review "binding-review.md")) {
        Write-Host "候选审阅表已生成：$review\binding-review.md" -ForegroundColor Green
    }
}
Write-Host "不要提交 $OutputRoot；审阅结果在真实游戏验证完成前必须保持 pending_review。" -ForegroundColor Yellow
