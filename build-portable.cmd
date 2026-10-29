@echo off
rem Портативная сборка: self-contained, .NET на машине не нужен.
setlocal
set "ROOT=%~dp0"
set "OUT=%ROOT%dist-portable"

echo [1/2] publish self-contained...
dotnet publish "%ROOT%player\TrayTune.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%OUT%" --nologo
if errorlevel 1 goto :err

echo [2/2] done: %OUT%\TrayTune.exe
exit /b 0

:err
echo.
echo BUILD FAILED.
pause
exit /b 1
