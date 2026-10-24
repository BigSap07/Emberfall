@echo off
rem Emberfall web cockpit: starts serve.py when port 8765 is free, opens it in an Edge app window, then exits.
rem The server runs under pythonw, so there is no console window for it; this window closes by itself.
rem The terminal dashboard (cockpit.ps1) is untouched and stays the fallback.
setlocal
set "HERE=%~dp0"
set "PORT=8765"
set "URL=http://127.0.0.1:%PORT%"

call :listening
if not errorlevel 1 goto open

set "PYW="
where pyw >nul 2>nul && set "PYW=pyw -3"
if not defined PYW where pythonw >nul 2>nul && set "PYW=pythonw"
if not defined PYW (
  echo Python not found. Install Python 3 from python.org, then run this again.
  pause
  exit /b 1
)
start "" /d "%HERE%" %PYW% "%HERE%serve.py" --port %PORT%

rem wait up to 20 seconds for the port (ping -n 2 to loopback is a one second sleep that needs no console)
for /l %%i in (1,1,20) do (
  call :listening
  if not errorlevel 1 goto open
  ping -n 2 127.0.0.1 >nul
)
echo The cockpit server did not start on port %PORT%. Run: python "%HERE%serve.py"
pause
exit /b 1

:open
reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe" >nul 2>nul
if errorlevel 1 (
  start "" "%URL%"
) else (
  start "" msedge --app=%URL%
)
exit /b 0

:listening
netstat -ano -p tcp | findstr /r /c:"127.0.0.1:%PORT% .*LISTENING" >nul
exit /b %errorlevel%
