Set-StrictMode -Version Latest

function Get-SteamCmdBranchBuildId {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content,

        [string]$Branch = "public-beta"
    )

    $branchPattern = '(?s)"' + [Regex]::Escape($Branch) + '"\s*\{(?<body>.*?)\}'
    $branchMatches = [Regex]::Matches(
        $Content,
        $branchPattern,
        [Text.RegularExpressions.RegexOptions]::IgnoreCase
    )

    foreach ($branchMatch in $branchMatches) {
        $buildMatch = [Regex]::Match(
            $branchMatch.Groups["body"].Value,
            '"buildid"\s+"(?<build>[0-9]+)"',
            [Text.RegularExpressions.RegexOptions]::IgnoreCase
        )
        if ($buildMatch.Success) {
            return $buildMatch.Groups["build"].Value
        }
    }

    return ""
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
            throw "Could not start SteamCMD: $SteamCmdPath"
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
            throw "SteamCMD returned exit code $steamExitCode and no valid $Branch buildid was found in its output."
        }
        throw "No valid $Branch buildid was found in SteamCMD output."
    }

    if ($steamExitCode -ne 0) {
        Write-Warning (
            "SteamCMD returned exit code $steamExitCode, but its output contains a complete " +
            "$Branch buildid=$remoteBuildId. This can occur when SteamCMD self-updates and restarts; " +
            "continuing with local build validation."
        )
    }

    [PSCustomObject]@{
        RemoteBuildId = $remoteBuildId
        SteamExitCode = $steamExitCode
        OutputPath = $OutputPath
    }
}
