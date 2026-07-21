$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$toolsDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $toolsDirectory "steamcmd-query.ps1")

$pythonCommand = (Get-Command python -ErrorAction Stop).Source
$guardScript = Join-Path $toolsDirectory "latest_beta_guard.py"
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("sasuke-steamcmd-query-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

try {
    $goodSteamCmd = Join-Path $tempRoot "steamcmd-good.sh"
    @'
#!/usr/bin/env bash
cat <<'EOF'
"2868840"
{
  "depots"
  {
    "branches"
    {
      "public-beta"
      {
        "buildid" "24251656"
      }
    }
  }
}
EOF
exit 7
'@ | Set-Content -LiteralPath $goodSteamCmd -Encoding utf8NoBOM
    & chmod +x $goodSteamCmd
    if ($LASTEXITCODE -ne 0) {
        throw "chmod failed for the valid SteamCMD fixture"
    }

    $goodOutput = Join-Path $tempRoot "good-output.txt"
    $result = Invoke-SteamCmdBetaQuery `
        -SteamCmdPath $goodSteamCmd `
        -OutputPath $goodOutput `
        -PythonCommand $pythonCommand `
        -GuardScript $guardScript `
        -Branch "public-beta"

    if ($result.RemoteBuildId -ne "24251656") {
        throw "valid output returned an unexpected buildid: $($result.RemoteBuildId)"
    }
    if ($result.SteamExitCode -ne 7) {
        throw "the SteamCMD exit code was not preserved"
    }
    if (-not (Test-Path -LiteralPath $goodOutput)) {
        throw "valid SteamCMD output was not captured"
    }

    $badSteamCmd = Join-Path $tempRoot "steamcmd-bad.sh"
    @'
#!/usr/bin/env bash
echo 'SteamCMD updated, but no app info was returned.'
exit 7
'@ | Set-Content -LiteralPath $badSteamCmd -Encoding utf8NoBOM
    & chmod +x $badSteamCmd
    if ($LASTEXITCODE -ne 0) {
        throw "chmod failed for the invalid SteamCMD fixture"
    }

    $rejected = $false
    try {
        Invoke-SteamCmdBetaQuery `
            -SteamCmdPath $badSteamCmd `
            -OutputPath (Join-Path $tempRoot "bad-output.txt") `
            -PythonCommand $pythonCommand `
            -GuardScript $guardScript `
            -Branch "public-beta" | Out-Null
    }
    catch {
        if ($_.Exception.Message -notmatch "退出码 7") {
            throw
        }
        $rejected = $true
    }

    if (-not $rejected) {
        throw "invalid SteamCMD output with exit code 7 was accepted"
    }

    Write-Host "STEAMCMD_QUERY_OK valid_exit7=true invalid_exit7_rejected=true buildid=24251656"
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
