---
name: 近卫技能破防与伤害类型机制明确化
overview: 复用现有 ignoreDefense 机制统一"物理/法术"伤害类型：术师常驻法术(挂 IgnoreDefenseAttacker)、狙击物理(不挂)、部分近卫开技能时临时破防(skillPenetrateActive)。不新建法伤数值池，所有伤害走同一结算入口。
todos:
  - id: add-penetrate-flag
    content: OperatorUnit 增加 skillPenetrateActive 与 IgnoresDefense 属性，近战读取点改用属性
    status: pending
  - id: update-attackers
    content: RangedAttacker/AoEAttacker/SpawnerContactAttacker 三处 ignoreDefense 改用 unit.IgnoresDefense
    status: pending
    dependencies:
      - add-penetrate-flag
  - id: create-skill-penetrate
    content: 新建 Skill_PenetrateDefense 技能子类（OnSkillStart 置位、OnSkillEnd 复位）
    status: pending
  - id: wire-prefabs
    content: 术师挂 IgnoreDefenseAttacker；破防近卫挂 Skill_PenetrateDefense；狙击确保不挂
    status: pending
    dependencies:
      - update-attackers
      - create-skill-penetrate
  - id: update-design-doc
    content: 更新干员职业设计文档中近卫"一律不能破甲"的过时说明
    status: pending
---

## 用户需求

- 建立"物理 / 法术"伤害区分机制：**不单独做法伤数值池**，复用现有 `ignoreDefense` 开关（明日方舟模型）。
- 术师（法师）：常驻无视敌人防御，充当"法术"伤害。
- 狙击：物理伤害，正常吃敌人防御减伤，不能破防。
- 部分近卫：常态为物理，发动技能期间临时穿透敌人防御（复用术师同一结算路径）。

## 核心特性

- 不新增魔法伤害数值类型，避免物理/法术双数值体系带来的复杂度。
- 所有伤害统一走 `Enemy2.TakeDamage(int damage, bool ignoreDefense, int penetrationPercent)` 入口，`ignoreDefense=true` 即无视防御。
- 破防做成"可挂载技能"，术师用常驻组件、近卫用技能临时置位，二者共用同一判定逻辑，未来其他职业可复用。

## 技术栈

- 沿用现有 Unity C# 塔防框架，不引入新依赖。

## 实现方案

采用最小改动：复用现有 `IgnoreDefenseAttacker` 标记组件 + `Enemy2.TakeDamage` 的 `ignoreDefense` 通道，仅新增一个"技能临时破防"开关与对应技能类。

### 关键决策

1. **不做法伤独立数值**：现有 `TakeDamage` 已通过 `ignoreDefense` 区分"无视防御(法术)"与"吃防御(物理)"，机制完备，只需在判定点增加一个可临时置位的开关。
2. **`OperatorUnit` 增加 `skillPenetrateActive` 标志 + `IgnoresDefense` 属性**：属性 `=> GetComponent<IgnoreDefenseAttacker>() != null || skillPenetrateActive`。所有"干员攻击敌人"的判定点统一改用该属性，术师（常驻组件）与近卫（技能置位）自动纳入同一条链路。
3. **新增 `Skill_PenetrateDefense : OperatorSkill`**：`OnSkillStart` 置 `owner.skillPenetrateActive = true`，`OnSkillEnd` 复位为 `false`；沿用现有 `OperatorSkill` 的 `Initialize/OnSkillStart/OnSkillEnd` 钩子，与 `Skill_PowerUp` 等同类保持一致（含颜色反馈）。
4. **预制体接线（配置层，非代码）**：

- 术师预制体挂 `IgnoreDefenseAttacker`（常驻法术）。
- 狙击预制体不挂该组件、天赋也不给 `DefensePenetration`，确保纯物理。
- 需要破防的近卫预制体，把技能组件换成/挂上 `Skill_PenetrateDefense`。

### 性能与可靠性

- `IgnoresDefense` 内部仍只调用一次 `GetComponent`，攻击频率下开销可忽略。
- 技能结束经 `EndSkill → OnSkillEnd` 复位标志，撤退/销毁随实例回收，无悬挂状态。
- 与现有天赋破防（`GetPenetrationPercent`）、`isMarked ×1.3` 完全正交，不冲突。

## 架构设计

- 修改点集中在"伤害类型判定"一处抽象：`OperatorUnit.IgnoresDefense` 成为唯一真值来源，便于后续新增"真伤/百分比破防"等扩展。
- 四种攻击出口（近战、远程、光波、接触）统一调用该属性，杜绝逻辑分叉。

## 目录结构

```
Assets/塔防脚本/
├── 干员/
│   └── OperatorUnit.cs              # [MODIFY] 增加 skillPenetrateActive 字段与 IgnoresDefense 属性；近战攻击读取点改用属性
│   ├── RangedAttacker.cs            # [MODIFY] ignoreDefense 读取点改用 unit.IgnoresDefense
│   ├── AoEAttacker.cs               # [MODIFY] 同上
│   └── SpawnerContactAttacker.cs    # [MODIFY] 同上
└── skill/
    └── Skill_PenetrateDefense.cs    # [NEW] 技能期间临时穿透防御的技能子类
```

（预制体接线在 `Assets/Resources/人物/干员/` 下的术师/狙击/近卫预制体中完成，不涉及脚本新增。）

## 关键代码结构

```
// OperatorUnit.cs 新增
[HideInInspector] public bool skillPenetrateActive = false;
public bool IgnoresDefense => GetComponent<IgnoreDefenseAttacker>() != null || skillPenetrateActive;
```

```
// Skill_PenetrateDefense.cs（核心契约）
public class Skill_PenetrateDefense : OperatorSkill
{
    public override void OnSkillStart();   // owner.skillPenetrateActive = true
    public override void OnSkillEnd();     // owner.skillPenetrateActive = false
}
```