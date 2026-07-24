$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$toolsDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $toolsDirectory "steamcmd-query.ps1")
. (Join-Path $toolsDirectory "dotnet-sdk.ps1")

$resolved = Resolve-Dotnet9Command
if ($resolved.Version -notmatch '^9\.0\.[0-9]+$') {
    throw "a compatible .NET 9 SDK was not resolved"
}

$isWindowsPlatform = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
if ($isWindowsPlatform) {
    $childPowerShell = Join-Path $PSHOME "powershell.exe"
    $invalid = Test-Dotnet9Candidate -FilePath $childPowerShell
    if ($invalid.IsValid) {
        throw "a non-dotnet executable was accepted as a .NET 9 SDK"
    }
}

Write-Output "DOTNET_SDK_OK version=$($resolved.Version) command=$($resolved.Command)"
