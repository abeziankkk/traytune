@echo off
setlocal
set "ROOT=%~dp0"

if not exist "%ROOT%dist\TaskbarPlayer.exe" (
    call "%ROOT%build-release.cmd"
    if errorlevel 1 exit /b 1
)

start "" "%ROOT%dist\TaskbarPlayer.exe"
exit /b 0
