using UnityEngine;

/// <summary>
/// 职业符号语言：为 7 个职业各分配「符号字符 + 中文职业名」。
/// 不使用职业色块，避免覆盖干员立绘外观。
///
/// 设计决策（用户明确要求）：符号永远和职业文字一起出现（如 "» 先锋" / "⚔ 近卫"），
/// 不会单独出现，避免玩家看不出职业。
///
/// 符号映射：» 先锋 / ⚔ 近卫 / ■ 重装 / ⌖ 狙击 / ✶ 术师 / ✚ 医疗 / ◈ 特种。
/// 注意：⚔ 为 emoji，若目标字体（如 NotoSansSC）不含该字形会渲染成 □；
/// 如需稳妥可改用 Segoe UI Symbol 字体或替换为其它剑形字符。
/// </summary>
public static class OperatorClassBadge
{
    /// <summary> 职业徽标样式：符号字符 + 职业名。 </summary>
    public readonly struct ClassStyle
    {
        public readonly string className;
        public readonly string symbol;

        public ClassStyle(string className, string symbol)
        {
            this.className = className;
            this.symbol = symbol;
        }
    }

    public static ClassStyle Get(OperatorData.OperatorType opType)
    {
        switch (opType)
        {
            case OperatorData.OperatorType.Vanguard:   return new ClassStyle("先锋", "»");
            case OperatorData.OperatorType.Guard:      return new ClassStyle("近卫", "⚔");
            case OperatorData.OperatorType.Defender:   return new ClassStyle("重装", "■");
            case OperatorData.OperatorType.Sniper:     return new ClassStyle("狙击", "⌖");
            case OperatorData.OperatorType.Caster:     return new ClassStyle("术师", "✶");
            case OperatorData.OperatorType.Medic:      return new ClassStyle("医疗", "✚");
            case OperatorData.OperatorType.Specialist: return new ClassStyle("特种", "◈");
            default:                                   return new ClassStyle("未知", "?");
        }
    }

    /// <summary> 徽标文本固定为「符号 + 空格 + 职业名」（如 "» 先锋"），符号不单独出现。 </summary>
    public static string GetBadgeText(OperatorData.OperatorType opType)
    {
        var s = Get(opType);
        return $"{s.symbol} {s.className}";
    }
}
