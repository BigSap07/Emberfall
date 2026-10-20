# Unattended nightly health run. The owner gets D:\Emberfall\nightly\<date>\REPORT.md every morning
# without starting an agent.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/nightly.ps1
#
# WHAT IT DOES, in order:
#   1. Takes a lock, so two nightly runs cannot overlap.
#   2. Works ONLY in its own worktree (default D:\Emberfall\Game-nightly), created detached at
#      origin/main if missing. No other checkout is touched: agents work in those.
#   3. REFUSES to run, and says why in the report, if a Unity editor has that worktree open. This is
#      checked before anything in the worktree is changed.
#   4. Fetches, force-checks-out origin/main detached, hard-resets, and cleans untracked files in
#      that worktree only. `git clean` is run WITHOUT -x on purpose: Library/ is gitignored, and
#      wiping it would turn every night into a full asset reimport.
#   5. Hashes the real local save, runs Tools/build-check.ps1 (which writes STATUS.md) and
#      Tools/HeroValidator/validate_heroes.py, then runs a capture sweep of every PlayModeScreenshot
#      target at 1920x1080 through ScratchCaptures (scratch identity, forced-offline), then hashes
#      the real save again and flags any change.
#   6. Writes everything to <NightlyRoot>\<date>\ with a REPORT.md that diffs against the previous
#      night, keeps the last 14 nights, and exits non-zero if there are new failures.
#
# EXIT CODES
#   0  ran; nothing new is failing
#   1  ran; there are NEW failures against the previous night, or the real save changed, or the run
#      itself broke part-way
#   2  refused to run (an editor has the worktree open, another nightly is running, the worktree
#      could not be prepared)
#
# "NEW" means: not failing the previous night that has a results.json. The first night has nothing
# to diff against, so its build-check and validator failures are recorded as the baseline and do
# not by themselves fail the run. A changed real save, or a broken run, always does.
#
# WHAT IT NEVER DOES: print, copy or read supabase.json (it only tests whether the file EXISTS in
# the nightly worktree); make any network call other than `git fetch origin` (Unity's own package
# and licensing traffic is Unity's, not this script's); post to Discord; touch any other worktree;
# kill any process it did not start, and then only by PID.

param(
    [string]$NightlyRoot = 'D:\Emberfall\nightly',
    [string]$Worktree = 'D:\Emberfall\Game-nightly',
    # Empty means: find it from the Unity Hub's install path and the project's ProjectVersion.txt.
    [string]$UnityPath = '',
    # yyyy-MM-dd; today when empty. Only for re-running or testing a given day.
    [string]$Date = '',
    [int]$Keep = 14,
    # Wall-clock ceiling for the Unity capture run. Raised automatically when Library/ is missing,
    # because a first open imports the whole project.
    [int]$CaptureTimeoutMinutes = 90,
    # No new capture for this long, once the capture queue has started, means the editor is stuck.
    [int]$StallMinutes = 10,
    # Unity creates its -logFile within seconds of starting. None after this long means the editor
    # never came up (no desktop to open a window on, a licence dialog nobody can answer).
    [int]$LogStartMinutes = 5,
    # A capture whose pixels differ from the previous night's by at least this percentage is listed
    # as a notable change.
    [double]$ChangeThresholdPercent = 3.0,
    [switch]$SkipCaptures,
    # Do not raise the editor to the foreground during the sweep (it is raised by default, because
    # a backgrounded Game view photographs stale frames).
    [switch]$NoFocus
)

$ErrorActionPreference = 'Stop'
$env:GIT_TERMINAL_PROMPT = '0'
$env:PYTHONIOENCODING = 'utf-8'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $Date) { $Date = (Get-Date).ToString('yyyy-MM-dd') }
$NightlyRoot = [IO.Path]::GetFullPath($NightlyRoot).TrimEnd('\')
$Worktree = [IO.Path]::GetFullPath($Worktree).TrimEnd('\')
$night = Join-Path $NightlyRoot $Date
$utf8 = New-Object System.Text.UTF8Encoding($false)
$script:LogFile = ''
$startedAt = Get-Date

$R = [ordered]@{
    date = $Date
    status = 'aborted'
    started = $startedAt.ToString('yyyy-MM-dd HH:mm:ss')
    finished = ''
    fatal = ''
    refusal = @()
    worktree = $Worktree
    head = ''
    headSubject = ''
    originMain = ''
    fetchOk = $false
    fetchNote = ''
    unityPath = ''
    supabaseJsonPresent = $false
    buildCheck = [ordered]@{ ran = $false; exit = $null; failures = @(); statusMd = $false; note = '' }
    fingerprints = [ordered]@{}
    validator = [ordered]@{ ran = $false; exit = $null; checked = 0; fail = 0; warn = 0; info = 0; findings = @(); note = '' }
    captures = [ordered]@{
        ran = $false; expected = @(); targets = @(); files = [ordered]@{}; missing = @(); extras = @(); identical = @()
        unityExit = $null; timedOut = $false; elapsedMinutes = 0; notes = @(); logFailLines = @()
        unityStarted = ''; unityEnded = ''
    }
    realSave = [ordered]@{
        path = ''; before = ''; after = ''; changed = $false
        lastWrite = ''; playerLogWrite = ''; gameProcesses = @()
    }
    previousNight = ''
    newFailures = @()
    clearedFailures = @()
    exitCode = 1
}

# ------------------------------------------------------------------------------------------ helpers

function Say([string]$message) {
    $line = '[nightly ' + (Get-Date).ToString('HH:mm:ss') + '] ' + $message
    Write-Host $line
    if ($script:LogFile) { [IO.File]::AppendAllText($script:LogFile, $line + "`r`n", $utf8) }
}

# Git with stderr folded in and the verdict taken from the exit code. Not named Git: PowerShell
# resolves a function before an executable, so a function called Git that runs git calls itself.
function Invoke-NightlyGit([string]$dir, [string[]]$gitArgs) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = & git -C $dir @gitArgs 2>&1 | ForEach-Object { "$_" }
        return [pscustomobject]@{ Code = $LASTEXITCODE; Out = @($out) }
    } finally { $ErrorActionPreference = $previous }
}

function Same-Path([string]$a, [string]$b) {
    return [string]::Equals([IO.Path]::GetFullPath($a).TrimEnd('\'), [IO.Path]::GetFullPath($b).TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)
}

function Get-FileHashOrAbsent([string]$path) {
    if (-not $path -or -not (Test-Path -LiteralPath $path)) { return 'absent' }
    $sha = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($path)
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
    finally { $stream.Dispose(); $sha.Dispose() }
}

# Unity.exe processes whose command line names the nightly worktree, plus the editor's own lock file.
# Returns a list of reasons; empty means nothing has the worktree open.
function Get-UnityHoldingWorktree {
    $reasons = @()
    if (-not (Test-Path -LiteralPath $Worktree)) { return $reasons }
    $back = [regex]::Escape($Worktree)
    $fwd = [regex]::Escape($Worktree.Replace('\', '/'))
    $pattern = '(?i)(?:' + $back + '|' + $fwd + ')(?=[\\/"''\s]|$)'
    try {
        foreach ($p in @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'")) {
            if ($p.CommandLine -and ($p.CommandLine -match $pattern)) {
                $reasons += ('Unity.exe PID ' + $p.ProcessId + ' was started on ' + $Worktree)
            }
        }
    } catch { $reasons += ('could not list processes to check for an open editor: ' + $_.Exception.Message) }
    $lock = Join-Path $Worktree 'Temp\UnityLockfile'
    if (Test-Path -LiteralPath $lock) {
        try {
            $fs = [IO.File]::Open($lock, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            $fs.Dispose()
        } catch [IO.IOException] {
            $reasons += 'Temp\UnityLockfile in the worktree is held open by a running editor'
        } catch { }
    }
    return $reasons
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
    foreach ($c in $candidates) { if (Test-Path -LiteralPath $c) { return $c } }
    return ''
}

function Read-Json([string]$path) {
    try { return ([IO.File]::ReadAllText($path) | ConvertFrom-Json) } catch { return $null }
}

# ------------------------------------------------------------------------------- image comparison

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class NightlyImage
{
    private static byte[] Load(string path, out int w, out int h)
    {
        byte[] file = File.ReadAllBytes(path);
        using (MemoryStream ms = new MemoryStream(file))
        using (Bitmap src = new Bitmap(ms))
        {
            w = src.Width;
            h = src.Height;
            BitmapData d = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] px = new byte[d.Stride * h];
                Marshal.Copy(d.Scan0, px, 0, px.Length);
                return px;
            }
            finally { src.UnlockBits(d); }
        }
    }

    public static int[] Size(string path)
    {
        using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(path)))
        using (Image img = Image.FromStream(ms, false, false))
            return new int[] { img.Width, img.Height };
    }

    // Percentage of pixels whose largest channel difference exceeds threshold. -1 when the sizes differ.
    public static double DiffPercent(string a, string b, int threshold)
    {
        int wa, ha, wb, hb;
        byte[] pa = Load(a, out wa, out ha);
        byte[] pb = Load(b, out wb, out hb);
        if (wa != wb || ha != hb) return -1.0;
        long diff = 0;
        for (int i = 0; i + 3 < pa.Length; i += 4)
        {
            int db = Math.Abs(pa[i] - pb[i]);
            int dg = Math.Abs(pa[i + 1] - pb[i + 1]);
            int dr = Math.Abs(pa[i + 2] - pb[i + 2]);
            if (db > threshold || dg > threshold || dr > threshold) diff++;
        }
        return 100.0 * diff / ((long)wa * ha);
    }

    // True when the image is one flat colour (a black or empty frame): nothing was painted.
    public static bool IsBlank(string path)
    {
        int w, h;
        byte[] px = Load(path, out w, out h);
        int minL = 255, maxL = 0;
        for (int i = 0; i + 3 < px.Length; i += 28)
        {
            int l = (px[i] + px[i + 1] + px[i + 2]) / 3;
            if (l < minL) minL = l;
            if (l > maxL) maxL = l;
        }
        return (maxL - minL) < 6;
    }
}
'@

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class NightlyFocus
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern void keybd_event(byte k, byte s, uint f, IntPtr e);
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint a, uint b, IntPtr c, uint d);
    [DllImport("user32.dll")] static extern bool SwitchToThisWindow(IntPtr h, bool alt);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

    // Brings the window to the foreground if it is not already there. The foreground lock is
    // cleared for this session only (fWinIni 0: nothing is written to the user's profile). With no
    // interactive desktop these calls simply fail and the result is false.
    //
    // Any window of the editor's own process in front counts as raised. The editor has more than one
    // top-level window (the main one and a floating Game view), and MainWindowHandle may name either;
    // comparing handles alone fired the Alt key and SwitchToThisWindow every few seconds while the
    // editor already held the foreground, and editors stalled at Play entry with their main thread
    // blocked in a cross-thread SendMessage under the window proc (R23, native stacks of two stalls).
    public static bool Raise(IntPtr h, int pid)
    {
        if (h == IntPtr.Zero) return false;
        IntPtr front = GetForegroundWindow();
        if (front == h) return true;
        uint frontPid;
        if (front != IntPtr.Zero && GetWindowThreadProcessId(front, out frontPid) != 0 && frontPid == (uint)pid) return true;
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

# ------------------------------------------------------------------------------------------ phases

function Initialize-Worktree {
    # Registered? Compare against git's own list rather than trusting that the folder exists.
    [void](Invoke-NightlyGit $repoRoot @('worktree', 'prune'))
    $list = Invoke-NightlyGit $repoRoot @('worktree', 'list', '--porcelain')
    $registered = $false
    $first = $true
    foreach ($l in $list.Out) {
        if ($l -match '^worktree\s+(.+)$') {
            # The first entry is the primary checkout. Resetting that would destroy someone's work.
            if ($first -and (Same-Path $Matches[1] $Worktree)) { throw "$Worktree is the primary checkout, not a spare worktree. Not touching it." }
            $first = $false
            if (Same-Path $Matches[1] $Worktree) { $registered = $true }
        }
    }
    if ($registered -and -not (Test-Path -LiteralPath $Worktree)) {
        throw "git lists $Worktree as a worktree but the folder is missing"
    }
    if ($registered) { return }

    if ((Test-Path -LiteralPath $Worktree) -and @(Get-ChildItem -LiteralPath $Worktree -Force -ErrorAction SilentlyContinue).Count -gt 0) {
        throw "$Worktree exists, is not empty, and is not a registered worktree. Not touching it."
    }
    Say "creating worktree $Worktree detached at origin/main"
    $fetch = Invoke-NightlyGit $repoRoot @('fetch', 'origin')
    if ($fetch.Code -ne 0) { Say ('fetch before create failed: ' + ($fetch.Out -join ' / ')) }
    $add = Invoke-NightlyGit $repoRoot @('worktree', 'add', '--detach', $Worktree, 'origin/main')
    if ($add.Code -ne 0) { throw ('git worktree add failed: ' + ($add.Out -join ' / ')) }
}

function Sync-Worktree {
    # The three destructive commands below run only after the folder is proven to be the nightly
    # worktree and nothing else.
    $top = Invoke-NightlyGit $Worktree @('rev-parse', '--show-toplevel')
    if ($top.Code -ne 0 -or -not (Same-Path ($top.Out | Select-Object -First 1) $Worktree)) {
        throw "refusing to reset: git toplevel for $Worktree is '$($top.Out -join ' ')'"
    }

    Say 'git fetch origin'
    $fetch = Invoke-NightlyGit $Worktree @('fetch', 'origin')
    if ($fetch.Code -eq 0) { $R.fetchOk = $true }
    else { $R.fetchNote = 'git fetch failed, so this run tested origin/main as of the last successful fetch: ' + ($fetch.Out -join ' / ') }

    $co = Invoke-NightlyGit $Worktree @('checkout', '--force', '--detach', 'origin/main')
    if ($co.Code -ne 0) { throw ('checkout --detach origin/main failed: ' + ($co.Out -join ' / ')) }
    $rs = Invoke-NightlyGit $Worktree @('reset', '--hard', 'origin/main')
    if ($rs.Code -ne 0) { throw ('reset --hard origin/main failed: ' + ($rs.Out -join ' / ')) }
    $cl = Invoke-NightlyGit $Worktree @('clean', '-fd')
    if ($cl.Code -ne 0) { throw ('clean -fd failed: ' + ($cl.Out -join ' / ')) }

    $R.head = (Invoke-NightlyGit $Worktree @('rev-parse', 'HEAD')).Out | Select-Object -First 1
    $R.originMain = (Invoke-NightlyGit $Worktree @('rev-parse', 'origin/main')).Out | Select-Object -First 1
    $R.headSubject = (Invoke-NightlyGit $Worktree @('log', '-1', '--format=%s')).Out | Select-Object -First 1
    Say ('worktree at ' + $R.head)

    # Generated files from a previous run must not be mistaken for this one's.
    $status = Join-Path $Worktree 'STATUS.md'
    if (Test-Path -LiteralPath $status) { Remove-Item -LiteralPath $status -Force }
}

function Get-RealSavePath {
    $settings = Join-Path $Worktree 'ProjectSettings\ProjectSettings.asset'
    $company = ''; $product = ''
    foreach ($line in [IO.File]::ReadLines($settings)) {
        if (-not $company -and $line -match '^\s*companyName:\s*(.+?)\s*$') { $company = $Matches[1] }
        if (-not $product -and $line -match '^\s*productName:\s*(.+?)\s*$') { $product = $Matches[1] }
        if ($company -and $product) { break }
    }
    $fileName = 'emberfall_save.json'
    $svc = Join-Path $Worktree 'Assets\Scripts\Game\SaveService.cs'
    if (Test-Path -LiteralPath $svc) {
        $m = [regex]::Match([IO.File]::ReadAllText($svc), 'const\s+string\s+FileName\s*=\s*"([^"]+)"')
        if ($m.Success) { $fileName = $m.Groups[1].Value }
    }
    if (-not $company -or -not $product) { throw 'could not read companyName/productName from ProjectSettings.asset' }
    $low = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData\LocalLow'
    return (Join-Path (Join-Path (Join-Path $low $company) $product) $fileName)
}

function Invoke-BuildCheck {
    $script = Join-Path $Worktree 'Tools\build-check.ps1'
    if (-not (Test-Path -LiteralPath $script)) { $R.buildCheck.note = 'Tools/build-check.ps1 is missing from this commit'; return }
    Say 'build-check'
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $lines = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $script 2>&1 | ForEach-Object { "$_" })
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previous }
    [IO.File]::WriteAllLines((Join-Path $night 'build-check.log'), [string[]]$lines, $utf8)
    $R.buildCheck.ran = $true
    $R.buildCheck.exit = $code

    $statusPath = Join-Path $Worktree 'STATUS.md'
    $failures = @()
    if (Test-Path -LiteralPath $statusPath) {
        Copy-Item -LiteralPath $statusPath -Destination (Join-Path $night 'STATUS.md') -Force
        $R.buildCheck.statusMd = $true
        $in = $false
        foreach ($l in [IO.File]::ReadAllLines($statusPath)) {
            if ($l -match '^##\s+build-check\s*$') { $in = $true; continue }
            if ($in -and $l -match '^##\s') { break }
            if ($in -and $l -match '^-\s+(.+)$') { $failures += $Matches[1] }
        }
    } else {
        $R.buildCheck.note = 'build-check did not write STATUS.md; failures below are the [FAIL] lines of its log'
        foreach ($l in $lines) { if ($l -match '^\s*\[FAIL\]\s*(.*)$') { $failures += ('harness: ' + $Matches[1]) } }
    }
    if ($code -ne 0 -and $failures.Count -eq 0) { $failures += ('build-check exited ' + $code + ' with no failure lines') }

    # STATUS.md cuts a harness failure at 200 characters and ends it with "...". The report quotes
    # failures word for word, so a truncated one is replaced by the full [FAIL] line from the log.
    $full = @()
    foreach ($l in $lines) { if ($l -match '^\s*\[FAIL\]\s*(.*?)\s*$') { $full += $Matches[1] } }
    $failures = @($failures | ForEach-Object {
        $f = $_
        if ($f -like 'harness: *...') {
            $prefix = $f.Substring('harness: '.Length, $f.Length - 'harness: '.Length - 3)
            $hit = $full | Where-Object { $_.StartsWith($prefix) } | Select-Object -First 1
            if ($hit) { $f = 'harness: ' + $hit }
        }
        $f
    })
    $R.buildCheck.failures = @($failures)

    # Fingerprint drift, from the harness lines in the log (the same patterns Tools/status.ps1 reads).
    foreach ($kind in @('stage', 'sweep', 'talent')) {
        $entry = [ordered]@{ state = 'not compared'; count = 0; moved = @() }
        foreach ($l in $lines) {
            $mv = [regex]::Match($l, '(\d+) of (\d+) ' + $kind + ' fingerprints MOVED: (.*)$')
            if ($mv.Success) {
                $entry.state = 'moved'; $entry.count = [int]$mv.Groups[2].Value
                $entry.moved = @(($mv.Groups[3].Value -split ',\s*') | Where-Object { $_ })
                break
            }
            $ok = [regex]::Match($l, '\ball (\d+) ' + $kind + ' fingerprints match\b')
            if ($ok.Success) { $entry.state = 'match'; $entry.count = [int]$ok.Groups[1].Value; break }
        }
        $R.fingerprints[$kind] = $entry
    }
}

function Invoke-Validator {
    $script = Join-Path $Worktree 'Tools\HeroValidator\validate_heroes.py'
    $heroes = Join-Path $Worktree 'Assets\Resources\Content\heroes.json'
    if (-not (Test-Path -LiteralPath $script)) { $R.validator.note = 'validate_heroes.py is missing from this commit'; return }
    $python = Get-Command python -ErrorAction SilentlyContinue
    $exe = if ($python) { $python.Source } elseif (Test-Path 'C:\Python314\python.exe') { 'C:\Python314\python.exe' } else { '' }
    if (-not $exe) { $R.validator.note = 'no python on PATH'; return }
    Say 'hero validator'
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = @(& $exe -I $script $heroes 2>&1 | ForEach-Object { "$_" })
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previous }
    [IO.File]::WriteAllLines((Join-Path $night 'validator.txt'), [string[]]$out, $utf8)
    $R.validator.ran = $true
    $R.validator.exit = $code
    $findings = @()
    $checked = 0
    foreach ($l in $out) {
        $t = $l.TrimEnd()
        if (-not $t.Trim()) { continue }
        if ($t -match '^Summary:\s*(\d+)\s+fail\s+(\d+)\s+warn\s+(\d+)\s+info') {
            $R.validator.fail = [int]$Matches[1]; $R.validator.warn = [int]$Matches[2]; $R.validator.info = [int]$Matches[3]
            continue
        }
        if ($t -match '^\S+(  OK)?$' -and $t -notmatch '^DUPLICATE') { $checked++ }
        if ($t -notmatch '^\S+\s+OK$') { $findings += $t }
    }
    $R.validator.checked = $checked
    $R.validator.findings = @($findings)
    if (-not ($out | Where-Object { $_ -match '^Summary:' })) { $R.validator.note = 'no Summary line in the validator output' }
}

# Reads PlayModeScreenshot.cs for the targets it really has, so a target added tomorrow is swept
# tomorrow night without anyone editing this script.
function Get-CaptureTargets {
    $src = Join-Path $Worktree 'Assets\Editor\PlayModeScreenshot.cs'
    $text = [IO.File]::ReadAllText($src)
    $all = @()
    $am = [regex]::Match($text, 'string\[\]\s+all\s*=\s*\{(?<body>[^}]*)\}')
    if ($am.Success) { $all = @([regex]::Matches($am.Groups['body'].Value, '"([^"]+)"') | ForEach-Object { $_.Groups[1].Value }) }
    $begins = @([regex]::Matches($text, 'Begin\("([^"]+)"\)') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)

    $queue = New-Object System.Collections.Generic.List[string]
    $expected = New-Object System.Collections.Generic.List[string]
    $notes = @()
    # Every screen in the tool's own "all" list is swept as a target of its own, NOT through
    # Screen:ALL. ALL schedules each screen at an absolute frame number, and the Battle screen waits
    # for its first round for as long as that takes; when that wait runs past the slots that follow,
    # their frames have already gone by and ALL never shows them again. Measured on the first run:
    # everything after Battle was skipped and the editor sat in Play mode until someone closed it.
    # One session per target costs a domain reload each and cannot be skewed that way.
    if ($begins -contains 'Screen:ALL') {
        if ($all.Count -eq 0) { $queue.Add('Screen:ALL'); $notes += 'could not read the "all" list from PlayModeScreenshot.cs, so Screen:ALL was used' }
        foreach ($n in $all) { $queue.Add('Screen:' + $n); $expected.Add('screen_' + $n + '.png') }
    }
    foreach ($b in $begins) {
        if ($b -eq 'Screen:ALL') { continue }
        if ($b -like 'Screen:*') {
            $n = $b.Substring(7)
            if ($all -contains $n) { continue }
            $queue.Add($b); $expected.Add('screen_' + $n + '.png')
        } else {
            $queue.Add($b)
            switch ($b) {
                'Events'  { $expected.Add('events_screen.png') }
                'Recital' { $expected.Add('recital_screen.png'); $expected.Add('recital_battle.png') }
                'Proving' { $expected.Add('proving_screen.png'); $expected.Add('proving_battle.png') }
                default   { $notes += ("target '" + $b + "' has no known output file name, so it is swept but not checked") }
            }
        }
    }
    return [pscustomobject]@{ Queue = @($queue); Expected = @($expected | Select-Object -Unique); Notes = $notes }
}

# True when this process can show a window: not the services session, and the user's shell is
# running in the same session. A task with no desktop fails the second test even when its session
# id is not 0.
function Test-InteractiveDesktop {
    if (-not [Environment]::UserInteractive) { return $false }
    $session = [System.Diagnostics.Process]::GetCurrentProcess().SessionId
    if ($session -eq 0) { return $false }
    return [bool](Get-Process -Name explorer -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $session })
}

function Invoke-CaptureSweep {
    $c = $R.captures
    $targets = Get-CaptureTargets
    $c.targets = @($targets.Queue)
    $c.expected = @($targets.Expected)
    $c.notes += $targets.Notes
    if ($targets.Queue.Count -eq 0) { $c.notes += 'PlayModeScreenshot.cs yielded no targets'; return }

    # The captures need a real desktop: the GUI editor has to open a window and hold the foreground.
    # A task registered with S4U (or any logon without a desktop) has neither, and Unity then never
    # even writes its log, so launching it would only wait out the whole ceiling.
    if (-not (Test-InteractiveDesktop)) {
        $c.notes += 'no interactive desktop: the task must run as an Interactive logon; captures skipped'
        Say 'no interactive desktop; skipping the capture sweep without launching Unity'
        return
    }

    $version = ''
    $pv = Join-Path $Worktree 'ProjectSettings\ProjectVersion.txt'
    if (Test-Path -LiteralPath $pv) {
        $m = [regex]::Match([IO.File]::ReadAllText($pv), 'm_EditorVersion:\s*(\S+)')
        if ($m.Success) { $version = $m.Groups[1].Value }
    }
    $unity = Get-UnityExecutable $version
    $R.unityPath = $unity
    if (-not $unity) { $c.notes += ('Unity ' + $version + ' was not found, so no capture was taken'); return }

    $shots = Join-Path $Worktree 'Tools\Screenshots'
    New-Item -ItemType Directory -Force $shots | Out-Null
    Get-ChildItem -LiteralPath $shots -Filter '*.png' -File -ErrorAction SilentlyContinue | Remove-Item -Force
    Get-ChildItem -LiteralPath $shots -Force -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '.*' } | Remove-Item -Force

    $timeout = $CaptureTimeoutMinutes
    if (-not (Test-Path -LiteralPath (Join-Path $Worktree 'Library'))) {
        $timeout = [Math]::Max($timeout, 240)
        $c.notes += ('Library/ was missing, so this run imported the whole project first; the capture ceiling was raised to ' + $timeout + ' minutes')
    }

    $unityLog = Join-Path $night 'unity-captures.log'
    $queueArg = ($targets.Queue -join ';')
    $argText = '-projectPath "' + $Worktree + '" -executeMethod Emberfall.EditorTools.ScratchCaptures.Run -captures "' + $queueArg + '" -logFile "' + $unityLog + '"'
    Say ('launching Unity for ' + $targets.Queue.Count + ' capture target(s): ' + $queueArg)

    # ScratchCaptures sets an EditorPref (shared by every editor on this machine) for the length of
    # its run and puts it back only if it finishes. If the editor is stopped early the pref stays
    # set, so this script records it first and restores it after.
    $pref = Get-FirstRunPref
    # The Unity AI Assistant MCP bridge is held off for the sweep and put back after it
    # (Tools/mcp-bridge-pref.ps1 says why); a run cut short is put back by the next capture session.
    . (Join-Path $PSScriptRoot 'mcp-bridge-pref.ps1')
    try {
    Say (Disable-McpBridgeForCapture @(Get-Process -Name Unity -ErrorAction SilentlyContinue | ForEach-Object { $_.Id }))
    $began = Get-Date
    $proc = Start-Process -FilePath $unity -ArgumentList $argText -PassThru
    $null = $proc.Handle
    $c.ran = $true
    $deadline = $began.AddMinutes($timeout)
    $runStarted = $false
    $marks = -1
    $lastProgress = Get-Date
    $logSeen = $false
    $tick = 0
    $holdFocus = $false
    $holdSince = Get-Date
    while (-not $proc.WaitForExit(3000)) {
        # The Game view only repaints while the editor holds the foreground; behind another window
        # it keeps ticking, "captures" on schedule and photographs the previous frame (measured: a
        # Battle capture that was the Hub). Raised every few seconds, by PID, for this editor only.
        # Never between a play entry request and its first heartbeat (read every fifth tick below),
        # for at most 150 s: a foreground change while the editor enters Play deadlocked it (R23).
        $tick++
        if ($holdFocus -and ((Get-Date) - $holdSince).TotalSeconds -ge 150) { $holdFocus = $false }
        if (-not $NoFocus -and -not $holdFocus) {
            try { $proc.Refresh(); [void][NightlyFocus]::Raise($proc.MainWindowHandle, $proc.Id) } catch { }
        }
        if ($tick % 5 -ne 0) { continue }
        if ((Get-Date) -gt $deadline) {
            $c.timedOut = $true
            Say ('Unity PID ' + $proc.Id + ' passed the ' + $timeout + ' minute ceiling; stopping it by PID')
            try { Stop-Process -Id $proc.Id -Force -ErrorAction Stop } catch { Say ('could not stop PID ' + $proc.Id + ': ' + $_.Exception.Message) }
            [void]$proc.WaitForExit(30000)
            break
        }
        # Startup watchdog. Unity opens its log almost at once; if it has not by now, no later
        # check (they all read the log) can ever fire, so stop it here rather than at the ceiling.
        if (-not $logSeen) {
            if (Test-Path -LiteralPath $unityLog) { $logSeen = $true }
            elseif (((Get-Date) - $began).TotalMinutes -ge $LogStartMinutes) {
                $c.notes += ('Unity wrote no log within ' + $LogStartMinutes + ' minutes of launch, so it was stopped')
                Say ('Unity PID ' + $proc.Id + ' wrote no log within ' + $LogStartMinutes + ' minutes; stopping it by PID')
                try { Stop-Process -Id $proc.Id -Force -ErrorAction Stop } catch { Say ('could not stop PID ' + $proc.Id + ': ' + $_.Exception.Message) }
                [void]$proc.WaitForExit(30000)
                break
            }
        }
        # Stall watchdog. Once ScratchCaptures has started its queue, every capture adds a line to
        # the log; ten minutes with none means the editor is stuck (a hung Play session, a modal
        # dialog), and waiting out the full ceiling would only delay the report.
        $text = Read-SharedText $unityLog
        if ($text) {
            $entryAt = $text.LastIndexOf('PlayModeScreenshot: play entry requested')
            $wasHeld = $holdFocus
            $holdFocus = ($entryAt -ge 0) -and ($entryAt -gt $text.LastIndexOf('PlayModeScreenshot: play heartbeat,'))
            if ($holdFocus -and -not $wasHeld) { $holdSince = Get-Date }
            if (-not $runStarted -and $text.Contains('ScratchCaptures: scratch identity')) { $runStarted = $true; $lastProgress = Get-Date }
            if ($runStarted) {
                $now = ([regex]::Matches($text, 'ScratchCaptures: triggered|PlayModeScreenshot: requested')).Count
                if ($now -ne $marks) { $marks = $now; $lastProgress = Get-Date }
                elseif (((Get-Date) - $lastProgress).TotalMinutes -ge $StallMinutes) {
                    $c.timedOut = $true
                    $c.notes += ('no capture progress for ' + $StallMinutes + ' minutes, so Unity was stopped')
                    Say ('Unity PID ' + $proc.Id + ' made no capture progress for ' + $StallMinutes + ' minutes; stopping it by PID')
                    try { Stop-Process -Id $proc.Id -Force -ErrorAction Stop } catch { Say ('could not stop PID ' + $proc.Id + ': ' + $_.Exception.Message) }
                    [void]$proc.WaitForExit(30000)
                    break
                }
            }
        }
    }
    $c.unityExit = if ($proc.HasExited) { $proc.ExitCode } else { $null }
    $c.elapsedMinutes = [Math]::Round(((Get-Date) - $began).TotalMinutes, 1)
    $c.unityStarted = $began.ToString('yyyy-MM-dd HH:mm:ss')
    $c.unityEnded = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')

    # Anything of ours still alive (an import worker that outlived the editor): by PID, and only
    # processes whose command line names this worktree.
    Start-Sleep -Seconds 3
    foreach ($left in @(Get-UnityProcessesOnWorktree)) {
        Say ('stopping leftover Unity.exe PID ' + $left)
        try { Stop-Process -Id $left -Force -ErrorAction Stop } catch { }
    }

    Restore-FirstRunPref $pref
    } finally {
        $mcpNote = Restore-McpBridgeAfterCapture
        if ($mcpNote) { Say $mcpNote }
    }

    # What Unity said about itself.
    if (Test-Path -LiteralPath $unityLog) {
        $log = [IO.File]::ReadAllLines($unityLog)
        if (-not ($log | Where-Object { $_ -match 'ScratchCaptures: identity restored' })) {
            $c.notes += 'Unity ended before ScratchCaptures finished its queue (it never logged restoring the identity), so the sweep is incomplete'
        }
        $c.logFailLines = @($log | Where-Object { $_ -match '\[FAIL\]' } | Select-Object -Unique | Select-Object -First 20)
        $cs = @($log | Where-Object { $_ -match 'error CS\d+' } | Select-Object -Unique | Select-Object -First 5)
        foreach ($e in $cs) { $c.notes += ('compile error in Unity log: ' + $e.Trim()) }
        if ($log | Where-Object { $_ -match 'ScratchCaptures: \[ok\] the real save is byte for byte' }) { $c.notes += 'ScratchCaptures confirmed the real save was untouched' }
        if (-not ($log | Where-Object { $_ -match 'ScratchCaptures: triggered' })) { $c.notes += 'the Unity log never shows ScratchCaptures triggering a capture' }
    } else { $c.notes += 'Unity wrote no log file' }

    # Harvest. Only files written during this run count, so nothing stale can pass as tonight's.
    $dest = Join-Path $night 'captures'
    New-Item -ItemType Directory -Force $dest | Out-Null
    foreach ($f in @(Get-ChildItem -LiteralPath $shots -Filter '*.png' -File -ErrorAction SilentlyContinue)) {
        if ($f.LastWriteTime -ge $began.AddMinutes(-1)) { Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $dest $f.Name) -Force }
    }
}

# Unity holds its log open for writing, so a plain read can be refused.
function Read-SharedText([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return '' }
    try {
        $fs = New-Object IO.FileStream($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
        try { $sr = New-Object IO.StreamReader($fs); return $sr.ReadToEnd() } finally { $fs.Dispose() }
    } catch { return '' }
}

# The EditorPref ScratchCaptures flips for its run. EditorPrefs live in the registry with a hash
# suffix on the name, so it is found by prefix.
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
    $others = @(Get-Process -Name Unity -ErrorAction SilentlyContinue)
    if ($others.Count -gt 0) { return }
    $now = Get-FirstRunPref
    try {
        if ($before.Existed) {
            if (-not $now.Existed -or $now.Value -ne $before.Value) {
                Set-ItemProperty -LiteralPath $script:PrefRegPath -Name $before.Name -Value $before.Value -Type DWord
                Say ('restored EditorPref ' + $before.Name + ' to ' + $before.Value)
            }
        } elseif ($now.Existed -and $now.Value -ne 0) {
            # Absent before, left set by an interrupted run: the "off" state ScratchCaptures itself writes back.
            Set-ItemProperty -LiteralPath $script:PrefRegPath -Name $now.Name -Value 0 -Type DWord
            Say ('reset EditorPref ' + $now.Name + ' to 0 (it was absent before this run)')
        }
    } catch { Say ('could not restore the Emberfall.SuppressFirstRun EditorPref: ' + $_.Exception.Message) }
}

function Get-UnityProcessesOnWorktree {
    $ids = @()
    $back = [regex]::Escape($Worktree)
    $fwd = [regex]::Escape($Worktree.Replace('\', '/'))
    $pattern = '(?i)(?:' + $back + '|' + $fwd + ')(?=[\\/"''\s]|$)'
    try {
        foreach ($p in @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'")) {
            if ($p.CommandLine -and ($p.CommandLine -match $pattern)) { $ids += [int]$p.ProcessId }
        }
    } catch { }
    return $ids
}

function Measure-Captures($prev) {
    $c = $R.captures
    $dest = Join-Path $night 'captures'
    $have = @{}
    if (Test-Path -LiteralPath $dest) {
        foreach ($f in @(Get-ChildItem -LiteralPath $dest -Filter '*.png' -File)) { $have[$f.Name] = $f.FullName }
    }
    $prevDir = ''
    if ($prev) { $prevDir = Join-Path (Join-Path $NightlyRoot $prev.date) 'captures' }

    $files = [ordered]@{}
    foreach ($name in ($have.Keys | Sort-Object)) {
        $entry = [ordered]@{ width = 0; height = 0; bytes = (Get-Item -LiteralPath $have[$name]).Length; blank = $false; diffPercent = $null; change = '' }
        try {
            $sz = [NightlyImage]::Size($have[$name])
            $entry.width = $sz[0]; $entry.height = $sz[1]
            $entry.blank = [NightlyImage]::IsBlank($have[$name])
        } catch { $entry.change = 'unreadable image: ' + $_.Exception.Message }
        if ($prev -and $prevDir) {
            $old = Join-Path $prevDir $name
            if (Test-Path -LiteralPath $old) {
                try {
                    $psz = [NightlyImage]::Size($old)
                    if ($psz[0] -ne $entry.width -or $psz[1] -ne $entry.height) {
                        $entry.diffPercent = 100.0
                        $entry.change = ('size ' + $psz[0] + 'x' + $psz[1] + ' -> ' + $entry.width + 'x' + $entry.height)
                    } else {
                        $entry.diffPercent = [Math]::Round([NightlyImage]::DiffPercent($old, $have[$name], 24), 2)
                    }
                } catch { $entry.change = 'could not be compared: ' + $_.Exception.Message }
            } else { $entry.change = 'not captured the previous night' }
        }
        $files[$name] = $entry
    }
    $c.files = $files
    $c.missing = @($c.expected | Where-Object { -not $have.ContainsKey($_) })
    $c.extras = @($have.Keys | Where-Object { $c.expected -notcontains $_ } | Sort-Object)

    # Captures that are byte for byte the same file. Two screens can look the same offline, but a
    # Game view that did not repaint photographs the PREVIOUS screen, and that looks exactly like
    # this, so the groups are listed for a human to judge.
    $byHash = @{}
    foreach ($name in $have.Keys) {
        $h = Get-FileHashOrAbsent $have[$name]
        if (-not $byHash.ContainsKey($h)) { $byHash[$h] = @() }
        $byHash[$h] += $name
    }
    $c.identical = @($byHash.Values | Where-Object { @($_).Count -gt 1 } | ForEach-Object { (@($_) | Sort-Object) -join ' = ' } | Sort-Object)
}

function Get-Previous {
    $dirs = @(Get-ChildItem -LiteralPath $NightlyRoot -Directory -ErrorAction SilentlyContinue |
              Where-Object { $_.Name -match '^\d{4}-\d{2}-\d{2}$' -and $_.Name -lt $Date } | Sort-Object Name -Descending)
    foreach ($d in $dirs) {
        $rj = Join-Path $d.FullName 'results.json'
        if (-not (Test-Path -LiteralPath $rj)) { continue }
        $j = Read-Json $rj
        if ($j -and $j.status -eq 'completed') { return $j }
    }
    return $null
}

function Compare-Nights($prev) {
    $new = New-Object System.Collections.Generic.List[string]
    $cleared = New-Object System.Collections.Generic.List[string]
    if (-not $prev) { return }
    $R.previousNight = $prev.date

    $oldF = @($prev.buildCheck.failures)
    $newF = @($R.buildCheck.failures)
    foreach ($f in $newF) { if ($oldF -notcontains $f) { $new.Add('build-check: ' + $f) } }
    foreach ($f in $oldF) { if ($newF -notcontains $f) { $cleared.Add('build-check: ' + $f) } }

    if ($R.validator.ran -and $prev.validator.ran) {
        if ($R.validator.fail -gt [int]$prev.validator.fail) {
            $new.Add('validator: ' + $R.validator.fail + ' fail (was ' + $prev.validator.fail + ')')
        } elseif ($R.validator.fail -lt [int]$prev.validator.fail) {
            $cleared.Add('validator: ' + $R.validator.fail + ' fail (was ' + $prev.validator.fail + ')')
        }
    }

    if ($R.captures.ran -and $prev.captures.ran) {
        $prevMissing = @($prev.captures.missing)
        foreach ($m in @($R.captures.missing)) { if ($prevMissing -notcontains $m) { $new.Add('capture: ' + $m + ' was not produced (it was last night)') } }
        foreach ($m in $prevMissing) { if (@($R.captures.missing) -notcontains $m) { $cleared.Add('capture: ' + $m + ' is produced again') } }
    }
    $R.newFailures = @($new)
    $R.clearedFailures = @($cleared)
}

# ------------------------------------------------------------------------------------------ report

function Format-Names([object[]]$names) {
    if (-not $names -or $names.Count -eq 0) { return 'none' }
    return (($names | ForEach-Object { '`' + $_ + '`' }) -join ' ')
}

function Build-Report($prev) {
    $md = New-Object System.Collections.Generic.List[string]
    function Add([string]$s) { $md.Add($s) }

    $verdict = 'OK'
    if ($R.status -eq 'refused') { $verdict = 'REFUSED TO RUN' }
    elseif ($R.realSave.changed) { $verdict = 'REAL SAVE CHANGED' }
    elseif ($R.status -ne 'completed') { $verdict = 'RUN BROKE PART-WAY' }
    elseif ($R.newFailures.Count -gt 0) { $verdict = 'NEW FAILURES (' + $R.newFailures.Count + ')' }
    else {
        # Nothing new, but "OK" over a red build-check or a half-empty capture set would mislead.
        $known = @($R.buildCheck.failures).Count + @($R.captures.missing).Count + [int]$R.validator.fail
        if (-not $R.buildCheck.ran) { $known++ }
        if ($known -gt 0) { $verdict = 'NO NEW FAILURES, ' + $known + ' ALREADY KNOWN' }
    }

    Add ('# Emberfall nightly ' + $R.date + ': ' + $verdict)
    Add ''
    if ($R.realSave.changed) {
        Add '> **THE REAL SAVE CHANGED DURING THIS RUN.** The capture sweep was meant to be unable to touch it.'
        Add ('> Before: `' + $R.realSave.before + '`  After: `' + $R.realSave.after + '`')
        Add ('> File: `' + $R.realSave.path + '`')
        Add ('> The file was last written at ' + $R.realSave.lastWrite + '; `Player.log` beside it at ' + $R.realSave.playerLogWrite + '.')
        $cc = $R.captures
        if ($cc.ran -and $cc.unityStarted) {
            $w = [datetime]::Parse($R.realSave.lastWrite)
            $inside = ($w -ge [datetime]::Parse($cc.unityStarted)) -and ($w -le [datetime]::Parse($cc.unityEnded))
            Add ('> This run''s Unity was alive from ' + $cc.unityStarted + ' to ' + $cc.unityEnded + '; the write falls ' + $(if ($inside) { '**INSIDE** that window, so the capture sweep is the prime suspect.' } else { 'outside that window, so the sweep is not the writer.' }))
        } else {
            Add '> This run started no Unity, so nothing it did could have written the file; look for a game session or an editor on the real identity that was running meanwhile.'
        }
        if (@($R.realSave.gameProcesses).Count -gt 0) { Add ('> Game processes alive at the end of the run: ' + (@($R.realSave.gameProcesses) -join '; ')) }
        Add '> Do not play the game from this machine until you have looked at that file.'
        Add ''
    }
    if ($R.status -eq 'refused') {
        Add 'Nothing in the worktree was changed, and nothing was run. Why:'
        Add ''
        # Not $r: PowerShell variables are case-insensitive, and $r would overwrite the results object $R.
        foreach ($why in $R.refusal) { Add ('- ' + $why) }
        Add ''
    }
    if ($R.fatal) { Add ('**The run stopped with an error:** ' + $R.fatal); Add '' }

    Add '| | |'
    Add '|---|---|'
    Add ('| started | ' + $R.started + ' |')
    Add ('| finished | ' + $R.finished + ' |')
    if ($R.head) {
        Add ('| tested | origin/main `' + $R.originMain.Substring(0, [Math]::Min(10, $R.originMain.Length)) + '`: ' + $R.headSubject + ' |')
        Add ('| worktree | `' + $R.worktree + '` at `' + $R.head.Substring(0, [Math]::Min(10, $R.head.Length)) + '` |')
    }
    if ($R.fetchNote) { Add ('| fetch | ' + $R.fetchNote + ' |') }
    if ($R.status -eq 'refused') { }
    elseif ($prev) { Add ('| compared with | ' + $prev.date + ' |') }
    else { Add '| compared with | **nothing: this is the first night with a results record, so there is nothing to diff against.** Today''s failures are the baseline and are not counted as new. |' }
    Add ('| supabase.json in the worktree | ' + $(if ($R.supabaseJsonPresent) { 'PRESENT (not read; the forced-offline switch still applies)' } else { 'absent, as designed (existence checked, content never read)' }) + ' |')
    Add ''

    if ($R.status -eq 'refused') { return ($md -join "`r`n") + "`r`n" }

    # ---- failures
    Add '## Failures against the previous night'
    Add ''
    if (-not $prev) {
        Add 'First night: nothing to diff against. The current failures are listed under build-check below.'
    } else {
        Add ('**New (' + $R.newFailures.Count + '):**')
        Add ''
        if ($R.newFailures.Count -eq 0) { Add 'None.' } else { foreach ($f in $R.newFailures) { Add ('- ' + $f) } }
        Add ''
        Add ('**Cleared (' + $R.clearedFailures.Count + '):**')
        Add ''
        if ($R.clearedFailures.Count -eq 0) { Add 'None.' } else { foreach ($f in $R.clearedFailures) { Add ('- ' + $f) } }
    }
    Add ''

    # ---- build-check
    Add '## build-check'
    Add ''
    if (-not $R.buildCheck.ran) { Add ('**Not run.** ' + $R.buildCheck.note) }
    else {
        Add ('Exit code ' + $R.buildCheck.exit + ', **' + @($R.buildCheck.failures).Count + ' failure(s)**. Full output: `build-check.log`; status: `STATUS.md`.' + $(if ($R.buildCheck.note) { ' ' + $R.buildCheck.note } else { '' }))
        Add ''
        if (@($R.buildCheck.failures).Count -gt 0) {
            foreach ($f in $R.buildCheck.failures) {
                $tag = ''
                if ($prev -and (@($prev.buildCheck.failures) -notcontains $f)) { $tag = ' **(NEW)**' }
                elseif ($prev) { $tag = ' (also failing last night)' }
                Add ('- ' + $f + $tag)
            }
        }
    }
    Add ''

    # ---- fingerprints
    Add '## Fingerprint drift'
    Add ''
    if (-not $R.buildCheck.ran) { Add 'Not available: build-check did not run.' }
    else {
        Add '| baseline | now | last night | newly moved | settled again |'
        Add '|---|---|---|---|---|'
        foreach ($kind in @('stage', 'sweep', 'talent')) {
            $cur = $R.fingerprints[$kind]
            $nowText = if ($cur.state -eq 'moved') { @($cur.moved).Count.ToString() + ' of ' + $cur.count + ' MOVED' } elseif ($cur.state -eq 'match') { 'all ' + $cur.count + ' match' } else { 'not compared' }
            $oldText = 'n/a'; $newlyMoved = 'n/a'; $settled = 'n/a'
            if ($prev -and $prev.fingerprints -and $prev.fingerprints.$kind) {
                $p = $prev.fingerprints.$kind
                $oldText = if ($p.state -eq 'moved') { @($p.moved).Count.ToString() + ' of ' + $p.count + ' MOVED' } elseif ($p.state -eq 'match') { 'all ' + $p.count + ' match' } else { 'not compared' }
                $pm = @($p.moved); $cm = @($cur.moved)
                $newlyMoved = Format-Names @($cm | Where-Object { $pm -notcontains $_ })
                $settled = Format-Names @($pm | Where-Object { $cm -notcontains $_ })
            }
            Add ('| ' + $kind + ' | ' + $nowText + ' | ' + $oldText + ' | ' + $newlyMoved + ' | ' + $settled + ' |')
        }
        foreach ($kind in @('stage', 'sweep', 'talent')) {
            $cur = $R.fingerprints[$kind]
            if ($cur.state -eq 'moved') { Add ''; Add ('Moved ' + $kind + ' fingerprints now: ' + (Format-Names @($cur.moved))) }
        }
    }
    Add ''

    # ---- validator
    Add '## Hero validator'
    Add ''
    if (-not $R.validator.ran) { Add ('**Not run.** ' + $R.validator.note) }
    else {
        $v = $R.validator
        $line = '**' + $v.checked + ' heroes checked**, ' + $v.fail + ' fail, ' + $v.warn + ' warn, ' + $v.info + ' info (exit ' + $v.exit + ').'
        if ($prev -and $prev.validator.ran) {
            $line += ' Last night: ' + $prev.validator.checked + ' checked, ' + $prev.validator.fail + ' fail, ' + $prev.validator.warn + ' warn, ' + $prev.validator.info + ' info.'
        }
        Add $line
        if ($v.note) { Add ''; Add $v.note }
        if (@($v.findings).Count -gt 0) {
            Add ''
            Add 'Findings, word for word (full output: `validator.txt`):'
            Add ''
            Add '```'
            foreach ($f in @($v.findings | Select-Object -First 60)) { Add $f }
            Add '```'
        }
    }
    Add ''

    # ---- captures
    $c = $R.captures
    Add '## Captures (1920x1080)'
    Add ''
    if (-not $c.ran) {
        Add '**The capture sweep did not run.**'
        Add ''
        foreach ($n in $c.notes) { Add ('- ' + $n) }
    } else {
        $got = @($c.files.Keys).Count
        Add ('Unity ran for ' + $c.elapsedMinutes + ' minutes' + $(if ($c.timedOut) { ' and was **stopped at the time ceiling**' } else { ', exit code ' + $c.unityExit }) + '. ' + $got + ' of ' + @($c.expected).Count + ' expected captures were produced. Targets swept: ' + ((@($c.targets) | ForEach-Object { '`' + $_ + '`' }) -join ' ') + '.')
        foreach ($n in $c.notes) { Add ''; Add ('- ' + $n) }
        Add ''
        Add ('**Failed (' + @($c.missing).Count + '):** ' + (Format-Names @($c.missing)))
        $blank = @($c.files.Keys | Where-Object { $c.files[$_].blank })
        Add ''
        Add ('**Blank (one flat colour, so nothing painted) (' + $blank.Count + '):** ' + (Format-Names $blank))
        Add ''
        Add ('**Byte-identical captures (' + @($c.identical).Count + ' group(s)):** ' + $(if (@($c.identical).Count -eq 0) { 'none' } else { (@($c.identical) | ForEach-Object { '`' + $_ + '`' }) -join '; ' }))
        if (@($c.identical).Count -gt 0) {
            Add ''
            Add 'Each group is one image filed under several names. Two screens can look the same offline; otherwise the Game view did not repaint between them and the later capture is a stale frame of the earlier screen, which is not evidence about the screen it is named for.'
        }
        if (@($c.extras).Count -gt 0) { Add ''; Add ('**Produced but not expected:** ' + (Format-Names @($c.extras))) }
        if (@($c.logFailLines).Count -gt 0) {
            Add ''
            Add '**[FAIL] lines in the Unity log:**'
            Add ''
            Add '```'
            foreach ($l in $c.logFailLines) { Add $l.Trim() }
            Add '```'
        }
        Add ''
        if (-not $prev -or -not $prev.captures -or -not $prev.captures.ran) {
            Add 'No captures from a previous night to compare against, so size and pixel changes are not reported tonight.'
        } else {
            $odd = @($c.files.Keys | Where-Object { $c.files[$_].diffPercent -ne $null } |
                     Sort-Object { -[double]$c.files[$_].diffPercent })
            $notable = @($odd | Where-Object { [double]$c.files[$_].diffPercent -ge $ChangeThresholdPercent })
            Add ('**Notable changes (' + $notable.Count + ' at or above ' + $ChangeThresholdPercent + '% of pixels or with a new size):**')
            Add ''
            if ($odd.Count -eq 0) { Add 'No capture could be compared.' }
            else {
                Add '| capture | pixels changed | note |'
                Add '|---|---:|---|'
                foreach ($n in @($odd | Select-Object -First 10)) {
                    $e = $c.files[$n]
                    $flag = if ([double]$e.diffPercent -ge $ChangeThresholdPercent) { 'NOTABLE' } else { '' }
                    $text = (@($e.change, $flag) | Where-Object { $_ }) -join '; '
                    Add ('| `' + $n + '` | ' + $e.diffPercent + '% | ' + $text + ' |')
                }
                Add ''
                Add 'Percentage of pixels whose largest colour channel moved by more than 24 of 255, against the same file last night. Top 10 shown; animated effects account for a few percent on their own.'
            }
            $fresh = @($c.files.Keys | Where-Object { $c.files[$_].change -eq 'not captured the previous night' })
            if ($fresh.Count -gt 0) { Add ''; Add ('First captured tonight: ' + (Format-Names $fresh)) }
        }
    }
    Add ''

    # ---- safety
    Add '## Real save and cloud'
    Add ''
    Add ('- Real save: `' + $R.realSave.path + '`')
    Add ('- Hash before: `' + $R.realSave.before + '`')
    Add ('- Hash after: `' + $R.realSave.after + '`')
    Add ('- ' + $(if ($R.realSave.changed) { '**CHANGED.**' } else { 'Unchanged.' }))
    Add '- Captures run on a scratch identity with the cloud forced off (`ScratchCaptures`, `EditorCaptureGuard`); the only network call this script makes is `git fetch origin`.'
    Add ''

    Add '## Files in this folder'
    Add ''
    Add '`REPORT.md`, `results.json` (what tomorrow''s diff reads), `STATUS.md`, `build-check.log`, `validator.txt`, `unity-captures.log`, `captures\`, `nightly.log`.'
    return ($md -join "`r`n") + "`r`n"
}

# --------------------------------------------------------------------------------------------- main

$lockFile = Join-Path $NightlyRoot '.nightly.lock'
$haveLock = $false
$ownRun = $false
$exitCode = 1
try {
    New-Item -ItemType Directory -Force $NightlyRoot | Out-Null

    # One nightly at a time.
    if (Test-Path -LiteralPath $lockFile) {
        $other = 0
        [void][int]::TryParse(([IO.File]::ReadAllText($lockFile)).Trim(), [ref]$other)
        $alive = $false
        if ($other -gt 0 -and $other -ne $PID) {
            $op = Get-Process -Id $other -ErrorAction SilentlyContinue
            if ($op -and $op.ProcessName -match 'powershell|pwsh') { $alive = $true }
        }
        if ($alive) {
            # Not our run: leave its folder and its lock alone, and write nothing of our own.
            Write-Host ('[nightly] another nightly run is in progress (PID ' + $other + '); not starting a second one')
            exit 2
        }
    }
    [IO.File]::WriteAllText($lockFile, [string]$PID, $utf8)
    $haveLock = $true

    # A re-run on the same day supersedes the earlier one.
    if (Test-Path -LiteralPath $night) {
        if (-not (Same-Path (Split-Path -Parent $night) $NightlyRoot)) { throw "refusing to clear $night" }
        Remove-Item -LiteralPath $night -Recurse -Force
    }
    New-Item -ItemType Directory -Force $night | Out-Null
    $script:LogFile = Join-Path $night 'nightly.log'
    $ownRun = $true
    Say ('nightly ' + $Date + ' in ' + $night)

    $prev = Get-Previous

    Initialize-Worktree

    # Before anything in the worktree is changed.
    $holders = @(Get-UnityHoldingWorktree)
    if ($holders.Count -gt 0) {
        $R.status = 'refused'
        $R.refusal = @($holders)
        $R.refusal += 'Close that editor (or finish what it is doing) and the next run will go ahead.'
        Say ('refusing to run: ' + ($holders -join '; '))
        $exitCode = 2
    } else {
        Sync-Worktree
        $R.supabaseJsonPresent = Test-Path -LiteralPath (Join-Path $Worktree 'Assets\Resources\Config\supabase.json')

        $savePath = Get-RealSavePath
        $R.realSave.path = $savePath
        $R.realSave.before = Get-FileHashOrAbsent $savePath
        Say ('real save hash before: ' + $R.realSave.before)

        try { Invoke-BuildCheck } catch { $R.buildCheck.note = 'build-check could not be run: ' + $_.Exception.Message; Say $R.buildCheck.note }
        try { Invoke-Validator } catch { $R.validator.note = 'validator could not be run: ' + $_.Exception.Message; Say $R.validator.note }

        if ($SkipCaptures) { $R.captures.notes += 'skipped with -SkipCaptures' }
        else {
            try { Invoke-CaptureSweep } catch { $R.captures.notes += ('capture sweep broke: ' + $_.Exception.Message); Say ('capture sweep broke: ' + $_.Exception.Message) }
        }
        try { Measure-Captures $prev } catch { $R.captures.notes += ('could not measure captures: ' + $_.Exception.Message) }

        $R.realSave.after = Get-FileHashOrAbsent $savePath
        $R.realSave.changed = ($R.realSave.before -ne $R.realSave.after)
        if ($R.realSave.changed) {
            # Evidence for whoever reads the alert: when the file was written, and what else on this
            # machine could have written it. A built game session writes Player.log beside the save.
            if (Test-Path -LiteralPath $savePath) { $R.realSave.lastWrite = (Get-Item -LiteralPath $savePath).LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss') }
            $playerLog = Join-Path (Split-Path -Parent $savePath) 'Player.log'
            if (Test-Path -LiteralPath $playerLog) { $R.realSave.playerLogWrite = (Get-Item -LiteralPath $playerLog).LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss') }
            $R.realSave.gameProcesses = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^Emberfall' } |
                                          ForEach-Object { $_.ProcessName + ' PID ' + $_.Id + ' since ' + $_.StartTime.ToString('HH:mm:ss') })
        }
        Say ('real save hash after:  ' + $R.realSave.after + $(if ($R.realSave.changed) { '   *** CHANGED ***' } else { '   (unchanged)' }))

        $R.status = 'completed'
        Compare-Nights $prev
        if ($R.realSave.changed -or $R.newFailures.Count -gt 0) { $exitCode = 1 } else { $exitCode = 0 }
    }
}
catch {
    $R.fatal = $_.Exception.Message
    if ($R.status -ne 'refused') { $R.status = 'aborted' }
    Say ('FATAL: ' + $_.Exception.Message)
    # A worktree that could not be prepared is a refusal to run; anything later is a broken run.
    $exitCode = if ($R.head) { 1 } else { 2 }
}
finally {
    $R.finished = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    $R.exitCode = $exitCode
    if ($ownRun -and (Test-Path -LiteralPath $night)) {
        try {
            $prevForReport = $null
            if ($R.previousNight) { $prevForReport = Read-Json (Join-Path (Join-Path $NightlyRoot $R.previousNight) 'results.json') }
            [IO.File]::WriteAllText((Join-Path $night 'results.json'), ($R | ConvertTo-Json -Depth 8), $utf8)
            [IO.File]::WriteAllText((Join-Path $night 'REPORT.md'), (Build-Report $prevForReport), $utf8)
            Say ('report: ' + (Join-Path $night 'REPORT.md'))
        } catch { Write-Host ('[nightly] could not write the report: ' + $_.Exception.Message) }

        # Keep the last N nights. Only date-named folders directly under the nightly root.
        try {
            $old = @(Get-ChildItem -LiteralPath $NightlyRoot -Directory -ErrorAction SilentlyContinue |
                     Where-Object { $_.Name -match '^\d{4}-\d{2}-\d{2}$' } | Sort-Object Name -Descending | Select-Object -Skip $Keep)
            foreach ($d in $old) {
                if (Same-Path (Split-Path -Parent $d.FullName) $NightlyRoot) {
                    Say ('deleting old night ' + $d.Name)
                    Remove-Item -LiteralPath $d.FullName -Recurse -Force
                }
            }
        } catch { Write-Host ('[nightly] retention failed: ' + $_.Exception.Message) }
    }
    if ($haveLock -and (Test-Path -LiteralPath $lockFile)) { Remove-Item -LiteralPath $lockFile -Force -ErrorAction SilentlyContinue }
}
exit $exitCode
