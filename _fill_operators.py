# -*- coding: utf-8 -*-
# 重写：清理 opType/selectQuote/starPassiveDesc 的重复/缩进错误，按模板顺序重建。
import os, re, glob

DATA_DIR = r"d:\unity\mowang\Assets\数据2\干员数据"

PASSIVE = {
    0: "部署即返还部署费用，技力回复 +30%",
    1: "专注强化，攻击力 +30%",
    2: "防御 +40%，且致命伤时保留 1 点生命（每场 1 次）",
    3: "暴击率 +25%",
    4: "法术穿透：无视 50% 防御，攻击力 +20%",
    5: "治疗量 +30%",
    6: "再部署更划算（撤退返还 +50%）",
}

# opType 补全（None=保持原文件里第一个 opType 值，即原值不动）
OP_FIX = {
    "女战士": 1, "武士": 1, "死亡剑士": 1,
    "晶": 2, "珑": 2,
    "星豹": 6, "紫雷狼": 6,
}

# quote 补全（None=不动）
QUOTE = {
    "先锋测试": "（测试单位）这只是个占位，别当真。",
    "光波": "光，无处不在。",
    "地面射手": "地面，才是我的主场。",
    "拳师": "一拳一个。",
    "醒": "……（它似乎并不想说话）",
    "女战士": "剑锋所指，无所不破。",
    "武士": "心静，则刀快。",
    "晶": "坚如磐石，稳如山岳。",
    "珑": "进可攻，退可守。",
    "星豹": "快，是你抓不住我的理由。",
    "死亡剑士": "死亡，只是我的起点。",
    "紫雷狼": "雷霆之下，皆是猎物。",
    "坚守先锋": "身后是同伴，我不能退。",
    "斥候先锋": "前方交给我探。",
    "游击先锋": "打了就跑，才是艺术。",
    "突击先锋": "跟我冲！",
    "驻防先锋": "这里，我来守。",
    "风暴先锋": "风暴，因我而起。",
    "战术先锋": "和我同行吗？可别拖后腿。",
    "奥术": "奥秘，本就该被驾驭。",
    "圣光": "圣光，会庇护每一个人。",
    "铁壁": "想过去？先踏过我的身躯。",
    "钩爪": "上钩吧。",
    "法师": "见识一下真正的法术。",
    "荆棘": "刺痛，是靠近我的代价。",
    "净化": "污秽，终将被洗净。",
    "猎手先锋": "猎物，跑不掉的。",
}

def read_file(p):
    with open(p, "r", encoding="utf-8") as f:
        return f.read()

def write_file(p, txt):
    with open(p, "w", encoding="utf-8") as f:
        f.write(txt)

def get_opType(txt):
    # 取第一个 opType 行的值
    for line in txt.split("\n"):
        m = re.match(r'^\s*opType:\s*(\d+)', line)
        if m:
            return int(m.group(1))
    return 0

def process(path):
    name = os.path.splitext(os.path.basename(path))[0]
    txt = read_file(path)
    lines = txt.split("\n")

    # 1) 删除所有 opType / selectQuote / starPassiveDesc 行（清理重复+坏缩进）
    cleaned = []
    for line in lines:
        s = line.strip()
        if s.startswith("opType:") or s.startswith("selectQuote:") or s.startswith("starPassiveDesc:"):
            continue
        cleaned.append(line)

    # 2) 决定最终值
    final_op = OP_FIX.get(name)
    if final_op is None:
        final_op = get_opType(txt)  # 保持原值
    quote = QUOTE.get(name)  # None 表示不写 selectQuote（保持无该字段，原格式）
    passive = PASSIVE.get(final_op, PASSIVE[0])

    # 3) 按模板顺序重建：
    #    opType 插在 attackRange 前；starPassiveDesc 插在 maxStarRating 前；
    #    selectQuote 插在 maxStarRating 后（若存在 maxStarRating）
    out = []
    max_star_seen = False
    for line in cleaned:
        s = line.strip()
        if s.startswith("attackRange:"):
            out.append(f"  opType: {final_op}")
            out.append(line)
        elif s.startswith("maxStarRating:"):
            out.append(f'  starPassiveDesc: "{passive}"')
            out.append(line)
            max_star_seen = True
            if quote is not None:
                out.append(f'  selectQuote: "{quote}"')
        else:
            out.append(line)
    # 兜底：若没有 maxStarRating（不应发生），末尾补
    if not max_star_seen:
        out.append(f'  starPassiveDesc: "{passive}"')
        if quote is not None:
            out.append(f'  selectQuote: "{quote}"')
        out.append(f"  opType: {final_op}")

    write_file(path, "\n".join(out))
    print(f"[完成] {name}: opType={final_op}")

def main():
    for p in glob.glob(os.path.join(DATA_DIR, "*.asset")):
        process(p)

if __name__ == "__main__":
    main()
