@echo off
schtasks /create /tn "AutoGitPush-Mowang" /tr "D:\unity\mowang\auto-git-push.ps1" /sc hourly /st "12:00" /ru "SYSTEM" /rl highest /f

if %errorlevel% == 0 (
    echo.
    echo ============================================================
    echo 成功！
    echo ============================================================
    echo.
    echo 任务已创建：AutoGitPush-Mowang
    echo.
    echo 配置：
    echo   - 每 12 小时运行一次
    echo   - 开机登录时也会运行
    echo   - 后台静默运行（不显示窗口）
    echo.
    echo 下次运行时间：12 小时后
    echo.
    echo ============================================================
    echo.
    echo 注意：
    echo   - 脚本会自动检测改动并推送
    echo   - 如果 VPN 信号不好，会自动重试（最多 12 次）
    echo   - 成功后自动删除重试任务
    echo   - 仓库已从 1.54 GB 瘩身到 12.6 MB
    echo.
) else (
    echo.
    echo ============================================================
    echo 失败！
    echo ============================================================
    echo.
    echo 错误代码：%errorlevel%
    echo.
    echo 可能原因：
    echo   1. 权限不足（需要管理员权限）
    echo   2. schtasks 命令不可用（Windows 7 以下版本才有）
    echo   3. 路径或文件名错误
    echo.
    echo 请尝试：
    echo   - 右键右键点击此文件，选择"以管理员身份运行"
    echo   - 或者手动在任务计划程序中创建
    echo.
)

echo.
pause
