# Builds the Windows playtest player: the zip a non-developer unzips and runs.
#
#   powershell -ExecutionPolicy Bypass -File Tools/build-windows.ps1
#   powershell -ExecutionPolicy Bypass -File Tools/build-windows.ps1 -OutputRoot D:\Somewhere -NoZip
#
# It wraps the one Windows build path that already exists, EmberfallBuild.BuildWindows, through
# EmberfallBuild.BuildWindowsPlaytest. It invents no second one. What this script adds is the part
# that makes a build traceable:
#
#   - it REFUSES A DIRTY TREE. The player carries the short hash on screen (Game.BuildStamp), and
#     a build whose hash does not describe its contents is worse than no hash: a report against
#     it sends whoever reads it to the wrong code with total confidence. Tracked changes anywhere
#     fail it, and so do untracked files under Assets, Packages and ProjectSettings, because Unity
#     builds those whether git knows about them or not;
#   - it passes the hash it checked, so the stamp and the check cannot disagree;
#   - it checks the tree is STILL clean afterwards. The editor writes settings back on its way
#     out, and a build that changed a tracked file was built from something no commit describes;
#   - it checks the player it got: the executable exists, the Dev Arena scene is absent, no
#     development-only class made it into the game assembly, and the hash is packed inside.
#
# Non-development and Mono are EmberfallBuild's decisions, not this script's: BuildWindowsPlaytest
# turns Development off, and ApplyWindowsPlayerSettings picks Mono because this editor install has
# no Windows IL2CPP module. The output goes OUTSIDE the working tree by default, for the reason
# BuildWindowsHandoff gives: a player several hundred megabytes big does not belong in a checkout.
#
# Close the editor on this project first. Unity will not open a project another editor holds.

param(
    [string]$OutputRoot = "",
    [string]$UnityExe = "",
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Fail([string]$message) {
    Write-Host ""
    Write-Host "BUILD REFUSED: $message" -ForegroundColor Red
    exit 1
}

# Git, with its stderr kept out of PowerShell's error stream. Windows PowerShell 5.1 turns a native
# command's stderr into an error record under "Stop", and git prints its line-ending warnings there.
function Invoke-Git {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & git -C $root @args 2>$null
        $script:GitExit = $LASTEXITCODE
        return $output
    } finally {
        $ErrorActionPreference = $previous
    }
}

# Whether a file holds an ASCII string, read in chunks: resources.assets runs to hundreds of
# megabytes and does not need to be in memory at once. Chunks overlap by the needle's length so a
# match across a boundary is still found.
function Test-FileContains([string]$path, [string]$needle) {
    $chunkSize = 16MB
    $buffer = New-Object byte[] ($chunkSize + $needle.Length)
    $stream = [IO.File]::OpenRead($path)
    try {
        $carry = 0
        while ($true) {
            $read = $stream.Read($buffer, $carry, $chunkSize)
            if ($read -le 0) { return $false }
            $count = $carry + $read
            if ([Text.Encoding]::ASCII.GetString($buffer, 0, $count).Contains($needle)) { return $true }
            $carry = [Math]::Min($needle.Length - 1, $count)
            [Array]::Copy($buffer, $count - $carry, $buffer, 0, $carry)
        }
    } finally {
        $stream.Dispose()
    }
}

# What would make this tree's hash a lie about the player built from it, one line per cause.
function Get-TreeProblems {
    $problems = @()
    $status = @(Invoke-Git status --porcelain --untracked-files=all)
    if ($script:GitExit -ne 0) { Fail "git status failed in $root." }
    foreach ($line in $status) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $code = $line.Substring(0, 2)
        $path = $line.Substring(3).Trim('"')
        if ($code -eq "??") {
            if ($path -match '^(Assets|Packages|ProjectSettings)/') { $problems += "untracked  $path" }
        } else {
            $problems += "changed    $path"
        }
    }
    return $problems
}

# ---------------------------------------------------------------------------------------- tree

Write-Host "Emberfall Windows playtest build" -ForegroundColor Cyan
Write-Host "  project  $root"

$null = Invoke-Git rev-parse --git-dir
if ($script:GitExit -ne 0) { Fail "$root is not a git working tree." }
foreach ($marker in @("MERGE_HEAD", "REBASE_HEAD", "CHERRY_PICK_HEAD")) {
    $gitPath = (Invoke-Git rev-parse --git-path $marker)
    if ($gitPath -and (Test-Path (Join-Path $root $gitPath))) { Fail "a $marker is in progress. Finish it first." }
}

$before = @(Get-TreeProblems)
if ($before.Count -gt 0) {
    Write-Host ""
    Write-Host "The working tree does not match HEAD:" -ForegroundColor Red
    $before | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    Fail ("commit or remove these first. A build stamped with HEAD's hash would not be the code " +
          "HEAD describes, and a bug report against it would send somebody to the wrong code.")
}

# Ignored files still ship when they sit in a Resources folder. The only one expected is the cloud
# config, which is ignored on purpose (it holds the project URL and key) and decides whether the
# player can reach accounts at all - so it is reported either way rather than silently packed.
$cloud = "absent: the player runs entirely offline, with no accounts, cloud save, friends or guilds"
$ignored = @(Invoke-Git status --porcelain --ignored=matching --untracked-files=all -- Assets |
             Where-Object { $_ -like "!! *" } | ForEach-Object { $_.Substring(3).Trim('"') })
foreach ($path in $ignored) {
    if ($path -notmatch '/Resources/') { continue }
    if ($path -eq "Assets/Resources/Config/supabase.json") {
        $cloud = "PRESENT: the player signs in to the Supabase project in Assets/Resources/Config/supabase.json"
        continue
    }
    if ($path -match '^Assets/Resources/(Config/supabase\.json\.meta|BuildStamp\.txt(\.meta)?)$') { continue }
    Fail "$path is gitignored but sits in a Resources folder, so it would ship without being in the commit."
}

$hash = (Invoke-Git rev-parse --short HEAD)
if ($script:GitExit -ne 0 -or -not $hash) { Fail "could not read HEAD's hash." }
$hash = "$hash".Trim()
$branch = "$(Invoke-Git rev-parse --abbrev-ref HEAD)".Trim()
Write-Host "  commit   $hash on $branch"
Write-Host "  cloud    $cloud"

# A hash nobody else can fetch cannot be traced by anybody else. Loud, not fatal: building before
# pushing is a legitimate order of work, as long as the push happens before the zip leaves.
$remotes = @(Invoke-Git branch -r --contains HEAD)
if ($remotes.Count -eq 0) {
    Write-Host "  WARNING  $hash is on no remote branch. Push it before this build leaves the machine," -ForegroundColor Yellow
    Write-Host "           or a report naming it cannot be traced by anyone else." -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------------------- unity

if (-not $UnityExe) { $UnityExe = $env:EMBERFALL_UNITY_EXE }
if (-not $UnityExe) {
    $versionLine = Get-Content (Join-Path $root "ProjectSettings\ProjectVersion.txt") |
                   Where-Object { $_ -match '^m_EditorVersion:\s*(\S+)' } | Select-Object -First 1
    if (-not $versionLine) { Fail "ProjectSettings/ProjectVersion.txt names no editor version." }
    $editorVersion = ([regex]::Match($versionLine, '^m_EditorVersion:\s*(\S+)')).Groups[1].Value
    $hubRoots = @("C:\Program Files\Unity\Hub\Editor")
    $secondary = Join-Path $env:APPDATA "UnityHub\secondaryInstallPath.json"
    if (Test-Path $secondary) {
        $extra = (Get-Content $secondary -Raw | ConvertFrom-Json)
        if ($extra) { $hubRoots = @("$extra") + $hubRoots }
    }
    foreach ($hubRoot in $hubRoots) {
        $candidate = Join-Path $hubRoot "$editorVersion\Editor\Unity.exe"
        if (Test-Path $candidate) { $UnityExe = $candidate; break }
    }
    if (-not $UnityExe) {
        Fail ("no Unity $editorVersion found under " + ($hubRoots -join " or ") +
              ". Pass -UnityExe or set EMBERFALL_UNITY_EXE.")
    }
}
if (-not (Test-Path $UnityExe)) { Fail "$UnityExe does not exist." }
Write-Host "  unity    $UnityExe"

# The editor holds Temp/UnityLockfile open while the project is loaded. Opening it for writing is
# the only honest test: the file outlives a crashed editor, so its existence proves nothing.
$lock = Join-Path $root "Temp\UnityLockfile"
if (Test-Path $lock) {
    try { $handle = [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None'); $handle.Close() }
    catch { Fail "an editor has this project open. Close it; Unity will not build a project another editor holds." }
}

if (-not $OutputRoot) { $OutputRoot = Join-Path (Split-Path -Parent $root) "Emberfall-Builds" }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if ($OutputRoot.StartsWith($root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    Fail "$OutputRoot is inside the working tree. Pick a folder outside it."
}
New-Item -ItemType Directory -Force $OutputRoot | Out-Null
Write-Host "  output   $OutputRoot"

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$log = Join-Path $OutputRoot "build-$hash-$stamp.log"
$unityArgs = @(
    "-quit", "-batchmode",
    "-projectPath", "`"$root`"",
    "-buildTarget", "Win64",
    "-executeMethod", "Emberfall.EditorTools.EmberfallBuild.BuildWindowsPlaytest",
    "-emberfallOutput", "`"$OutputRoot`"",
    "-emberfallCommit", $hash,
    "-logFile", "`"$log`""
)
Write-Host ""
Write-Host "Building. This takes several minutes; the editor log is $log"
$started = Get-Date
$unity = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
$minutes = [Math]::Round(((Get-Date) - $started).TotalMinutes, 1)
if ($unity.ExitCode -ne 0) {
    Write-Host ""
    if (Test-Path $log) {
        Write-Host "Last lines of the editor log:" -ForegroundColor Red
        Get-Content $log -Tail 40 | ForEach-Object { Write-Host "  $_" }
    }
    Fail "Unity exited with code $($unity.ExitCode) after $minutes min."
}

# ---------------------------------------------------------------------------------------- player

$folders = @(Get-ChildItem $OutputRoot -Directory | Where-Object { $_.Name -like "Emberfall-*-$hash-win" })
if ($folders.Count -ne 1) { Fail "expected one Emberfall-*-$hash-win folder in $OutputRoot, found $($folders.Count)." }
$folder = $folders[0].FullName
$exe = Join-Path $folder "Emberfall.exe"
if (-not (Test-Path $exe)) { Fail "Unity reported success and $exe does not exist." }
$data = Join-Path $folder "Emberfall_Data"

# A development player holds the Dev Arena as a second scene, level1. This one must not.
if (Test-Path (Join-Path $data "level1")) { Fail "the player holds a second scene. The Dev Arena reached a playtest build." }

# The compiler guard is what keeps development-only code out, so read the assembly it produced.
$assembly = Join-Path $data "Managed\Assembly-CSharp.dll"
if (-not (Test-Path $assembly)) { Fail "$assembly is missing." }
$assemblyText = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($assembly))
foreach ($name in @("DevArenaUi", "DevArenaBoard", "DesignGallery", "OpenDevArena", "DebugSlowMotionToggle")) {
    if ($assemblyText.Contains($name)) { Fail "$name is compiled into the player. A development-only guard is missing." }
}
if (-not $assemblyText.Contains("BuildStamp")) { Fail "Game.BuildStamp is not in the player, so it cannot name its build." }

# The stamp is a TextAsset, packed raw into the resources file. Its absence means the player
# will print "unstamped", which defeats the point of this script.
$packed = Join-Path $data "resources.assets"
if (-not (Test-Path $packed)) { Fail "$packed is missing." }
if (-not (Test-FileContains $packed $hash)) { Fail "the commit $hash is not packed into the player, so it would say 'unstamped'." }

# ---------------------------------------------------------------------------------------- after

$after = @(Get-TreeProblems)
if ($after.Count -gt 0) {
    Write-Host ""
    Write-Host "The build changed the working tree:" -ForegroundColor Red
    $after | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    Fail ("the player in $folder was built from files HEAD does not hold, so its hash describes " +
          "something else. Do not hand it out. Commit whatever the editor wrote if it is meant to " +
          "stay (it usually is a setting Unity rewrote), then build again.")
}

$readme = Join-Path $root "Docs\PLAYTEST-README.md"
if (Test-Path $readme) { Copy-Item $readme (Join-Path $folder "READ-ME-FIRST.txt") -Force }

$version = ($folders[0].Name -replace '^Emberfall-', '') -replace "-$hash-win$", ''
$built = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'")
$sizeMb = [Math]::Round(((Get-ChildItem $folder -Recurse -File | Measure-Object Length -Sum).Sum) / 1MB, 1)
$buildInfo = @(
    "Emberfall $version, commit $hash",
    "Branch:  $branch",
    "Built:   $built",
    "Cloud:   $cloud",
    "",
    "The game shows 'BUILD $version ($hash)' in the bottom-left corner of the main screen and at",
    "the bottom of Settings. Put those words in every report."
)
[IO.File]::WriteAllLines((Join-Path $folder "BUILD.txt"), [string[]]$buildInfo, [Text.UTF8Encoding]::new($false))

$zip = ""
if (-not $NoZip) {
    $zip = "$folder.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path $folder -DestinationPath $zip -CompressionLevel Optimal
}

Write-Host ""
Write-Host "BUILT Emberfall $version ($hash) in $minutes min, $sizeMb MB" -ForegroundColor Green
Write-Host "  player   $exe"
if ($zip) { Write-Host "  zip      $zip ($([Math]::Round((Get-Item $zip).Length / 1MB, 1)) MB)" }
Write-Host "  cloud    $cloud"
exit 0
