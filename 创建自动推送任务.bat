@echo off
chcp 65001 >nul 2>&1
echo.
echo ============================================================
echo   自动推送任务安装器 v3
echo ============================================================
echo.
echo   任务1: AutoGitPush-Mowang  — 每 10 分钟推送一次
echo   任务2: AutoGitBundle-Mowang — 每天 22:00 完整备份
echo.
echo   需要管理员权限创建任务计划。
echo   请右键此文件 → "以管理员身份运行"
echo.
pause

:: ===== 任务1: 每 10 分钟推送 =====
schtasks /delete /tn "AutoGitPush-Mowang" /f >nul 2>&1
schtasks /create /tn "AutoGitPush-Mowang" ^
    /tr "powershell.exe -ExecutionPolicy Bypass -NoProfile -File D:\unity\mowang\auto-git-push.ps1" ^
    /sc minute /mo 10 ^
    /rl highest ^
    /f

if %errorlevel% == 0 (
    echo   [OK] 任务1创建成功: 每 10 分钟推送
) else (
    echo   [FAIL] 任务1创建失败，错误代码: %errorlevel%
)

:: ===== 任务2: 每天 22:00 完整备份 =====
schtasks /delete /tn "AutoGitBundle-Mowang" /f >nul 2>&1
schtasks /create /tn "AutoGitBundle-Mowang" ^
    /tr "powershell.exe -ExecutionPolicy Bypass -NoProfile -File D:\unity\mowang\auto-git-bundle.ps1" ^
    /sc daily /st "22:00" ^
    /rl highest ^
    /f

if %errorlevel% == 0 (
    echo   [OK] 任务2创建成功: 每天 22:00 完整备份到 D:\GitBackups\
) else (
    echo   [FAIL] 任务2创建失败，错误代码: %errorlevel%
)

echo.
echo ============================================================
echo   立即运行一次推送测试？
echo ============================================================
pause
powershell.exe -ExecutionPolicy Bypass -NoProfile -File D:\unity\mowang\auto-git-push.ps1
echo.
echo   日志: type D:\unity\mowang\auto-git-push.log
echo.
pause
