# Creates "Emberfall Cockpit.lnk" on the Desktop. It only creates the shortcut; it pins nothing.
#   Tools\cockpit\make-shortcut.ps1                   Desktop
#   Tools\cockpit\make-shortcut.ps1 -OutDir C:\temp   somewhere else (for a trial)
# Target: cmd.exe /c start "" /min "<repo>\Tools\cockpit\cockpit-web.bat", working dir Tools\cockpit.
param([string]$OutDir = [Environment]::GetFolderPath('Desktop'))

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$bat = Join-Path $here 'cockpit-web.bat'
$ico = Join-Path $here 'web\ember.ico'
if (-not (Test-Path $bat)) { throw "cockpit-web.bat not found next to this script: $bat" }

if (-not (Test-Path $ico)) {
    $py = if (Get-Command py -ErrorAction SilentlyContinue) { @('py', '-3') } elseif (Get-Command python -ErrorAction SilentlyContinue) { @('python') } else { $null }
    if (-not $py) { throw 'web\ember.ico is missing and Python is not installed to generate it (make_icon.py).' }
    & $py[0] @($py | Select-Object -Skip 1) (Join-Path $here 'make_icon.py') $ico
    if ($LASTEXITCODE -ne 0) { throw 'make_icon.py failed' }
}

New-Item -ItemType Directory -Force $OutDir | Out-Null
$lnk = Join-Path $OutDir 'Emberfall Cockpit.lnk'
$sh = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk)
$sh.TargetPath = Join-Path $env:SystemRoot 'System32\cmd.exe'
$sh.Arguments = "/c start `"`" /min `"$bat`""
$sh.WorkingDirectory = $here
$sh.IconLocation = "$ico,0"
$sh.WindowStyle = 7          # minimized
$sh.Description = 'Emberfall cockpit (web)'
$sh.Save()
Write-Host "Created $lnk"
