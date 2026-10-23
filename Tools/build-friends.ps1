# One entry for the friends test build: the Windows zip and the Android APK, both cloud, both with the free test
# store and the play log, built from the project this script lives in (run it from the play slot, on main).
#
#   powershell -ExecutionPolicy Bypass -File Tools/build-friends.ps1 -WhatIf     # prints the plan, launches nothing
#   powershell -ExecutionPolicy Bypass -File Tools/build-friends.ps1             # builds into D:\Emberfall\Builds\friends-<date>
#   ... -OutputFolder D:\Somewhere -UnityExe <path> -SkipWindows -SkipAndroid
#
# What it runs is Tools/build-windows.ps1 and Tools/build-android.ps1, each with -Cloud and
# -Defines "EMBERFALL_TESTSTORE;EMBERFALL_PLAYLOG". Those scripts keep every check they have (clean tree, the cloud
# config rules, the player checks). This one adds the order, the folder and the tolerance:
#
#   - the output folder is D:\Emberfall\Builds\friends-<yyyy-mm-dd> unless -OutputFolder says otherwise. It is
#     created if missing and NEVER emptied: a build of this commit that is already in it stops the run;
#   - a missing Android module does not stop the Windows build. The Windows zip is built, and the steps that
#     install the Android module are printed;
#   - supabase.json is only tested for existence here. This script never opens, prints or copies it, and the child
#     scripts include it untouched (-Cloud), as they always have;
#   - -WhatIf prints the checks it can make without Unity and the exact commands, then stops. It starts no editor,
#     creates no folder and writes nothing.
#
# Close the editor on this project first. Exit code: 0 when every build that was attempted succeeded, 1 otherwise.

param(
    [string]$OutputFolder = "",
    [string]$UnityExe = "",
    [string]$ProjectRoot = "",
    [switch]$SkipWindows,
    [switch]$SkipAndroid,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$Defines = "EMBERFALL_TESTSTORE;EMBERFALL_PLAYLOG"
$root = if ($ProjectRoot) { [IO.Path]::GetFullPath($ProjectRoot) } else { Split-Path -Parent $PSScriptRoot }
. (Join-Path $PSScriptRoot "unity-editor.ps1")

function Invoke-GitText {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try { $o = & git -C $root @args 2>$null; $script:GitExit = $LASTEXITCODE; return $o } finally { $ErrorActionPreference = $previous }
}

$problems = New-Object System.Collections.Generic.List[string]
$notes = New-Object System.Collections.Generic.List[string]

if ($WhatIf) { Write-Host "DRY RUN (-WhatIf): nothing below is launched, created or written." -ForegroundColor Cyan }
Write-Host "Emberfall friends build"
Write-Host "  project  $root"
Write-Host "  defines  $Defines"

# ---------------------------------------------------------------------------------------------- checks
$branch = "$(Invoke-GitText rev-parse --abbrev-ref HEAD)".Trim()
$hash = "$(Invoke-GitText rev-parse --short HEAD)".Trim()
if (-not $hash) { $problems.Add("$root is not a git working tree.") }
else {
    Write-Host "  commit   $hash on $branch"
    if ($branch -ne "main") { $notes.Add("the branch is '$branch', not main. A friends build is meant to come from main in the play slot.") }
    $changed = @(Invoke-GitText status --porcelain --untracked-files=no)
    if ($changed.Count -gt 0) { $notes.Add("git status lists $($changed.Count) changed tracked file(s). The child scripts refuse a tree that does not match HEAD.") }
}

$config = Join-Path $root "Assets\Resources\Config\supabase.json"
if (Test-Path $config) { Write-Host "  cloud    supabase.json is present (not opened here); both builds are -Cloud" }
else { $problems.Add("Assets/Resources/Config/supabase.json is missing, and both builds are -Cloud. Build from the play slot, which holds it.") }

$unity = $null
try { $unity = Find-UnityEditor $root $UnityExe; Write-Host "  unity    $unity" }
catch { $problems.Add($_.Exception.Message) }

$androidGaps = @()
if ($unity) { $androidGaps = @(Get-AndroidModuleGaps $unity) }
$doAndroid = -not $SkipAndroid
if ($doAndroid -and $unity -and $androidGaps.Count -gt 0) {
    $doAndroid = $false
    Write-Host "  android  module incomplete: Windows only" -ForegroundColor Yellow
} elseif ($doAndroid -and $unity) {
    Write-Host "  android  module complete (SDK, NDK, OpenJDK)"
}
$doWindows = -not $SkipWindows

if (-not $OutputFolder) { $OutputFolder = Join-Path "D:\Emberfall\Builds" ("friends-" + (Get-Date -Format "yyyy-MM-dd")) }
$OutputFolder = [IO.Path]::GetFullPath($OutputFolder)
if ($OutputFolder.StartsWith($root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { $problems.Add("$OutputFolder is inside the working tree.") }
Write-Host "  output   $OutputFolder"

# The names the child scripts will write, so an existing build stops the run before anything is replaced.
$version = ""
$src = Join-Path $root "Assets\Editor\EmberfallBuild.cs"
if (Test-Path $src) {
    $m = [regex]::Match((Get-Content $src -Raw), 'public const string Version = "([^"]+)"')
    if ($m.Success) { $version = $m.Groups[1].Value }
}
if (-not $version) { $problems.Add("could not read EmberfallBuild.Version.") }
elseif ($hash) {
    $expected = @()
    if ($doWindows) { $expected += @("Emberfall-$version-$hash-release-cloud-win", "Emberfall-$version-$hash-release-cloud-win.zip") }
    if ($doAndroid) { $expected += @("Emberfall-$version-$hash-release-cloud-android.apk") }
    foreach ($name in $expected) {
        if (Test-Path (Join-Path $OutputFolder $name)) { $problems.Add("$name is already in $OutputFolder. This script never overwrites or deletes a build; pass a new -OutputFolder.") }
    }
}

# ---------------------------------------------------------------------------------------------- commands
$q = [char]34
$unityPart = if ($UnityExe) { " -UnityExe $q$UnityExe$q" } else { "" }
$winCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File $q$(Join-Path $PSScriptRoot 'build-windows.ps1')$q -Cloud -Defines $q$Defines$q -OutputRoot $q$OutputFolder$q$unityPart"
$andCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File $q$(Join-Path $PSScriptRoot 'build-android.ps1')$q -Cloud -Defines $q$Defines$q -OutputRoot $q$OutputFolder$q$unityPart"

Write-Host ""
Write-Host "Plan"
if ($doWindows) { Write-Host "  1. Windows zip:  $winCommand" } else { Write-Host "  1. Windows zip:  skipped (-SkipWindows)" }
if ($doAndroid) { Write-Host "  2. Android APK:  $andCommand" }
elseif ($SkipAndroid) { Write-Host "  2. Android APK:  skipped (-SkipAndroid)" }
elseif ($unity -and $androidGaps.Count -gt 0) {
    Write-Host "  2. Android APK:  not built, the Android module is incomplete"
    Get-AndroidInstallSteps $unity $androidGaps | ForEach-Object { Write-Host "     $_" -ForegroundColor Yellow }
    Write-Host "     When it is installed, build just the APK with:"
    Write-Host "     $andCommand"
}

foreach ($n in $notes) { Write-Host "  NOTE     $n" -ForegroundColor Yellow }
foreach ($p in $problems) { Write-Host "  PROBLEM  $p" -ForegroundColor Red }

if ($WhatIf) {
    Write-Host ""
    if ($problems.Count -gt 0) { Write-Host "DRY RUN: $($problems.Count) problem(s) above would stop the real run." -ForegroundColor Red }
    else { Write-Host "DRY RUN: the checks pass. Run again without -WhatIf to build." -ForegroundColor Green }
    exit 0
}

if ($problems.Count -gt 0) { Write-Host ""; Write-Host "BUILD REFUSED: fix the problems above." -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------------------------------------- build
New-Item -ItemType Directory -Force $OutputFolder | Out-Null
$childArgs = @("-Cloud", "-Defines", $Defines, "-OutputRoot", $OutputFolder)
if ($UnityExe) { $childArgs += @("-UnityExe", $UnityExe) }

$results = @()
if ($doWindows) {
    Write-Host ""; Write-Host "=== Windows ===" -ForegroundColor Cyan
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "build-windows.ps1") @childArgs
    $results += [pscustomobject]@{ Name = "Windows zip"; Exit = $LASTEXITCODE }
}
if ($doAndroid) {
    Write-Host ""; Write-Host "=== Android ===" -ForegroundColor Cyan
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "build-android.ps1") @childArgs
    $results += [pscustomobject]@{ Name = "Android APK"; Exit = $LASTEXITCODE }
}

Write-Host ""
Write-Host "Summary ($hash, $Defines)"
$failed = $false
foreach ($r in $results) {
    if ($r.Exit -eq 0) { Write-Host "  OK       $($r.Name)" -ForegroundColor Green }
    else { Write-Host "  FAILED   $($r.Name) (exit $($r.Exit))" -ForegroundColor Red; $failed = $true }
}
if (-not $doAndroid -and -not $SkipAndroid) { Write-Host "  SKIPPED  Android APK: the Android module is incomplete (steps above)" -ForegroundColor Yellow }
Get-ChildItem $OutputFolder -File | Where-Object { $_.Name -like "Emberfall-*$hash*" -and $_.Extension -in ".zip", ".apk" } |
    ForEach-Object { Write-Host ("  file     {0}  {1} MB" -f $_.FullName, [Math]::Round($_.Length / 1MB, 1)) }
exit $(if ($failed) { 1 } else { 0 })
