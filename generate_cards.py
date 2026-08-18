import os
import json

# Unity YAML 模板 - 使用字符串替换避免 { 冲突
YAML_TEMPLATE = """%YAML 1.1
%TAG !u! tag:yousandi.cn,2023:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID_0}
  m_PrefabInstance: {fileID_0}
  m_PrefabAsset: {fileID_0}
  m_GameObject: {fileID_0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID_script}
  m_Name: {cardId}
  m_EditorClassIdentifier: 
  cardId: {cardId}
  displayName: "{displayName}"
  description: "{description}"
  cardType: {cardType}
  rarity: {rarity}
  icon: {fileID_0}
  cardBack: {fileID_0}
  cardFront: {fileID_0}
  effectType: {effectType}
  effectValue: {effectValue}
  effectValue2: {effectValue2}
  secondaryEffectType: {secondaryEffectType}
  secondaryEffectValue: {secondaryEffectValue}
  secondaryEffectValue2: {secondaryEffectValue2}
  effectTarget: {effectTarget}
  targetOperatorType: {targetOperatorType}
  targetOperatorDataId: -1
  purchaseCooldownPenalty: {purchaseCooldownPenalty}
  cardScope: 0
  isGuardianRewindCard: 0
  triggerType: 0
  triggerValue: 0
  isCurse: 0
  curseEffectType: 0
  curseEffectValue: 0
  curseSecondaryEffectType: 0
  curseSecondaryEffectValue: 0
  curseRemovable: 1
"""

# 卡片定义 (cardId, displayName, description, cardType, rarity, effectType, effectValue, effectValue2, secondaryEffectType, secondaryEffectValue, secondaryEffectValue2, effectTarget, targetOperatorType, purchaseCooldownPenalty)
CARDS = [
    # ============ DEFENSE (类型2) ============
    ("def_hp", "生命强化", "全体干员最大生命值+25%", 2, 0, 15, 25, 0, 0, 0, 0, 0, 0, 0),
    ("def_armor", "铁壁防御", "全体干员防御力+30%", 2, 0, 5, 30, 0, 0, 0, 0, 0, 0, 0),
    ("def_dodge", "灵活闪避", "全体干员攻速+20%", 2, 0, 8, 20, 0, 0, 0, 0, 0, 0, 0),
    ("def_vanguard_block", "先锋壁垒", "先锋干员阻挡数+1", 2, 1, 0, 0, 0, 0, 0, 0, 1, 1, 15),
    ("def_defender_block", "重装壁垒", "重装干员阻挡数+1", 2, 1, 0, 0, 0, 0, 0, 0, 1, 2, 15),
    ("def_guard", "近卫防御", "近卫干员防御力+40%，生命值+15%", 2, 1, 5, 40, 0, 15, 15, 0, 1, 0, 15),
    ("def_ranged_hp", "远程生存", "狙击/术师/医疗干员最大生命值+20%", 2, 0, 15, 20, 0, 0, 0, 0, 0, 0, 0),
    ("def_medic", "医疗守护", "医疗干员治疗量+30%", 2, 0, 0, 30, 0, 0, 0, 0, 1, 5, 15),
    ("def_specialist", "特种机动", "特种干员移动速度+50%", 2, 0, 35, 50, 0, 0, 0, 0, 1, 6, 15),
    ("def_armor_regen", "自然恢复", "全体干员最大生命值+20%，攻击吸血+1%", 2, 1, 15, 20, 0, 13, 1, 0, 0, 0, 0),

    # ============ GUARDIAN (类型3) ============
    ("grd_hp", "守护之心", "守护点最大生命值+3", 3, 0, 3, 3, 0, 0, 0, 0, 0, 0, 0),
    ("grd_damage", "守护之矛", "守护点攻击力+150", 3, 0, 21, 150, 0, 0, 0, 0, 0, 0, 0),
    ("grd_speed", "守护加速", "守护点攻速+50%", 3, 0, 24, 50, 0, 0, 0, 0, 0, 0, 0),
    ("grd_range", "守护射程", "守护点攻击范围+2格", 3, 0, 22, 2, 0, 0, 0, 0, 0, 0, 0),
    ("grd_pierce", "守护穿透", "守护点攻击无视100%防御", 3, 1, 10, 100, 0, 0, 0, 0, 0, 0, 0),
    ("grd_multi", "守护风暴", "守护点同时攻击目标数+2", 3, 1, 23, 2, 0, 0, 0, 0, 0, 0, 0),
    ("grd_regen", "守护回复", "守护点每10秒回复1点生命", 3, 0, 20, 10, 0, 0, 0, 0, 0, 0, 0),
    ("grd_teleport", "瞬移精通", "R键传送冷却时间减少40%", 3, 1, 27, 40, 0, 0, 0, 0, 0, 0, 0),
    ("grd_rewind", "时光延长", "时光回溯额外回退3秒", 3, 1, 25, 3, 0, 0, 0, 0, 0, 0, 0),
    ("grd_shield", "能量护盾", "守护点获得2层护盾", 3, 1, 28, 2, 0, 0, 0, 0, 0, 0, 0),

    # ============ SKILL (类型5) ============
    ("skl_charge", "技能充能", "全体干员攻速+20%", 5, 0, 8, 20, 0, 0, 0, 0, 0, 0, 0),
    ("skl_duration", "技能延续", "全体干员技能持续时间+40%", 5, 0, 0, 40, 0, 0, 0, 0, 0, 0, 0),
    ("skl_guard", "卫士之怒", "近卫干员技能期间攻击力+50%", 5, 1, 0, 50, 0, 0, 0, 0, 1, 0, 15),
    ("skl_sniper_chain", "狙击连锁", "狙击干员攻击可在敌人间弹射1次", 5, 1, 0, 1, 0, 0, 0, 0, 1, 4, 15),
    ("skl_caster_invincible", "法师无敌", "术师干员释放技能时处于无敌状态", 5, 2, 0, 1, 0, 0, 0, 0, 1, 3, 15),
    ("skl_defender_heal", "重装复苏", "重装干员使用技能时恢复10%生命值", 5, 0, 0, 10, 0, 0, 0, 0, 1, 2, 15),
    ("skl_medic_attack", "医者仁心", "医疗干员释放技能时同时攻击敌人", 5, 2, 0, 1, 0, 0, 0, 0, 1, 5, 15),
    ("skl_auto", "自动技能", "所有持续3秒及以下的技能冷却完毕后自动释放", 5, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0),
    ("skl_specialist_tp", "特种传送", "特种干员可免费传送1次到任意位置", 5, 2, 0, 1, 0, 0, 0, 0, 1, 6, 15),
    ("skl_vanguard_dp", "先锋征召", "先锋干员部署后返还30%部署费用", 5, 0, 0, 30, 0, 0, 0, 0, 1, 1, 15),

    # ============ RARE (类型4) ============
    ("rar_attack_all", "全军突击", "全体干员攻击力+15%，攻速+15%", 4, 2, 4, 15, 0, 8, 15, 0, 0, 0, 0),
    ("rar_defense_all", "全军防御", "全体干员防御力+25%，生命值+15%", 4, 2, 5, 25, 0, 15, 15, 0, 0, 0, 0),
    ("rar_vanguard_speed", "先锋共鸣", "每有1个先锋干员，所有干员移速+5%", 4, 2, 0, 5, 0, 0, 0, 0, 0, 0, 0),
    ("rar_guard_passive", "战阵联动", "近战干员被动不再受单个敌人限制，同时阻挡多个敌人也能触发", 4, 2, 0, 1, 0, 0, 0, 0, 0, 0, 0),
    ("rar_boss_slayer", "精英杀手", "全体干员对精英和Boss伤害+40%", 4, 2, 14, 40, 0, 0, 0, 0, 0, 0, 0),
    ("rar_dp_burst", "部署爆发", "干员部署后前3次攻击伤害翻倍", 4, 2, 0, 3, 0, 0, 0, 0, 0, 0, 0),
    ("rar_kill_reinforce", "击杀强化", "每击杀1个敌人，全体干员攻击力+1%(本场，上限30%)", 4, 2, 0, 1, 30, 0, 0, 0, 0, 0, 0),
    ("rar_first_strike", "先发制人", "战斗开始前5秒，全体干员攻击力+80%", 4, 3, 0, 80, 5, 0, 0, 0, 0, 0, 0),
    ("rar_dp_cap", "调度大师", "部署点数上限+100", 4, 2, 0, 100, 0, 0, 0, 0, 0, 0, 0),
    ("rar_starting_gold", "初始财富", "开局金币+80", 4, 2, 0, 80, 0, 0, 0, 0, 0, 0, 0),

    # ============ SPECIAL (类型0) ============
    ("spc_gold", "财源滚滚", "金币获取+35%", 0, 0, 6, 35, 0, 0, 0, 0, 0, 0, 0),
    ("spc_draw", "命运抉择", "每场战斗后额外抽取1张卡片", 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0),
    ("spc_reroll", "免费重抽", "本局免费重抽2次", 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0),
    ("spc_extra_shop", "免费购物", "本局免费购买1张卡片", 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0),
    ("spc_card_capacity", "星卡容量", "可持有的最大卡片数量+1", 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0),
    ("spc_guard_range", "先锋扩张", "先锋干员可部署范围+2格", 0, 1, 0, 2, 0, 0, 0, 0, 1, 1, 15),
    ("spc_sniper_range", "狙击射程", "狙击干员攻击范围+3格", 0, 1, 0, 3, 0, 0, 0, 0, 1, 4, 15),
    ("spc_guard_attack", "卫士横扫", "近卫干员同时攻击所有被阻挡的敌人", 0, 2, 0, 1, 0, 0, 0, 0, 1, 0, 15),
    ("spc_repair", "战场维修", "每场战斗结束后守护点生命回满", 0, 0, 32, 1, 0, 0, 0, 0, 0, 0, 0),
    ("spc_revive", "复活契约", "干员死亡时30%概率复活并恢复50%生命值", 0, 2, 0, 30, 0, 0, 0, 0, 0, 0, 0),
]

# 文件夹映射
FOLDER_MAP = {
    0: "Assets/Resources/TalentCards/Special",
    1: "Assets/Resources/TalentCards/Attack", 
    2: "Assets/Resources/TalentCards/Defense",
    3: "Assets/Resources/TalentCards/guardian",
    4: "Assets/Resources/TalentCards/Rare",
    5: "Assets/Resources/TalentCards/Skill",
}

def generate_card(card):
    cardId, displayName, description, cardType, rarity, effectType, effectValue, effectValue2, secondaryEffectType, secondaryEffectValue, secondaryEffectValue2, effectTarget, targetOperatorType, purchaseCooldownPenalty = card
    
    folder = FOLDER_MAP[cardType]
    filepath = os.path.join("D:/unity/mowang", folder, f"{cardId}.asset")
    
    # 确保文件夹存在
    os.makedirs(os.path.dirname(filepath), exist_ok=True)
    
    content = YAML_TEMPLATE.format(
        cardId=cardId,
        displayName=displayName,
        description=description,
        cardType=cardType,
        rarity=rarity,
        effectType=effectType,
        effectValue=effectValue,
        effectValue2=effectValue2,
        secondaryEffectType=secondaryEffectType,
        secondaryEffectValue=secondaryEffectValue,
        secondaryEffectValue2=secondaryEffectValue2,
        effectTarget=effectTarget,
        targetOperatorType=targetOperatorType,
        purchaseCooldownPenalty=purchaseCooldownPenalty,
        fileID_0="{fileID: 0}",
        fileID_script="{fileID: 11500000, guid: 25b5543c5e7422745b6fe739aad8016e, type: 3}",
    )
    
    with open(filepath, "w", encoding="utf-8") as f:
        f.write(content)
    
    print(f"Created: {filepath}")

# 生成所有卡片
for card in CARDS:
    generate_card(card)

print(f"\n总计生成 {len(CARDS)} 张卡片")
