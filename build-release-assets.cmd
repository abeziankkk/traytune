@echo off
chcp 65001 >nul
rem Собирает файлы для релиза.
rem   TrayTune-net10.exe    - один файл, нужен .NET 10 Desktop Runtime (~25 МБ)
rem   TrayTune-portable.exe - один файл, .NET не нужен (~72 МБ)
rem
rem Промежуточные сборки идут в dist-release (мусор, в git не попадает),
rem а в "TrayTune release" копируется только то, что публикуется.
setlocal
set "ROOT=%~dp0"
set "OUT=%ROOT%dist-release"
set "CLEAN=%ROOT%..\TrayTune release"

echo [1/6] framework-dependent single file...
dotnet publish "%ROOT%player\TrayTune.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "%OUT%\fd" --nologo
if errorlevel 1 goto :err

echo [2/6] self-contained single file...
dotnet publish "%ROOT%player\TrayTune.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%OUT%\sc" --nologo
if errorlevel 1 goto :err

echo [3/6] copying exe files...
copy /y "%OUT%\fd\TrayTune.exe" "%OUT%\TrayTune-net10.exe" >nul
if errorlevel 1 goto :err
copy /y "%OUT%\sc\TrayTune.exe" "%OUT%\TrayTune-portable.exe" >nul
if errorlevel 1 goto :err

echo [4/6] copying docs...
copy /y "%ROOT%Troubleshooting.txt" "%OUT%\Troubleshooting.txt" >nul

echo [5/6] cleanup...
rmdir /s /q "%OUT%\fd" 2>nul
rmdir /s /q "%OUT%\sc" 2>nul

echo [6/6] clean release folder: %CLEAN%
if not exist "%CLEAN%" mkdir "%CLEAN%"
copy /y "%OUT%\TrayTune-net10.exe" "%CLEAN%\TrayTune-net10.exe" >nul
if errorlevel 1 goto :locked
copy /y "%OUT%\TrayTune-portable.exe" "%CLEAN%\TrayTune-portable.exe" >nul
if errorlevel 1 goto :locked
copy /y "%OUT%\Troubleshooting.txt" "%CLEAN%\Troubleshooting.txt" >nul
copy /y "%ROOT%README.md" "%CLEAN%\README.md" >nul
copy /y "%ROOT%LICENSE" "%CLEAN%\LICENSE" >nul

echo.
echo Ready for the release: %CLEAN%
dir /b "%CLEAN%"
exit /b 0

:locked
echo.
echo Не удалось обновить файлы в "%CLEAN%": приложение запущено. Закрой TrayTune и повтори.
pause
exit /b 1

:err
echo.
echo BUILD FAILED. Close TrayTune.exe if it is running and try again.
pause
exit /b 1
