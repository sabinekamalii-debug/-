#!/usr/bin/env python3
"""
mcp_call.py - MCP for Unity 工具调用封装

用法:
    python mcp_call.py init
    python mcp_call.py status
    python mcp_call.py tools
    python mcp_call.py call <tool> <json_params>
    python mcp_call.py call-file <tool> <json_file>
    python mcp_call.py read <uri>
    python mcp_call.py raw <json_body>

示例:
    python mcp_call.py call read_console {"filter":"Error","count":5}
    python mcp_call.py call-file read_console params.json
    python mcp_call.py read mcpforunity://editor/state
"""

import sys, os, json, time, re, subprocess, argparse, tempfile

MCP_BASE_URL = "http://127.0.0.1:8080/mcp"
SESSION_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".mcp_session")
TMP_BODY    = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".mcp_tmp_body.json")


def curl_post(body, session_id=None, timeout=30):
    with open(TMP_BODY, "w", encoding="utf-8") as f:
        json.dump(body, f, ensure_ascii=False)
    args = ["curl.exe", "-sS", "-X", "POST", MCP_BASE_URL,
            "-H", "Content-Type: application/json",
            "-H", "Accept: application/json, text/event-stream"]
    if session_id:
        args += ["-H", f"mcp-session-id: {session_id}"]
    args += ["--data-binary", f"@{TMP_BODY}", "--max-time", str(timeout)]
    result = subprocess.run(args, capture_output=True)
    raw = result.stdout
    if isinstance(raw, bytes):
        raw = raw.decode("utf-8", errors="replace")
    return raw


def curl_post_raw(body_str, session_id=None, timeout=60):
    """直接发送原始 JSON 字符串(不用 json.dump)"""
    with open(TMP_BODY, "w", encoding="utf-8") as f:
        f.write(body_str)
    args = ["curl.exe", "-sS", "-X", "POST", MCP_BASE_URL,
            "-H", "Content-Type: application/json",
            "-H", "Accept: application/json, text/event-stream"]
    if session_id:
        args += ["-H", f"mcp-session-id: {session_id}"]
    args += ["--data-binary", f"@{TMP_BODY}", "--max-time", str(timeout)]
    result = subprocess.run(args, capture_output=True)
    raw = result.stdout
    if isinstance(raw, bytes):
        raw = raw.decode("utf-8", errors="replace")
    return raw


def parse_sse(raw):
    events = []
    pattern = re.compile(r'data:\s*(.+?)(?=\ndata:|\n\n|$)', re.DOTALL)
    for m in pattern.finditer(raw):
        try:
            events.append(json.loads(m.group(1).strip()))
        except Exception:
            pass
    return events


def find_result(events):
    for e in events:
        if "result" in e:
            return e["result"]
    return None


def find_error(events):
    for e in events:
        if "error" in e:
            return e["error"]
    return None


def get_sid():
    if os.path.exists(SESSION_FILE):
        with open(SESSION_FILE, "r", encoding="utf-8") as f:
            return f.read().strip()
    return None


def save_sid(sid):
    with open(SESSION_FILE, "w", encoding="utf-8") as f:
        f.write(sid)


def extract_sid_from_headers_cmd(body):
    """用 curl -i 配合 cmd 拿到 HTTP headers 里的 mcp-session-id"""
    with open(TMP_BODY, "w", encoding="utf-8") as f:
        json.dump(body, f, ensure_ascii=False)
    cmd = (f'curl.exe -sS -i -X POST "{MCP_BASE_URL}" '
           f'-H "Content-Type: application/json" '
           f'-H "Accept: application/json, text/event-stream" '
           f'--data-binary "@{TMP_BODY}" --max-time 15')
    result = subprocess.run(cmd, shell=True, capture_output=True)
    raw = result.stdout
    if isinstance(raw, bytes):
        raw = raw.decode("utf-8", errors="replace")
    m = re.search(r'mcp-session-id:\s*([^\r\n]+)', raw, re.IGNORECASE)
    return m.group(1).strip() if m else None


def pretty_print(content):
    for block in content:
        if block.get("type") == "text":
            text = block.get("text", "")
            try:
                parsed = json.loads(text)
                print(json.dumps(parsed, ensure_ascii=False, indent=2))
            except Exception:
                print(text)
        elif block.get("type") == "image":
            print(f"[image: {block.get('data','')[:80]}...]")
        else:
            print(json.dumps(block, ensure_ascii=False, indent=2))


# ---- commands ----
def cmd_init():
    print(f"Connecting to {MCP_BASE_URL} ...")
    body = {"jsonrpc":"2.0","id":1,"method":"initialize",
            "params":{"protocolVersion":"2024-11-05","capabilities":{},
                      "clientInfo":{"name":"mcp_call-mavis","version":"1.0"}}}
    new_sid = extract_sid_from_headers_cmd(body)
    if not new_sid:
        print("ERROR: Could not get mcp-session-id. Is Unity MCP server running?", file=sys.stderr)
        sys.exit(1)
    save_sid(new_sid)
    print(f"Session: {new_sid}")
    # notifications/initialized
    curl_post({"jsonrpc":"2.0","method":"notifications/initialized","params":{}}, session_id=new_sid, timeout=5)
    # confirm
    print("Confirming...")
    stdout = curl_post({"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}, session_id=new_sid, timeout=15)
    events = parse_sse(stdout)
    err = find_error(events)
    if err:
        print(f"Server error: {err.get('message', err)}", file=sys.stderr)
    result = find_result(events)
    if result and "tools" in result:
        print(f"Connected! Tools: {len(result['tools'])}")
    else:
        print("Session is active.")
    print(f"Done. Run 'python mcp_call.py status' to verify.")


def cmd_status():
    sid = get_sid()
    if not sid:
        print("No session. Run 'python mcp_call.py init' first.", file=sys.stderr)
        sys.exit(1)
    print(f"Session: {sid}")
    stdout = curl_post({"jsonrpc":"2.0","id":99,"method":"tools/list","params":{}}, session_id=sid, timeout=15)
    events = parse_sse(stdout)
    err = find_error(events)
    if err:
        print(f"Session invalid: {err.get('message', err)}", file=sys.stderr)
        if os.path.exists(SESSION_FILE):
            os.remove(SESSION_FILE)
        sys.exit(1)
    result = find_result(events)
    if result and "tools" in result:
        print(f"Server alive. Tools: {len(result['tools'])}")
    else:
        print("No response. Is Unity MCP server still running?", file=sys.stderr)


def cmd_tools():
    sid = get_sid() or die("No session. Run init first.")
    stdout = curl_post({"jsonrpc":"2.0","id":100,"method":"tools/list","params":{}}, session_id=sid, timeout=15)
    events = parse_sse(stdout)
    err = find_error(events)
    if err:
        print(f"Error: {err.get('message', err)}", file=sys.stderr)
        sys.exit(1)
    result = find_result(events)
    if result and "tools" in result:
        for t in result["tools"]:
            print(f"  {t.get('name','?')}")
            desc = t.get("description","")
            if desc:
                for line in desc.split("\n")[:2]:
                    print(f"    {line.strip()}")


def cmd_call(tool_name, params_str):
    sid = get_sid() or die("No session. Run init first.")
    params = {}
    if params_str:
        # params_str can be JSON or a file path — detect by leading '{'
        p = params_str.strip()
        if p.startswith("{"):
            try:
                params = json.loads(p)
            except Exception as e:
                print(f"Invalid JSON: {e}", file=sys.stderr)
                sys.exit(1)
        else:
            # treat as file path
            if not os.path.exists(p):
                print(f"File not found: {p}", file=sys.stderr)
                sys.exit(1)
            with open(p, "r", encoding="utf-8") as f:
                params = json.load(f)

    body = {"jsonrpc":"2.0","id":int(time.time()*1000)%100000,
            "method":"tools/call","params":{"name":tool_name,"arguments":params}}
    stdout = curl_post(body, session_id=sid, timeout=60)
    events = parse_sse(stdout)
    err = find_error(events)
    if err:
        print(f"MCP Error [{err.get('code','?')}]: {err.get('message',err)}", file=sys.stderr)
        sys.exit(1)
    result = find_result(events)
    if result:
        pretty_print(result.get("content", []))
    else:
        print("No result.", file=sys.stderr)


def cmd_read(uri):
    sid = get_sid() or die("No session. Run init first.")
    body = {"jsonrpc":"2.0","id":int(time.time()*1000)%100000,
            "method":"resources/read","params":{"uri":uri}}
    stdout = curl_post(body, session_id=sid, timeout=30)
    events = parse_sse(stdout)
    err = find_error(events)
    if err:
        print(f"MCP Error: {err.get('message',err)}", file=sys.stderr)
        sys.exit(1)
    result = find_result(events)
    if result:
        for c in result.get("contents", []):
            text = c.get("text","")
            try:
                print(json.dumps(json.loads(text), ensure_ascii=False, indent=2))
            except Exception:
                print(text)


def cmd_raw(raw_str):
    sid = get_sid() or die("No session. Run init first.")
    stdout = curl_post_raw(raw_str, session_id=sid, timeout=60)
    events = parse_sse(stdout)
    err = find_error(events)
    if err:
        print(f"MCP Error: {err.get('message',err)}", file=sys.stderr)
        sys.exit(1)
    result = find_result(events)
    if result:
        if isinstance(result, dict) and "content" in result:
            pretty_print(result["content"])
        else:
            print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        print("No result.")
        print("Raw:", stdout[:500], file=sys.stderr)


def die(msg):
    print(msg, file=sys.stderr)
    sys.exit(1)


# ---- main ----
if __name__ == "__main__":
    # Parse manually to avoid PowerShell {} interpretation issues
    if len(sys.argv) < 2 or sys.argv[1] in ("help", "--help", "-h"):
        print(__doc__)
        sys.exit(0)

    action = sys.argv[1]

    if action == "init":
        cmd_init()
    elif action == "status":
        cmd_status()
    elif action == "tools":
        cmd_tools()
    elif action == "call":
        if len(sys.argv) < 3:
            die("Usage: python mcp_call.py call <tool_name> <json_or_file>")
        cmd_call(sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else "")
    elif action == "call-file":
        if len(sys.argv) < 4:
            die("Usage: python mcp_call.py call-file <tool_name> <params_file>")
        cmd_call(sys.argv[2], sys.argv[3])
    elif action == "read":
        if len(sys.argv) < 3:
            die("Usage: python mcp_call.py read <mcpforunity://uri>")
        cmd_read(sys.argv[2])
    elif action == "raw":
        if len(sys.argv) < 3:
            die("Usage: python mcp_call.py raw <json_body>")
        cmd_raw(sys.argv[2])
    else:
        die(f"Unknown action: {action}. Run with no args for help.")
