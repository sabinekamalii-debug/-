# 临时修改记录 — StarBudget 从 7 提升到 9

**修改日期：** 2026-08-18
**修改人：** AI 助手（测试阶段调整）
**预期恢复日期：** 测试阶段结束后（正式上线前必须改回）

---

## 修改内容

将 `BalanceConfig.StarBudget` 从 `7` 临时改为 `9`。

**文件路径：** `Assets/塔防脚本/肉鸽/BalanceConfig.cs` 第 107 行

**修改前：**
```csharp
public const int StarBudget = 7;
```

**修改后：**
```csharp
public const int StarBudget = 9;
```

---

## 修改原因

测试阶段需要支持更多干员组合，以便全面测试各种阵容搭配和平衡性。

---

## ⚠️ 必须恢复（Critical）

**正式上线前必须将 StarBudget 改回 7。**

原因：
- 7 星预算是经过数值设计文档（`03-干员玩法设计.md` §6.1）校准的核心平衡参数
- 改为 9 会破坏阵容选择的取舍策略（如 "1 个 ★5 核心 + 2 个 ★1 辅助" 的经典搭配将被稀释）
- 影响局内经济体系（升星金币消耗、商店定价、奖励数值均基于 7 星预算设计）
- 影响关卡难度曲线（敌人数值按 7 星阵容强度校准）

---

## 恢复步骤

1. 打开 `Assets/塔防脚本/肉鸽/BalanceConfig.cs`
2. 找到第 107 行：`public const int StarBudget = 9;`
3. 改回 `public const int StarBudget = 7;`
4. 同时删除该行上方注释块中 "⚠️ 临时修改（测试阶段）" 相关文字
5. 删除本文件 `Docs/TempChanges-StarBudget-9.md`
6. 提交变更并备注 "恢复 StarBudget 为 7（测试结束）"

---

## 相关参考

- 数值设计文档：`Assets/游戏设计文档/03-干员玩法设计.md` §6.1 升星养成
- 平衡配置总表：`Assets/塔防脚本/肉鸽/BalanceConfig.cs`
- 选人面板逻辑：`Assets/塔防脚本/干员/OperatorSelectionPanel.cs`
