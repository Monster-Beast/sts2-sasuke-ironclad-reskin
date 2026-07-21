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

function Test-Python3Candidate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [string[]]$Prefix = @()
    )

    try {
        $arguments = @($Prefix) + @(
            "-c",
            "import sys; print(str(sys.version_info[0]) + '.' + str(sys.version_info[1]))"
        )
        $result = Invoke-CapturedNativeProcess -FilePath $FilePath -Arguments $arguments
    }
    catch {
        return [PSCustomObject]@{
            IsValid = $false
            ExitCode = -1
            Version = ""
            Detail = $_.Exception.Message
        }
    }

    $version = ([string]$result.StandardOutput).Trim()
    $isValid = $result.ExitCode -eq 0 -and $version -match '^3\.[0-9]+$'
    $detailParts = @($result.StandardOutput, $result.StandardError) |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }
    $detail = ($detailParts | ForEach-Object { ([string]$_).Trim() }) -join " | "

    [PSCustomObject]@{
        IsValid = $isValid
        ExitCode = $result.ExitCode
        Version = if ($isValid) { $version } else { "" }
        Detail = $detail
    }
}

function Resolve-Python3Command {
    [CmdletBinding()]
    param(
        [string]$ExplicitPath = ""
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath -PathType Leaf)) {
            throw "PythonPath does not exist: $ExplicitPath"
        }
        $resolved = (Resolve-Path -LiteralPath $ExplicitPath).Path
        $probe = Test-Python3Candidate -FilePath $resolved
        if (-not $probe.IsValid) {
            throw "PythonPath is not a working Python 3 interpreter: $resolved; exit=$($probe.ExitCode); detail=$($probe.Detail)"
        }
        return [PSCustomObject]@{
            Command = $resolved
            Prefix = @()
            Version = $probe.Version
        }
    }

    $isWindows = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
    $candidateSpecs = if ($isWindows) {
        @(
            @{ Name = "py"; Prefix = @("-3") },
            @{ Name = "python3"; Prefix = @() },
            @{ Name = "python"; Prefix = @() }
        )
    }
    else {
        @(
            @{ Name = "python3"; Prefix = @() },
            @{ Name = "python"; Prefix = @() },
            @{ Name = "py"; Prefix = @("-3") }
        )
    }

    $diagnostics = New-Object System.Collections.Generic.List[string]
    foreach ($candidate in $candidateSpecs) {
        $command = Get-Command $candidate.Name -ErrorAction SilentlyContinue
        if (-not $command -or -not $command.Source) {
            continue
        }

        $probe = Test-Python3Candidate -FilePath $command.Source -Prefix $candidate.Prefix
        if ($probe.IsValid) {
            return [PSCustomObject]@{
                Command = $command.Source
                Prefix = @($candidate.Prefix)
                Version = $probe.Version
            }
        }

        $diagnostics.Add(
            "$($candidate.Name)=$($command.Source), exit=$($probe.ExitCode), detail=$($probe.Detail)"
        )
    }

    $detail = if ($diagnostics.Count -gt 0) {
        $diagnostics -join "; "
    }
    else {
        "no python, python3, or py command was found"
    }
    throw (
        "A working Python 3 interpreter was not found. $detail. " +
        "Install Python 3, disable the Windows App execution alias, or supply -PythonPath."
    )
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
