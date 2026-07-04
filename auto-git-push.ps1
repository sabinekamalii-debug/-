# ============================================================
# Unity Project Auto Git Push Script
# Runs every 12 hours via Windows Task Scheduler
# Also runs at user logon to catch missed pushes
# Retries push up to 12 times with 5-min intervals (60-min window)
# ============================================================

$projectPath = "D:\unity\mowang"
$logFile     = "$projectPath\auto-git-push.log"

Set-Location $projectPath

function Write-Log {
    param([string]$msg)
    $time = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "[$time] $msg"
    try { Add-Content -Path $logFile -Value $line -Encoding UTF8 -ErrorAction SilentlyContinue } catch {}
}

Write-Log "===== Start checking ====="

if (-not (Test-Path "$projectPath\.git")) {
    Write-Log "Error: Not a git repository"
    exit 1
}

# --- Check for changes ---
$status = git status --porcelain 2>&1
$hasChanges = ($status -ne $null -and $status.ToString().Trim() -ne "")

if ($hasChanges) {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Log "Changes detected, committing..."

    git add -A 2>&1 | ForEach-Object { Write-Log $_ }
    git reset HEAD -- auto-git-push.log 2>&1 | Out-Null

    $commitMsg = "auto: $timestamp"
    git commit -m $commitMsg 2>&1 | ForEach-Object { Write-Log $_ }
} else {
    Write-Log "No changes to commit"
}

# --- Check if there are unpushed commits ---
$hasUpstream = $false
try {
    $upstreamResult = git rev-parse --abbrev-ref --symbolic-full-name main@{upstream} 2>&1
    if ($LASTEXITCODE -eq 0 -and $upstreamResult -ne $null -and $upstreamResult.ToString().Trim() -ne "") {
        $hasUpstream = $true
    }
} catch {}

if ($hasUpstream) {
    $unpushed = git log origin/main..HEAD --oneline 2>&1
    if ($LASTEXITCODE -ne 0 -or $unpushed.ToString().Trim() -eq "") {
        Write-Log "No unpushed commits, exiting"
        exit 0
    }
    Write-Log "Found unpushed commits"
    $pushCmd = "git push"
} else {
    Write-Log "No upstream set, will push with -u"
    $pushCmd = "git push -u origin main"
}

# --- Retry push: 12 attempts, 5 min apart = 60-min window ---
$pushSuccess = $false
$maxRetries = 12
$retryInterval = 300

for ($i = 1; $i -le $maxRetries; $i++) {
    Write-Log "Push attempt $i/$maxRetries..."
    $output = Invoke-Expression "$pushCmd 2>&1"
    $output | ForEach-Object { Write-Log $_ }

    if ($LASTEXITCODE -eq 0) {
        $pushSuccess = $true
        Write-Log "Push succeeded on attempt $i"
        break
    }

    if ($i -lt $maxRetries) {
        Write-Log "Will retry in $retryInterval seconds..."
        Start-Sleep -Seconds $retryInterval
    }
}

if (-not $pushSuccess) {
    Write-Log "Push failed after $maxRetries retries, will try again next scheduled run"
}

Write-Log "===== Done ====="
