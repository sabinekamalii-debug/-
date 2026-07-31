@echo off
setlocal enabledelayedexpansion

echo.
echo ============================================================
echo   Auto Git Push Task Installer v3
echo ============================================================
echo.
echo   Task 1: AutoGitPush-Mowang   - Push every 10 minutes
echo   Task 2: AutoGitBundle-Mowang - Full backup daily at 22:00
echo.
echo   Admin rights required.
echo   Right-click this file ^> "Run as administrator"
echo.
pause

:: Delete old tasks
schtasks /delete /tn "AutoGitPush-Mowang" /f >nul 2>&1
schtasks /delete /tn "AutoGitBundle-Mowang" /f >nul 2>&1

:: Task 1: Push every 10 minutes
schtasks /create /tn "AutoGitPush-Mowang" /tr "powershell.exe -ExecutionPolicy Bypass -NoProfile -File D:\unity\mowang\auto-git-push.ps1" /sc minute /mo 10 /rl highest /f
if %errorlevel% == 0 (
    echo   [OK] Task 1: Push every 10 min
) else (
    echo   [FAIL] Task 1: error %errorlevel%
)

:: Task 2: Daily bundle backup at 22:00
schtasks /create /tn "AutoGitBundle-Mowang" /tr "powershell.exe -ExecutionPolicy Bypass -NoProfile -File D:\unity\mowang\auto-git-bundle.ps1" /sc daily /st "22:00" /rl highest /f
if %errorlevel% == 0 (
    echo   [OK] Task 2: Daily bundle at 22:00 -^> D:\GitBackups\
) else (
    echo   [FAIL] Task 2: error %errorlevel%
)

echo.
echo ============================================================
echo   Run push test now?
echo ============================================================
pause
echo.
echo Running auto-git-push.ps1...
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "D:\unity\mowang\auto-git-push.ps1"
echo.
echo Log: D:\unity\mowang\auto-git-push.log
echo.
pause
