Set-StrictMode -Version Latest

function Invoke-SteamCmdBetaQuery {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$SteamCmdPath,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

        [Parameter(Mandatory = $true)]
        [string]$PythonCommand,

        [string[]]$PythonPrefix = @(),

        [Parameter(Mandatory = $true)]
        [string]$GuardScript,

        [string]$Branch = "public-beta"
    )

    $outputDirectory = Split-Path -Parent $OutputPath
    if ($outputDirectory) {
        New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    }

    $nativePreferenceVariable = Get-Variable `
        -Name PSNativeCommandUseErrorActionPreference `
        -ErrorAction SilentlyContinue
    $previousNativePreference = if ($null -ne $nativePreferenceVariable) {
        [bool]$nativePreferenceVariable.Value
    }
    else {
        $null
    }

    try {
        if ($null -ne $nativePreferenceVariable) {
            $PSNativeCommandUseErrorActionPreference = $false
        }

        & $SteamCmdPath `
            +login anonymous `
            +app_info_update 1 `
            +app_info_print 2868840 `
            +quit 2>&1 |
            Tee-Object -FilePath $OutputPath |
            Out-Host
        $steamExitCode = $LASTEXITCODE

        $parseArguments = @($PythonPrefix) + @(
            $GuardScript,
            "parse-remote",
            "--steamcmd-output", $OutputPath,
            "--branch", $Branch
        )
        $parsedLines = @(& $PythonCommand @parseArguments 2>$null)
        $parseExitCode = $LASTEXITCODE
        $remoteBuildId = if ($parsedLines.Count -gt 0) {
            ([string]$parsedLines[-1]).Trim()
        }
        else {
            ""
        }

        if ($parseExitCode -ne 0 -or $remoteBuildId -notmatch '^[0-9]+$') {
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
    finally {
        if ($null -ne $nativePreferenceVariable) {
            $PSNativeCommandUseErrorActionPreference = $previousNativePreference
        }
    }
}
