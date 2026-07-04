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

    public enum OperatorType { Melee, Ranged }

    [Header("类型")]
    public OperatorType opType;

    [Header("部署")]
    [Tooltip("部署时可选格子半径，例如 4 表示周围 4 格、7 表示 7 格等")]
    public float deployRadius = 4.0f;

    [Header("预制体/图标")]
    public GameObject unitPrefab;
    public Sprite icon;

    [Header("购买/冷却")]
    [Tooltip("购买后冷却时间（秒），0 表示无冷却")]
    public float purchaseCooldown = 0f;

    [Header("站位/地形")]
    [Tooltip("是否可同时站在地面（Ground）与高台（HighGround）")]
    public bool canStandOnGroundAndHighGround = false;

    [Tooltip("是否为范围攻击型干员（如光波）")]
    public bool isAoEAttacker = false;

    [Tooltip("是否为治疗型干员（如牧师）")]
    public bool isHealer = false;
}