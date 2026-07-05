# 魔王肉鸽系统重构规划书

> 基于 9 项设计目标，在现有框架上改造。不推倒重来，保留 RogueRuntimeState / RogueFlowRouter / RogueResultController 等核心骨架。

---

## 一、设计目标总览

| 编号 | 目标 | 核心改动 |
|------|------|----------|
| 0 | **天赋卡免费化** | 移除 costRunPoint，所有抽卡免费 |
| 1 | **点数系统重构** | 点数不再用于买卡，改为：转永久点 / 增加局内点 / 开局多选天赋 |
| 2 | **守护点免死** | 致命伤害时伤害消失 + 随机高天赋 + 攻速 x3 持续 20 秒 |
| 3 | **开局天赋** | 开局自动获得 1 个天赋 + 可用点数换额外天赋 |
| 4 | **Boss 掉落抽卡** | 大怪死亡爆 3 张卡，技能蓝色 / 数值紫色，悬浮翻面 |
| 5 | **组合技系统** | 多天赋凑合成组合技，减数值惩罚引导组合（土豆兄弟式） |
| 6 | **天赋押注** | 押注天赋本局不可用，无伤 50%/45%/5%，非无伤 40%/50%/10% |
| 7 | **剧情卡片** | 每次通关给 1 张剧情卡，查看后送角色小传笔记 |
| 8 | **关卡间对话** | 通关后 1-2 句精简对话，8 秒读完再选天赋进下一关 |
| 9 | **人物列传** | 主线通关后解锁新模式，角色个人剧情 |

---

## 二、分阶段任务拆解

### 阶段 1：天赋卡免费化 + 点数系统重构（目标 0 + 1）

**目标**：移除天赋卡的点数消耗，重构点数用途

#### 任务 1.1：移除天赋卡点数消耗
- **文件**：`TalentCardData.cs`
- **改动**：`costRunPoint` 字段标记 `[Obsolete]` 或直接移除
- **文件**：`RogueRuntimeState.TryPickTalentCard()`
- **改动**：移除 `RunPoint < card.costRunPoint` 检查和扣点逻辑，改为免费选卡
- **文件**：`RogueResultController.cs`
- **改动**：选卡界面移除消耗文本显示，移除 `canAfford` 判断
- **文件**：`InGameRoguePicker.cs`
- **改动**：同样移除点数检查

#### 任务 1.2：重构点数系统语义
- **文件**：`RogueRuntimeState.cs`
- **改动**：
  - `AvailablePoint` → 局外可用点数（保留，用途变更）
  - `RunPoint` → 局内点数（保留，用途变更）
  - `PermanentPoint` → 永久点数（保留，提供被动加成）
  - 新增 `AvailablePointToExtraTalent()` — 消耗可用点数换取开局额外天赋
  - `ApplySettlement()` — 调整点数返还公式

#### 任务 1.3：调整结算点数返还
- **文件**：`RogueRuntimeState.ApplySettlement()`
- **改动**：
  - 胜利：返还基础点数（建议 3 点）
  - 大胜（无伤满血）：返还额外点数（建议 5 点）
  - 失败：返还少量点数（建议 1 点）
  - 移除原有押注逻辑（押注移到任务 6.1 重做）

#### 任务 1.4：调整 RogueEntry 界面
- **文件**：`RogueEntryController.cs`
- **改动**：
  - 移除「点数兑换」按钮的 5:1 文案
  - 改为两个按钮：「兑换永久点(5:1)」「多选天赋(消耗X点)」
  - 刷新文本显示新语义

---

### 阶段 2：开局天赋系统（目标 3）

**目标**：开局自动获得 1 个天赋 + 可用点数换额外天赋

#### 任务 2.1：开局自动抽卡（已有基础）
- **现状**：第一关开始时已实现自动抽卡（`IsFirstStageDrop`）
- **改动**：每关开始时都触发，不限于第一关
- **文件**：`RogueEntryController.StartRun()`
- **文件**：`RogueFlowRouter.OnFirstStagePickCompleted()`

#### 任务 2.2：可用点数换额外天赋
- **文件**：`RogueRuntimeState.cs`
- **新增方法**：`TryExchangePointForExtraTalent()` — 消耗 N 点可用点数，标记可多选 1 张天赋
- **文件**：`RogueEntryController.cs`
- **新增按钮**：「消耗 X 点多选 1 天赋」
- **文件**：`RogueResultController.cs`
- **改动**：选卡时检查是否有额外选卡权，若有则可选 2 张

---

### 阶段 3：Boss 掉落抽卡系统（目标 4）

**目标**：大怪死亡爆 3 张卡，技能蓝色背面 / 数值紫色背面，悬浮翻面

#### 任务 3.1：Boss 标记系统
- **文件**：`Spawner.cs`
- **改动**：在波次数据中标记 Boss 波，Boss 敌人设置 `isBossEnemy = true`
- **文件**：`WaveData.cs`
- **新增字段**：`bool isBossWave`
- **文件**：`Enemy2.cs`
- **新增字段**：`bool isBossEnemy`

#### 任务 3.2：Boss 死亡触发抽卡
- **文件**：`Enemy2.OnDeath()`
- **改动**：Boss 死亡时触发 3 选 1 抽卡（复用现有 IsMidGameDrop 机制）
- **文件**：`GameManager.cs`
- **新增方法**：`OnBossKilled()` — 类似 `OnPurpleEnemyKilled()` 但不染色

#### 任务 3.3：卡牌背面颜色区分
- **文件**：`TalentCardData.cs`
- **改动**：
  - `Skill` 类型卡 → 蓝色背面（自动生成或指定默认蓝色背面）
  - `Attack`/`Defense`/`Guardian` 类型卡 → 紫色背面
  - 新增字段：`bool isGlobal` — 标记全局/本局
- **文件**：`RogueResultController.RefreshCardSlotVisuals()`
- **改动**：根据 `cardType` 显示对应颜色背面

#### 任务 3.4：悬浮翻面交互
- **文件**：`RogueResultController.cs`
- **改动**：
  - 利用现有 `AddCardSlotHover()` EventTrigger
  - `PointerEnter` → 翻面动画（显示正面）
  - `PointerExit` → 翻回背面
  - 移除现有的「点击→视频→翻面」流程，改为悬浮即翻

---

### 阶段 4：守护点免死系统（目标 2）

**目标**：守护点受到致命伤害时伤害消失 + 随机高天赋 + 攻速 x3 持续 20 秒

#### 任务 4.1：免死触发
- **文件**：`GameManager.TakeDamage()`
- **改动**：
  - 当 `playerHealth - damageAmount <= 0` 时触发免死
  - 伤害归零（不扣血）
  - 设置 `_isGuardianSaveActive = true`
  - 每局只能触发一次（`_guardianSaveUsed` 标记）

#### 任务 4.2：免死随机天赋
- **文件**：`RogueRuntimeState.cs`
- **新增方法**：`GetRandomHighTierTalent()` — 从卡池中随机抽取 1 张稀有/传奇天赋
- **文件**：`GameManager.cs`
- **改动**：免死时调用，3 本局 + 1 全局（共 4 选 1 或直接给？需确认）

#### 任务 4.3：攻速翻倍 Buff
- **文件**：`TalentEffectApplier.cs`
- **新增方法**：`GetAttackSpeedMultiplier()` — 返回当前攻速倍率
- **新增方法**：`ActivateGuardianSaveBuff(float duration)` — 攻速 x3，20 秒后恢复
- **文件**：`OperatorUnit.cs`
- **改动**：攻击间隔计算时乘以 `TalentEffectApplier.GetAttackSpeedMultiplier()`

---

### 阶段 5：天赋押注系统（目标 6）

**目标**：押注天赋本局不可用，根据结果概率返还

#### 任务 5.1：押注数据结构
- **文件**：`RogueRuntimeState.cs`
- **新增字段**：
  - `List<string> BetTalentIds` — 押注的天赋 ID
  - `List<string> ConsumedTalentIds` — 已消耗的天赋 ID
- **新增方法**：
  - `BetTalent(cardId)` — 押注（本局不可用）
  - `ConsumeTalent(cardId)` — 一次性消耗
  - `ResolveBet(bool noHit)` — 结算押注概率

#### 任务 5.2：押注界面
- **文件**：新建 `TalentBetPanel.cs`
- **功能**：
  - 开局时显示已获得的天赋列表
  - 玩家选择押注的天赋（可多选）
  - 确认后天赋本局禁用
- **文件**：`RogueEntryController.cs`
- **新增按钮**：「押注天赋」

#### 任务 5.3：押注结算
- **文件**：`RogueRuntimeState.ResolveBet()`
- **逻辑**：
  - 无伤通关：50% 返还+随机新天赋，45% 仅返还，5% 消失（实际 4%）
  - 非无伤通关：40% 返还双倍，50% 仅返还，10% 消失（实际 8%）
- **文件**：`RogueResultController.SettleIfNeeded()`
- **改动**：结算时调用 `ResolveBet()`

---

### 阶段 6：组合技系统（目标 5）

**目标**：多天赋凑合成组合技，减数值惩罚引导组合

#### 任务 6.1：组合技数据结构
- **文件**：新建 `ComboSkillData.cs` (ScriptableObject)
- **字段**：
  - `string comboId`
  - `string displayName`
  - `string description`
  - `string[] requiredTalentIds` — 所需天赋组合
  - `TalentEffect comboEffect` — 组合效果
  - `string[] penaltyTalentIds` — 冲突天赋（减数值惩罚）

#### 任务 6.2：组合检测引擎
- **文件**：新建 `ComboDetector.cs`
- **功能**：
  - 每次选卡后检查 `SelectedTalentCardIds` 是否满足某个组合
  - 满足则激活组合效果
  - 检测冲突天赋，应用减数值惩罚
- **文件**：`TalentEffectApplier.cs`
- **改动**：注入组合加成和惩罚

#### 任务 6.3：组合技 UI 提示
- **文件**：`RogueResultController.cs`
- **改动**：选卡时若形成组合，显示提示动画
- **文件**：新建 `ComboNotification.cs`
- **功能**：组合激活时弹出提示

---

### 阶段 7：剧情卡片系统增强（目标 7）

**目标**：每次通关给剧情卡，查看后送角色小传笔记

#### 任务 7.1：通关解锁剧情卡（已有基础）
- **现状**：`LevelEndMenu.cs` 已有 `cardToUnlockOnWin` 字段
- **改动**：确保每个关卡都配置了对应的剧情卡

#### 任务 7.2：角色小传笔记系统
- **文件**：新建 `CharacterNoteData.cs` (ScriptableObject)
- **字段**：
  - `string noteId`
  - `string characterName`
  - `string noteContent` — 带剧透的小传内容
  - `Sprite characterPortrait`
- **文件**：新建 `CharacterNoteUnlockState.cs`
- **功能**：查看剧情卡后解锁对应角色小传
- **文件**：`StoryCardButton.cs`
- **改动**：查看剧情卡后调用 `CharacterNoteUnlockState.Unlock()`

#### 任务 7.3：小传笔记收藏界面
- **文件**：新建 `CharacterNoteCollection.cs`
- **功能**：在 StoryCardCollection 场景中增加角色小传页签
- 展示已解锁的小传笔记

---

### 阶段 8：关卡间对话系统（目标 8）

**目标**：通关后 1-2 句精简对话，8 秒读完再选天赋进下一关

#### 任务 8.1：对话数据结构
- **文件**：新建 `InterLevelDialogue.cs` (ScriptableObject)
- **字段**：
  - `string dialogueId`
  - `string speakerName`
  - `string[] lines` — 1-2 句对话
  - `float autoAdvanceTime` — 默认 8 秒
  - `Sprite speakerPortrait`

#### 任务 8.2：对话显示界面
- **文件**：新建 `InterLevelDialogueController.cs`
- **功能**：
  - 通关后弹出对话面板（不切场景）
  - 逐字显示对话（复用 NewbieTutorialController 的打字机效果）
  - 8 秒后或点击跳过
  - 对话结束后显示选卡 / 下一关按钮

#### 任务 8.3：接入关卡流程
- **文件**：`LevelEndMenu.cs`
- **改动**：
  - 胜利后先显示对话（`InterLevelDialogueController`）
  - 对话结束后再显示结束菜单 / 天赋选择
- **文件**：`RogueResultController.cs`
- **改动**：结算时先播对话再选卡

---

### 阶段 9：人物列传新模式（目标 9）

**目标**：主线通关后解锁新模式，角色个人剧情

#### 任务 9.1：模式解锁条件
- **文件**：新建 `GameProgressManager.cs`
- **功能**：
  - 记录主线通关进度
  - 主线全部通关后解锁人物列传入口
  - 持久化到 PlayerPrefs

#### 任务 9.2：人物列传入口
- **文件**：`Title.unity` 场景
- **改动**：标题画面增加「人物列传」按钮（主线通关后激活）
- **文件**：新建 `CharacterChronicleController.cs`
- **功能**：
  - 显示角色列表（已解锁的小传笔记角色）
  - 选择角色进入个人剧情

#### 任务 9.3：角色个人剧情场景
- **文件**：新建 `CharacterChronicle.unity` 场景
- **功能**：
  - 加载角色对应的 Naninovel 剧本
  - 战斗 + 剧情结合
  - 纯娱乐性质，不影响主线

---

## 三、优先级与依赖关系

```
阶段 1（免费化 + 点数重构）
  ↓
阶段 2（开局天赋）← 依赖阶段 1
  ↓
阶段 3（Boss 掉落）← 独立
  ↓
阶段 4（守护点免死）← 独立
  ↓
阶段 5（天赋押注）← 依赖阶段 1
  ↓
阶段 6（组合技）← 依赖阶段 1-5 完成
  ↓
阶段 7（剧情卡片）← 独立
  ↓
阶段 8（关卡间对话）← 独立
  ↓
阶段 9（人物列传）← 依赖阶段 7
```

**可并行开发的阶段**：
- 阶段 3 + 4（Boss 掉落 + 守护点免死）
- 阶段 7 + 8（剧情卡片 + 关卡间对话）

---

## 四、工作量估算

| 阶段 | 任务数 | 新建文件 | 修改文件 | 预估复杂度 |
|------|--------|----------|----------|------------|
| 1 | 4 | 0 | 4 | ⭐⭐ 中 |
| 2 | 2 | 0 | 3 | ⭐ 低 |
| 3 | 4 | 0 | 4 | ⭐⭐⭐ 高 |
| 4 | 3 | 0 | 3 | ⭐⭐ 中 |
| 5 | 3 | 1 | 2 | ⭐⭐⭐ 高 |
| 6 | 3 | 3 | 2 | ⭐⭐⭐⭐ 很高 |
| 7 | 3 | 3 | 2 | ⭐⭐ 中 |
| 8 | 3 | 2 | 2 | ⭐⭐ 中 |
| 9 | 3 | 3 | 1 | ⭐⭐⭐ 高 |

---

## 五、建议执行顺序

1. **阶段 1** — 天赋卡免费化 + 点数重构（基础，必须先做）
2. **阶段 2** — 开局天赋（紧接阶段 1）
3. **阶段 3** — Boss 掉落（核心战斗体验）
4. **阶段 4** — 守护点免死（战斗深度）
5. **阶段 5** — 天赋押注（策略深度）
6. **阶段 7** — 剧情卡片（内容填充，可与战斗系统并行）
7. **阶段 8** — 关卡间对话（叙事体验）
8. **阶段 6** — 组合技（最复杂，最后做）
9. **阶段 9** — 人物列传（主线完成后才需要）

---

## 六、需要确认的设计细节

1. **阶段 2**：每关都给开局抽卡，还是只有第一关？
2. **阶段 4**：守护点免死每局触发几次？（建议 1 次）
3. **阶段 4**：免死给的「3 本局 + 1 全局」是 4 选 1 还是直接给 4 张？
4. **阶段 5**：押注可以押几张天赋？（建议 1-3 张）
5. **阶段 6**：组合技需要先设计组合表，谁来设计？
6. **阶段 8**：对话内容谁写？
