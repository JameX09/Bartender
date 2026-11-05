@echo off
rem Zostavi hru cez Unity (bez otvarania editora) do bin\Bartender3D.exe
setlocal
cd /d "%~dp0"
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe"
if not exist "%UNITY%" (
    echo [build] Unity 6000.6.5f1 sa nenasla: %UNITY%
    exit /b 1
)
if not exist build mkdir build
echo [build] Unity zostavuje hru, prvy build trva niekolko minut...
"%UNITY%" -batchmode -quit -projectPath "%CD%" -executeMethod BuildScript.Build -logFile "build\unity.log"
if errorlevel 1 (
    echo [build] CHYBA - podrobnosti v build\unity.log
    echo [build] Tip: ak mas projekt otvoreny v Unity editore, zavri ho.
    exit /b 1
)
echo [build] OK - bin\Bartender3D.exe
exit /b 0