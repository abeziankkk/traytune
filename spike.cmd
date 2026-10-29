@echo off
setlocal
set "ROOT=%~dp0"
set "EXE=%ROOT%spike\bin\Debug\net10.0-windows10.0.19041.0\SmtcSpike.exe"

if not exist "%EXE%" (
    echo building spike...
    dotnet build "%ROOT%spike\SmtcSpike.csproj" -v q --nologo || goto :err
)

if "%~1"=="" (
    "%EXE%" dump
) else (
    "%EXE%" %*
)
pause
exit /b 0

:err
echo.
echo BUILD FAILED. Need .NET SDK 10 and internet for the first restore.
pause
exit /b 1
