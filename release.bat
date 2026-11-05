@echo off
rem Zabali zostavenu hru do zipu na zverejnenie (napr. itch.io): release\Bartender3D-<verzia>-win64.zip
setlocal
cd /d "%~dp0"
if not exist "bin\Bartender3D.exe" (
    echo [release] Najprv zostav hru: build.bat
    exit /b 1
)
set "VER=1.0.0"
if not exist release mkdir release
set "OUT=release\Bartender3D-%VER%-win64.zip"
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference='Stop';" ^
  "$tmp = Join-Path $env:TEMP 'bt3d_release'; if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force };" ^
  "$dst = Join-Path $tmp 'Bartender3D'; New-Item -ItemType Directory -Force $dst | Out-Null;" ^
  "Copy-Item 'bin\*' $dst -Recurse;" ^
  "Get-ChildItem $dst -Directory -Filter '*DoNotShip*' | Remove-Item -Recurse -Force;" ^
  "Copy-Item 'README_PLAYERS.txt' (Join-Path $dst 'README.txt');" ^
  "if (Test-Path '%OUT%') { Remove-Item '%OUT%' };" ^
  "Compress-Archive -Path $dst -DestinationPath '%OUT%';" ^
  "Remove-Item $tmp -Recurse -Force"
if errorlevel 1 (
    echo [release] CHYBA pri baleni
    exit /b 1
)
echo [release] OK - %OUT%
exit /b 0