import json, sys, re

def parse_sse(path):
    with open(path, 'r', encoding='utf-8', errors='replace') as f:
        text = f.read()
    # SSE 格式: event: ... \n data: {...}\n\n
    out = []
    for block in text.split('\n\n'):
        block = block.strip()
        if not block:
            continue
        data_lines = []
        for line in block.split('\n'):
            if line.startswith('data:'):
                data_lines.append(line[5:].lstrip())
        if data_lines:
            payload = '\n'.join(data_lines)
            try:
                out.append(json.loads(payload))
            except Exception as e:
                out.append({'__raw': payload, '__err': str(e)})
    return out

for path in [r'D:\unity\mowang\.mcp_tools_list.txt',
             r'D:\unity\mowang\.mcp_editor_state.txt']:
    print('=' * 80)
    print('FILE:', path)
    print('=' * 80)
    msgs = parse_sse(path)
    for m in msgs:
        if 'result' in m:
            r = m['result']
            if 'tools' in r:
                tools = r['tools']
                print(f'tools_count = {len(tools)}')
                names = [t.get('name') for t in tools]
                # 分组展示
                for n in names:
                    print('  -', n)
            elif 'contents' in r:
                # resources/read
                for c in r['contents']:
                    uri = c.get('uri')
                    text = c.get('text', '')
                    print(f'uri = {uri}')
                    print('text:')
                    try:
                        obj = json.loads(text)
                        print(json.dumps(obj, ensure_ascii=False, indent=2)[:4000])
                    except Exception:
                        print(text[:4000])
            else:
                print(json.dumps(r, ensure_ascii=False, indent=2)[:4000])
        elif 'error' in m:
            print('ERROR:', json.dumps(m['error'], ensure_ascii=False, indent=2))
        else:
            print(json.dumps(m, ensure_ascii=False, indent=2)[:1500])
