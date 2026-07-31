/// <summary>
/// 休息节点上下文：记录玩家从哪个关卡节点进入 Rest 场景，
/// 返回时标记该关完成并解锁下一关。
/// </summary>
public static class RestReturnContext
{
    private static int _restLevelNumber = -1;

    /// <summary> 进入休息点时设置关卡编号。 </summary>
    public static void SetRestLevel(int levelNumber) => _restLevelNumber = levelNumber;

    /// <summary> 获取并清除关卡编号（返回 -1 表示非休息入口进入）。 </summary>
    public static int GetAndClear()
    {
        int n = _restLevelNumber;
        _restLevelNumber = -1;
        return n;
    }
}
