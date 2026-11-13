@echo off
rem SpartanX installer: copies this folder to %LOCALAPPDATA%\Programs\SpartanX,
rem adds a Start menu shortcut and starts SpartanX. No admin rights needed.
setlocal
set "DEST=%LOCALAPPDATA%\Programs\SpartanX"

if /i "%~dp0"=="%DEST%\" goto shortcut

echo Installing SpartanX to "%DEST%" ...
rem Close a running copy first so its files can be replaced.
if exist "%DEST%\SpartanX.exe" start "" /wait "%DEST%\SpartanX.exe" --quit --silent

robocopy "%~dp0." "%DEST%" /E /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
    echo Copy failed. / Copia non riuscita.
    pause
    exit /b 1
)

:shortcut
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$d = Join-Path $env:LOCALAPPDATA 'Programs\SpartanX';" ^
  "$l = Join-Path ([Environment]::GetFolderPath('Programs')) 'SpartanX.lnk';" ^
  "$s = (New-Object -ComObject WScript.Shell).CreateShortcut($l);" ^
  "$s.TargetPath = Join-Path $d 'SpartanX.exe'; $s.WorkingDirectory = $d; $s.Save()"

echo SpartanX installed. / SpartanX installato.
start "" "%DEST%\SpartanX.exe"
endlocal
