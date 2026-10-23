# Builds a Windows playtest player: the zip a non-developer unzips and runs.
#
#   powershell -ExecutionPolicy Bypass -File Tools/build-windows.ps1                 # release, offline
#   powershell -ExecutionPolicy Bypass -File Tools/build-windows.ps1 -Development    # development, offline
#   powershell -ExecutionPolicy Bypass -File Tools/build-windows.ps1 -Cloud          # release, cloud
#   ... -OutputRoot D:\Somewhere -NoZip -UnityExe <path>
#   ... -Defines "EMBERFALL_TESTSTORE;EMBERFALL_PLAYLOG"   # extra scripting defines for this build only
#
# It wraps the one Windows build path that already exists, EmberfallBuild.BuildWindows, through
# EmberfallBuild.BuildWindowsPlaytest. It invents no second one. What this script adds is the part
# that makes a build traceable and makes its variant a decision rather than an accident:
#
#   - it REFUSES A DIRTY TREE. The player carries the short hash on screen (Game.BuildStamp), and
#     a build whose hash does not describe its contents is worse than no hash: a report against
#     it sends whoever reads it to the wrong code with total confidence. Tracked changes anywhere
#     fail it, and so do untracked files under Assets, Packages and ProjectSettings, because Unity
#     builds those whether git knows about them or not;
#   - it OWNS THE CLOUD CONFIG. Assets/Resources/Config/supabase.json is gitignored, so whether a
#     player has accounts would otherwise depend on whether that file happened to be sitting in the
#     working copy. Offline is the default: the file is moved out of the project for the build and
#     put back afterwards, on failure and on Ctrl+C as well, and a run that was killed outright is
#     put right by the next run before it does anything else. -Cloud requires the file;
#   - -Development builds the development player, with the Dev Arena. Every other setting is the
#     same, and the variant is in the folder name and on the first line of output;
#   - it passes the hash it checked, so the stamp and the check cannot disagree;
#   - it checks the tree is STILL clean afterwards. The editor writes settings back on its way
#     out, and a build that changed a tracked file was built from something no commit describes;
#   - it checks the player it got: the Dev Arena scene and classes are there exactly when the
#     variant says so, the hash is packed inside, and the cloud project's address is packed inside
#     exactly when the variant says so;
#   - -Defines "A;B" (R27) passes extra scripting defines to this one player build through
#     -emberfallDefines, which EmberfallBuild hands to BuildPlayerOptions.extraScriptingDefines. Nothing
#     is written to ProjectSettings.asset, so the tree stays clean. Absent, the build is exactly as before;
#   - it reports what the player weighs: the folder, the zip, and where the bytes went, from the
#     size report EmberfallBuild writes beside the player.
#
# Mono is EmberfallBuild's decision, not this script's: this editor install has no Windows IL2CPP
# module. The output goes OUTSIDE the working tree by default, for the reason BuildWindowsHandoff
# gives: a player several hundred megabytes big does not belong in a checkout.
#
# Close the editor on this project first. Unity will not open a project another editor holds.

param(
    [string]$OutputRoot = "",
    [string]$UnityExe = "",
    [switch]$Cloud,
    [switch]$Development,
    [switch]$NoZip,
    [string]$Defines = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$flavour = if ($Development) { "development" } else { "release" }
$network = if ($Cloud) { "cloud" } else { "offline" }
$variant = "$flavour-$network"
Write-Host "MODE: $flavour, $network ($variant)" -ForegroundColor Cyan

# The defines, normalised the way BuildDefines.Parse reads them: names of letters, digits and underscores.
$definesList = @()
if ($Defines) {
    $definesList = @($Defines -split '[;,]' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    foreach ($name in $definesList) {
        if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw "BUILD REFUSED: '$name' is not a scripting define name." }
    }
}
$definesText = ($definesList | Select-Object -Unique) -join ";"

# Refusals are thrown, not exited, so the finally block at the bottom always runs: it is what puts
# the cloud config back and stops a Unity that is still building.
function Fail([string]$message) { throw "BUILD REFUSED: $message" }

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
#
# git status names the CANDIDATES; content decides. Status flags a file whose bytes on disk differ
# from what git last checked out, and a line-ending difference is enough: Unity rewrites
# ProjectSettings/*.asset with LF, core.autocrlf checked them out as CRLF, and status called them
# modified with an empty diff. Trusting the flag refused good trees and quarantined good players.
# So a flagged file is a problem only when its content, cleaned the way `git add` would clean it
# (hash-object applies the same eol and text filters), is not the blob HEAD holds. Anything that
# cannot be compared that way - a deletion, a rename, a path HEAD does not have - stays a problem.
function Test-SameAsHead([string]$path) {
    $full = Join-Path $root ($path -replace '/', [char]92)
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { return $false }
    $headBlob = "$(Invoke-Git rev-parse --verify --quiet "HEAD:$path")".Trim()
    if ($script:GitExit -ne 0 -or -not $headBlob) { return $false }
    $diskBlob = "$(Invoke-Git hash-object --path $path -- $full)".Trim()
    if ($script:GitExit -ne 0 -or -not $diskBlob) { return $false }
    return ($diskBlob -eq $headBlob)
}

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
        } elseif ($code -match '^[ MT][ MT]$' -and (Test-SameAsHead $path)) {
            # Flagged, but the content is HEAD's: a line-ending or stat-only difference.
            continue
        } else {
            $problems += "changed    $path"
        }
    }
    return $problems
}

function Get-FolderMb([string]$path) {
    return [Math]::Round(((Get-ChildItem $path -Recurse -File | Measure-Object Length -Sum).Sum) / 1MB, 1)
}

# ------------------------------------------------------------------------------ the cloud config
#
# The stash lives outside every working tree, one per checkout, and survives a reboot. A file in it
# means a run moved the config out and never put it back.
$config = Join-Path $root "Assets\Resources\Config\supabase.json"
$configMeta = "$config.meta"
$stashDir = Join-Path $env:LOCALAPPDATA ("Emberfall-build\cloud-stash\" + (($root -replace '[:\\/]+', '-').Trim('-')))
$stashed = Join-Path $stashDir "supabase.json"
$stashedMeta = Join-Path $stashDir "supabase.json.meta"
$script:ConfigMoved = $false

function Restore-CloudConfig {
    if (-not (Test-Path $stashed)) { return $true }
    if (Test-Path $config) {
        Write-Host "  WARNING  $config exists AND a copy is stashed in $stashDir. Left both alone;" -ForegroundColor Yellow
        Write-Host "           compare them and delete the one you do not want." -ForegroundColor Yellow
        return $false
    }
    Move-Item $stashed $config
    if ((Test-Path $stashedMeta) -and -not (Test-Path $configMeta)) { Move-Item $stashedMeta $configMeta }
    return $true
}

function Stash-CloudConfig {
    New-Item -ItemType Directory -Force $stashDir | Out-Null
    $script:ConfigMoved = $true
    Move-Item $config $stashed
    if (Test-Path $configMeta) { Move-Item $configMeta $stashedMeta -Force }
}

$exitCode = 1
$script:UnityProcess = $null
$script:ExpectedFolder = $null
$succeeded = $false

try {
    # A killed run leaves the config in the stash. Put it back before anything reads the project.
    if (Test-Path $stashed) {
        if (-not (Restore-CloudConfig)) { Fail "the cloud config is both in the project and in the stash." }
        Write-Host "  restored the cloud config a previous run moved out and never put back" -ForegroundColor Yellow
    }

    # ---------------------------------------------------------------------------------------- tree

    Write-Host "Emberfall Windows playtest build"
    Write-Host "  project  $root"

    $null = Invoke-Git rev-parse --git-dir
    if ($script:GitExit -ne 0) { Fail "$root is not a git working tree." }
    # What git leaves while an operation is UNFINISHED. Not REBASE_HEAD: git writes that during a
    # rebase and leaves it behind for days after the rebase completes, so testing for it refused
    # clean trees. A rebase in progress is the rebase-merge directory (the merge backend, and -i)
    # or rebase-apply (the apply backend, and git am).
    #
    # --git-path answers relative to the root in a plain clone but ABSOLUTE in a linked worktree
    # (.git/worktrees/<name>/...), and Join-Path onto an absolute path builds a path that never
    # exists - so in a worktree none of these guards could ever fire. A rooted answer is used as is.
    foreach ($marker in @("MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply")) {
        $gitPath = "$(Invoke-Git rev-parse --git-path $marker)".Trim()
        if (-not $gitPath) { continue }
        if (-not [IO.Path]::IsPathRooted($gitPath)) { $gitPath = Join-Path $root $gitPath }
        if (Test-Path $gitPath) {
            $what = if ($marker -like "rebase-*") { "rebase" } else { $marker }
            Fail "a $what is in progress ($gitPath exists). Finish or abort it first."
        }
    }

    $before = @(Get-TreeProblems)
    if ($before.Count -gt 0) {
        Write-Host ""
        Write-Host "The working tree does not match HEAD:" -ForegroundColor Red
        $before | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        Fail ("commit or remove these first. A build stamped with HEAD's hash would not be the code " +
              "HEAD describes, and a bug report against it would send somebody to the wrong code.")
    }

    # Ignored files still ship when they sit in a Resources folder. The cloud config is handled
    # below; the stamp is EmberfallBuild's own and exists only during a build. Anything else fails.
    $ignored = @(Invoke-Git status --porcelain --ignored=matching --untracked-files=all -- Assets |
                 Where-Object { $_ -like "!! *" } | ForEach-Object { $_.Substring(3).Trim('"') })
    foreach ($path in $ignored) {
        if ($path -notmatch '/Resources/') { continue }
        if ($path -match '^Assets/Resources/(Config/supabase\.json(\.meta)?|BuildStamp\.txt(\.meta)?)$') { continue }
        Fail "$path is gitignored but sits in a Resources folder, so it would ship without being in the commit."
    }

    # The cloud project's address, read from the config before anything moves it, so the player can
    # be searched for it afterwards: present in a cloud build, absent in an offline one.
    $cloudHost = ""
    if (Test-Path $config) {
        $url = "$((Get-Content $config -Raw | ConvertFrom-Json).url)"
        if ($url -match '^https?://([^/]+)') { $cloudHost = $Matches[1] }
    }
    if ($Cloud) {
        if (-not (Test-Path $config)) { Fail "-Cloud needs Assets/Resources/Config/supabase.json, and it is not there." }
        if (-not $cloudHost) { Fail "Assets/Resources/Config/supabase.json has no usable url." }
        $cloudLine = "ON: the player signs in to $cloudHost"
    } else {
        $cloudLine = "OFF: no account service, nothing syncs anywhere"
    }

    $hash = (Invoke-Git rev-parse --short HEAD)
    if ($script:GitExit -ne 0 -or -not $hash) { Fail "could not read HEAD's hash." }
    $hash = "$hash".Trim()
    $branch = "$(Invoke-Git rev-parse --abbrev-ref HEAD)".Trim()
    Write-Host "  commit   $hash on $branch"
    Write-Host "  cloud    $cloudLine"
    if ($definesText) { Write-Host "  defines  $definesText" }

    # A hash nobody else can fetch cannot be traced by anybody else. Loud, not fatal: building
    # before pushing is a legitimate order of work, as long as the push happens before the zip leaves.
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

    # The editor holds Temp/UnityLockfile open while the project is loaded. Opening it for writing
    # is the only honest test: the file outlives a crashed editor, so its existence proves nothing.
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

    # EmberfallBuild.Version names the folder. Read here so a folder left by an earlier run of the
    # same commit and variant is cleared first: Unity writes into an existing folder, and a file it
    # did not write this time would otherwise ride along into the zip.
    $buildSource = Get-Content (Join-Path $root "Assets\Editor\EmberfallBuild.cs") -Raw
    $versionMatch = [regex]::Match($buildSource, 'public const string Version = "([^"]+)"')
    if (-not $versionMatch.Success) { Fail "could not read EmberfallBuild.Version." }
    $version = $versionMatch.Groups[1].Value
    $folderName = "Emberfall-$version-$hash-$variant-win"
    $folder = Join-Path $OutputRoot $folderName
    $script:ExpectedFolder = $folder
    foreach ($old in @($folder, "$folder.zip", "$folder.sizes.txt", "$folder-BurstDebugInformation", "$folder-REFUSED")) {
        if (Test-Path $old) { Remove-Item $old -Recurse -Force }
    }

    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $log = Join-Path $OutputRoot "build-$hash-$variant-$stamp.log"
    $unityArgs = @(
        "-quit", "-batchmode",
        "-projectPath", "`"$root`"",
        "-buildTarget", "Win64",
        "-executeMethod", "Emberfall.EditorTools.EmberfallBuild.BuildWindowsPlaytest",
        "-emberfallOutput", "`"$OutputRoot`"",
        "-emberfallCommit", $hash,
        "-emberfallVariant", $variant,
        "-logFile", "`"$log`""
    )
    if ($definesText) { $unityArgs += @("-emberfallDefines", "`"$definesText`"") }

    # Last thing before Unity starts, so every refusal above leaves the config where it was.
    if (-not $Cloud -and (Test-Path $config)) {
        Stash-CloudConfig
        Write-Host "  moved the cloud config out for this build; it goes back when the run ends"
    }

    Write-Host ""
    Write-Host "Building. This takes a few minutes; the editor log is $log"
    $started = Get-Date
    $script:UnityProcess = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru -NoNewWindow
    # Touching Handle now is what makes ExitCode readable later: without it Windows PowerShell's
    # Process object can report a null exit code for a process it did not wait on through -Wait.
    $null = $script:UnityProcess.Handle
    $script:UnityProcess.WaitForExit()
    $minutes = [Math]::Round(((Get-Date) - $started).TotalMinutes, 1)
    if ($script:UnityProcess.ExitCode -ne 0) {
        Write-Host ""
        if (Test-Path $log) {
            Write-Host "Last lines of the editor log:" -ForegroundColor Red
            Get-Content $log -Tail 40 | ForEach-Object { Write-Host "  $_" }
        }
        Fail "Unity exited with code $($script:UnityProcess.ExitCode) after $minutes min."
    }

    # The config goes back now rather than in the finally, so the after-check below sees the tree
    # as it will be left. The finally still covers every path that never reaches this line.
    if ($script:ConfigMoved) {
        if (-not (Restore-CloudConfig)) { Fail "could not put the cloud config back." }
        $script:ConfigMoved = $false
    }

    # ---------------------------------------------------------------------------------------- player

    $exe = Join-Path $folder "Emberfall.exe"
    if (-not (Test-Path $exe)) { Fail "Unity reported success and $exe does not exist." }
    $data = Join-Path $folder "Emberfall_Data"

    # A development player holds the Dev Arena as a second scene, level1. A release one must not.
    $hasArenaScene = Test-Path (Join-Path $data "level1")
    if ($Development -and -not $hasArenaScene) { Fail "the development player holds no second scene, so the Dev Arena cannot open." }
    if (-not $Development -and $hasArenaScene) { Fail "the release player holds a second scene. The Dev Arena reached a release build." }

    # The compiler guard is what decides whether development-only code is in, so read the assembly
    # it produced.
    $assembly = Join-Path $data "Managed\Assembly-CSharp.dll"
    if (-not (Test-Path $assembly)) { Fail "$assembly is missing." }
    $assemblyText = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($assembly))
    foreach ($name in @("DevArenaUi", "DevArenaBoard", "DesignGallery", "OpenDevArena")) {
        $present = $assemblyText.Contains($name)
        if ($Development -and -not $present) { Fail "$name is missing from the development player." }
        if (-not $Development -and $present) { Fail "$name is compiled into the release player. A development-only guard is missing." }
    }
    if ($assemblyText.Contains("DebugSlowMotionToggle")) { Fail "the editor-only slow-motion key is compiled into a player." }
    if (-not $assemblyText.Contains("BuildStamp")) { Fail "Game.BuildStamp is not in the player, so it cannot name its build." }
    # R27: PlayLogService is compiled only under EMBERFALL_PLAYLOG, so the define decides whether the play log is in.
    $hasPlayLog = $assemblyText.Contains("PlayLogService")
    if (($definesList -contains "EMBERFALL_PLAYLOG") -and -not $hasPlayLog) { Fail "-Defines asked for EMBERFALL_PLAYLOG and the play log is not in the player." }
    if (-not ($definesList -contains "EMBERFALL_PLAYLOG") -and $hasPlayLog) { Fail "the play log is compiled into a player that did not ask for EMBERFALL_PLAYLOG." }

    # The stamp and the cloud config are both TextAssets, packed raw into the resources file.
    $packed = Join-Path $data "resources.assets"
    if (-not (Test-Path $packed)) { Fail "$packed is missing." }
    if (-not (Test-FileContains $packed $hash)) { Fail "the commit $hash is not packed into the player, so it would say 'unstamped'." }
    if ($cloudHost) {
        $hasHost = Test-FileContains $packed $cloudHost
        if ($Cloud -and -not $hasHost) { Fail "a cloud build without $cloudHost packed inside it." }
        if (-not $Cloud -and $hasHost) { Fail "an OFFLINE build with $cloudHost packed inside it. Do not hand it out." }
    }

    # ---------------------------------------------------------------------------------------- after

    $after = @(Get-TreeProblems)
    if ($after.Count -gt 0) {
        Write-Host ""
        Write-Host "The build changed the working tree:" -ForegroundColor Red
        $after | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        Fail ("the player in $folder was built from files HEAD does not hold, so its hash describes " +
              "something else. Commit whatever the editor wrote if it is meant to stay (it usually " +
              "is a setting Unity rewrote), then build again.")
    }

    # Unity writes Burst's debug symbols beside the player and names the folder DoNotShip itself.
    # It is for a developer reading a native crash, not for the person unzipping the game, so it is
    # moved out of the folder being zipped and kept beside it rather than deleted.
    $burst = Join-Path $folder "Emberfall_BurstDebugInformation_DoNotShip"
    if (Test-Path $burst) { Move-Item $burst "$folder-BurstDebugInformation" }

    $readme = Join-Path $root "Docs\PLAYTEST-README.md"
    if (Test-Path $readme) { Copy-Item $readme (Join-Path $folder "READ-ME-FIRST.txt") -Force }

    $label = "BUILD $version ($hash)" + $(if ($Development) { " DEV" } else { "" })
    $built = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'")
    $buildInfo = @(
        "Emberfall $version, commit $hash",
        "Mode:    $flavour, $network",
        "Branch:  $branch",
        "Built:   $built",
        "Cloud:   $cloudLine",
        $(if ($definesText) { "Defines: $definesText" } else { "Defines: (none)" }),
        "",
        "The game shows '$label' in the bottom-left corner of the main screen and at",
        "the bottom of Settings. Put those words in every report."
    )
    [IO.File]::WriteAllLines((Join-Path $folder "BUILD.txt"), [string[]]$buildInfo, [Text.UTF8Encoding]::new($false))

    $folderMb = Get-FolderMb $folder
    $zip = ""
    if (-not $NoZip) {
        $zip = "$folder.zip"
        # Not Compress-Archive: in Windows PowerShell 5.1 it buffers each file in memory and dies
        # with "Stream was too long" above 2 GB, and resources.assets.resS is past 3 GB now.
        # ZipFile streams each file. The folder itself is the zip's top entry, as Compress-Archive
        # -Path <folder> made it.
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::CreateFromDirectory($folder, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
    }

    Write-Host ""
    Write-Host "BUILT Emberfall $version ($hash), $flavour, $network, in $minutes min" -ForegroundColor Green
    Write-Host "  player   $exe"
    $sizeLine = "  sizes    folder $folderMb MB unzipped"
    if ($zip) { $sizeLine += ", zip $([Math]::Round((Get-Item $zip).Length / 1MB, 1)) MB" }
    Write-Host $sizeLine
    $sizes = "$folder.sizes.txt"
    if (Test-Path $sizes) {
        Get-Content $sizes | ForEach-Object { Write-Host "           $_" }
    } else {
        Write-Host "           (no size report: EmberfallBuild could not write $sizes, see the editor log)" -ForegroundColor Yellow
    }
    Write-Host "  cloud    $cloudLine"
    $succeeded = $true
    $exitCode = 0
}
catch {
    Write-Host ""
    Write-Host $_.Exception.Message -ForegroundColor Red
}
finally {
    # Ctrl+C lands here too. A Unity still building would go on packing the project after the
    # config is put back, so it is stopped first, and a player it did not finish is renamed so it
    # cannot be mistaken for a checked one.
    if ($script:UnityProcess -and -not $script:UnityProcess.HasExited) {
        Write-Host "Stopping the Unity build that was still running." -ForegroundColor Yellow
        Stop-Process -Id $script:UnityProcess.Id -Force
        $script:UnityProcess.WaitForExit()
    }
    if ($script:ConfigMoved) {
        if (Restore-CloudConfig) { Write-Host "  put the cloud config back" }
    }
    if (-not $succeeded -and $script:ExpectedFolder -and (Test-Path $script:ExpectedFolder)) {
        if (Test-Path "$($script:ExpectedFolder).zip") { Remove-Item "$($script:ExpectedFolder).zip" -Force }
        Move-Item $script:ExpectedFolder "$($script:ExpectedFolder)-REFUSED"
        Write-Host "  the unchecked player is at $($script:ExpectedFolder)-REFUSED. Do not hand it out." -ForegroundColor Red
    }
}
exit $exitCode
