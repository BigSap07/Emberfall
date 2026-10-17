# Runs a LIST of PlayModeScreenshot targets in ONE Unity editor session and reports one line per target.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/capture-batch.ps1 `
#       -Targets "Screen:MainMenu;Screen:Summon;Screen:Battle" -LogDir D:\Emberfall\handoff\<RUN>\logs -Tag batch1
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/capture-batch.ps1 -TargetsFile targets.txt -LogDir ... -Tag batch2
#
# TESTER RULE: A run captures its targets in batches in one editor session, never one launch per target.
# A single launch is only for retrying a target. Docs/CAPTURE.md has the how-to.
#
# WHAT IT DOES, in order:
#   1. Refuses to start (exit 2, and says what to do) if a Unity.exe already has the project open, or
#      if Temp\__Backupscenes exists. A killed editor leaves that folder behind, and the next editor
#      then stops on a "Recovering Scene Backups" dialog nobody answers.
#   2. Hashes the real local save, copies ProjectSettings.asset aside, notes the first-run EditorPref.
#   3. Starts ONE editor with ScratchCaptures.Run and every target (scratch identity, cloud held off),
#      records its PID, raises its window every few seconds (a backgrounded Game view photographs
#      stale frames), and kills only that PID if it has to.
#   3b. An editor that exits BY ITSELF (any exit code) before ScratchCaptures logs "identity restored" is
#      treated like a hung one: the same cleanup, a 10 s wait, then ONE relaunch with the targets not
#      yet captured. The outer watchdog and this share that one relaunch.
#   4. OUTER WATCHDOG, for an editor whose update loop is dead so the in-editor watchdog cannot run:
#      the log shows "play entry requested", no "play heartbeat" follows it, and the in-editor
#      watchdog printed nothing, for 150 s. The editor is killed by its PID and started ONCE more
#      with the targets not yet captured, the stuck one first, counted as its second attempt.
#      Before that relaunch, Temp\__Backupscenes is MOVED (never deleted) to
#      D:\Emberfall\tmp\backupscenes-r11-<tag>-<n>\ and a ProjectSettings.asset left on the scratch
#      identity is put back from the copy taken in step 2.
#   5. Hashes the real save again, then prints one RESULT line per target and one SUMMARY line.
#
#   RESULT <target> captured
#   RESULT <target> FAILED: <why>
#   RESULT <target> not reached
#   SUMMARY wall=..s targets=.. captured=.. FAILED=.. not_reached=.. play_entries=.. heartbeats=..
#           watchdog_recoveries=.. outer_relaunches=.. launches=.. real_save=unchanged|CHANGED
#
# EMBERFALL_CAPTURE_SKIP_RELOAD=1 runs the session with domain reload off (ScratchCaptures sets and
# restores the Enter Play Mode options); this script also keeps a copy of EditorSettings.asset and puts
# it back if it changed, after the run and after any kill or crash cleanup.
#
# SWITCHES: EMBERFALL_CAPTURE_WATCHDOG=0 switches off the in-editor watchdog AND the outer one (the
# stall and ceiling nets below stay). EMBERFALL_CAPTURE_FORCE_MISS=<target> makes that target's first
# play entry send no heartbeat, to show the recovery on demand.
#
# EXIT CODES  0 every target captured and the real save unchanged;  1 ran, with a FAILED or
# not-reached target or a changed save;  2 refused to start.
#
# It never reads supabase.json, never posts anywhere, and never deletes: files it must clear away are
# moved. It stops only the PID it started.

param(
    [string]$Targets = '',
    [string]$TargetsFile = '',
    [string]$LogDir = '',
    [string]$Tag = '',
    # Wall-clock ceiling for the whole editor session, in minutes.
    [int]$CeilMin = 90,
    # No capture progress for this long, once the queue has started, means the editor is stuck.
    [int]$StallMinutes = 10,
    # Unity creates its -logFile within seconds. None after this long means it never came up.
    [int]$LogStartMinutes = 5,
    # The outer watchdog: seconds with a play entry requested and no heartbeat and no watchdog line.
    [int]$OuterNoBeatSeconds = 150,
    # The outer watchdog after the in-editor watchdog's own Recover line, with nothing after it.
    [int]$OuterRecoverStallSeconds = 120,
    [string]$Project = '',
    [string]$UnityPath = '',
    [string]$BackupRoot = 'D:\Emberfall\tmp',
    [switch]$NoFocus
)

$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($false)
if (-not $Project) { $Project = Split-Path -Parent $PSScriptRoot }
$Project = [IO.Path]::GetFullPath($Project).TrimEnd('\')
$script:Say = ''

function Say([string]$message) {
    $line = '[capture-batch ' + (Get-Date).ToString('HH:mm:ss') + '] ' + $message
    Write-Host $line
    if ($script:Say) { [IO.File]::AppendAllText($script:Say, $line + "`r`n", $utf8) }
}

function Refuse([string]$message) {
    Say ('REFUSED: ' + $message)
    exit 2
}

# ------------------------------------------------------------------------------------ arguments

if (-not $LogDir -or -not $Tag) { Refuse 'both -LogDir and -Tag are required' }
New-Item -ItemType Directory -Force $LogDir | Out-Null
$LogDir = [IO.Path]::GetFullPath($LogDir).TrimEnd('\')
$script:Say = Join-Path $LogDir ('capture-batch-' + $Tag + '.txt')

$raw = $Targets
if (-not $raw -and $TargetsFile) {
    if (-not (Test-Path -LiteralPath $TargetsFile)) { Refuse ('-TargetsFile ' + $TargetsFile + ' does not exist') }
    $raw = (([IO.File]::ReadAllLines($TargetsFile) | Where-Object { $_.Trim() -and -not $_.TrimStart().StartsWith('#') }) -join ';')
}
$targetList = @($raw -split ';' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ($targetList.Count -eq 0) { Refuse 'no targets: pass -Targets "<t1>;<t2>;..." or -TargetsFile <file>' }

# ------------------------------------------------------------------------------------ helpers

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class CaptureFocus
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern void keybd_event(byte k, byte s, uint f, IntPtr e);
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint a, uint b, IntPtr c, uint d);
    [DllImport("user32.dll")] static extern bool SwitchToThisWindow(IntPtr h, bool alt);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);

    // Brings the window to the foreground if it is not already there. The foreground lock is
    // cleared for this session only. With no interactive desktop these calls fail and the result is false.
    public static bool Raise(IntPtr h)
    {
        if (h == IntPtr.Zero) return false;
        if (GetForegroundWindow() == h) return true;
        SystemParametersInfo(0x2001, 0, IntPtr.Zero, 0);
        keybd_event(0x12, 0, 0, IntPtr.Zero);
        keybd_event(0x12, 0, 2, IntPtr.Zero);
        if (IsIconic(h)) ShowWindow(h, 9);
        SwitchToThisWindow(h, true);
        SetForegroundWindow(h);
        return GetForegroundWindow() == h;
    }
}
'@

function Get-FileHashOrAbsent([string]$path) {
    if (-not $path -or -not (Test-Path -LiteralPath $path)) { return 'absent' }
    $sha = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($path)
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
    finally { $stream.Dispose(); $sha.Dispose() }
}

# The real save, found the way Tools/nightly.ps1 finds it: the company and product in
# ProjectSettings.asset, under AppData\LocalLow. Read BEFORE the editor starts, while they are real.
function Get-RealSavePath {
    $settings = Join-Path $Project 'ProjectSettings\ProjectSettings.asset'
    $company = ''; $product = ''
    foreach ($line in [IO.File]::ReadAllLines($settings)) {
        if (-not $company -and $line -match '^\s*companyName:\s*(.+?)\s*$') { $company = $Matches[1] }
        if (-not $product -and $line -match '^\s*productName:\s*(.+?)\s*$') { $product = $Matches[1] }
        if ($company -and $product) { break }
    }
    $fileName = 'emberfall_save.json'
    $svc = Join-Path $Project 'Assets\Scripts\Game\SaveService.cs'
    if (Test-Path -LiteralPath $svc) {
        $m = [regex]::Match([IO.File]::ReadAllText($svc), 'const\s+string\s+FileName\s*=\s*"([^"]+)"')
        if ($m.Success) { $fileName = $m.Groups[1].Value }
    }
    if (-not $company -or -not $product) { throw 'could not read companyName/productName from ProjectSettings.asset' }
    $low = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData\LocalLow'
    return (Join-Path (Join-Path (Join-Path $low $company) $product) $fileName)
}

function Get-CompanyName {
    $settings = Join-Path $Project 'ProjectSettings\ProjectSettings.asset'
    foreach ($line in [IO.File]::ReadAllLines($settings)) {
        if ($line -match '^\s*companyName:\s*(.+?)\s*$') { return $Matches[1] }
    }
    return ''
}

function Get-UnityExecutable([string]$projectVersion) {
    if ($UnityPath) { if (Test-Path -LiteralPath $UnityPath) { return $UnityPath } else { return '' } }
    $candidates = @()
    $secondary = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
    if (Test-Path -LiteralPath $secondary) {
        $m = [regex]::Match([IO.File]::ReadAllText($secondary), '"((?:\\.|[^"\\])*)"')
        if ($m.Success) {
            $base = $m.Groups[1].Value.Replace('\\', '\')
            $candidates += (Join-Path $base ($projectVersion + '\Editor\Unity.exe'))
        }
    }
    $candidates += ('C:\Program Files\Unity\Hub\Editor\' + $projectVersion + '\Editor\Unity.exe')
    $candidates += ('D:\Unity\Unity Hub\' + $projectVersion + '\Editor\Unity.exe')
    foreach ($c in $candidates) { if (Test-Path -LiteralPath $c) { return $c } }
    return ''
}

# Unity.exe processes whose command line names this project, as PIDs.
function Get-UnityOnProject {
    $ids = @()
    $back = [regex]::Escape($Project)
    $fwd = [regex]::Escape($Project.Replace('\', '/'))
    $pattern = '(?i)(?:' + $back + '|' + $fwd + ')(?=[\\/"''\s]|$)'
    try {
        foreach ($p in @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'")) {
            if ($p.CommandLine -and ($p.CommandLine -match $pattern)) { $ids += [int]$p.ProcessId }
        }
    } catch { }
    return $ids
}

# THE ONE WAY THIS SCRIPT READS THE EDITOR LOG. Unity holds its log open for writing, and keeps it open
# for a moment after the editor exits, so a plain ReadAllLines/ReadAllText/Get-Content (which ask for
# read-only sharing) is refused with "being used by another process". This opens it with
# FileShare.ReadWrite and tries again a few times, a second apart. Returns '' when the file does not
# exist and $null when it still could not be read after every try.
function Read-SharedText([string]$path, [int]$tries = 1) {
    if (-not (Test-Path -LiteralPath $path)) { return '' }
    for ($i = 1; $i -le $tries; $i++) {
        try {
            $fs = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
            try { $sr = New-Object IO.StreamReader($fs); return $sr.ReadToEnd() } finally { $fs.Dispose() }
        } catch {
            if ($i -lt $tries) { Start-Sleep -Seconds 1 }
        }
    }
    return $null
}

# After the editor has exited: wait (never kill) until its PID, and anything else naming the project, is
# really gone, so the log is closed before it is read for the last time. At most 20 s.
function Wait-EditorGone($proc) {
    for ($i = 0; $i -lt 20; $i++) {
        $alive = -not $proc.HasExited
        if (-not $alive -and @(Get-UnityOnProject).Count -eq 0) { return }
        Start-Sleep -Seconds 1
    }
    Say ('WARNING: Unity PID ' + $proc.Id + ' or a Unity process on the project was still alive 20 s after the editor ended; reading its log anyway')
}

function Test-InteractiveDesktop {
    if (-not [Environment]::UserInteractive) { return $false }
    $session = [System.Diagnostics.Process]::GetCurrentProcess().SessionId
    if ($session -eq 0) { return $false }
    return [bool](Get-Process -Name explorer -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $session })
}

# The EditorPref ScratchCaptures flips for its run (stored in the registry with a hash suffix).
$script:PrefRegPath = 'HKCU:\Software\Unity Technologies\Unity Editor 5.x'
function Get-FirstRunPref {
    $state = [pscustomobject]@{ Name = ''; Existed = $false; Value = 0 }
    try {
        $key = Get-Item -LiteralPath $script:PrefRegPath -ErrorAction Stop
        foreach ($n in $key.GetValueNames()) {
            if ($n -like 'Emberfall.SuppressFirstRun_h*') { $state.Name = $n; $state.Existed = $true; $state.Value = $key.GetValue($n) }
        }
    } catch { }
    return $state
}

function Restore-FirstRunPref($before) {
    # Not while another editor is running: that one may legitimately have the pref set for its own run.
    if (@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count -gt 0) { return }
    $now = Get-FirstRunPref
    try {
        if ($before.Existed) {
            if (-not $now.Existed -or $now.Value -ne $before.Value) {
                Set-ItemProperty -LiteralPath $script:PrefRegPath -Name $before.Name -Value $before.Value -Type DWord
                Say ('restored EditorPref ' + $before.Name + ' to ' + $before.Value)
            }
        } elseif ($now.Existed -and $now.Value -ne 0) {
            Set-ItemProperty -LiteralPath $script:PrefRegPath -Name $now.Name -Value 0 -Type DWord
            Say ('reset EditorPref ' + $now.Name + ' to 0 (it was absent before this run)')
        }
    } catch { Say ('could not restore the Emberfall.SuppressFirstRun EditorPref: ' + $_.Exception.Message) }
}

# Moves a file or folder to a name that is not taken. Never deletes.
function Move-Aside([string]$path, [string]$destination) {
    $dest = $destination
    $i = 1
    while (Test-Path -LiteralPath $dest) { $dest = $destination + '-' + $i; $i++ }
    try {
        New-Item -ItemType Directory -Force (Split-Path -Parent $dest) | Out-Null
        Move-Item -LiteralPath $path -Destination $dest
    } catch {
        Say ('could not move ' + $path + ' to ' + $dest + ': ' + $_.Exception.Message)
        return ''
    }
    return $dest
}

# ------------------------------------------------------------------------------------ preflight

$version = ''
$pv = Join-Path $Project 'ProjectSettings\ProjectVersion.txt'
if (Test-Path -LiteralPath $pv) {
    $m = [regex]::Match([IO.File]::ReadAllText($pv), 'm_EditorVersion:\s*(\S+)')
    if ($m.Success) { $version = $m.Groups[1].Value }
}
$unity = Get-UnityExecutable $version
if (-not $unity) { Refuse ('Unity ' + $version + ' was not found (pass -UnityPath)') }

$open = @(Get-UnityOnProject)
if ($open.Count -gt 0) {
    Refuse ('Unity.exe PID ' + ($open -join ', ') + ' already has ' + $Project + ' open. Close it (only if it is yours), then run again.')
}
$backupScenes = Join-Path $Project 'Temp\__Backupscenes'
if (Test-Path -LiteralPath $backupScenes) {
    Refuse ($backupScenes + ' exists: a killed editor left it, and a new editor would stop on the "Recovering Scene Backups" dialog. Move it away (do not delete), then run again: ' +
            'Move-Item -LiteralPath "' + $backupScenes + '" -Destination "' + $BackupRoot + '\backupscenes-r11-' + $Tag + '-0"')
}
if (-not (Test-InteractiveDesktop)) {
    Say 'WARNING: no interactive desktop in this session. The editor may never open a window, and the Game view will not repaint.'
}

$savePath = Get-RealSavePath
$saveBefore = Get-FileHashOrAbsent $savePath
$settingsFile = Join-Path $Project 'ProjectSettings\ProjectSettings.asset'
$settingsBackup = Join-Path $LogDir ('ProjectSettings.asset.' + $Tag + '.before')
Copy-Item -LiteralPath $settingsFile -Destination $settingsBackup -Force
$settingsBefore = Get-FileHashOrAbsent $settingsFile
# EditorSettings.asset holds the Enter Play Mode options. EMBERFALL_CAPTURE_SKIP_RELOAD=1 changes them
# for one session; they must never be left changed, so the file is copied aside like the one above.
$editorSettingsFile = Join-Path $Project 'ProjectSettings\EditorSettings.asset'
$editorSettingsBackup = Join-Path $LogDir ('EditorSettings.asset.' + $Tag + '.before')
$editorSettingsBefore = Get-FileHashOrAbsent $editorSettingsFile
if (Test-Path -LiteralPath $editorSettingsFile) { Copy-Item -LiteralPath $editorSettingsFile -Destination $editorSettingsBackup -Force }
if ($env:EMBERFALL_CAPTURE_SKIP_RELOAD -and $env:EMBERFALL_CAPTURE_SKIP_RELOAD.Trim() -eq '1') {
    Say 'EMBERFALL_CAPTURE_SKIP_RELOAD=1: this session enters Play without a domain reload (the project setting is restored afterwards)'
}

# Puts EditorSettings.asset back from the copy, only if it changed. Returns $true when it had to.
function Restore-EditorSettings {
    if (-not (Test-Path -LiteralPath $editorSettingsBackup)) { return $false }
    if ((Get-FileHashOrAbsent $editorSettingsFile) -eq $editorSettingsBefore) { return $false }
    Copy-Item -LiteralPath $editorSettingsBackup -Destination $editorSettingsFile -Force
    Say 'ProjectSettings\EditorSettings.asset had changed; the copy taken before launch 1 was put back'
    return $true
}
$realCompany = Get-CompanyName
$pref = Get-FirstRunPref
$shots = Join-Path $Project 'Tools\Screenshots'
New-Item -ItemType Directory -Force $shots | Out-Null
$trigger = Join-Path $shots '.trigger'
if (Test-Path -LiteralPath $trigger) {
    $moved = Move-Aside $trigger (Join-Path $LogDir ('stale-trigger-' + $Tag + '.txt'))
    Say ('a stale Tools\Screenshots\.trigger was moved to ' + $moved)
}

Say ('project ' + $Project + ', Unity ' + $unity)
Say ('real save ' + $savePath + ' hash before ' + $saveBefore)
Say ('targets (' + $targetList.Count + '): ' + ($targetList -join ';'))

# ------------------------------------------------------------------------------------ one launch

$began = Get-Date
$logs = @()          # one entry per launch: Log path, Offset into $targetList, Reason it ended
$relaunches = 0

function Start-Editor([string[]]$queue, [int]$launch) {
    $log = Join-Path $LogDir ('capture-batch-' + $Tag + '-launch' + $launch + '.log')
    $argText = '-projectPath "' + $Project + '" -executeMethod Emberfall.EditorTools.ScratchCaptures.Run -captures "' + ($queue -join ';') + '" -logFile "' + $log + '"'
    Say ('launch ' + $launch + ': ' + $queue.Count + ' target(s)')
    $proc = Start-Process -FilePath $unity -ArgumentList $argText -PassThru
    $null = $proc.Handle
    Say ('launch ' + $launch + ': Unity PID ' + $proc.Id + ', log ' + $log)
    [IO.File]::AppendAllText((Join-Path $LogDir 'pids.txt'), ('PID ' + $proc.Id + ' capture-batch ' + $Tag + ' launch ' + $launch + ' ' + (Get-Date).ToString('s') + "`r`n"), $utf8)
    return [pscustomobject]@{ Proc = $proc; Log = $log }
}

# Watches one editor until it exits or something is wrong. Returns '' when it exited by itself, else
# the reason it should be stopped: ceiling, no-log, stall or outer-hang.
function Watch-Editor($proc, [string]$log) {
    $started = Get-Date
    $deadline = $started.AddMinutes($CeilMin)
    $outerOff = ($env:EMBERFALL_CAPTURE_WATCHDOG -and $env:EMBERFALL_CAPTURE_WATCHDOG.Trim() -eq '0')
    $logSeen = $false
    $runStarted = $false
    $tick = 0
    $markCount = -1; $markAt = Get-Date; $lastMark = ''
    $progress = -1; $progressAt = Get-Date
    while (-not $proc.WaitForExit(3000)) {
        $tick++
        # The Game view only repaints while the editor holds the foreground. By PID, this editor only.
        if (-not $NoFocus) { try { $proc.Refresh(); [void][CaptureFocus]::Raise($proc.MainWindowHandle) } catch { } }
        $now = Get-Date
        if ($now -gt $deadline) { return 'ceiling' }
        if (-not $logSeen) {
            if (Test-Path -LiteralPath $log) { $logSeen = $true }
            elseif (($now - $started).TotalMinutes -ge $LogStartMinutes) { return 'no-log' }
        }
        if ($tick % 2 -ne 0) { continue }
        $text = Read-SharedText $log
        if (-not $text) { continue }

        # Outer watchdog. Every line that says what the play entry is doing is a marker. The last
        # marker, and how long it has been the last, is the whole decision.
        $marks = [regex]::Matches($text, 'PlayModeScreenshot: (?:play entry requested|play heartbeat,|watchdog RECOVER|watchdog re-entering|watchdog gives up|FAILED )|ScratchCaptures: triggered')
        if ($marks.Count -ne $markCount) {
            $markCount = $marks.Count; $markAt = $now
            $lastMark = if ($marks.Count -gt 0) { $marks[$marks.Count - 1].Value } else { '' }
        }
        if (-not $outerOff) {
            $age = ($now - $markAt).TotalSeconds
            if ($lastMark -like '*play entry requested' -and $age -ge $OuterNoBeatSeconds) {
                Say ('outer watchdog: a play entry was requested ' + [int]$age + ' s ago, no heartbeat followed and the in-editor watchdog printed nothing')
                return 'outer-hang'
            }
            if (($lastMark -like '*watchdog RECOVER' -or $lastMark -like '*watchdog re-entering') -and $age -ge $OuterRecoverStallSeconds) {
                Say ('outer watchdog: the in-editor watchdog printed "' + $lastMark + '" ' + [int]$age + ' s ago and nothing since')
                return 'outer-hang'
            }
        }

        # Stall net: once the queue has started, no capture progress for StallMinutes.
        if (-not $runStarted -and $text.Contains('ScratchCaptures: scratch identity')) { $runStarted = $true; $progressAt = $now }
        if ($runStarted) {
            $p = ([regex]::Matches($text, 'ScratchCaptures: triggered|PlayModeScreenshot: requested')).Count
            if ($p -ne $progress) { $progress = $p; $progressAt = $now }
            elseif (($now - $progressAt).TotalMinutes -ge $StallMinutes) { return 'stall' }
        }
    }
    return ''
}

function Stop-Editor($proc, [string]$why) {
    Say ('stopping Unity PID ' + $proc.Id + ' by PID (' + $why + ')')
    try { Stop-Process -Id $proc.Id -Force -ErrorAction Stop } catch { Say ('could not stop PID ' + $proc.Id + ': ' + $_.Exception.Message) }
    [void]$proc.WaitForExit(30000)
}

# After a kill (before a relaunch, or at the end of the batch): what a dead editor leaves that would
# trip the next one.
function Clear-AfterKill([int]$launch) {
    Start-Sleep -Seconds 3
    for ($i = 0; $i -lt 5 -and @(Get-UnityOnProject).Count -gt 0; $i++) { Start-Sleep -Seconds 3 }
    $left = @(Get-UnityOnProject)
    if ($left.Count -gt 0) { Say ('WARNING: Unity.exe PID ' + ($left -join ', ') + ' still names the project after the kill (not stopped: not started by this script)') }

    if (Test-Path -LiteralPath $backupScenes) {
        $moved = Move-Aside $backupScenes (Join-Path $BackupRoot ('backupscenes-r11-' + $Tag + '-' + $launch))
        Say ('Temp\__Backupscenes moved (not deleted) to ' + $moved)
    }
    if (Test-Path -LiteralPath $trigger) {
        $moved = Move-Aside $trigger (Join-Path $LogDir ('stale-trigger-' + $Tag + '-' + $launch + '.txt'))
        Say ('a stale .trigger moved to ' + $moved)
    }
    # A killed run can leave the scratch company in ProjectSettings.asset. A relaunch would then
    # record THAT as the real identity and restore to it. The copy taken before launch 1 goes back.
    $now = Get-CompanyName
    if ($now -ne $realCompany) {
        Copy-Item -LiteralPath $settingsBackup -Destination $settingsFile -Force
        Say ('ProjectSettings.asset held company "' + $now + '"; the copy taken before launch 1 was put back (company "' + $realCompany + '")')
    }
    [void](Restore-EditorSettings)
    Restore-FirstRunPref $pref
    # A relaunch over a leftover Temp\__Backupscenes would stop on the recovery dialog.
    return (-not (Test-Path -LiteralPath $backupScenes))
}

# ------------------------------------------------------------------------------------ the session

$offset = 0
$queue = @($targetList)
$launch = 1
$retryFirst = $false
while ($true) {
    if ($retryFirst) {
        [Environment]::SetEnvironmentVariable('EMBERFALL_CAPTURE_FIRST_ATTEMPT', '2', 'Process')
    } else {
        [Environment]::SetEnvironmentVariable('EMBERFALL_CAPTURE_FIRST_ATTEMPT', $null, 'Process')
    }
    $editor = Start-Editor $queue $launch
    $reason = Watch-Editor $editor.Proc $editor.Log
    if ($reason) { Stop-Editor $editor.Proc $reason }
    $exitCode = if ($editor.Proc.HasExited) { $editor.Proc.ExitCode } else { $null }
    Wait-EditorGone $editor.Proc
    $text = Read-SharedText $editor.Log 5
    # ScratchCaptures logs "identity restored" when its queue is done and the real identity is back.
    # A log that could not be read at all says nothing about that, so it never counts as an early exit.
    $unreadable = ($null -eq $text)
    if ($unreadable) { Say ('WARNING: could not read ' + $editor.Log + ' after 5 tries; not treating this launch as an early exit'); $text = '' }
    $finished = ($text -match 'ScratchCaptures: identity restored')
    $logs += [pscustomobject]@{ Log = $editor.Log; Offset = $offset; Reason = $reason; Exit = $exitCode; Launch = $launch; Finished = ($finished -or $unreadable) }
    Say ('launch ' + $launch + ' ended: ' + $(if ($reason) { $reason } else { 'exited by itself, code ' + $exitCode }))

    # Two reasons to relaunch, sharing ONE relaunch for the whole batch: the outer watchdog killed a
    # hung editor, or the editor exited by itself (any code) before ScratchCaptures finished its queue.
    $early = ((-not $reason) -and (-not $finished) -and (-not $unreadable))
    if (($reason -ne 'outer-hang' -and -not $early) -or $launch -ge 2) { break }

    $triggered = ([regex]::Matches($text, "ScratchCaptures: triggered '")).Count
    if ($reason -eq 'outer-hang') {
        if ($triggered -lt 1) { Say 'the log shows no triggered target, so there is nothing to relaunch from'; break }
        $stuck = $offset + $triggered - 1
        $retryFirst = $true
    } elseif ($triggered -lt 1) {
        $stuck = $offset
        $retryFirst = $false
    } else {
        # The last triggered target is unfinished unless the log shows its PNG or its FAILED line.
        $tail = $text.Substring($text.LastIndexOf("ScratchCaptures: triggered '"))
        $lastDone = ($tail -match 'PlayModeScreenshot: requested \S+\.png') -or ($tail -match 'PlayModeScreenshot: FAILED ')
        if ($lastDone) { $stuck = $offset + $triggered; $retryFirst = $false }
        else { $stuck = $offset + $triggered - 1; $retryFirst = $true }
    }
    if ($stuck -ge $targetList.Count) { Say 'the editor exited early, but every target had been reached'; break }
    $left = $targetList.Count - $stuck
    if ($early) { Say ('editor exited by itself with ' + $left + ' targets left: relaunching once') }
    if (-not (Clear-AfterKill $launch)) { Say 'Temp\__Backupscenes could not be moved aside, so the editor is not relaunched'; break }
    # A new editor started seconds after the last one showed licensing initialisation lines and then quit
    # (the single-launch run that exited early); give the machine time before the next one.
    Start-Sleep -Seconds 10
    $queue = @($targetList[$stuck..($targetList.Count - 1)])
    $offset = $stuck
    $launch++
    $relaunches++
    Say ('relaunching once with ' + $queue.Count + ' target(s), starting at ' + $targetList[$stuck] + $(if ($retryFirst) { ' (the stuck one, its second attempt)' } else { '' }))
}
[Environment]::SetEnvironmentVariable('EMBERFALL_CAPTURE_FIRST_ATTEMPT', $null, 'Process')
# The last editor was stopped by this script, or exited by itself with a nonzero code (a crash): leave
# the project as a normal run would, so the next batch does not refuse or stop on the recovery dialog.
if ($logs.Count -gt 0) {
    $lastLaunch = $logs[$logs.Count - 1]
    $crashed = (-not $lastLaunch.Reason) -and ($null -ne $lastLaunch.Exit) -and ($lastLaunch.Exit -ne 0)
    $unfinished = (-not $lastLaunch.Reason) -and (-not $lastLaunch.Finished)
    if ($lastLaunch.Reason -or $crashed -or $unfinished) {
        if ($crashed) { Say ('launch ' + $lastLaunch.Launch + ' exited with code ' + $lastLaunch.Exit + ': running the same cleanup as after a kill') }
        elseif ($unfinished) { Say ('launch ' + $lastLaunch.Launch + ' exited by itself before its queue finished: running the same cleanup as after a kill') }
        [void](Clear-AfterKill $lastLaunch.Launch)
    }
}
$wall = [int]((Get-Date) - $began).TotalSeconds

# Anything of the editor that outlived it is reported, not stopped: only the PID this script started is its to stop.
Start-Sleep -Seconds 3
$left = @(Get-UnityOnProject)
if ($left.Count -gt 0) { Say ('WARNING: Unity.exe PID ' + ($left -join ', ') + ' still names the project') }
Restore-FirstRunPref $pref

# ------------------------------------------------------------------------------------ the results

$saveAfter = Get-FileHashOrAbsent $savePath
$saveSame = ($saveBefore -eq $saveAfter)
$settingsAfter = Get-FileHashOrAbsent $settingsFile
$editorSettingsRestored = Restore-EditorSettings
$editorSettingsNote = if ($editorSettingsRestored) { 'had CHANGED during the run and was restored from the copy taken before launch 1' } else { 'unchanged' }
$settingsNote = if ($settingsAfter -eq $settingsBefore) { 'unchanged' } else { 'DIFFERENT (company "' + (Get-CompanyName) + '"; check git diff ProjectSettings/ProjectSettings.asset)' }

$r = @()
for ($i = 0; $i -lt $targetList.Count; $i++) {
    $r += [pscustomobject]@{ Target = $targetList[$i]; Triggered = $false; Files = @(); Fails = @(); WdFailed = $false; Recoveries = 0; Hung = $false }
}
$entries = 0; $beats = 0; $recoveries = 0; $batchFails = @()
foreach ($l in $logs) {
    if (-not (Test-Path -LiteralPath $l.Log)) { continue }
    $cur = -1
    $n = 0
    $content = Read-SharedText $l.Log 5
    if ($null -eq $content) {
        $out += ('NOTE launch ' + $l.Launch + ' log ' + $l.Log + ' could not be read; its targets are reported from nothing')
        $content = ''
    }
    foreach ($line in ($content -split "\r?\n")) {
        if ($line -match "ScratchCaptures: triggered '") {
            $cur = $l.Offset + $n; $n++
            if ($cur -lt $r.Count) { $r[$cur].Triggered = $true }
            continue
        }
        if ($line -match 'ScratchCaptures: \[FAIL\]|ScratchCaptures: identity restored') {
            if ($line -match '\[FAIL\]') { $batchFails += $line.Trim() }
            $cur = -1; continue
        }
        if ($line -match 'PlayModeScreenshot: play entry requested') { $entries++; continue }
        if ($line -match 'PlayModeScreenshot: play heartbeat,') { $beats++; continue }
        if ($line -match 'PlayModeScreenshot: watchdog RECOVER') {
            $recoveries++
            if ($cur -ge 0 -and $cur -lt $r.Count) { $r[$cur].Recoveries++ }
            continue
        }
        if ($cur -lt 0 -or $cur -ge $r.Count) { continue }
        if ($line -match 'PlayModeScreenshot: FAILED \S+ after a missed heartbeat') { $r[$cur].WdFailed = $true; continue }
        if ($line -match 'PlayModeScreenshot: requested (\S+\.png)') { $r[$cur].Files += $Matches[1]; continue }
        if ($line -match '\[FAIL\]') { $r[$cur].Fails += $line.Trim() }
    }
    # A launch the outer watchdog killed: its last triggered target is the one that hung.
    if ($l.Reason -eq 'outer-hang' -and $n -ge 1) { $r[$l.Offset + $n - 1].Hung = $true }
}

# A target the relaunch ran again is judged by the relaunch alone.
$fresh = $began.AddSeconds(-1)
$captured = 0; $failed = 0; $notReached = 0
$out = @()
for ($i = 0; $i -lt $r.Count; $i++) {
    $t = $r[$i]
    $line = ''
    if (-not $t.Triggered) { $line = 'RESULT ' + $t.Target + ' not reached'; $notReached++ }
    else {
        $files = @($t.Files | Select-Object -Unique)
        $stale = @($files | Where-Object {
            $p = Join-Path $shots $_
            -not (Test-Path -LiteralPath $p) -or (Get-Item -LiteralPath $p).LastWriteTime -lt $fresh
        })
        $why = ''
        if ($t.WdFailed) { $why = 'the watchdog marked it FAILED after a missed heartbeat on two play entries' }
        elseif ($files.Count -eq 0) {
            if ($t.Hung) { $why = 'the editor hung at its play entry and was killed (no relaunch could finish it)' }
            else { $why = 'no PNG was requested in the log' }
        }
        elseif ($stale.Count -gt 0) { $why = 'PNG missing or not fresh: ' + ($stale -join ', ') }
        elseif ($t.Fails.Count -gt 0) { $why = 'PNG written but the log has ' + $t.Fails.Count + ' [FAIL] line(s), first: ' + $t.Fails[0] }
        if ($why) { $line = 'RESULT ' + $t.Target + ' FAILED: ' + $why; $failed++ }
        else {
            $line = 'RESULT ' + $t.Target + ' captured'
            if ($t.Recoveries -gt 0) { $line += ' (after ' + $t.Recoveries + ' watchdog recovery)' }
            $captured++
        }
    }
    $out += $line
}
foreach ($b in $batchFails) { $out += ('NOTE ' + $b) }
foreach ($l in $logs) {
    if ($l.Reason) { $out += ('NOTE launch ' + $l.Launch + ' was stopped by this script: ' + $l.Reason) }
    elseif ($l.Exit -ne 0) { $out += ('NOTE launch ' + $l.Launch + ' exited with code ' + $l.Exit) }
    elseif (-not $l.Finished) { $out += ('NOTE launch ' + $l.Launch + ' exited by itself (code ' + $l.Exit + ') before ScratchCaptures finished its queue; see the "editor QUIT" lines in its log') }
}
$out += ('NOTE ProjectSettings.asset ' + $settingsNote)
$out += ('NOTE EditorSettings.asset ' + $editorSettingsNote)
$out += ('SUMMARY wall=' + $wall + 's targets=' + $r.Count + ' captured=' + $captured + ' FAILED=' + $failed + ' not_reached=' + $notReached +
         ' play_entries=' + $entries + ' heartbeats=' + $beats + ' watchdog_recoveries=' + $recoveries + ' outer_relaunches=' + $relaunches +
         ' launches=' + $logs.Count + ' real_save=' + $(if ($saveSame) { 'unchanged' } else { 'CHANGED' }))
foreach ($o in $out) { Say $o }
[IO.File]::WriteAllLines((Join-Path $LogDir ('capture-batch-' + $Tag + '-result.txt')), [string[]]$out, $utf8)

if (-not $saveSame) { Say ('THE REAL SAVE CHANGED: before ' + $saveBefore + ' after ' + $saveAfter) }
if ($saveSame -and $failed -eq 0 -and $notReached -eq 0) { exit 0 }
exit 1
