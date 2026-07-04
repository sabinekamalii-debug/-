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
    # Use try-catch to avoid locking issues
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

if (-not $hasChanges) {
    Write-Log "No changes, skip"
    # Even if no local changes, try to push if there are unpushed commits
    $unpushed = git log origin/main..HEAD --oneline 2>&1
    if ($LASTEXITCODE -ne 0 -or $unpushed.ToString().Trim() -eq "") {
        Write-Log "No unpushed commits either, exiting"
        exit 0
    }
    Write-Log "Found unpushed commits, will push"
} else {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Log "Changes detected, committing..."

    # Add all except the log file
    git add -A 2>&1 | ForEach-Object { Write-Log $_ }

    # Reset the log file from staging if it got added
    git reset HEAD -- auto-git-push.log 2>&1 | ForEach-Object { Write-Log $_ }

    $commitMsg = "auto: $timestamp"
    git commit -m $commitMsg 2>&1 | ForEach-Object { Write-Log $_ }

    if ($LASTEXITCODE -ne 0) {
        Write-Log "Commit failed, checking if there are still unpushed commits..."
    }
}

# --- Determine push command ---
# Check if upstream is set
$hasUpstream = $false
try {
    $upstreamResult = git rev-parse --abbrev-ref --symbolic-full-name main@{upstream} 2>&1
    if ($LASTEXITCODE -eq 0 -and $upstreamResult -ne $null -and $upstreamResult.ToString().Trim() -ne "") {
        $hasUpstream = $true
    }
} catch {}

$pushCmd = if ($hasUpstream) { "git push" } else { "git push -u origin main" }
Write-Log "Push command: $pushCmd (upstream=$hasUpstream)"

# --- Retry push: 12 attempts, 5 min apart = 60-min window for VPN recovery ---
$pushSuccess = $false
$maxRetries = 12
$retryInterval = 300  # 5 minutes

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
        Write-Log "Push failed, will retry in $retryInterval seconds ($(($i * $retryInterval) / 60) min elapsed so far)..."
        Start-Sleep -Seconds $retryInterval
    }
}

if (-not $pushSuccess) {
    Write-Log "Push failed after $maxRetries retries (60 min total), will try again next scheduled run"
}

Write-Log "===== Done ====="
