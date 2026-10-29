@echo off
setlocal
set "ROOT=%~dp0"

if not exist "%ROOT%dist\TrayTune.exe" (
    call "%ROOT%build-release.cmd"
    if errorlevel 1 exit /b 1
)

start "" "%ROOT%dist\TrayTune.exe"
exit /b 0
