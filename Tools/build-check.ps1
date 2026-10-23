# Offline compile check for the Emberfall project.
#
# Unity is not required: the game's engine-facing code is compiled against the API shim in
# Tools/UnityShim, and the engine-free core is compiled and executed by Tools/DevHarness.
# This catches typos, bad signatures and missing usings long before the editor opens.
#
#   pwsh Tools/build-check.ps1            # compile everything, then run the core harness (the FULL check)
#   pwsh Tools/build-check.ps1 -NoRun     # compile only
#   pwsh Tools/build-check.ps1 -Quick     # for work in progress: compile and every harness check, but the
#                                         # reference-walk guard (CheckFusionNeverGates) walks 20 seeds, not 60
#
# -QUICK IS NOT THE FULL CHECK and says so in its output and in STATUS.md. Every compile, every harness
# check and every assertion runs; only the number of reference walks behind the 60-walk guard drops
# (the harness reads EMBERFALL_QUICK_WALKS, set below). A merge candidate gets the full check, always.
#
# The reference walks run on worker threads: EMBERFALL_WALK_WORKERS=1 forces them serial (the default is
# the logical cores less two). Compiles are skipped when a stamp says nothing they read has changed:
# see Tools/compile-cache.ps1; EMBERFALL_NO_COMPILE_CACHE=1 turns that off.
#
# CROSS-PLATFORM. On Windows the compiler is the .NET Framework csc.exe (C# 5) and nothing here differs from
# what it always did. On Linux and macOS there is no csc.exe, so it falls back to the .NET SDK's Roslyn
# compiler with -langversion:5, the same file lists in the same order, and runs the exes through `dotnet`;
# every check, walk, fingerprint and the doc lint run the same. See Tools/csharp.ps1.

param([switch]$NoRun, [switch]$Quick)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "compile-cache.ps1")
. (Join-Path $PSScriptRoot "csharp.ps1")
$wall = [Diagnostics.Stopwatch]::StartNew()
$mode = if ($Quick) { "quick" } else { "full" }
# The caller's own value, put back by Finish, so an in-process -Quick run does not leave the shell
# walking 20 seeds for whatever it runs next.
$quickWalksWas = $env:EMBERFALL_QUICK_WALKS
if ($Quick) {
    Write-Host "[build-check] QUICK MODE: the reference-walk guard walks 20 seeds instead of 60. This is NOT the full check and is not a merge result." -ForegroundColor Yellow
    $env:EMBERFALL_QUICK_WALKS = "1"
} else {
    $env:EMBERFALL_QUICK_WALKS = "0"
}
$compiler = Get-CSharpCompiler
$out = Join-Path $root "Tools\DevHarness\bin"
New-Item -ItemType Directory -Force $out | Out-Null

if ($null -eq $compiler) {
    $env:EMBERFALL_QUICK_WALKS = $quickWalksWas
    if ($script:OnWindows) { Write-Error "C# compiler not found at C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" }
    else { Write-Error "C# compiler not found: no .NET SDK (dotnet with sdk/*/Roslyn/bincore/csc.dll and packs/Microsoft.NETCore.App.Ref) on PATH, at DOTNET_ROOT or in ~/.dotnet" }
    exit 1
}
if (-not $script:OnWindows) { Write-Host ("[build-check] compiler: " + $compiler.Describe) }

# ---- documentation lint.
#
# Runs FIRST because it needs no compiler and takes about a second, but deliberately does not exit
# on failure: a citation violation and a compile error are independent problems, and a run that
# stopped at the first would hide the second. The verdict is carried to the end and folded into the
# exit code there, so one run reports everything wrong with the branch.
$lintOk = $true
& (Join-Path $PSScriptRoot "doc-lint.ps1")
if ($LASTEXITCODE -ne 0) { $lintOk = $false }
Write-Host ""

# ---- the status file.
#
# Every exit from here on goes through Finish, which writes STATUS.md (Tools/status.ps1) from what
# this run found and then exits with the code this run decided. The failures are named as they are
# found, so the file says which checks failed rather than how many.
#
# STATUS CANNOT CHANGE THE VERDICT. The code is fixed before status.ps1 is called and passed to it,
# not read back; the call is wrapped so anything it throws is reported and swallowed; and exit names
# the code explicitly, so whatever $LASTEXITCODE the status run leaves behind is never consulted.
$failures = New-Object System.Collections.Generic.List[string]
$harnessRan = $false
$harnessNote = ""
$harnessLines = @()
if (-not $lintOk) { $failures.Add("doc-lint: new violations (listed above the compile steps)") }

function Finish([int]$code) {
    $env:EMBERFALL_QUICK_WALKS = $quickWalksWas
    Write-Host ("[build-check] " + $mode.ToUpper() + " check finished, exit " + $code + ", " + [int]$wall.Elapsed.TotalSeconds + " s wall")
    if ($Quick) { Write-Host "[build-check] That was a QUICK run (20 walks): it does not stand for the full check." -ForegroundColor Yellow }
    try {
        & (Join-Path $PSScriptRoot "status.ps1") -BuildCheck @{
            ExitCode = $code; Failures = $failures.ToArray(); HarnessRan = $harnessRan
            HarnessNote = $harnessNote; HarnessLines = $harnessLines
            Mode = $mode; WallSeconds = [int]$wall.Elapsed.TotalSeconds
        }
    } catch {
        Write-Host ("[status] STATUS.md was not written: " + $_.Exception.Message + " - build-check's result is unaffected") -ForegroundColor Yellow
    }
    exit $code
}

function Compile($label, $sources, $target, $outputPath, $defines) {
    $key = Get-CompileKey ([string[]]$sources) $target $outputPath $defines $compiler.Path
    $cached = Test-CompileCache $key $outputPath
    if ($null -ne $cached) {
        Write-Host ("[" + $label + "] " + $sources.Count + " files")
        $cached | ForEach-Object { Write-Host "  $_" }
        Write-Host ("[" + $label + "] ok (up to date: no input changed since it was last compiled)") -ForegroundColor Green
        return $true
    }
    $rsp = Join-Path (Get-TempDir) ("emberfall_" + $label + ".rsp")
    # UTF-8 WITHOUT A BOM, written through .NET rather than Set-Content -Encoding UTF8, which on
    # Windows PowerShell 5.1 means UTF-8 WITH one. csc.exe happens to tolerate a BOM at the top of
    # a response file, so this was never a visible failure - it is the same mistake that put a
    # UTF-16 block in .gitignore and a BOM on the Discord payload, and CLAUDE.md already documents
    # the idiom for the third case. Fixed here so the repository stops carrying an example of it.
    $lines = @($sources | ForEach-Object { '"' + $_ + '"' })
    [IO.File]::WriteAllLines($rsp, [string[]]$lines, [Text.UTF8Encoding]::new($false))
    Write-Host ("[" + $label + "] " + $sources.Count + " files")
    $defineArgs = @()
    if ($defines) { $defineArgs = @("-define:$defines") }
    $output = Invoke-CSharpCompiler $compiler (@("-nologo", "-target:$target", "-out:$outputPath") + $defineArgs + @("@$rsp"))
    $ok = ($LASTEXITCODE -eq 0)
    $shown = @($output | Where-Object { $_ -match "error|warning CS" } | ForEach-Object { "$_" })
    $shown | ForEach-Object { Write-Host "  $_" }
    if (-not $ok) { Write-Host ("[" + $label + "] FAILED") -ForegroundColor Red }
    else {
        Write-Host ("[" + $label + "] ok") -ForegroundColor Green
        Write-CompileCache $key $outputPath $shown
    }
    return $ok
}

# Sort-SourceOrder (Tools/csharp.ps1) is a no-op on Windows and gives Linux and macOS the order NTFS does.
# [\\/] rather than \\ so the folder test holds for Linux and macOS paths too; on Windows it is the same match.
$core = @(Get-ChildItem (Join-Path $root "Assets\Scripts\Core") -Filter *.cs -Recurse | Select-Object -Expand FullName | Sort-SourceOrder)
$shim = @(Get-ChildItem (Join-Path $root "Tools\UnityShim") -Filter *.cs -Recurse | Select-Object -Expand FullName | Sort-SourceOrder)
$runtime = @(Get-ChildItem (Join-Path $root "Assets\Scripts") -Filter *.cs -Recurse |
             Where-Object { $_.FullName -notmatch "[\\/]Scripts[\\/]Core[\\/]" } |
             Select-Object -Expand FullName | Sort-SourceOrder)
$editor = @()
if (Test-Path (Join-Path $root "Assets\Editor")) {
    $editor = @(Get-ChildItem (Join-Path $root "Assets\Editor") -Filter *.cs -Recurse | Select-Object -Expand FullName | Sort-SourceOrder)
}

# BattleClips.cs rides with the harness: it is engine-free on purpose, so the sheet reachability
# check reads the clip vocabulary the battle actually requests rather than a copy of it.
# GuildDungeonChecks.cs is compiled in whether or not Program.Main calls it yet: it lives in its
# own file so the guild branch and whoever owns Program.cs do not edit the same lines, and
# compiling it here means the one-line call can be wired in without touching this script again.
# SummonTiming.cs rides with it for the same reason BattleClips.cs does: it is engine-free on
# purpose, so the harness can check the invariants DESIGN-SUMMON-PRESENTATION states against the
# numbers the summon screen actually plays.
# CloudChecks.cs rides the same way, for the save and cloud audit's checks (Docs/CLOUD-GAPS.md).
# ArenaMatrix.cs is a partial of Program rather than a class of its own, because the matrix is the
# sweep's and the dark talents' own instruments generalised - it runs their arenas, their seeds and
# their hash, and a second copy of those would drift from the first.
# WallResolve.cs is a partial of Program for the same reason: its instruments are the economy walk
# and the CEIL guard read under the wall rule, not copies of them.
# GuildTechChecks.cs, GuildTalentParity.cs and GuildTalentThemes.cs are partials of Program for
# that reason too, and for one more: the guild talents are graded by the instrument that grades
# hero talents, and that instrument is a private part of Program (ParityStudy.cs). Pointing the
# existing one at the new talents was the job; a second copy of it next door would drift from the
# first. GuildTalentThemes.cs reads the same talents in the guild dungeon instead, across every
# floor theme and condition, which is the dimension one arena cannot show.
# SinkChecks.cs is a partial of Program like DailyLoopChecks.cs: the sink rules (SinkShop, SinkPricing,
# SinksState) are engine-free Core, so the harness runs them, and Program.Main calls CheckSinks.
# JuiceMath.cs rides with it for the same reason, and is free of UnityEngine so JuiceChecks.cs can prove the
# R12 hit-feedback decisions (flag, rollback, cap, lanes, curves) without the editor.
# BattleFraming.cs, BattleHudMetrics.cs and BattleHudLayout.cs ride with it for BattleClips.cs's
# reason: they are engine-free on purpose, so BattleHudChecks.cs proves the battle HUD's layout with
# the very functions the battle screen places it by.
$harness = @($core + (Join-Path $root "Assets\Scripts\Battle\BattleClips.cs") + (Join-Path $root "Assets\Scripts\Battle\JuiceMath.cs") + (Join-Path $root "Assets\Scripts\Battle\FeelMath.cs") + (Join-Path $root "Assets\Scripts\UI\Summon\SummonTiming.cs") + (Join-Path $root "Tools\DevHarness\Program.cs") + (Join-Path $root "Tools\DevHarness\GuildDungeonChecks.cs") + (Join-Path $root "Tools\DevHarness\CloudChecks.cs") + (Join-Path $root "Tools\DevHarness\ArenaMatrix.cs") + (Join-Path $root "Tools\DevHarness\WallResolve.cs") + (Join-Path $root "Tools\DevHarness\TempoStudy.cs") + (Join-Path $root "Tools\DevHarness\SolvePrereqs.cs") + (Join-Path $root "Tools\DevHarness\ParityStudy.cs") + (Join-Path $root "Tools\DevHarness\GuildPathChecks.cs") + (Join-Path $root "Tools\DevHarness\GuildTechChecks.cs") + (Join-Path $root "Tools\DevHarness\GuildTalentParity.cs") + (Join-Path $root "Tools\DevHarness\GuildTalentThemes.cs") + (Join-Path $root "Tools\DevHarness\TalentTrialChecks.cs") + (Join-Path $root "Tools\DevHarness\OnboardingChecks.cs") + (Join-Path $root "Tools\DevHarness\TelemetryChecks.cs") + (Join-Path $root "Tools\DevHarness\DailyLoopChecks.cs") + (Join-Path $root "Tools\DevHarness\DailyLoopCurve.cs") + (Join-Path $root "Tools\DevHarness\NotificationChecks.cs") + (Join-Path $root "Tools\DevHarness\SinkChecks.cs") + (Join-Path $root "Tools\DevHarness\SealedChecks.cs") + (Join-Path $root "Tools\DevHarness\QuartermasterChecks.cs") + (Join-Path $root "Tools\DevHarness\RelicEndgameChecks.cs") + (Join-Path $root "Tools\DevHarness\SummonInflationStudy.cs") + (Join-Path $root "Tools\DevHarness\WayfarersRoadChecks.cs") + (Join-Path $root "Tools\DevHarness\StoreChecks.cs") + (Join-Path $root "Tools\DevHarness\StoreOfferChecks.cs") + (Join-Path $root "Tools\DevHarness\StoreCurve.cs") + (Join-Path $root "Tools\DevHarness\LoopCurve.cs") + (Join-Path $root "Tools\DevHarness\AbilityCardChecks.cs") + (Join-Path $root "Tools\DevHarness\WallChestChecks.cs") + (Join-Path $root "Tools\DevHarness\AudioChecks.cs") + (Join-Path $root "Tools\DevHarness\JuiceChecks.cs") + (Join-Path $root "Tools\DevHarness\FeelChecks.cs") + (Join-Path $root "Tools\DevHarness\StageChecks.cs") + (Join-Path $root "Assets\Scripts\Battle\StagePresentation.cs") + (Join-Path $root "Tools\DevHarness\CaptureWatchdogChecks.cs") + (Join-Path $root "Assets\Editor\CaptureWatchdog.cs") + (Join-Path $root "Assets\Scripts\Battle\BattleFraming.cs") + (Join-Path $root "Assets\Scripts\UI\Battle\BattleHudMetrics.cs") + (Join-Path $root "Assets\Scripts\UI\Battle\BattleHudLayout.cs") + (Join-Path $root "Tools\DevHarness\BattleHudChecks.cs") + (Join-Path $root "Tools\DevHarness\UiKitChecks.cs") + (Join-Path $root "Tools\DevHarness\R28ScreenChecks.cs") + (Join-Path $root "Tools\DevHarness\BugHuntChecks.cs") + @(Get-ChildItem (Join-Path $root "Tools\DevHarness") -Filter *.cs | Where-Object { $_.Name -match '^R2[46]' } | ForEach-Object { $_.FullName } | Sort-SourceOrder))
$okHarness = Compile "core+harness" $harness "exe" (Join-Path $out "DevHarness.exe")
if ($okHarness) { Write-HarnessConfig (Join-Path $out "DevHarness.exe"); Write-RuntimeConfig $compiler (Join-Path $out "DevHarness.exe") }

$engineSources = @($core + $shim + $runtime + $editor)
# UNITY_EDITOR is required here, not optional: every file under Assets/Editor is wrapped in
# #if UNITY_EDITOR (or, for the two that aren't, still only reachable from the editor). Without the
# define the preprocessor strips their bodies to nothing before csc ever sees them, so this whole
# folder was compiling as empty files and reporting "ok" regardless of what was actually in them.
# Proven the hard way during the Aspects build: a deliberately broken Editor script (bad method
# name, wrong argument types) still passed this exact step, and only the real Unity compiler, with
# UNITY_EDITOR defined by the editor itself, caught it.
$okEngine = Compile "unity-code" $engineSources "library" (Join-Path $out "EmberfallEngineCheck.dll") "UNITY_EDITOR;EMBERFALL_UNITY_IAP"
# R26: the RELEASE side of the store's #if paths. No UNITY_EDITOR and no DEVELOPMENT_BUILD, so PurchaseService.MakeDebugStore
# and ModeOfThisBuild take their release branches: once plain (the shipped build: COMING SOON) and once with the test store
# define (EMBERFALL_TESTSTORE: the debug store must still compile into a release player).
$releaseSources = @($core + $shim + $runtime)
$okReleasePlain = Compile "release-plain" $releaseSources "library" (Join-Path $out "EmberfallReleasePlain.dll") "EMBERFALL_UNITY_IAP"
$okReleaseTest = Compile "release-teststore" $releaseSources "library" (Join-Path $out "EmberfallReleaseTestStore.dll") "EMBERFALL_UNITY_IAP;EMBERFALL_TESTSTORE"


# ---------------------------------------------------------------------------------------------
# [define-probe] THE SHIPPED DEFINES DO WHAT THE DOCS SAY, executed rather than read (R26).
#
# Tools/DefineProbe prints the switches that Core derives from scripting defines. It is compiled with the
# Core files twice: with no defines (the shipped build, which sets none) and with the opt-out and test
# defines, then both are run and the printed lines asserted. Every probe file under Tools/DefineProbe
# rides along, so a switch owner adds a line (and its expectation, below) without a new step.
# ---------------------------------------------------------------------------------------------
$probeOk = $true
Write-Host ""
$probeSources = @($core + @(Get-ChildItem (Join-Path $root "Tools\DefineProbe") -Filter *.cs | Select-Object -Expand FullName))
$probeCases = @(
    @{ Label = "define-probe-shipped"; Defines = ""; Expect = @("UiKitSwitch.Enabled=True", "UiKitSwitch.ShippedDefault=True") },
    @{ Label = "define-probe-optout"; Defines = "EMBERFALL_UIKIT_OFF;EMBERFALL_TESTSTORE"; Expect = @("UiKitSwitch.Enabled=False", "UiKitSwitch.ShippedDefault=False") }
)
# A line only present once its owner's file exists: asserted when the probe prints it, per case.
$probeExtra = @{
    "define-probe-shipped" = @("TestStoreSwitch.Enabled=False")
    "define-probe-optout" = @("TestStoreSwitch.Enabled=True")
}
foreach ($case in $probeCases) {
    $exe = Join-Path $out ($case.Label + ".exe")
    if (-not (Compile $case.Label $probeSources "exe" $exe $case.Defines)) { $probeOk = $false; continue }
    $printed = @(& $exe)
    foreach ($want in $case.Expect) {
        if ($printed -notcontains $want) {
            Write-Host ("  " + $case.Label + ": expected '" + $want + "', probe printed: " + ($printed -join " | ")) -ForegroundColor Red
            $probeOk = $false
        }
    }
    foreach ($want in $probeExtra[$case.Label]) {
        $name = $want.Split("=")[0]
        $got = @($printed | Where-Object { $_ -like ($name + "=*") })
        if ($got.Count -gt 0 -and $got -notcontains $want) {
            Write-Host ("  " + $case.Label + ": expected '" + $want + "', probe printed: " + ($got -join " | ")) -ForegroundColor Red
            $probeOk = $false
        }
    }
}
if ($probeOk) { Write-Host "[define-probe] ok - the shipped build has the kit on, the opt-out define turns it off" -ForegroundColor Green }
else { Write-Host "[define-probe] FAILED" -ForegroundColor Red }


# ---------------------------------------------------------------------------------------------
# [dev-arena] A RELEASE BUILD CONTAINS NONE OF IT, measured rather than intended.
#
# Three things have to hold, and each is checkable from the committed files:
#   1. every file under Assets/Scripts/Dev is wrapped in #if UNITY_EDITOR || DEVELOPMENT_BUILD,
#      so a release player does not contain the classes - the compiler removed them;
#   2. Assets/Dev/DevArena.unity is not in EditorBuildSettings in the committed state, so no build
#      picks it up by default. How it gets into a DEVELOPMENT player is that
#      EmberfallBuild.BuildWindows names it in BuildPlayerOptions.scenes, gated on that build being
#      a Development one. It is NOT DevArenaMenu.BuildHook: that hook amends
#      EditorBuildSettings.scenes from OnPreprocessBuild, which fires after the pipeline has
#      already resolved the scene list, so its addition reaches the settings asset and never the
#      player. Two Windows builds demonstrated it - one naming its scenes and one leaving them to
#      EditorBuildSettings - and both produced a player holding level0 and no level1. The hook is
#      kept for its release-side half, which strips the scene if somebody added it by hand;
#   3. the only shipped file that mentions the arena (MainMenuScreen's DEV button) mentions it
#      only inside the same guard.
# ---------------------------------------------------------------------------------------------
$devOk = $true
$devDir = Join-Path $root "Assets\Scripts\Dev"
$guard = '#if UNITY_EDITOR || DEVELOPMENT_BUILD'
Write-Host ""
if (Test-Path $devDir) {
    $devFiles = @(Get-ChildItem $devDir -Filter *.cs -Recurse)
    Write-Host ("[dev-arena] " + $devFiles.Count + " dev file(s)")
    foreach ($f in $devFiles) {
        $lines = Get-Content $f.FullName
        if ($lines.Count -lt 2 -or $lines[0].Trim() -ne $guard) {
            Write-Host ("  " + $f.Name + " does not open with " + $guard) -ForegroundColor Red
            $devOk = $false
        }
        if ($lines[$lines.Count - 1].Trim() -ne '#endif') {
            Write-Host ("  " + $f.Name + " does not close with #endif") -ForegroundColor Red
            $devOk = $false
        }
    }

    $sceneList = Get-Content (Join-Path $root "ProjectSettings\EditorBuildSettings.asset") -Raw
    if ($sceneList -match 'Assets/Dev/DevArena\.unity') {
        Write-Host "  Assets/Dev/DevArena.unity is in EditorBuildSettings - it must be added only by the build hook, for Development Builds" -ForegroundColor Red
        $devOk = $false
    }

    $shipped = @(Get-ChildItem (Join-Path $root "Assets\Scripts") -Filter *.cs -Recurse |
                 Where-Object { $_.FullName -notmatch "[\\/]Scripts[\\/]Dev[\\/]" } |
                 Where-Object { (Get-Content $_.FullName -Raw) -match 'Emberfall\.Dev|DevArena' })
    foreach ($f in $shipped) {
        $text = Get-Content $f.FullName -Raw
        $guarded = ([regex]::Matches($text, [regex]::Escape($guard))).Count
        if ($guarded -lt 1) {
            Write-Host ("  " + $f.Name + " names the Dev Arena outside the development guard") -ForegroundColor Red
            $devOk = $false
        }
    }

    if ($devOk) { Write-Host "[dev-arena] ok - nothing reaches a release build" -ForegroundColor Green }
    else { Write-Host "[dev-arena] FAILED" -ForegroundColor Red }
}

# ---------------------------------------------------------------------------------------------
# [offline-switch] EDITOR AUTOMATION CANNOT REACH THE REAL CLOUD, measured rather than intended.
#
# The worktree holds a gitignored supabase.json, and a Play session that does nothing about it signs
# in as the real account. SupabaseConfig.ForcedOffline is the one switch that stops that: while it
# is set the loader behaves as if the file were absent. Four things have to hold:
#   1. the real switch logic RUNS correctly: Tools/OfflineSwitchCheck executes SupabaseConfig,
#      GameApp.StartCloud and EditorCaptureGuard against the shim with a stand-in file in front of
#      the loader, and shows the file is never read and no SupabaseClient is built;
#   2. every Play-mode entry under Assets/Editor goes through EditorCaptureGuard.EnterPlayMode,
#      which arms the switch - so a new capture tool that assigns isPlaying = true itself fails here;
#   3. nothing about the switch exists outside #if UNITY_EDITOR in Assets/Scripts, so no player
#      build contains it or can reach it;
#   4. the only place that constructs a SupabaseClient in shipped code is GameApp.StartCloud, after
#      its `Configured` early return.
# ---------------------------------------------------------------------------------------------
$offlineOk = $true
Write-Host ""

# 2. no raw Play-mode entry outside the guard.
$rawEntries = @()
foreach ($f in (Get-ChildItem (Join-Path $root "Assets\Editor") -Filter *.cs -Recurse)) {
    if ($f.Name -eq "EditorCaptureGuard.cs") { continue }
    $n = 0
    foreach ($line in (Get-Content $f.FullName)) {
        $n++
        if ($line.TrimStart().StartsWith("//")) { continue }
        if ($line -match 'isPlaying\s*=\s*true') { $rawEntries += ($f.Name + ":" + $n) }
    }
}
if ($rawEntries.Count -gt 0) {
    Write-Host ("  Play mode entered without EditorCaptureGuard.EnterPlayMode at: " + ($rawEntries -join ", ")) -ForegroundColor Red
    $offlineOk = $false
}

# 3. the switch is editor-only in everything that ships.
$leaks = @()
$tokens = 'ForcedOffline|ForceOfflineKey|ForcedOfflineProblem|ClientsConstructed'
foreach ($f in (Get-ChildItem (Join-Path $root "Assets\Scripts") -Filter *.cs -Recurse)) {
    $stack = New-Object System.Collections.Generic.List[bool]
    $n = 0
    foreach ($line in (Get-Content $f.FullName)) {
        $n++
        $t = $line.Trim()
        if ($t -match '^#if\s+(.*)$') { $stack.Add($Matches[1].Trim() -eq 'UNITY_EDITOR'); continue }
        if ($t -match '^#(else|elif)\b') { if ($stack.Count -gt 0) { $stack[$stack.Count - 1] = $false }; continue }
        if ($t -match '^#endif') { if ($stack.Count -gt 0) { $stack.RemoveAt($stack.Count - 1) }; continue }
        if ($t.StartsWith("//")) { continue }
        if ($line -match $tokens -and -not $stack.Contains($true)) { $leaks += ($f.Name + ":" + $n) }
    }
}
if ($leaks.Count -gt 0) {
    Write-Host ("  the forced-offline switch is referenced outside #if UNITY_EDITOR at: " + ($leaks -join ", ")) -ForegroundColor Red
    $offlineOk = $false
}

# 4. the one place a client is built.
$builders = @(Get-ChildItem (Join-Path $root "Assets\Scripts") -Filter *.cs -Recurse |
              Where-Object { (Get-Content $_.FullName -Raw) -match 'new\s+SupabaseClient\s*\(' })
if ($builders.Count -ne 1 -or $builders[0].Name -ne "GameApp.cs") {
    Write-Host ("  SupabaseClient is constructed outside GameApp.StartCloud: " + (($builders | ForEach-Object { $_.Name }) -join ", ")) -ForegroundColor Red
    $offlineOk = $false
}
else {
    $app = Get-Content $builders[0].FullName -Raw
    $gate = $app.IndexOf('if (!config.Configured)')
    $build = $app.IndexOf('new SupabaseClient(')
    $gateReturns = $false
    if ($gate -ge 0 -and $build -gt $gate) {
        $gateReturns = $app.Substring($gate, $build - $gate).Contains('return;')
    }
    if (-not $gateReturns) {
        Write-Host '  GameApp.StartCloud no longer returns on !config.Configured before it builds a SupabaseClient' -ForegroundColor Red
        $offlineOk = $false
    }
}

# 1. the switch's logic, executed.
if ($okEngine) {
    $offlineExe = Join-Path $out "OfflineSwitchCheck.exe"
    $engineDll = Join-Path $out "EmberfallEngineCheck.dll"
    $offlineSrc = Join-Path $root "Tools\OfflineSwitchCheck\OfflineSwitchCheck.cs"
    $compileOut = Invoke-CSharpCompiler $compiler @("-nologo", "-target:exe", "-out:$offlineExe", "-reference:$engineDll", $offlineSrc)
    if ($LASTEXITCODE -ne 0) {
        $compileOut | ForEach-Object { Write-Host "  $_" }
        Write-Host "  OfflineSwitchCheck failed to compile" -ForegroundColor Red
        $offlineOk = $false
    }
    else {
        Write-RuntimeConfig $compiler $offlineExe
        $offlineLines = @(Invoke-BuiltExe $compiler $offlineExe)
        $offlineCode = $LASTEXITCODE
        foreach ($l in $offlineLines) { Write-Host ("  " + $l) }
        if ($offlineCode -ne 0) { $offlineOk = $false }
    }
}
else {
    Write-Host "  OfflineSwitchCheck not run: the engine code did not compile" -ForegroundColor Yellow
}

if ($offlineOk) { Write-Host "[offline-switch] ok - editor automation is offline by construction" -ForegroundColor Green }
else { Write-Host "[offline-switch] FAILED" -ForegroundColor Red }

if (-not $okHarness) { $failures.Add("core+harness: compile FAILED") }
if (-not $okEngine) { $failures.Add("unity-code: compile FAILED") }
if (-not $okReleasePlain) { $failures.Add("release-plain: compile FAILED") }
if (-not $okReleaseTest) { $failures.Add("release-teststore: compile FAILED (EMBERFALL_TESTSTORE in a release build)") }
if (-not $devOk) { $failures.Add("dev-arena: something reaches a release build") }
if (-not $offlineOk) { $failures.Add("offline-switch: editor automation can reach the real cloud") }
if (-not $probeOk) { $failures.Add("define-probe: a scripting define does not do what the docs say") }
if (-not ($okHarness -and $okEngine -and $okReleasePlain -and $okReleaseTest)) { $harnessNote = "not run, a compile failed"; Finish 1 }
if (-not $devOk) { $harnessNote = "not run, the dev-arena check failed"; Finish 1 }
if (-not $offlineOk) { $harnessNote = "not run, the offline-switch check failed"; Finish 1 }
if (-not $probeOk) { $harnessNote = "not run, the define-probe check failed"; Finish 1 }

if ($NoRun) { $harnessNote = "not run, -NoRun" }
else {
    Write-Host ""
    # Tee rather than a plain call, so STATUS.md can name each failing check. The output still
    # streams to the console line by line; the harness writes plain ASCII, so nothing is re-encoded.
    Invoke-BuiltExe $compiler (Join-Path $out "DevHarness.exe") | Tee-Object -Variable harnessLines
    $harnessCode = $LASTEXITCODE
    $harnessRan = $true
    $harnessLines = @($harnessLines)
    foreach ($l in $harnessLines) {
        if ($l -match '^\s*\[FAIL\]\s*(.*)$') {
            $name = $Matches[1]
            if ($name.Length -gt 200) { $name = $name.Substring(0, 200) + "..." }
            $failures.Add("harness: " + $name)
        }
    }
    if ($harnessCode -ne 0) {
        # A non-zero exit with no [FAIL] line (a crash, an unhandled exception) is still a failure.
        if (-not ($failures | Where-Object { $_ -like "harness: *" })) { $failures.Add("harness: exited $harnessCode with no [FAIL] line") }
        Finish $harnessCode
    }
}

# The lint verdict is applied last so its failure cannot mask a compile or harness failure, which
# are the more urgent of the two.
if (-not $lintOk) {
    Write-Host ""
    Write-Host "doc-lint reported new violations - see above." -ForegroundColor Red
    Finish 1
}
Finish 0
