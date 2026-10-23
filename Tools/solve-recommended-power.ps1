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
# BEFORE YOU RUN IT, READ Docs/ECONOMY-PROPOSAL-R4.md. The solver aims at the per-stage curve that proposal
# shipped: SolverTargetRatios in Program.cs is entry power over recommendedPower at each of the 72 stages,
# read off the reference walk on the shipped content. The CEIL band is read by kind (IsCeilWall, IsCeilBreezy, CeilKindOf; breezy has no upper edge) and
# the power band by stage (PowerBandFor). SolverTargetRatios is a SNAPSHOT of first-seed readings of one engine
# and one content state: it goes stale after any engine or content change (the counterattack rule alone moved 42
# stages on a what-if, because recommendedPower follows a single-seed walk that any level or combat edit re-draws),
# so re-read it after any such change, together with the content it describes. On the exact engine and content it
# was read from, -WhatIf moves nothing; that is all it claims.
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
# BattleClips.cs rides with the harness exactly as it does in build-check.ps1: the harness reads the
# clip vocabulary the battle requests, and without it this compile fails on its first using. The
# same holds for SummonTiming.cs, GuildDungeonChecks.cs, CloudChecks.cs and the battle HUD's engine-free
# files (BattleFraming.cs, BattleHudMetrics.cs, BattleHudLayout.cs), which Program.cs names directly: this
# list and build-check.ps1's are the same list, and a file added to one belongs in the other.
$sources = @(Get-ChildItem (Join-Path $root "Assets\Scripts\Core") -Filter *.cs -Recurse |
             Select-Object -Expand FullName) + (Join-Path $root "Assets\Scripts\Battle\BattleClips.cs") + (Join-Path $root "Assets\Scripts\Battle\JuiceMath.cs") + (Join-Path $root "Assets\Scripts\Battle\FeelMath.cs") +
           (Join-Path $root "Assets\Scripts\UI\Summon\SummonTiming.cs") +
           (Join-Path $root "Tools\DevHarness\Program.cs") + (Join-Path $root "Tools\DevHarness\GuildDungeonChecks.cs") +
           (Join-Path $root "Tools\DevHarness\CloudChecks.cs") + (Join-Path $root "Tools\DevHarness\ArenaMatrix.cs") +
           (Join-Path $root "Tools\DevHarness\WallResolve.cs") + (Join-Path $root "Tools\DevHarness\TempoStudy.cs") + (Join-Path $root "Tools\DevHarness\SolvePrereqs.cs") + (Join-Path $root "Tools\DevHarness\ParityStudy.cs") + (Join-Path $root "Tools\DevHarness\GuildPathChecks.cs") + (Join-Path $root "Tools\DevHarness\GuildTechChecks.cs") + (Join-Path $root "Tools\DevHarness\GuildTalentParity.cs") + (Join-Path $root "Tools\DevHarness\GuildTalentThemes.cs") +
           (Join-Path $root "Tools\DevHarness\TalentTrialChecks.cs") + (Join-Path $root "Tools\DevHarness\OnboardingChecks.cs") + (Join-Path $root "Tools\DevHarness\TelemetryChecks.cs") + (Join-Path $root "Tools\DevHarness\DailyLoopChecks.cs") + (Join-Path $root "Tools\DevHarness\DailyLoopCurve.cs") + (Join-Path $root "Tools\DevHarness\NotificationChecks.cs") + (Join-Path $root "Tools\DevHarness\SinkChecks.cs") + (Join-Path $root "Tools\DevHarness\SealedChecks.cs") + (Join-Path $root "Tools\DevHarness\QuartermasterChecks.cs") + (Join-Path $root "Tools\DevHarness\RelicEndgameChecks.cs") + (Join-Path $root "Tools\DevHarness\SummonInflationStudy.cs") + (Join-Path $root "Tools\DevHarness\WayfarersRoadChecks.cs") + (Join-Path $root "Tools\DevHarness\StoreChecks.cs") + (Join-Path $root "Tools\DevHarness\StoreOfferChecks.cs") + (Join-Path $root "Tools\DevHarness\StoreCurve.cs") + (Join-Path $root "Tools\DevHarness\AbilityCardChecks.cs") + (Join-Path $root "Tools\DevHarness\WallChestChecks.cs") + (Join-Path $root "Tools\DevHarness\AudioChecks.cs") + (Join-Path $root "Tools\DevHarness\JuiceChecks.cs") + (Join-Path $root "Tools\DevHarness\FeelChecks.cs") + (Join-Path $root "Tools\DevHarness\CaptureWatchdogChecks.cs") + (Join-Path $root "Assets\Editor\CaptureWatchdog.cs") +
           (Join-Path $root "Assets\Scripts\Battle\BattleFraming.cs") + (Join-Path $root "Assets\Scripts\UI\Battle\BattleHudMetrics.cs") +
           (Join-Path $root "Assets\Scripts\UI\Battle\BattleHudLayout.cs") + (Join-Path $root "Tools\DevHarness\BattleHudChecks.cs") +
           (Join-Path $root "Tools\DevHarness\StageChecks.cs") + (Join-Path $root "Assets\Scripts\Battle\StagePresentation.cs") +
           (Join-Path $root "Tools\DevHarness\UiKitChecks.cs") + (Join-Path $root "Tools\DevHarness\R28ScreenChecks.cs") + @(Get-ChildItem (Join-Path $root "Tools\DevHarness") -Filter R24*.cs | ForEach-Object { $_.FullName })
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
# This script rebuilds the same DevHarness.exe build-check.ps1 stamps and does not cache itself, so
# the stamp is reset: the next build-check compiles instead of trusting an exe it did not build.
. (Join-Path $PSScriptRoot "compile-cache.ps1")
Reset-CompileCache $exe
Write-HarnessConfig $exe
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

# The full 60-walk stuck scan, whatever a quick build-check left in this shell.
$quickWalksWas = $env:EMBERFALL_QUICK_WALKS
$env:EMBERFALL_QUICK_WALKS = "0"
try {
    & $exe $content --solve-recommended-power
    $code = $LASTEXITCODE
} finally {
    $env:EMBERFALL_QUICK_WALKS = $quickWalksWas
}
if ($code -ne 0) { exit $code }

Write-Host ""
Write-Host "Now run Tools/build-check.ps1: CheckCampaignPowerCurve and the geared curve's CEIL band"
Write-Host "are the guards on what this wrote, and neither of them is asserted by the solver itself."
exit 0
