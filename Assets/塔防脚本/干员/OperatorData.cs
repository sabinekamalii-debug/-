using UnityEngine;

[CreateAssetMenu(fileName = "NewOperator", menuName = "TowerDefense/OperatorData")]
public class OperatorData : ScriptableObject
{
    [Header("基础信息")]
    public string operatorName;
    public int cost = 10;
    public float maxHealth = 100f;
    [Tooltip("防御值：每 100 点获得 1% 减伤，最多 99900（99% 减伤）")]
    public int defense = 0;
    public float attackDamage = 10f;
    public float attackInterval = 1.0f;
    public float attackRange = 3.5f;

    public enum OperatorType
    {
        Vanguard = 0,   // 先锋 - 近战，低费回费
        Guard = 1,      // 近卫 - 近战，均衡输出
        Defender = 2,   // 重装 - 近战，高防御
        Sniper = 3,     // 狙击 - 远程，高攻速高暴击
        Caster = 4,     // 术师 - 远程，高伤害穿透
        Medic = 5,      // 医疗 - 远程，治疗
        Specialist = 6, // 特种 - 近战，特殊机制
    }

    public static class OperatorTypeHelper
    {
        public static bool IsMelee(OperatorType type)
        {
            return type == OperatorType.Vanguard ||
                   type == OperatorType.Guard ||
                   type == OperatorType.Defender ||
                   type == OperatorType.Specialist;
        }

        public static bool IsRanged(OperatorType type)
        {
            return type == OperatorType.Sniper ||
                   type == OperatorType.Caster ||
                   type == OperatorType.Medic;
        }
    }

    [Header("类型")]
    public OperatorType opType;

    [Header("部署")]
    [Tooltip("部署时可选格子半径，例如 4 表示周围 4 格、7 表示 7 格等")]
    public float deployRadius = 4.0f;

    [Header("预制体/图标")]
    public GameObject unitPrefab;
    public Sprite icon;

    [Header("购买/冷却")]
    [Tooltip("购买后冷却时间（秒）。再部署该干员需同时满足：①场上同名干员已消失 ②冷却已结束。0 表示无冷却（不建议，将无法限制重复部署）。")]
    public float purchaseCooldown = 30f;

    [Header("站位/地形")]
    [Tooltip("是否可同时站在地面（Ground）与高台（HighGround）")]
    public bool canStandOnGroundAndHighGround = false;

    [Tooltip("是否为范围攻击型干员（如光波）")]
    public bool isAoEAttacker = false;

    [Tooltip("是否为治疗型干员（如牧师）")]
    public bool isHealer = false;

    [Header("暴击（狙击职业专属）")]
    [Tooltip("基础暴击率（%），仅狙击职业有意义，与天赋卡暴击加算。狙击建议 20~30%。")]
    public int baseCritChance = 0;

    [Header("星级")]
    [Tooltip("星级上限：1~5。所有干员初始为1星，可强化到此上限")]
    public int maxStarRating = 1;

    [Tooltip("满星(=maxStarRating)解锁的职业被动说明文案，用于 RogueEntry 升星界面展示")]
    [TextArea(2, 3)]
    public string starPassiveDesc = "";

    /// <summary>
    /// 干员在养成名册中的唯一 key（用 operatorName）。
    /// 养成状态（星级）以 operatorName 为索引，跨部署实例共享。
    /// </summary>
    public string RegistryKey => string.IsNullOrEmpty(operatorName) ? name : operatorName;

    [Header("台词")]
    [Tooltip("干员选择台词（RogueEntry 界面展示）")]
    [TextArea(2, 3)]
    public string selectQuote = "";

    [Tooltip("是否为初始可用干员（开局即可选）")]
    public bool isInitialAvailable = false;

    [Header("废弃标注")]
    [Tooltip("标记该干员可能废弃（非正式游戏干员）。正式干员清单以角色画布下的 13 张卡为准，其余干员请先确认后再删除。")]
    public bool isDeprecated = false;

    [Tooltip("废弃原因/备注，防止后续维护时误认")]
    public string deprecatedNote = "";
}