<#
.SYNOPSIS
    mcp_call.ps1 - MiniMax/Mavis MCP for Unity 工具调用封装

.USAGE
    # 初始化(首次或 session 失效时)
    .\mcp_call.ps1 init

    # 调用工具
    .\mcp_call.ps1 call <tool_name> <json_params>
    # 示例:
    .\mcp_call.ps1 call manage_scene '{"action":"get_hierarchy","page_size":3}'
    .\mcp_call.ps1 call read_console '{"filter":"Error","count":10}'
    .\mcp_call.ps1 call find_gameobjects '{"name":"Battle"}'
    .\mcp_call.ps1 call execute_code '{"code":"UnityEngine.Debug.Log(\"hello\");"}'

    # 读取资源
    .\mcp_call.ps1 read <mcpforunity://uri>
    # 示例:
    .\mcp_call.ps1 read "mcpforunity://editor/state"
    .\mcp_call.ps1 read "mcpforunity://project/info"

    # 查看当前 session 状态
    .\mcp_call.ps1 status

    # 直接裸调(任意 JSON-RPC 请求)
    .\mcp_call.ps1 raw '{"jsonrpc":"2.0","id":5,"method":"tools/list","params":{}}'

.NOTES
    session 持久化在 .mcp_session 文件,Unity 重启后需重新 init。
#>

param(
    [Parameter(Position=0)]
    [ValidateSet('init', 'call', 'read', 'status', 'raw', 'tools', 'help')]
    [string]$Action = 'help',

    [Parameter(Position=1)]
    [string]$Arg1 = '',

    [Parameter(Position=2)]
    [string]$Arg2 = ''
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

# ---- 配置 ----
$MCP_BASE_URL = "http://127.0.0.1:8080/mcp"
$SESSION_FILE = Join-Path $PSScriptRoot ".mcp_session"
$INIT_FILE    = Join-Path $PSScriptRoot ".mcp_probe_init.json"
$TMP_BODY    = Join-Path $PSScriptRoot ".mcp_tmp_body.json"

# ---- helpers ----
function Get-SessionId {
    if (Test-Path $SESSION_FILE) {
        return (Get-Content $SESSION_FILE -Raw -Encoding UTF8).Trim()
    }
    return $null
}

function Save-SessionId($sid) {
    $sid | Out-File -FilePath $SESSION_FILE -Encoding UTF8 -NoNewline
}

function Write-Body($obj) {
    $obj | ConvertTo-Json -Compress | Out-File -FilePath $TMP_BODY -Encoding UTF8 -NoNewline
}

function Invoke-McpRaw($method, $id, $params = @{}) {
    $sid = Get-SessionId
    if (-not $sid) {
        Write-Error "No session. Run '.\mcp_call.ps1 init' first."
        return $null
    }
    $body = @{
        jsonrpc = "2.0"
        id      = $id
        method  = $method
        params  = $params
    }
    Write-Body $body
    $out = curl.exe -sS -X POST $MCP_BASE_URL `
        -H "Content-Type: application/json" `
        -H "Accept: application/json, text/event-stream" `
        -H "mcp-session-id: $sid" `
        --data-binary "@$TMP_BODY" `
        --max-time 30
    return $out
}

function Parse-Sse($raw) {
    $events = @()
    # split on double newlines
    $blocks = $raw -split "`n`n"
    foreach ($block in $blocks) {
        $block = $block.Trim()
        if (-not $block) { continue }
        $dataStr = ""
        foreach ($line in ($block -split "`n")) {
            if ($line -match '^data:\s*(.*)$') {
                $dataStr = $matches[1]
                break
            }
        }
        if ($dataStr) {
            try {
                $events += $dataStr | ConvertFrom-Json
            } catch {
                # ignore parse errors
            }
        }
    }
    return $events
}

function Get-SuccessfulResult($raw) {
    $events = Parse-Sse $raw
    foreach ($e in $events) {
        if ($e.PSObject.Properties.Name -contains 'result') {
            return $e.result
        }
    }
    return $null
}

function Read-Auto($path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes.Count -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        return [System.Text.Encoding]::UTF8.GetString($bytes)  # already utf-8 with BOM
    }
    # check for UTF-16
    if ($bytes.Count % 2 -eq 0) {
        $half = [Math]::Min(200, $bytes.Count)
        $nul = 0
        for ($i = 1; $i -lt $half; $i += 2) { if ($bytes[$i] -eq 0) { $nul++ } }
        if ($nul -gt $half * 0.3) {
            return [System.Text.Encoding]::Unicode.GetString($bytes)
        }
    }
    return [System.Text.Encoding]::UTF8.GetString($bytes)
}

# ---- actions ----
switch ($Action) {
    'help' {
        Get-Help $PSCommandPath -Full
    }

    'init' {
        Write-Host "Connecting to $MCP_BASE_URL ..." -ForegroundColor Cyan

        # initialize - use cmd.exe to run curl with -i (include headers) so we can parse session id
        $initBody = @{
            jsonrpc = "2.0"
            id      = 1
            method  = "initialize"
            params  = @{
                protocolVersion = "2024-11-05"
                capabilities    = @{}
                clientInfo      = @{
                    name    = "mcp_call-mavis"
                    version = "1.0"
                }
            }
        }
        Write-Body $initBody

        # Use cmd.exe to run curl -i, redirect to file so we can parse headers
        $curlCmd = "curl.exe -sS -i -X POST `"$MCP_BASE_URL`" -H `"Content-Type: application/json`" -H `"Accept: application/json, text/event-stream`" --data-binary `"$TMP_BODY`" --max-time 15 > `"$env:TEMP\mcp_init_full.txt`" 2>&1"
        cmd.exe /c $curlCmd

        $full = Get-Content "$env:TEMP\mcp_init_full.txt" -Raw -ErrorAction SilentlyContinue
        if (-not $full) {
            Write-Error "curl returned nothing. Is Unity MCP server running?"
            exit 1
        }

        # extract mcp-session-id from HTTP response headers
        $newSid = $null
        $lines = ($full -replace "`r`n","`n") -split "`n"
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '^mcp-session-id:\s*(.+)$') {
                $newSid = $matches[1].Trim()
                break
            }
        }

        if (-not $newSid) {
            Write-Error "Could not extract mcp-session-id from response headers."
            Write-Output $full | Select-Object -First 25
            exit 1
        }

        Save-SessionId $newSid
        Write-Host "Session: $newSid" -ForegroundColor Green

        # 2) send notifications/initialized
        $notifBody = @{ jsonrpc = "2.0"; method = "notifications/initialized"; params = @{} }
        Write-Body $notifBody
        curl.exe -sS -X POST $MCP_BASE_URL `
            -H "Content-Type: application/json" `
            -H "mcp-session-id: $newSid" `
            --data-binary "@$TMP_BODY" --max-time 5 | Out-Null

        # 3) confirm with tools/list
        Write-Host "Confirming with tools/list..." -ForegroundColor Cyan
        $listBody = @{ jsonrpc = "2.0"; id = 2; method = "tools/list"; params = @{} }
        Write-Body $listBody
        $raw2 = curl.exe -sS -X POST $MCP_BASE_URL `
            -H "Content-Type: application/json" `
            -H "Accept: application/json, text/event-stream" `
            -H "mcp-session-id: $newSid" `
            --data-binary "@$TMP_BODY" --max-time 15
        $events2 = Parse-Sse $raw2
        foreach ($e in $events2) {
            if ($e.result.PSObject.Properties.Name -contains 'tools') {
                Write-Host "Connected! Tools: $($e.result.tools.Count)" -ForegroundColor Green
            }
            if ($e.error) {
                Write-Error "Server error: $($e.error.message)"
            }
        }
        Write-Host "Session saved to .mcp_session  |  Run '.\mcp_call.ps1 status' to verify." -ForegroundColor Yellow
    }

    'status' {
        $sid = Get-SessionId
        if (-not $sid) {
            Write-Warning "No session. Run '.\mcp_call.ps1 init' first."
            exit 1
        }
        Write-Host "Session: $sid" -ForegroundColor Cyan

        # quick tools/list to check liveness
        $body = @{ jsonrpc = "2.0"; id = 99; method = "tools/list"; params = @{} }
        Write-Body $body
        $raw = curl.exe -sS -X POST $MCP_BASE_URL `
            -H "Content-Type: application/json" `
            -H "Accept: application/json, text/event-stream" `
            -H "mcp-session-id: $sid" `
            --data-binary "@$TMP_BODY" --max-time 15
        $events = Parse-Sse $raw
        $ok = $false
        foreach ($e in $events) {
            if ($e.result.PSObject.Properties.Name -contains 'tools') {
                Write-Host "Server alive. Tools: $($e.result.tools.Count)" -ForegroundColor Green
                $ok = $true
            }
            if ($e.error) {
                Write-Error "Session expired or error: $($e.error.message)"
                Remove-Item $SESSION_FILE -ErrorAction SilentlyContinue
                Write-Host "Session cleared. Run '.\mcp_call.ps1 init' again." -ForegroundColor Yellow
                exit 1
            }
        }
        if (-not $ok) {
            Write-Warning "No response. Is Unity MCP server still running?"
        }
    }

    'call' {
        if (-not $Arg1) {
            Write-Error "Usage: .\mcp_call.ps1 call <tool_name> <json_params>"
            exit 1
        }
        $toolName = $Arg1
        $params = @{}
        if ($Arg2) {
            try {
                $params = $Arg2 | ConvertFrom-Json
            } catch {
                Write-Error "Invalid JSON params: $Arg2"
                exit 1
            }
        }
        $callBody = @{
            jsonrpc = "2.0"
            id      = (Get-Date).Millisecond + (Get-Random -Maximum 1000)
            method  = "tools/call"
            params  = @{
                name      = $toolName
                arguments = $params
            }
        }
        Write-Body $callBody
        $raw = curl.exe -sS -X POST $MCP_BASE_URL `
            -H "Content-Type: application/json" `
            -H "Accept: application/json, text/event-stream" `
            -H "mcp-session-id: $(Get-SessionId)" `
            --data-binary "@$TMP_BODY" --max-time 60

        $events = Parse-Sse $raw
        $found = $false
        foreach ($e in $events) {
            if ($e.error) {
                Write-Error "MCP Error [$($e.error.code)]: $($e.error.message)"
                $found = $true
            }
            if ($e.result.PSObject.Properties.Name -contains 'content') {
                # pretty print content blocks
                $e.result.content | ForEach-Object {
                    if ($_.type -eq "text") {
                        # try to pretty-print JSON
                        try {
                            $j = $_."text" | ConvertFrom-Json
                            $j | ConvertTo-Json -Depth 20 -Compress:$false | Write-Output
                        } catch {
                            Write-Output $_."text"
                        }
                    } elseif ($_.type -eq "image") {
                        Write-Output "[image: $($_.data | Select-Object -First 50)... ]"
                    } else {
                        $_ | ConvertTo-Json -Depth 5 -Compress:$false | Write-Output
                    }
                }
                $found = $true
            }
        }
        if (-not $found) {
            Write-Warning "No result. Raw output:"
            Write-Output $raw | Select-Object -First 30
        }
    }

    'read' {
        if (-not $Arg1) {
            Write-Error "Usage: .\mcp_call.ps1 read <mcpforunity://uri>"
            exit 1
        }
        $uri = $Arg1.TrimStart('"').TrimEnd('"')
        $readBody = @{
            jsonrpc = "2.0"
            id      = (Get-Date).Millisecond + (Get-Random -Maximum 1000)
            method  = "resources/read"
            params  = @{ uri = $uri }
        }
        Write-Body $readBody
        $raw = curl.exe -sS -X POST $MCP_BASE_URL `
            -H "Content-Type: application/json" `
            -H "Accept: application/json, text/event-stream" `
            -H "mcp-session-id: $(Get-SessionId)" `
            --data-binary "@$TMP_BODY" --max-time 30

        $events = Parse-Sse $raw
        foreach ($e in $events) {
            if ($e.error) {
                Write-Error "MCP Error: $($e.error.message)"
            }
            if ($e.result.PSObject.Properties.Name -contains 'contents') {
                foreach ($c in $e.result.contents) {
                    $text = $c.text
                    try {
                        $j = $text | ConvertFrom-Json
                        $j | ConvertTo-Json -Depth 20 -Compress:$false | Write-Output
                    } catch {
                        Write-Output $text
                    }
                }
            }
        }
    }

    'tools' {
        $body = @{ jsonrpc = "2.0"; id = (Get-Random); method = "tools/list"; params = @{} }
        Write-Body $body
        $raw = curl.exe -sS -X POST $MCP_BASE_URL `
            -H "Content-Type: application/json" `
            -H "Accept: application/json, text/event-stream" `
            -H "mcp-session-id: $(Get-SessionId)" `
            --data-binary "@$TMP_BODY" --max-time 15
        $events = Parse-Sse $raw
        foreach ($e in $events) {
            if ($e.result.PSObject.Properties.Name -contains 'tools') {
                $e.result.tools | ForEach-Object {
                    Write-Host "  $($_.name)" -ForegroundColor Cyan
                    if ($_.description) {
                        Write-Host "    $($_.description)" -ForegroundColor Gray
                    }
                }
            }
        }
    }

    'raw' {
        if (-not $Arg1) {
            Write-Error "Usage: .\mcp_call.ps1 raw '<json-rpc-body>'"
            exit 1
        }
        $sid = Get-SessionId
        if (-not $sid) {
            Write-Error "No session. Run '.\mcp_call.ps1 init' first."
            exit 1
        }
        # write raw body as-is
        $Arg1.TrimStart('"').TrimEnd('"') | Out-File -FilePath $TMP_BODY -Encoding UTF8 -NoNewline
        $raw = curl.exe -sS -X POST $MCP_BASE_URL `
            -H "Content-Type: application/json" `
            -H "Accept: application/json, text/event-stream" `
            -H "mcp-session-id: $sid" `
            --data-binary "@$TMP_BODY" --max-time 60
        Write-Output $raw
    }
}
