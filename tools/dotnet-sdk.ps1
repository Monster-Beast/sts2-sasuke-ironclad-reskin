Set-StrictMode -Version Latest

function Test-Dotnet9Candidate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    try {
        $result = Invoke-CapturedNativeProcess `
            -FilePath $FilePath `
            -Arguments @("--list-sdks")
    }
    catch {
        return [PSCustomObject]@{
            IsValid = $false
            Command = $FilePath
            Version = ""
            Detail = $_.Exception.Message
        }
    }

    $sdkMatches = [Regex]::Matches(
        [string]$result.StandardOutput,
        '(?m)^(?<version>9\.0\.[0-9]+)\s+\['
    )
    $versions = @(
        $sdkMatches |
        ForEach-Object { $_.Groups["version"].Value } |
        Sort-Object { [Version]$_ } -Descending -Unique
    )

    if ($result.ExitCode -eq 0 -and $versions.Count -gt 0) {
        return [PSCustomObject]@{
            IsValid = $true
            Command = $FilePath
            Version = [string]$versions[0]
            Detail = ""
        }
    }

    $detailParts = @($result.StandardOutput, $result.StandardError) |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }
    $detail = ($detailParts | ForEach-Object { ([string]$_).Trim() }) -join " | "
    return [PSCustomObject]@{
        IsValid = $false
        Command = $FilePath
        Version = ""
        Detail = "exit=$($result.ExitCode), detail=$detail"
    }
}

function Resolve-Dotnet9Command {
    [CmdletBinding()]
    param([string]$ExplicitPath = "")

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath -PathType Leaf)) {
            throw "DotnetPath does not exist: $ExplicitPath"
        }
        $resolved = (Resolve-Path -LiteralPath $ExplicitPath).Path
        $probe = Test-Dotnet9Candidate -FilePath $resolved
        if (-not $probe.IsValid) {
            throw "DotnetPath does not provide a .NET 9 SDK: $resolved; $($probe.Detail)"
        }
        return $probe
    }

    $paths = New-Object System.Collections.Generic.List[string]
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command -and $command.Source) {
        $paths.Add($command.Source)
    }

    foreach ($candidate in @(
        "$env:ProgramFiles\dotnet\dotnet.exe",
        "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe",
        "$env:USERPROFILE\.dotnet\dotnet.exe",
        "/usr/bin/dotnet",
        "/usr/local/bin/dotnet"
    )) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and
            (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            $paths.Add((Resolve-Path -LiteralPath $candidate).Path)
        }
    }

    $attempts = New-Object System.Collections.Generic.List[string]
    foreach ($candidate in @($paths | Select-Object -Unique)) {
        $probe = Test-Dotnet9Candidate -FilePath $candidate
        if ($probe.IsValid) {
            return $probe
        }
        $attempts.Add("$candidate, $($probe.Detail)")
    }

    $attemptText = if ($attempts.Count -gt 0) {
        ($attempts -join "; ")
    }
    else {
        "no dotnet executable was found"
    }
    throw (
        "A compatible .NET 9 SDK was not found: $attemptText. " +
        "Install Microsoft.DotNet.SDK.9 with winget or supply -DotnetPath. " +
        "Installing only the .NET runtime is not sufficient."
    )
}
