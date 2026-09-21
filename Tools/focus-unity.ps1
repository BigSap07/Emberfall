# Give the running Unity editor OS focus so its asset pipeline runs an auto-refresh.
#
# EditorApplication.update ticks without focus, so file-triggered editor tools always FIRE, but
# the asset pipeline only refreshes when the editor regains focus. Without this, an automated
# capture silently runs against stale assemblies and screenshots the previous build.
#
# Two Windows behaviours have to be defeated, both learned the hard way on this branch:
#
#  1. A plain SetForegroundWindow returns true and does nothing when Unity already believes it
#     has focus - no focus-CHANGE event fires, so no refresh. Something must actually take the
#     foreground away and give it back.
#  2. Windows' foreground lock stops a background process from stealing focus at all. The
#     documented way through is to clear SPI_SETFOREGROUNDLOCKTIMEOUT and tap ALT, which makes
#     this process eligible to set the foreground window; SwitchToThisWindow then lands it.
#
# Neither AttachThreadInput nor minimise/restore was sufficient on its own here.
$sig = @'
using System;
using System.Runtime.InteropServices;
public class UnityFocus {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, IntPtr e);
  [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint a, uint b, IntPtr c, uint d);
  [DllImport("user32.dll")] public static extern bool SwitchToThisWindow(IntPtr h, bool alt);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
}
'@
if (-not ([System.Management.Automation.PSTypeName]'UnityFocus').Type) { Add-Type -TypeDefinition $sig }

# THE EDITOR FOR THIS PROJECT, not whichever editor Windows lists first. Two checkouts of this
# repository can be open at once - a worktree is exactly that - and focusing the wrong one
# refreshes the wrong assets while the capture quietly runs against stale ones. Unity titles its
# main window "<project folder> - <scene> - <platform> - Unity <version>", so the folder name this
# script lives under is enough to tell them apart.
$projectName = Split-Path -Leaf (Split-Path -Parent $PSScriptRoot)
$windows = @(Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 })
$p = $windows | Where-Object { $_.MainWindowTitle -like "$projectName - *" } | Select-Object -First 1
if (-not $p -and $windows.Count -eq 1) { $p = $windows[0] }
if (-not $p) {
    Write-Error "no Unity editor window found for '$projectName' - is the editor open on THIS project? Found: $($windows.MainWindowTitle -join '; ')"
    exit 1
}
$h = $p.MainWindowHandle

# If Unity already HOLDS the foreground, no amount of SetForegroundWindow will produce a
# focus-change event and no refresh will run. Minimising first guarantees a real deactivate,
# so the restore below is a genuine reactivation rather than a no-op.
if ([UnityFocus]::GetForegroundWindow() -eq $h) {
    [void][UnityFocus]::ShowWindow($h, 6)   # SW_MINIMIZE
    Start-Sleep -Milliseconds 600
}

[void][UnityFocus]::SystemParametersInfo(0x2001, 0, [IntPtr]::Zero, 3)  # SPI_SETFOREGROUNDLOCKTIMEOUT = 0
[UnityFocus]::keybd_event(0x12, 0, 0, [IntPtr]::Zero)                   # ALT down
[UnityFocus]::keybd_event(0x12, 0, 2, [IntPtr]::Zero)                   # ALT up
Start-Sleep -Milliseconds 200
[void][UnityFocus]::ShowWindow($h, 9)   # SW_RESTORE
[void][UnityFocus]::SwitchToThisWindow($h, $true)
[void][UnityFocus]::SetForegroundWindow($h)
Start-Sleep -Milliseconds 300

if ([UnityFocus]::GetForegroundWindow() -eq $h) { "focused: $($p.MainWindowTitle)" }
else { Write-Warning "could not bring Unity to the foreground; the asset refresh may not run" }
