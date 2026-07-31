using UnityEngine;

/// <summary>
/// 单张天赋卡的数据（设计文档 M3 六大类）。
/// 在 Project 里右键 Create → 天赋卡 → Talent Card Data 创建，用于选卡界面显示与扣费。
/// 首版只做数据与显示，效果（攻防数值等）可后续在战斗里按 cardId 或 type 分支实现。
/// </summary>
[CreateAssetMenu(fileName = "TalentCard", menuName = "天赋卡/Talent Card Data", order = 0)]
public class TalentCardData : ScriptableObject
{
    [Tooltip("唯一 ID，用于本局已选记录与去重")]
    public string cardId;

    [Tooltip("卡片上显示的名称")]
    public string displayName = "天赋";

    [Tooltip("详细描述（选卡界面展示）")]
    [TextArea(2, 4)]
    public string description = "";

    [Tooltip("类型：特殊/攻击/防御/守护/稀有/技能")]
    public TalentCardType cardType = TalentCardType.Attack;

    [Tooltip("稀有度，影响出现概率与选卡池")]
    public TalentCardRarity rarity = TalentCardRarity.Common;

    [Tooltip("可选：卡片图标（16:9）")]
    public Sprite icon;
    
    [Tooltip("可选：卡片背面（未点击时显示）")]
    public Sprite cardBack;
    
    [Tooltip("可选：卡片正面（视频播放完毕后显示）")]
    public Sprite cardFront;

    [Header("商店货位（运行时）")]
    [Tooltip("货位折扣倍率（0=无折扣信息，<1=打折）。运行时赋值，不序列化。")]
    [System.NonSerialized] public float slotDiscount = 0f;

    [Header("战斗效果")]
    [Tooltip("主效果类型")]
    public TalentEffectType effectType = TalentEffectType.None;
    [Tooltip("主效果数值")]
    public int effectValue = 0;
    [Tooltip("主效果辅助数值（如上限、持续时间）")]
    public int effectValue2 = 0;
    [Tooltip("副效果类型（可选，用于复合卡）")]
    public TalentEffectType secondaryEffectType = TalentEffectType.None;
    [Tooltip("副效果数值")]
    public int secondaryEffectValue = 0;
    [Tooltip("副效果辅助数值（如上限、持续时间）")]
    public int secondaryEffectValue2 = 0;

    [Header("作用范围（混合型方案）")]
    [Tooltip("效果作用范围：全局 / 职业 / 专属")]
    public CardEffectTarget effectTarget = CardEffectTarget.Global;
    [Tooltip("职业卡：目标职业")]
    public OperatorData.OperatorType targetOperatorType = OperatorData.OperatorType.Guard;
    [Tooltip("专属卡：目标干员ID（dataId）")]
    public int targetOperatorDataId = -1;
    [Tooltip("副作用：干员购买冷却增加秒数（职业卡15，专属卡60，全局卡0）")]
    public int purchaseCooldownPenalty = 0;

    [Header("特殊卡配置")]
    [Tooltip("卡牌作用域：本局生效 / 仅本次战斗")]
    public CardScope cardScope = CardScope.PerRun;
    [Tooltip("是否为守护点回溯专用救场卡")]
    public bool isGuardianRewindCard = false;
    [Tooltip("一次性战斗卡：使用后立即触发的效果类型（用完即消失）")]
    public GuardianRewindTriggerType triggerType = GuardianRewindTriggerType.None;
    [Tooltip("一次性战斗卡：触发效果的数值参数")]
    public int triggerValue = 0;

    [Header("诅咒系统")]
    [Tooltip("是否为诅咒卡（负面效果）。诅咒卡不在选卡池中出现，只能通过随机事件获得。")]
    public bool isCurse = false;

    [Tooltip("诅咒效果类型（与 effectType 共用同一枚举，但效果为负）")]
    public TalentEffectType curseEffectType = TalentEffectType.None;

    [Tooltip("诅咒效果数值（正值会转为负值生效）")]
    public int curseEffectValue = 0;

    [Tooltip("诅咒副效果类型")]
    public TalentEffectType curseSecondaryEffectType = TalentEffectType.None;

    [Tooltip("诅咒副效果数值")]
    public int curseSecondaryEffectValue = 0;

    [Tooltip("是否可通过 Rest 节点或事件移除")]
    public bool curseRemovable = true;
}

/// <summary> 天赋卡效果类型。 </summary>
public enum TalentEffectType
{
    None = 0,
    AttackBonus,      // 全局攻击力固定加成
    DefenseBonus,     // 全局防御力固定加成
    GuardianHpBonus,  // 守护点生命固定加成
    AttackPercent,    // 攻击力百分比加成（每1 = +1%）
    DefensePercent,   // 防御力百分比加成（每1 = +1%）
    GoldBonus,        // 击杀金币百分比加成（每1 = +1%）
    ScoreBonus,       // 击杀分数百分比加成（每1 = +1%）
    AttackSpeedPercent,    // 攻速百分比加成（每1 = +1%）
    AttackRangeBonus,      // 攻击范围固定加成（格数）
    DefensePenetration,    // 无视防御百分比（每1 = +1%）
    CritChanceBonus,       // 暴击率加成（每1 = +1%）
    CritDamageBonus,       // 暴击伤害加成（每1 = +1%，基础150%）
    LifeStealPercent,      // 攻击吸血百分比（每1 = +1%）
    EliteDamageBonus,      // 对精英怪伤害加成（每1 = +1%）
    MaxHpPercent,          // 最大生命值百分比加成（每1 = +1%）
    KillStackAttack,       // 击杀叠加攻击力（value1=每层攻击，value2=上限，0=无上限）
    LowHpAttackBonus,      // 低血时攻击力加成（每1 = +1%，阈值50%）
    KillAttackSpeedBuff,   // 击杀后攻速加成（value1=百分比，value2=持续秒数）
    AoeRangePercent,       // 攻击范围（AoE半径）百分比加成（每1 = +1%）
    GuardianRegenInterval, // 守护点回血间隔秒数（value=秒，越小越快）
    GuardianDamageBonus,   // 守护点射击伤害固定加成
    GuardianRangeBonus,    // 守护点射击射程固定加成
    GuardianMultiTarget,   // 守护点同时攻击目标数（value=数量）
    GuardianAttackSpeedPercent, // 守护点攻速百分比加成（每1 = +1%）
    GuardianRewindExtraTime,   // 时光回溯额外回退秒数
    GuardianRewindExtraCount,  // 时光回溯额外次数
    TeleportCooldownReduction, // 传送冷却减少秒数
    GuardianShieldCount,       // 守护点护盾次数
    GuardianResonancePerOp,    // 每个存活干员给守护点加的HP
    GuardianDamageReductionMax, // 守护点每次受伤最大扣血量
    GuardianLowHpDamageMultiplier, // 守护点低血时伤害倍率（value=倍率百分比，阈值30%）
    GuardianBattleEndHeal,     // 每场战斗结束回满守护点HP（value=1表示启用）
    TeleportAttackSpeedBuff,   // 传送后攻速buff（value1=百分比，value2=持续秒）
    RewindAttackSpeedBuff,     // 回溯后攻速buff（value1=百分比，value2=持续秒）
    MoveSpeedPercent,        // 移动速度百分比加成（每1 = +1%，先锋职业卡专用）
}

/// <summary> 卡牌作用域。 </summary>
public enum CardScope
{
    PerRun = 0,       // 本局生效（持续到本局结束）
    PerBattle = 1,    // 仅本次战斗生效（战斗结束后消失）
}

/// <summary> 效果作用范围（混合型方案）。 </summary>
public enum CardEffectTarget
{
    Global = 0,       // 全局卡：所有干员生效
    ByClass = 1,      // 职业卡：特定职业干员生效
    ByOperator = 2,   // 专属卡：指定干员生效
}

/// <summary> 守护点回溯救场卡的立即触发类型。 </summary>
public enum GuardianRewindTriggerType
{
    None = 0,
    InstantDP,           // 立即获得部署点数（用完即消）
    InstantGuardianHeal, // 立即回复守护点血量（用完即消）
    InstantAttackBuff,   // 本场战斗攻击力+X%（用完即消）
    InstantAttackSpeedBuff, // 本场战斗攻速+X%（用完即消）
    InstantAllOperatorsHeal, // 立即回复全体干员血量（用完即消）
    InstantFreezeAllEnemies, // 冻结全场敌人X秒（用完即消）
    InstantDamageAllEnemies, // 对全场敌人造成X伤害（用完即消）
    InstantKillWeakest,  // 立即击杀血量最低的敌人（用完即消）
}

/// <summary> 设计文档 3.1 六类天赋。 </summary>
public enum TalentCardType
{
    Special = 0,   // 特殊卡
    Attack,        // 攻击卡
    Defense,      // 防御卡
    Guardian,     // 守护卡
    Rare,         // 稀有卡
    Skill         // 技能卡
}

/// <summary> 设计文档 3.2 稀有度与出现概率（首版可不做传奇特效）。 </summary>
public enum TalentCardRarity
{
    Common = 0,   // 普通 60%
    Advanced,    // 进阶 28%
    Rare,        // 稀有 10%
    Legendary   // 传奇 2%
}
