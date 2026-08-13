/// <summary>
/// 场景名称常量，统一管理所有场景名字符串。
/// 避免硬编码散落各处，改场景名时只需改一处。
/// </summary>
public static class SceneNames
{
    public const string RogueResult = "RogueResult";
    public const string Title = "Title";
    public const string Plot = "plot";
    public const string RogueEntry = "RogueEntry";
    public const string SoulShop = "SoulShop";
    public const string GoldShop = "GoldShop";
    public const string Rest = "Rest";
    public const string BattleScene = "BattleScene";
    public const string StoryCardCollection = "StoryCardCollection";

    /// <summary>
    /// 第一关战斗场景（旧架构兜底用）。
    /// ⚠️ 注意：Assets/Scenes/level/level 1.unity 是【已淘汰的旧架构场景】，
    /// 把关卡数据写死在场景里，新架构不再用它做新设计。
    /// 真正生效的首战 = BattleScene + ActConfig.normalLevelPool[0] 指向的 LevelConfig。
    /// FirstLevel 仅在 RogueFlowRouter 找不到 ActConfig/LevelConfig 时作为兜底加载，
    /// 见 RogueFlowRouter.EnterBattleFromEntry 的兜底分支。
    /// </summary>
    public const string FirstLevel = "level 1";

    // 随机事件场景（统一单场景）
    public const string RandomEvent = "RandomEvent";
}