Set-StrictMode -Version Latest

function Get-SteamCmdBranchBuildId {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content,

        [string]$Branch = "public-beta"
    )

    $branchPattern = '(?s)"' + [Regex]::Escape($Branch) + '"\s*\{(?<body>.*?)\}'
    $branchMatch = [Regex]::Match($Content, $branchPattern, [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $branchMatch.Success) {
        return ""
    }

    $buildMatch = [Regex]::Match(
        $branchMatch.Groups["body"].Value,
        '"buildid"\s+"(?<build>[0-9]+)"',
        [Text.RegularExpressions.RegexOptions]::IgnoreCase
    )
    if (-not $buildMatch.Success) {
        return ""
    }
    return $buildMatch.Groups["build"].Value
}

function Invoke-SteamCmdBetaQuery {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$SteamCmdPath,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

        [string]$Branch = "public-beta"
    )

    $outputDirectory = Split-Path -Parent $OutputPath
    if ($outputDirectory) {
        New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    }

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $SteamCmdPath
    $startInfo.Arguments = "+login anonymous +app_info_update 1 +app_info_print 2868840 +quit"
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw "无法启动 SteamCMD：$SteamCmdPath"
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        $steamExitCode = $process.ExitCode
    }
    finally {
        $process.Dispose()
    }

    $combinedOutput = @($stdout, $stderr) -join [Environment]::NewLine
    [System.IO.File]::WriteAllText(
        $OutputPath,
        $combinedOutput,
        (New-Object System.Text.UTF8Encoding($false))
    )
    if ($combinedOutput) {
        Write-Host ($combinedOutput.TrimEnd())
    }

    $remoteBuildId = Get-SteamCmdBranchBuildId -Content $combinedOutput -Branch $Branch
    if ($remoteBuildId -notmatch '^[0-9]+$') {
        if ($steamExitCode -ne 0) {
            throw "SteamCMD 返回退出码 $steamExitCode，且输出中未找到有效的 $Branch buildid。"
        }
        throw "SteamCMD 输出中未找到有效的 $Branch buildid。"
    }

    if ($steamExitCode -ne 0) {
        Write-Warning (
            "SteamCMD 返回退出码 $steamExitCode，但输出已完整包含 $Branch buildid=$remoteBuildId。" +
            "这通常发生在 SteamCMD 自更新并重启原进程时；继续执行本机版本校验。"
        )
    }

    [PSCustomObject]@{
        RemoteBuildId = $remoteBuildId
        SteamExitCode = $steamExitCode
        OutputPath = $OutputPath
    }
}
