# ============================================================
# Auto Git Bundle Backup Script
# 由 Windows 任务计划每天 22:00 调用，运行后退出
# 生成完整仓库备份到 D:\GitBackups\
# ============================================================

$projectPath = "D:\unity\mowang"
$backupDir   = "D:\GitBackups"
$logFile     = "$projectPath\auto-git-push.log"

Set-Location $projectPath

function Write-Log {
    param([string]$msg)
    $time = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    try { Add-Content -Path $logFile -Value "[$time] $msg" -Encoding UTF8 -ErrorAction SilentlyContinue } catch {}
}

Write-Log "----- Bundle backup start -----"

# 确保备份目录存在
if (-not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    Write-Log "Created backup dir: $backupDir"
}

# 生成 bundle
$dateStr = Get-Date -Format "yyyy-MM-dd_HHmm"
$bundlePath = "$backupDir\mowang_$dateStr.bundle"

$bundlResult = git bundle create $bundlePath --all 2>&1
if ($LASTEXITCODE -eq 0) {
    $size = [math]::Round((Get-Item $bundlePath).Length / 1MB, 1)
    Write-Log "Bundle OK: $bundlePath ($size MB)"

    # 清理超过 7 天的旧 bundle
    $oldBundles = Get-ChildItem "$backupDir\mowang_*.bundle" | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-7) }
    foreach ($old in $oldBundles) {
        Remove-Item $old.FullName -Force
        Write-Log "Deleted old bundle: $($old.Name)"
    }
} else {
    Write-Log "Bundle FAILED:"
    $bundlResult | ForEach-Object { Write-Log "  $_" }
}

Write-Log "----- Bundle done -----"
