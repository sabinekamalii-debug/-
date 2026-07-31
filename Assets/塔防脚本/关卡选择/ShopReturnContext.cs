/// <summary>
/// 商店关卡上下文：记录玩家从哪个关卡节点进入 GoldShop，
/// 返回时用于标记该关完成并解锁下一关。
/// </summary>
public static class ShopReturnContext
{
    private static int _shopLevelNumber = -1;

    /// <summary> 进入商店时设置关卡编号。 </summary>
    public static void SetShopLevel(int levelNumber) => _shopLevelNumber = levelNumber;

    /// <summary> 获取并清除商店关卡编号（返回 -1 表示非商店入口进入）。 </summary>
    public static int GetAndClear()
    {
        int n = _shopLevelNumber;
        _shopLevelNumber = -1;
        return n;
    }
}
