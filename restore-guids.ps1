cd D:\unity\mowang
$metaFiles = git show f3c9986 --name-only -- '*.meta' | Where-Object { $_ -match '^Assets/' }
$restored = 0; $alreadyOk = 0; $notFound = 0; $noHexGuid = 0

foreach ($metaPath in $metaFiles) {
    $metaPath = $metaPath.Trim()
    if ($metaPath -notmatch '^Assets/') { continue }
    
    # Get old GUID from git
    $oldContent = git show "f3c9986:$metaPath" 2>$null
    if ($LASTEXITCODE -ne 0) { continue }
    
    if ($oldContent -match 'guid:\s+([a-f0-9]{32})') {
        $oldGuid = $Matches[1]
    } else { $noHexGuid++; continue }
    
    # Find file on disk
    $diskPath = 'D:\unity\mowang\' + ($metaPath -replace '/', '\')
    $actualPath = $null
    
    if (Test-Path $diskPath) {
        $actualPath = $diskPath
    } else {
        $fileName = [System.IO.Path]::GetFileName($metaPath)
        $dirName = [System.IO.Path]::GetFileName([System.IO.Path]::GetDirectoryName($metaPath))
        $searchRoot = "D:\unity\mowang\Assets\先存放文件夹\$dirName"
        if (Test-Path $searchRoot) {
            $found = Get-ChildItem -Path $searchRoot -Filter $fileName -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($found) { $actualPath = $found.FullName }
        }
        if (-not $actualPath) {
            $found = Get-ChildItem -Path 'D:\unity\mowang\Assets' -Filter $fileName -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($found) { $actualPath = $found.FullName }
        }
    }
    
    if (-not $actualPath) { $notFound++; continue }
    
    # Read current .meta and replace GUID
    $currentContent = Get-Content $actualPath -Raw
    if ($currentContent -match "guid:\s+$oldGuid") {
        $alreadyOk++; continue
    }
    
    $newContent = $currentContent -replace 'guid:\s+[A-Za-z0-9+/=]+', "guid: $oldGuid"
    Set-Content -Path $actualPath -Value $newContent -NoNewline
    $restored++
}

Write-Host "Restored: $restored"
Write-Host "Already OK: $alreadyOk"
Write-Host "Not found: $notFound"
Write-Host "No hex GUID: $noHexGuid"
