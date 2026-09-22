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
$harness = @($core + (Join-Path $root "Assets\Scripts\Battle\BattleClips.cs") + (Join-Path $root "Tools\DevHarness\Program.cs"))
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

if (-not ($okHarness -and $okEngine)) { exit 1 }
if (-not $devOk) { exit 1 }

if (-not $NoRun) {
    Write-Host ""
    & (Join-Path $out "DevHarness.exe")
    $harnessCode = $LASTEXITCODE
    if ($harnessCode -ne 0) { exit $harnessCode }
}

# The lint verdict is applied last so its failure cannot mask a compile or harness failure, which
# are the more urgent of the two.
if (-not $lintOk) {
    Write-Host ""
    Write-Host "doc-lint reported new violations - see above." -ForegroundColor Red
    exit 1
}
