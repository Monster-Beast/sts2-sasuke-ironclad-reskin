$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$toolsDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $toolsDirectory "steamcmd-query.ps1")

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
    "2868841"
    {
      "manifests"
      {
        "public-beta"
        {
          "gid" "4669006088270458095"
          "size" "2972630778"
        }
      }
    }
    "2868842"
    {
      "manifests"
      {
        "public-beta"
        {
          "gid" "7169427731078769081"
          "size" "2387142857"
        }
      }
    }
    "branches"
    {
      "public"
      {
        "buildid" "23811903"
      }
      "public-beta"
      {
        "buildid" "24251656"
        "description" "The sts2 public beta branch"
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
        -Branch "public-beta"

    if ($result.RemoteBuildId -ne "24251656") {
        throw "valid repeated-block output returned an unexpected buildid: $($result.RemoteBuildId)"
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
            -Branch "public-beta" | Out-Null
    }
    catch {
        if ($_.Exception.Message -notmatch "exit code 7") {
            throw
        }
        $rejected = $true
    }

    if (-not $rejected) {
        throw "invalid SteamCMD output with exit code 7 was accepted"
    }

    Write-Output "STEAMCMD_QUERY_OK repeated_blocks=true valid_exit7=true invalid_exit7_rejected=true buildid=24251656"
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
