@echo off
rem Builds two exe files for a GitHub release:
rem   TrayTune-net10.exe      - small, needs .NET 10 Desktop Runtime
rem   TrayTune-portable.exe   - self-contained, runs anywhere, ~72 MB
setlocal
set "ROOT=%~dp0"
set "OUT=%ROOT%dist-release"

echo [1/4] framework-dependent build...
dotnet publish "%ROOT%player\TrayTune.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "%OUT%\fd" --nologo
if errorlevel 1 goto :err

echo [2/4] self-contained build...
dotnet publish "%ROOT%player\TrayTune.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%OUT%\sc" --nologo
if errorlevel 1 goto :err

echo [3/4] renaming...
copy /y "%OUT%\fd\TrayTune.exe" "%OUT%\TrayTune-net10.exe" >nul
if errorlevel 1 goto :err
copy /y "%OUT%\sc\TrayTune.exe" "%OUT%\TrayTune-portable.exe" >nul
if errorlevel 1 goto :err

echo [4/4] cleanup...
rmdir /s /q "%OUT%\fd" 2>nul
rmdir /s /q "%OUT%\sc" 2>nul

echo.
echo Ready for the release: %OUT%
dir /b "%OUT%"
exit /b 0

:err
echo.
echo BUILD FAILED. Close TrayTune.exe if it is running and try again.
pause
exit /b 1
