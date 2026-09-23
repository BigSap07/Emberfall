# Builds and runs Tools/CloudProbe: the real cloud and save code against a fake engine.
#
#   powershell -File Tools/CloudProbe/run-cloud-probe.ps1
#
# Not part of Tools/build-check.ps1. It compiles six files out of Assets/Scripts/Game against
# FakeEngine.cs instead of Tools/UnityShim, which is a different build from either of the two
# build-check makes, and it needs nothing live: every request is answered from a script.
# Docs/CLOUD-GAPS.md says which of its scenarios prove which finding.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { Write-Error "C# compiler not found at $csc"; exit 1 }

$out = Join-Path $root "Tools\DevHarness\bin"
New-Item -ItemType Directory -Force $out | Out-Null

$core = @(Get-ChildItem (Join-Path $root "Assets\Scripts\Core") -Filter *.cs -Recurse | Select-Object -Expand FullName)
$game = @("Game\Cloud\AccountService.cs", "Game\Cloud\CloudLocalState.cs", "Game\Cloud\SupabaseClient.cs",
          "Game\Cloud\SupabaseConfig.cs", "Game\Cloud\CloudSaveService.cs", "Game\SaveService.cs") |
        ForEach-Object { Join-Path $root ("Assets\Scripts\" + $_) }
$probe = @((Join-Path $PSScriptRoot "FakeEngine.cs"), (Join-Path $PSScriptRoot "CloudProbe.cs"))

$rsp = Join-Path $env:TEMP "emberfall_cloudprobe.rsp"
$lines = @(($core + $game + $probe) | ForEach-Object { '"' + $_ + '"' })
[IO.File]::WriteAllLines($rsp, [string[]]$lines, [Text.UTF8Encoding]::new($false))

$exe = Join-Path $out "CloudProbe.exe"
$output = & $csc -nologo -target:exe "-out:$exe" "@$rsp" 2>&1
if ($LASTEXITCODE -ne 0) {
    $output | Where-Object { $_ -match "error" } | ForEach-Object { Write-Host "  $_" }
    Write-Host "[cloud-probe] compile FAILED" -ForegroundColor Red
    exit 1
}

& $exe $root
exit $LASTEXITCODE
