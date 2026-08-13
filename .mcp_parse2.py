import json, sys, re, os

def read_auto(path):
    with open(path, 'rb') as f:
        raw = f.read()
    # 检测 UTF-16 LE (FF FE 或大量 00 间隔)
    if raw[:2] == b'\xff\xfe':
        return raw.decode('utf-16-le')
    if raw[:2] == b'\xfe\xff':
        return raw.decode('utf-16-be')
    # 启发式:每两个字节一个 0x00 + ASCII
    sample = raw[:200]
    nul_count = sample.count(b'\x00')
    if nul_count > len(sample) * 0.3:
        return raw.decode('utf-16-le')
    return raw.decode('utf-8', errors='replace')

def parse_sse(text):
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
                out.append({'__raw': payload[:300], '__err': str(e)})
    return out

# 1) editor state
state_text = read_auto(r'D:\unity\mowang\.mcp_editor_state.txt')
state_msgs = parse_sse(state_text)
for m in state_msgs:
    if 'result' in m and 'contents' in m['result']:
        for c in m['result']['contents']:
            inner = json.loads(c['text'])
            data = inner.get('data', {})
            print('=== editor state ===')
            print('unity_version =', data.get('unity', {}).get('unity_version'))
            print('instance_id   =', data.get('unity', {}).get('instance_id'))
            print('platform      =', data.get('unity', {}).get('platform'))
            sc = data.get('editor', {}).get('active_scene') or {}
            print('active_scene  =', sc.get('path'), '(', sc.get('name'), ')')
            print('play_mode     =', data.get('editor', {}).get('play_mode'))
            print('is_compiling  =', data.get('compilation', {}).get('is_compiling'))
            print('domain_reload_pending =', data.get('compilation', {}).get('is_domain_reload_pending'))
            print('ready_for_tools =', data.get('advice', {}).get('ready_for_tools'))
            print('activity_phase =', data.get('activity', {}).get('phase'))
            print('keys:', list(data.keys()))
    elif 'error' in m:
        print('STATE ERR:', m['error'])

# 2) tools list
tools_text = read_auto(r'D:\unity\mowang\.mcp_tools_list.txt')
tools_msgs = parse_sse(tools_text)
for m in tools_msgs:
    if 'result' in m and 'tools' in m['result']:
        tools = m['result']['tools']
        print()
        print(f'=== tools list: {len(tools)} tools ===')
        for t in tools:
            print('  -', t.get('name'))
    elif 'error' in m:
        print('TOOLS ERR:', m['error'])
