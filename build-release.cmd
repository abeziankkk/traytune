@echo off
setlocal
set "ROOT=%~dp0"
set "OUT=%ROOT%dist"

echo [1/3] publish...
dotnet publish "%ROOT%player\TrayTune.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "%OUT%" --nologo
if errorlevel 1 goto :err

echo [2/3] copy exe next to this file...
copy /y "%OUT%\TrayTune.exe" "%ROOT%TrayTune.exe" >nul
if errorlevel 1 goto :err

echo [3/3] done: %ROOT%TrayTune.exe
exit /b 0

:err
echo.
echo BUILD FAILED. Need .NET SDK 10 and internet for the first restore.
pause
exit /b 1
