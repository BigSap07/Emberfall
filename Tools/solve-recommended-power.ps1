# Re-derive campaign.json's recommendedPower table, and the levels underneath it.
#
#   pwsh Tools/solve-recommended-power.ps1            # solve and write campaign.json
#   pwsh Tools/solve-recommended-power.ps1 -WhatIf    # solve against a copy, write nothing
#
# WHAT IT IS. recommendedPower is derived content, not an authored number: it is the simulated
# player's ENTRY power at a stage divided by the middle of that stage's difficulty band. That
# makes it a function of what a player can field, so it goes stale the moment anything a player
# fields changes - a hero's kit, a rarity's stat line, the starting roster, the shape of the
# economy. balance/campaign-curve derived the whole table this way by hand and it was out of band
# again two merges later. This is the same derivation as a command, so re-running it is the answer
# instead of re-reasoning about it.
#
# WHAT IT TOUCHES. Four fields on each of campaign.json's stages and nothing else:
# recommendedPower, floorLevel, ceilingLevel, and the authored enemy levels. It does not touch the
# purse, the first-clear rewards, the idle rates, waveCount, battleCount, the roster's
# COMPOSITION, or one line of the file's commentary. It never touches heroes.json, enemies.json
# or any code.
#
# WHEN TO RUN IT. After any change to a hero the balance line fields, and after any change to the
# economy. See DESIGN-CAMPAIGN-STRUCTURE.md.
#
# AFTERWARDS, ALWAYS. Run Tools/build-check.ps1. The solver asserts nothing about its own output;
# CheckCampaignPowerCurve is the guard on the numbers and the geared curve's CEIL band is the
# guard on the levels under them. If the roster levels moved, the stage fingerprints moved with
# them, and regenerating that baseline is a separate deliberate act:
# EMBERFALL_REGEN_STAGE_FP=1.

param([switch]$WhatIf)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$out = Join-Path $root "Tools\DevHarness\bin"
New-Item -ItemType Directory -Force $out | Out-Null

if (-not (Test-Path $csc)) { Write-Error "C# compiler not found at $csc"; exit 1 }

# Compiled here rather than leaning on a build-check that ran at some point earlier, because the
# whole value of this thing is that its answer describes the tree as it stands right now.
$sources = @(Get-ChildItem (Join-Path $root "Assets\Scripts\Core") -Filter *.cs -Recurse |
             Select-Object -Expand FullName) + (Join-Path $root "Tools\DevHarness\Program.cs")
$rsp = Join-Path $env:TEMP "emberfall_recpwr.rsp"
$lines = @($sources | ForEach-Object { '"' + $_ + '"' })
[IO.File]::WriteAllLines($rsp, [string[]]$lines, [Text.UTF8Encoding]::new($false))

$exe = Join-Path $out "DevHarness.exe"
Write-Host ("[core+harness] " + $sources.Count + " files")
$compile = & $csc -nologo "-target:exe" "-out:$exe" "@$rsp" 2>&1
if ($LASTEXITCODE -ne 0) {
    $compile | Where-Object { $_ -match "error" } | ForEach-Object { Write-Host "  $_" }
    Write-Host "[core+harness] FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "[core+harness] ok" -ForegroundColor Green
Write-Host ""

$content = Join-Path $root "Assets\Resources\Content"

# -WhatIf points the solver at a throwaway copy of the content folder. The walk, the arithmetic
# and the report are identical; the campaign.json it overwrites is the copy's, and the tree is
# left alone. That is the honest form of a dry run here, because this tool's output IS a file.
if ($WhatIf) {
    $scratch = Join-Path $env:TEMP ("emberfall-recpwr-whatif-" + [Guid]::NewGuid().ToString("n"))
    New-Item -ItemType Directory -Force $scratch | Out-Null
    Copy-Item (Join-Path $content "*.json") $scratch
    Write-Host "-WhatIf: solving against a copy in $scratch - campaign.json in the tree is not touched."
    Write-Host ""
    $content = $scratch
}

& $exe $content --solve-recommended-power
$code = $LASTEXITCODE
if ($code -ne 0) { exit $code }

Write-Host ""
Write-Host "Now run Tools/build-check.ps1: CheckCampaignPowerCurve and the geared curve's CEIL band"
Write-Host "are the guards on what this wrote, and neither of them is asserted by the solver itself."
exit 0
