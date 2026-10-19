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
    try {
        & (Join-Path $PSScriptRoot "status.ps1") -BuildCheck @{
            ExitCode = $code; Failures = $failures.ToArray(); HarnessRan = $harnessRan
            HarnessNote = $harnessNote; HarnessLines = $harnessLines
        }
    } catch {
        Write-Host ("[status] STATUS.md was not written: " + $_.Exception.Message + " - build-check's result is unaffected") -ForegroundColor Yellow
    }
    exit $code
}

function Compile($label, $sources, $target, $outputPath, $defines) {
    $rsp = Join-Path $env:TEMP ("emberfall_" + $label + ".rsp")
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
    $output = & $csc -nologo "-target:$target" "-out:$outputPath" @defineArgs "@$rsp" 2>&1
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
$harness = @($core + (Join-Path $root "Assets\Scripts\Battle\BattleClips.cs") + (Join-Path $root "Assets\Scripts\Battle\JuiceMath.cs") + (Join-Path $root "Assets\Scripts\UI\Summon\SummonTiming.cs") + (Join-Path $root "Tools\DevHarness\Program.cs") + (Join-Path $root "Tools\DevHarness\GuildDungeonChecks.cs") + (Join-Path $root "Tools\DevHarness\CloudChecks.cs") + (Join-Path $root "Tools\DevHarness\ArenaMatrix.cs") + (Join-Path $root "Tools\DevHarness\WallResolve.cs") + (Join-Path $root "Tools\DevHarness\TempoStudy.cs") + (Join-Path $root "Tools\DevHarness\SolvePrereqs.cs") + (Join-Path $root "Tools\DevHarness\ParityStudy.cs") + (Join-Path $root "Tools\DevHarness\GuildPathChecks.cs") + (Join-Path $root "Tools\DevHarness\GuildTechChecks.cs") + (Join-Path $root "Tools\DevHarness\GuildTalentParity.cs") + (Join-Path $root "Tools\DevHarness\GuildTalentThemes.cs") + (Join-Path $root "Tools\DevHarness\TalentTrialChecks.cs") + (Join-Path $root "Tools\DevHarness\OnboardingChecks.cs") + (Join-Path $root "Tools\DevHarness\TelemetryChecks.cs") + (Join-Path $root "Tools\DevHarness\DailyLoopChecks.cs") + (Join-Path $root "Tools\DevHarness\SinkChecks.cs") + (Join-Path $root "Tools\DevHarness\AudioChecks.cs") + (Join-Path $root "Tools\DevHarness\JuiceChecks.cs") + (Join-Path $root "Tools\DevHarness\StageChecks.cs") + (Join-Path $root "Assets\Scripts\Battle\StagePresentation.cs") + (Join-Path $root "Tools\DevHarness\CaptureWatchdogChecks.cs") + (Join-Path $root "Assets\Editor\CaptureWatchdog.cs") + (Join-Path $root "Assets\Scripts\Battle\BattleFraming.cs") + (Join-Path $root "Assets\Scripts\UI\Battle\BattleHudMetrics.cs") + (Join-Path $root "Assets\Scripts\UI\Battle\BattleHudLayout.cs") + (Join-Path $root "Tools\DevHarness\BattleHudChecks.cs"))
$okHarness = Compile "core+harness" $harness "exe" (Join-Path $out "DevHarness.exe")

$engineSources = @($core + $shim + $runtime + $editor)
# UNITY_EDITOR is required here, not optional: every file under Assets/Editor is wrapped in
# #if UNITY_EDITOR (or, for the two that aren't, still only reachable from the editor). Without the
# define the preprocessor strips their bodies to nothing before csc ever sees them, so this whole
# folder was compiling as empty files and reporting "ok" regardless of what was actually in them.
# Proven the hard way during the Aspects build: a deliberately broken Editor script (bad method
# name, wrong argument types) still passed this exact step, and only the real Unity compiler, with
# UNITY_EDITOR defined by the editor itself, caught it.
$okEngine = Compile "unity-code" $engineSources "library" (Join-Path $out "EmberfallEngineCheck.dll") "UNITY_EDITOR"


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
                 Where-Object { $_.FullName -notmatch "\\Scripts\\Dev\\" } |
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
    $compileOut = & $csc -nologo -target:exe "-out:$offlineExe" "-reference:$engineDll" $offlineSrc 2>&1
    if ($LASTEXITCODE -ne 0) {
        $compileOut | ForEach-Object { Write-Host "  $_" }
        Write-Host "  OfflineSwitchCheck failed to compile" -ForegroundColor Red
        $offlineOk = $false
    }
    else {
        $offlineLines = @(& $offlineExe)
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
if (-not $devOk) { $failures.Add("dev-arena: something reaches a release build") }
if (-not $offlineOk) { $failures.Add("offline-switch: editor automation can reach the real cloud") }
if (-not ($okHarness -and $okEngine)) { $harnessNote = "not run, a compile failed"; Finish 1 }
if (-not $devOk) { $harnessNote = "not run, the dev-arena check failed"; Finish 1 }
if (-not $offlineOk) { $harnessNote = "not run, the offline-switch check failed"; Finish 1 }

if ($NoRun) { $harnessNote = "not run, -NoRun" }
else {
    Write-Host ""
    # Tee rather than a plain call, so STATUS.md can name each failing check. The output still
    # streams to the console line by line; the harness writes plain ASCII, so nothing is re-encoded.
    & (Join-Path $out "DevHarness.exe") | Tee-Object -Variable harnessLines
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
