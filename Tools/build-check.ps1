# Offline compile check for the Emberfall project.
#
# Unity is not required: the game's engine-facing code is compiled against the API shim in
# Tools/UnityShim, and the engine-free core is compiled and executed by Tools/DevHarness.
# This catches typos, bad signatures and missing usings long before the editor opens.
#
#   pwsh Tools/build-check.ps1            # compile everything, then run the core harness
#   pwsh Tools/build-check.ps1 -NoRun     # compile only

param([switch]$NoRun)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$out = Join-Path $root "Tools\DevHarness\bin"
New-Item -ItemType Directory -Force $out | Out-Null

if (-not (Test-Path $csc)) { Write-Error "C# compiler not found at $csc"; exit 1 }

function Compile($label, $sources, $target, $outputPath) {
    $rsp = Join-Path $env:TEMP ("emberfall_" + $label + ".rsp")
    ($sources | ForEach-Object { '"' + $_ + '"' }) | Set-Content -Path $rsp -Encoding UTF8
    Write-Host ("[" + $label + "] " + $sources.Count + " files")
    $output = & $csc -nologo "-target:$target" "-out:$outputPath" "@$rsp" 2>&1
    $ok = ($LASTEXITCODE -eq 0)
    $output | Where-Object { $_ -match "error|warning CS" } | ForEach-Object { Write-Host "  $_" }
    if (-not $ok) { Write-Host ("[" + $label + "] FAILED") -ForegroundColor Red }
    else { Write-Host ("[" + $label + "] ok") -ForegroundColor Green }
    return $ok
}

$core = @(Get-ChildItem (Join-Path $root "Assets\Scripts\Core") -Filter *.cs -Recurse | Select-Object -Expand FullName)
$shim = @(Get-ChildItem (Join-Path $root "Tools\UnityShim") -Filter *.cs -Recurse | Select-Object -Expand FullName)
$runtime = @(Get-ChildItem (Join-Path $root "Assets\Scripts") -Filter *.cs -Recurse |
             Where-Object { $_.FullName -notmatch "\\Scripts\\Core\\" } |
             Select-Object -Expand FullName)
$editor = @()
if (Test-Path (Join-Path $root "Assets\Editor")) {
    $editor = @(Get-ChildItem (Join-Path $root "Assets\Editor") -Filter *.cs -Recurse | Select-Object -Expand FullName)
}

$harness = @($core + (Join-Path $root "Tools\DevHarness\Program.cs"))
$okHarness = Compile "core+harness" $harness "exe" (Join-Path $out "DevHarness.exe")

$engineSources = @($core + $shim + $runtime + $editor)
$okEngine = Compile "unity-code" $engineSources "library" (Join-Path $out "EmberfallEngineCheck.dll")

if (-not ($okHarness -and $okEngine)) { exit 1 }

if (-not $NoRun) {
    Write-Host ""
    & (Join-Path $out "DevHarness.exe")
    exit $LASTEXITCODE
}
