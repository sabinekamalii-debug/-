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

    /// <summary> 第一关战斗场景（首战强制进入，避免误导向 plot 剧情/收藏场景）。 </summary>
    public const string FirstLevel = "level 1";

    // 随机事件场景（统一单场景）
    public const string RandomEvent = "RandomEvent";
}