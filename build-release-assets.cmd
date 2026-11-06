@echo off
rem Builds release assets:
rem   TrayTune-net10.exe    - single file, needs .NET 10 Desktop Runtime (~24 MB)
rem   TrayTune-portable.exe - single file, no .NET required (~72 MB)
setlocal
set "ROOT=%~dp0"
set "OUT=%ROOT%dist-release"

echo [1/5] framework-dependent single file...
dotnet publish "%ROOT%player\TrayTune.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "%OUT%\fd" --nologo
if errorlevel 1 goto :err

echo [2/5] self-contained single file...
dotnet publish "%ROOT%player\TrayTune.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%OUT%\sc" --nologo
if errorlevel 1 goto :err

echo [3/5] copying exe files...
copy /y "%OUT%\fd\TrayTune.exe" "%OUT%\TrayTune-net10.exe" >nul
if errorlevel 1 goto :err
copy /y "%OUT%\sc\TrayTune.exe" "%OUT%\TrayTune-portable.exe" >nul
if errorlevel 1 goto :err

echo [4/5] copying readme...
copy /y "%ROOT%Troubleshooting.txt" "%OUT%\Troubleshooting.txt" >nul

echo [5/5] cleanup...
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
