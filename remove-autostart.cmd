@echo off
chcp 65001 >nul
rem ---------------------------------------------------------------------------
rem  Убирает задачи планировщика, которые остались от версий TrayTune до 0.2.0.
rem  Именно они запускали приложение при входе в систему, даже если в
rem  «Автозагрузке» и в настройках программы автозапуск выключен.
rem
rem  Запускать от имени администратора: правый клик по файлу ->
rem  «Запуск от имени администратора».
rem ---------------------------------------------------------------------------

setlocal

net session >nul 2>&1
if errorlevel 1 (
    echo.
    echo   Нужны права администратора.
    echo   Правый клик по этому файлу - "Запуск от имени администратора".
    echo.
    pause
    exit /b 1
)

echo.
echo   Удаляю старые задачи TrayTune...
schtasks /delete /tn "\TrayTune" /f >nul 2>&1
schtasks /delete /tn "\TrayTune.Run" /f >nul 2>&1

echo.
schtasks /query /tn "\TrayTune" >nul 2>&1
if errorlevel 1 (echo   TrayTune      - удалена) else (echo   TrayTune      - ВСЁ ЕЩЁ ЕСТЬ)
schtasks /query /tn "\TrayTune.Run" >nul 2>&1
if errorlevel 1 (echo   TrayTune.Run  - удалена) else (echo   TrayTune.Run  - ВСЁ ЕЩЁ ЕСТЬ)

echo.
echo   Готово. Автозапуска больше нет: приложение стартует только вручную.
echo   Если нужен автозапуск - включи его в настройках TrayTune (галочка).
echo.
pause
exit /b 0
