# ============================================================
# Auto Git Push Script v3
# 由 Windows 任务计划每 10 分钟调用，运行后退出
# ============================================================

$projectPath = "D:\unity\mowang"
$logFile     = "$projectPath\auto-git-push.log"

Set-Location $projectPath

function Write-Log {
    param([string]$msg)
    $time = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    try { Add-Content -Path $logFile -Value "[$time] $msg" -Encoding UTF8 -ErrorAction SilentlyContinue } catch {}
}

Write-Log "----- Check start -----"

# --- 1. 清理死代理 (global + local) ---
$proxyG = git config --global --get http.proxy 2>&1
$proxyL = git config --local --get http.proxy 2>&1
if (($proxyG -and $proxyG.ToString().Trim() -ne "") -or ($proxyL -and $proxyL.ToString().Trim() -ne "")) {
    Write-Log "Git proxy found — testing connectivity..."
    $testResult = git ls-remote --heads origin 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Log "Proxy dead, removing all proxy config"
        git config --global --unset http.proxy 2>&1 | Out-Null
        git config --global --unset https.proxy 2>&1 | Out-Null
        git config --local --unset http.proxy 2>&1 | Out-Null
        git config --local --unset https.proxy 2>&1 | Out-Null
        git config --local --unset http.sslbackend 2>&1 | Out-Null
    } else {
        Write-Log "Proxy works, keeping it"
    }
}

# --- 2. 检查是否有改动 ---
$status = git status --porcelain 2>&1
if ($status -eq $null -or $status.ToString().Trim() -eq "") {
    Write-Log "No changes, exit"
    Write-Log "----- Done -----"
    exit 0
}

# --- 3. 安全检查：危险目录 ---
$dangerousDirs = @("Library/", "Temp/", "Logs/", "Obj/", "Build/", "Builds/", "UserSettings/")
$statusLines = $status -split "`n"
$foundDangerous = @()
foreach ($line in $statusLines) {
    $path = $line.Trim().Substring(3).Trim()  # 去掉状态前缀 "XY "
    foreach ($dir in $dangerousDirs) {
        if ($path -like "$dir*" -or $path -like "*/$dir*") {
            $foundDangerous += $path
        }
    }
}
if ($foundDangerous.Count -gt 0) {
    Write-Log "DANGER: dangerous paths detected, aborting commit:"
    foreach ($p in $foundDangerous) { Write-Log "  $p" }
    Write-Log "----- Aborted -----"
    exit 1
}

# --- 4. git add . ---
git add . 2>&1 | ForEach-Object { Write-Log $_ }
# 排除日志自身
git reset HEAD -- auto-git-push.log 2>&1 | Out-Null

# --- 5. commit ---
$fileCount = ($statusLines | Where-Object { $_.Trim() -ne "" }).Count
$now = Get-Date
$dateStr = $now.ToString("yyyy-MM-dd HH:mm")
$commitMsg = "Auto Backup $dateStr`nChanged: $fileCount files"
$commitResult = git commit -m $commitMsg 2>&1
if ($LASTEXITCODE -ne 0) {
    $commitText = $commitResult -join "`n"
    if ($commitText -match "nothing to commit" -or $commitText -match "no changes") {
        Write-Log "Nothing to commit (already staged)"
    } else {
        Write-Log "Commit failed: $commitText"
        Write-Log "----- Done -----"
        exit 1
    }
} else {
    Write-Log "Committed: $fileCount files"
}

# --- 6. push (只试一次) ---
$unpushed = git log origin/main..HEAD --oneline 2>&1
if ($LASTEXITCODE -ne 0 -or $unpushed.ToString().Trim() -eq "") {
    Write-Log "No unpushed commits"
    Write-Log "----- Done -----"
    exit 0
}

$pushOutput = git push origin main 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Log "Push OK"
} else {
    Write-Log "Push FAILED (will retry next scheduled run)"
    $pushOutput | ForEach-Object { Write-Log "  $_" }
}

Write-Log "----- Done -----"
