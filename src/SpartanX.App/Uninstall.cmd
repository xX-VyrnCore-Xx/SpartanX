@echo off
rem SpartanX uninstaller: closes every SpartanX process, restores the Windows taskbar,
rem removes launch-at-login and the Start menu shortcut, then deletes this folder.
setlocal
echo Uninstalling SpartanX / Disinstallazione di SpartanX
echo.
choice /c YN /m "Also delete your settings? / Eliminare anche le impostazioni?"
if errorlevel 2 (set "PURGE=") else (set "PURGE=--purge")

start "" /wait "%~dp0SpartanX.exe" --uninstall --silent %PURGE%

del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\SpartanX.lnk" 2>nul

rem Safety: only delete the folder if it is SpartanX's own (named SpartanX...).
for %%I in ("%~dp0.") do set "DIRNAME=%%~nxI"
echo %DIRNAME%| findstr /b /i "SpartanX" >nul
if errorlevel 1 (
    echo SpartanX is closed. Delete its files manually from "%~dp0".
    echo SpartanX e' chiuso. Elimina manualmente i suoi file da "%~dp0".
    pause
    exit /b 0
)

echo Removing / Rimozione di "%~dp0" ...
cd /d "%TEMP%"
start "" /min cmd /c "timeout /t 2 /nobreak >nul & rd /s /q "%~dp0""
endlocal
