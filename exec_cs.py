"""
执行 C# 代码文件（Unity 编辑器内）
用法: python exec_cs.py <code_file.cs>
"""
import sys, json, urllib.request, os

MCP_URL = "http://127.0.0.1:8080/mcp"
SESSION_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".mcp_session.txt")

def post(payload, sid=None):
    data = json.dumps(payload).encode("utf-8")
    headers = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
    if sid: headers["mcp-session-id"] = sid
    req = urllib.request.Request(MCP_URL, data=data, headers=headers, method="POST")
    with urllib.request.urlopen(req, timeout=120) as resp:
        return resp.headers.get("mcp-session-id"), resp.read().decode("utf-8")

def parse_sse(body):
    for line in body.splitlines():
        if line.startswith("data:"):
            try: return json.loads(line[5:].strip())
            except: pass
    return None

def get_session():
    if os.path.exists(SESSION_FILE):
        sid = open(SESSION_FILE, encoding="utf-8").read().strip()
        _, res = post({"jsonrpc":"2.0","id":99,"method":"tools/list"}, sid)
        if res and "result" in parse_sse(res) if res else "":
            return sid
    return None

def new_session():
    sid, _ = post({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"unity-agent","version":"1.0"}}})
    post({"jsonrpc":"2.0","method":"notifications/initialized"}, sid)
    open(SESSION_FILE,"w",encoding="utf-8").write(sid)
    return sid

def main():
    if len(sys.argv) < 2:
        print("用法: python exec_cs.py <code_file.cs>"); sys.exit(1)
    code = open(sys.argv[1], encoding="utf-8").read()
    sid = get_session() or new_session()
    payload = {"jsonrpc":"2.0","id":20,"method":"tools/call",
               "params":{"name":"execute_code","arguments":{"action":"execute","code":code}}}
    _, body = post(payload, sid)
    res = parse_sse(body)
    if res and "result" in res:
        for c in res["result"].get("content",[]):
            if c.get("type")=="text": print(c["text"])
            else: print(json.dumps(c, ensure_ascii=False, indent=2))
    elif res and res.get("error"):
        print(json.dumps(res["error"], ensure_ascii=False, indent=2))

if __name__ == "__main__":
    main()
