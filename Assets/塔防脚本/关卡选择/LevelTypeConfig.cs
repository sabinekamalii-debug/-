using UnityEngine;

[CreateAssetMenu(fileName = "LevelTypeConfig", menuName = "魔塔/关卡类型配置", order = 1)]
public class LevelTypeConfig : ScriptableObject
{
    [Header("普通战斗")]
    public Sprite normalBattleIcon;

    [Header("商店")]
    public Sprite shopIcon;

    [Header("精英关卡")]
    public Sprite eliteIcon;

    [Header("Boss关卡")]
    public Sprite bossIcon;

    [Header("随机事件")]
    public Sprite randomEventIcon;

    [Header("休息点")]
    public Sprite restIcon;
}
