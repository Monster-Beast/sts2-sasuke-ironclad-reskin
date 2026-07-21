Set-StrictMode -Version Latest

function ConvertTo-NativeArgument {
    [CmdletBinding()]
    param(
        [AllowEmptyString()]
        [string]$Value
    )

    if ($Value -eq "") {
        return '""'
    }
    if ($Value -notmatch '[\s"]') {
        return $Value
    }
    return '"' + $Value.Replace('"', '\"') + '"'
}

function Invoke-CapturedNativeProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [string[]]$Arguments = @()
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $FilePath
    $startInfo.Arguments = (($Arguments | ForEach-Object { ConvertTo-NativeArgument -Value ([string]$_) }) -join " ")
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw "Could not start process: $FilePath"
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        $exitCode = $process.ExitCode
    }
    finally {
        $process.Dispose()
    }

    [PSCustomObject]@{
        ExitCode = $exitCode
        StandardOutput = $stdout
        StandardError = $stderr
    }
}

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

    $result = Invoke-CapturedNativeProcess `
        -FilePath $SteamCmdPath `
        -Arguments @(
            "+login", "anonymous",
            "+app_info_update", "1",
            "+app_info_print", "2868840",
            "+quit"
        )

    $combinedOutput = @($result.StandardOutput, $result.StandardError) -join [Environment]::NewLine
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
        if ($result.ExitCode -ne 0) {
            throw "SteamCMD returned exit code $($result.ExitCode) and no valid $Branch buildid was found in its output."
        }
        throw "No valid $Branch buildid was found in SteamCMD output."
    }

    if ($result.ExitCode -ne 0) {
        Write-Warning (
            "SteamCMD returned exit code $($result.ExitCode), but its output contains a complete " +
            "$Branch buildid=$remoteBuildId. This can occur when SteamCMD self-updates and restarts; " +
            "continuing with local build validation."
        )
    }

    [PSCustomObject]@{
        RemoteBuildId = $remoteBuildId
        SteamExitCode = $result.ExitCode
        OutputPath = $OutputPath
    }
}
