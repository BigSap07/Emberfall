# Emberfall cockpit: read-only terminal dashboard. Usage:
#   Tools\cockpit\cockpit.ps1          live data from D:\Emberfall
#   Tools\cockpit\cockpit.ps1 -Mock    the sample data in Tools\cockpit\mock
param([switch]$Mock)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$py = @()
if (Get-Command py -ErrorAction SilentlyContinue) { $py = @('py', '-3') }
elseif (Get-Command python -ErrorAction SilentlyContinue) { $py = @('python') }
else { Write-Host 'Python not found. Install Python 3 from python.org.' -ForegroundColor Yellow; exit 1 }

$exe = $py[0]
$pre = @($py | Select-Object -Skip 1)

& $exe @pre -c 'import textual' 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Textual is missing. Run:' -ForegroundColor Yellow
    Write-Host "  $($py -join ' ') -m pip install textual"
    exit 1
}

$cockpitArgs = @(Join-Path $here 'cockpit.py')
if ($Mock) { $cockpitArgs += '--mock' }
& $exe @pre @cockpitArgs
