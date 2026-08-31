# Give the running Unity editor OS focus, so its asset pipeline runs an auto-refresh.
# EditorApplication.update ticks without focus, but the asset pipeline does not, so an
# automated capture will happily run against stale assemblies unless something forces this.
$sig = @'
using System;
using System.Runtime.InteropServices;
public class UnityFocus {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
}
'@
if (-not ([System.Management.Automation.PSTypeName]'UnityFocus').Type) { Add-Type -TypeDefinition $sig }
$p = Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Error "no Unity editor window found"; exit 1 }
$h = $p.MainWindowHandle
$fg = [UnityFocus]::GetForegroundWindow()
$t1 = [UnityFocus]::GetWindowThreadProcessId($fg, [ref]$null)
$t2 = [UnityFocus]::GetWindowThreadProcessId($h, [ref]$null)
[void][UnityFocus]::AttachThreadInput($t1, $t2, $true)
[void][UnityFocus]::ShowWindow($h, 9)
[void][UnityFocus]::BringWindowToTop($h)
[void][UnityFocus]::SetForegroundWindow($h)
[void][UnityFocus]::AttachThreadInput($t1, $t2, $false)
"focused: $($p.MainWindowTitle)"
