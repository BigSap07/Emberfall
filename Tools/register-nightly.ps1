# Registers the Windows Task Scheduler task that runs Tools/nightly.ps1 at 03:30 every day.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/register-nightly.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/register-nightly.ps1 -Remove
#
# The task runs:
#   - daily at 03:30, as the CURRENT user, with an Interactive logon: it runs only while that user is
#     logged on, in their desktop session, and no password is stored or asked for. That is
#     deliberate: the capture sweep launches the GUI Unity editor, which needs a desktop to open a
#     window on and the foreground to render the Game view. Leave the machine logged in overnight;
#     with nobody logged on at 03:30 there is no session for the task to run in;
#   - only on AC power (it will not start on battery; a run already underway is not stopped if the
#     charger is pulled, because a half-finished Unity import is worse than a finished one);
#   - once at a time (a second start while one is running is ignored), with a five hour ceiling;
#   - late rather than never: if the machine was off or asleep at 03:30 it runs when it next can.
#
# Re-running this replaces the task. NOTHING here runs the nightly itself.
#
# The task points at nightly.ps1 BESIDE this script, so run this from the checkout that should own
# the schedule. D:\Emberfall\Game-nightly is the natural one once these scripts are on main: it is
# reset to origin/main every night and no agent works in it.
#
# Why not S4U: a task run with S4U has no interactive desktop. nightly.ps1, build-check and the
# validator do not need one, but the Unity editor never wrote its log under it, so every scheduled
# night from 2026-10-13 sat out the full capture ceiling with no captures. A run started by hand
# from a logged-on session worked. nightly.ps1 now also skips the capture sweep, with a note in the
# report, when it finds it has no desktop, so a wrongly registered task fails fast instead.

param(
    [string]$TaskName = 'Emberfall Nightly',
    [string]$At = '03:30',
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'

if ($Remove) {
    if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
        Write-Host "Removed the '$TaskName' task."
    } else { Write-Host "No task called '$TaskName'." }
    return
}

$script = Join-Path $PSScriptRoot 'nightly.ps1'
if (-not (Test-Path -LiteralPath $script)) { throw "nightly.ps1 not found beside this script: $script" }

$user = [Security.Principal.WindowsIdentity]::GetCurrent().Name

$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument ('-NoProfile -ExecutionPolicy Bypass -File "' + $script + '"') `
    -WorkingDirectory $PSScriptRoot
$trigger = New-ScheduledTaskTrigger -Daily -At $At
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Hours 5)
# New-ScheduledTaskSettingsSet leaves "start only on AC power" on unless -AllowStartIfOnBatteries
# is passed, but say so explicitly rather than lean on a default.
$settings.DisallowStartIfOnBatteries = $true

$task = New-ScheduledTask -Action $action -Trigger $trigger -Principal $principal -Settings $settings `
    -Description 'Emberfall nightly health run: build-check, hero validator and a capture sweep in D:\Emberfall\Game-nightly. Reports land in D:\Emberfall\nightly\<date>\REPORT.md.'
Register-ScheduledTask -TaskName $TaskName -InputObject $task -Force | Out-Null

$info = Get-ScheduledTaskInfo -TaskName $TaskName
Write-Host "Registered '$TaskName' for $user."
Write-Host "  runs:      $script"
Write-Host "  at:        $At daily, only on AC power, only while $user is logged on"
Write-Host ("  next run:  " + $info.NextRunTime)
Write-Host "  reports:   D:\Emberfall\nightly\<date>\REPORT.md"
Write-Host "To remove:   powershell -NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Remove"
