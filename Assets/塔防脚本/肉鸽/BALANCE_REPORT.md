# 📊 数值平衡自动报告

**自动生成时间**: 2026-07-31 18:51:47

> 本文件由 `BalanceScanner.cs` 自动生成，请勿手动编辑。
> 每次编译后自动刷新，也可通过 `Tools → 刷新数值报告` 手动触发。

---

## 一、肉鸽核心数值 (BalanceConfig.cs)

| 字段名 | 类型 | 值 |
|--------|------|-----|
| `FullGuardianHpForPerfectVictory` | int | 10 |
| `DeathConsolationBase` | int | 1 |
| `DeathConsolationPerStage` | int | 1 |
| `LossGoldGain` | int | 0 |
| `LossCardDrawGain` | int | 0 |
| `LossTalentPointGain` | int | 0 |
| `NormalWinGold` | int | 30 |
| `NormalWinCardDraw` | int | 1 |
| `NormalWinTalentPoint` | int | 2 |
| `NormalPerfectGold` | int | 50 |
| `NormalPerfectCardDraw` | int | 1 |
| `NormalPerfectTalentPoint` | int | 2 |
| `EliteWinGold` | int | 45 |
| `EliteWinCardDraw` | int | 2 |
| `EliteWinTalentPoint` | int | 3 |
| `ElitePerfectGold` | int | 75 |
| `ElitePerfectCardDraw` | int | 2 |
| `ElitePerfectTalentPoint` | int | 3 |
| `BossWinGold` | int | 70 |
| `BossWinCardDraw` | int | 3 |
| `BossWinTalentPoint` | int | 4 |
| `BossPerfectGold` | int | 115 |
| `BossPerfectCardDraw` | int | 3 |
| `BossPerfectTalentPoint` | int | 4 |
| `FirstClearBonusGold` | int | 20 |
| `BetNoHitReturnBonusChance` | float | 0.50f |
| `BetNoHitReturnOnlyChance` | float | 0.95f |
| `BetHitReturnDoubleChance` | float | 0.40f |
| `BetHitReturnOnlyChance` | float | 0.90f |
| `RerollCost` | int | 10 |
| `CardShopPriceCommon` | int | 15 |
| `CardShopPriceAdvanced` | int | 30 |
| `CardShopPriceRare` | int | 60 |
| `CardShopPriceLegendary` | int | 120 |
| `RewardDepthGrowthPerStage` | float | 0.03f |
| `ShopPriceDepthGrowthPerStage` | float | 0.02f |
| `ShopSlotCountDefault` | int | 5 |
| `ShopSlotCountMin` | int | 4 |
| `ShopSlotCountMax` | int | 6 |
| `ShopSlotDiscountChance` | float | 0.30f |
| `ShopRefreshBaseCost` | int | 10 |
| `ShopRefreshCostIncrement` | int | 5 |
| `CardTypeLimit` | int | 3 |
| `CardRemovalBaseCost` | int | 25 |
| `CardRemovalCostIncrement` | int | 15 |
| `CardRemovalLimitPerVisit` | int | 1 |

_共 46 个常量_

---

## 二、守护点 (GameManager.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `maxPlayerHealth` | int | 5 | public |
| `playerHealth` | int | 5 | public |
| `isGameOver` | bool | false | public |
| `enableEmergencyProtocol` | bool | true | public |
| `rewindSeconds` | float | 5.00f | public |
| `emergencyProtocolThreshold` | int | 1 | public |
| `postRewindStunSeconds` | float | 2.00f | public |

_共 7 个字段_


## 三、部署点系统 (DeploymentManager.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `maxDP` | int | 500 | public |
| `dpRecoverRate` | float | 2.00f | public |
| `currentDP` | int | 0 | public |
| `isGamePaused` | bool | false | public |
| `isRetreatMode` | bool | false | public |
| `retreatCooldownDuration` | float | 2.00f | public |
| `deployRangeExtra` | float | 0.50f | public |
| `deployCrossThicknessExtra` | float | 0.50f | public |
| `mobileDoubleTapInterval` | float | 0.35f | public |
| `mobileDoubleTapMaxMovePixels` | float | 80.00f | public |
| `editorDoubleClickInterval` | float | 0.35f | public |
| `editorDoubleClickMaxMovePixels` | float | 80.00f | public |

_共 12 个字段_


## 四、守护点射手 (DefensePointShooter.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `attackRange` | float | 5.00f | public |
| `attackInterval` | float | 1.00f | public |
| `attackDamage` | int | 500 | public |
| `ignoreDefense` | bool | false | public |
| `prioritizeNearDefensePoint` | bool | true | public |

_共 5 个字段_


## 五、肉鸽战斗控制器 (RogueBattleRunController.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `guardianMaxHp` | int | 10 | [SerializeField] |
| `guardianCurrentHp` | int | 10 | [SerializeField] |
| `betPlaced` | bool | false | [SerializeField] |
| `enableDebugHotkey` | bool | true | [SerializeField] |

_共 4 个字段_


---

## 六、干员基础数据 (OperatorData.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `operatorName` | string | (未设置) | public |
| `cost` | int | 10 | public |
| `maxHealth` | float | 100.00f | public |
| `defense` | int | 0 | public |
| `attackDamage` | float | 10.00f | public |
| `attackInterval` | float | 1.00f | public |
| `attackRange` | float | 3.50f | public |
| `deployRadius` | float | 4.00f | public |
| `purchaseCooldown` | float | 0.00f | public |
| `canStandOnGroundAndHighGround` | bool | false | public |
| `isAoEAttacker` | bool | false | public |
| `isHealer` | bool | false | public |
| `baseCritChance` | int | 0 | public |

_共 13 个字段_


## 七、干员运行时 (OperatorUnit.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `isMoving` | bool | false | public |
| `currentBlockCount` | int | 1 | public |
| `skillPreventAttack` | bool | false | public [HideInInspector] |
| `skillAttackAllBlocked` | bool | false | public [HideInInspector] |
| `deployCost` | int | 0 | public [HideInInspector] |
| `currentHealth` | int | 0 | public [HideInInspector] |
| `runtimeMaxHealth` | int | 0 | public [HideInInspector] |
| `runtimeAttackDamage` | int | 0 | public [HideInInspector] |
| `runtimeDefense` | int | 0 | public [HideInInspector] |
| `runtimeAttackInterval` | float | 0.00f | public [HideInInspector] |
| `traitAttackSpeedMultiplier` | float | 1.00f | public [HideInInspector] |
| `maxSP` | float | 10.00f | public |
| `currentSP` | float | 0.00f | public |
| `isSkillActive` | bool | false | public |
| `isSkillReady` | bool | false | public |

_共 15 个字段_


## 八、干员加成 (OperatorStatBonus.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `attackBonus` | int | 0 | public |
| `defenseBonus` | int | 0 | public |
| `deployCostBonus` | int | 0 | public |
| `healthBonus` | int | 0 | public |

_共 4 个字段_


## 九、天赋效果 (TalentEffectApplier.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
_无法创建 TalentEffectApplier 实例，跳过_


---

## 十、敌人基础数据 (EnemyData2.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `lives` | int | 0 | public |
| `defense` | int | 0 | public |
| `damage` | int | 0 | public |
| `damageforplayer` | int | 0 | public |
| `speed` | float | 0.00f | public |
| `attackInterval` | float | 1.00f | public |
| `attackRange` | float | 4.00f | public |
| `dpOnKill` | int | 0 | public |
| `penetrateDefense` | bool | false | public |
| `rangedAttack` | bool | false | public |
| `healRadius` | float | 0.00f | public |
| `healPercentOfMax` | float | 0.00f | public |
| `healInterval` | float | 0.00f | public |

_共 13 个字段_


## 十一、刷怪点血量 (SpawnerHealth.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `maxHealth` | int | 200 | public |
| `currentHealth` | int | 0 | public |
| `isBroken` | bool | false | public |

_共 3 个字段_


## 十二、刷怪点射手 (SpawnerShooter.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `attackRange` | float | 4.00f | public |
| `attackInterval` | float | 1.20f | public |
| `attackDamage` | int | 300 | public |
| `ignoreDefense` | bool | false | public |

_共 4 个字段_


---

## 十三、天赋卡数据 (TalentCardData.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `cardId` | string | (未设置) | public |
| `displayName` | string | 天赋 | public |
| `description` | string |  | public |
| `slotDiscount` | float | 0.00f | public |
| `effectValue` | int | 0 | public |
| `effectValue2` | int | 0 | public |
| `secondaryEffectValue` | int | 0 | public |
| `secondaryEffectValue2` | int | 0 | public |
| `targetOperatorDataId` | int | -1 | public |
| `purchaseCooldownPenalty` | int | 0 | public |
| `isGuardianRewindCard` | bool | false | public |
| `triggerValue` | int | 0 | public |
| `isCurse` | bool | false | public |
| `curseEffectValue` | int | 0 | public |
| `curseSecondaryEffectValue` | int | 0 | public |
| `curseRemovable` | bool | true | public |

_共 16 个字段_


---

## 技能系统

## 技能: Skill_AttackSpeed (Skill_AttackSpeed.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `speedMultiplier` | float | 2.00f | public |
| `initialSPOnDeploy` | float | 35.00f | public |
| `skillName` | string | 未命名技能 | public |
| `maxSP` | float | 10.00f | public |
| `duration` | float | 10.00f | public |
| `autoActivate` | bool | false | public |

_共 6 个字段_

## 技能: Skill_BlockAndStrikeAll (Skill_BlockAndStrikeAll.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `skillBlockCount` | int | 10 | public |
| `damageMultiplier` | float | 1.33f | public |
| `initialSP` | float | 30.00f | public |
| `skillName` | string | 未命名技能 | public |
| `maxSP` | float | 10.00f | public |
| `duration` | float | 10.00f | public |
| `autoActivate` | bool | false | public |

_共 7 个字段_

## 技能: Skill_DPBurst (Skill_DPBurst.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `dpBurst` | int | 15 | public |
| `initialSPOnDeploy` | float | 0.00f | public |
| `enableFlash` | bool | true | public |
| `skillName` | string | 未命名技能 | public |
| `maxSP` | float | 10.00f | public |
| `duration` | float | 10.00f | public |
| `autoActivate` | bool | false | public |

_共 7 个字段_

## 技能: Skill_GoldenDefense (Skill_GoldenDefense.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `healthMultiplier` | float | 3.00f | public |
| `blockCountBonus` | int | 2 | public |
| `skillName` | string | 未命名技能 | public |
| `maxSP` | float | 10.00f | public |
| `duration` | float | 10.00f | public |
| `autoActivate` | bool | false | public |

_共 6 个字段_

## 技能: Skill_PenetrateDefense (Skill_PenetrateDefense.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `initialSPOnDeploy` | float | 25.00f | public |
| `skillName` | string | 未命名技能 | public |
| `maxSP` | float | 10.00f | public |
| `duration` | float | 10.00f | public |
| `autoActivate` | bool | false | public |

_共 5 个字段_

## 技能: Skill_PowerUp (Skill_PowerUp.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `damageMultiplier` | float | 2.00f | public |
| `skillName` | string | 未命名技能 | public |
| `maxSP` | float | 10.00f | public |
| `duration` | float | 10.00f | public |
| `autoActivate` | bool | false | public |

_共 5 个字段_

## 技能: Skill_RangeExpand (Skill_RangeExpand.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `rangeMultiplier` | float | 1.50f | public |
| `skillName` | string | 未命名技能 | public |
| `maxSP` | float | 10.00f | public |
| `duration` | float | 10.00f | public |
| `autoActivate` | bool | false | public |

_共 5 个字段_

---

## 十四、阻挡系统 (UnitBlocker.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `maxBlockCount` | int | 2 | public |

_共 1 个字段_


## 十五、加速系统 (GameSpeedBoost.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `speedWhenHeld` | float | 24.00f | public |
| `useLeftControl` | bool | true | public |
| `useRightControl` | bool | true | public |

_共 3 个字段_


## 十六、传送系统 (TeleportController.cs)

| 字段名 | 类型 | 默认值 | 修饰符 |
|--------|------|--------|--------|
| `worldSpaceOverlayWorldUnits` | float | 5.00f | public |
| `cooldownOverlayAlpha` | float | 0.60f | public |
| `cooldownOverlaySortOrder` | int | 60 | public |
| `portalOffsetRight` | float | 1.20f | public |
| `operatorMoveOutDuration` | float | 0.50f | public |
| `portalShowAtDestinationDuration` | float | 1.00f | public |
| `teleportCooldownDuration` | float | 50.00f | public |

_共 7 个字段_


---

## 十七、关键战斗公式

| 公式 | 说明 |
|------|------|
| `reduction = Min(0.99f, defense / 10000f)` | 防御减伤：100防御=1%，上限99% |
| `Soul = DeathConsolationBase + 通关层数 × PerStage` | 死亡返还魂 |

---

## ScriptableObject 资产实测数值

> 以下数值来自 .asset 文件，非代码默认值。

### 敌人数据资产 (EnemyData2) (19 个资产)

| 资产名 | lives | defense | damage | damageforplayer | speed | attackInterval | attackRange | dpOnKill | penetrateDefense | rangedAttack | healRadius | healPercentOfMax | healInterval |
|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|
| big boss | 1700 | 0 | 20 | 2 | 2.00f | 0.90f | 4.00f | 4 | false | false | 0.00f | 0.00f | 0.00f |
| Enemy_enemy | 400 | 0 | 4 | 1 | 2.00f | 0.90f | 4.00f | 1 | false | false | 0.00f | 0.00f | 0.00f |
| small boss | 800 | 0 | 8 | 1 | 2.00f | 0.90f | 4.00f | 2 | false | false | 0.00f | 0.00f | 0.00f |
| 万录朵 | 2500 | 0 | 18 | 2 | 1.00f | 1.00f | 4.00f | 11 | false | false | 0.00f | 0.00f | 0.00f |
| 哥布林 | 70 | 0 | 14 | 1 | 4.00f | 0.90f | 4.00f | 1 | false | false | 0.00f | 0.00f | 0.00f |
| 奶妈怪 | 45 | 0 | 6 | 1 | 4.00f | 1.00f | 4.00f | 2 | false | false | 3.50f | 0.04f | 1.50f |
| 小骷髅 | 310 | 0 | 3 | 1 | 1.00f | 0.80f | 4.00f | 1 | false | false | 0.00f | 0.00f | 0.00f |
| 术师怪 | 60 | 0 | 18 | 1 | 3.50f | 1.00f | 4.00f | 2 | true | false | 0.00f | 0.04f | 1.50f |
| 杂兵潮 | 35 | 0 | 8 | 1 | 5.00f | 0.80f | 4.00f | 1 | false | false | 0.00f | 0.04f | 1.50f |
| 火之魔王 | 2100 | 4000 | 1600 | 3 | 1.00f | 2.00f | 6.00f | 55 | false | false | 0.00f | 0.00f | 0.00f |
| 疾跑者 | 30 | 0 | 10 | 1 | 9.00f | 0.70f | 4.00f | 1 | false | false | 0.00f | 0.04f | 1.50f |
| 石头怪 | 3000 | 2000 | 30 | 2 | 0.50f | 1.00f | 4.00f | 6 | false | false | 0.00f | 0.00f | 0.00f |
| 蜜蜂 | 2 | 0 | 8 | 1 | 2.50f | 0.20f | 4.00f | 1 | false | false | 0.00f | 0.00f | 0.00f |
| 远程怪 | 80 | 0 | 22 | 1 | 3.00f | 1.50f | 6.00f | 2 | false | true | 0.00f | 0.04f | 1.50f |
| 重甲兵 | 120 | 6000 | 16 | 1 | 2.20f | 1.20f | 4.00f | 3 | false | false | 0.00f | 0.04f | 1.50f |
| 重锤兵 | 200 | 0 | 40 | 1 | 2.50f | 2.00f | 4.00f | 4 | false | false | 0.00f | 0.04f | 1.50f |
| 骷髅 | 2600 | 1000 | 30 | 2 | 1.00f | 0.90f | 4.00f | 6 | false | false | 0.00f | 0.00f | 0.00f |
| 黑之魔王 | 20000 | 5000 | 200 | 3 | 3.00f | 1.00f | 4.00f | 60 | false | false | 0.00f | 0.00f | 0.00f |
| 黑之魔王分身 | 700 | 1000 | 13 | 1 | 1.50f | 1.00f | 4.00f | 3 | false | false | 0.00f | 0.00f | 0.00f |

### 干员数据资产 (OperatorData) (29 个资产)

| 资产名 | operatorName | cost | maxHealth | defense | attackDamage | attackInterval | attackRange | deployRadius | purchaseCooldown | canStandOnGroundAndHighGround | isAoEAttacker | isHealer | baseCritChance |
|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|
| 万录朵 | 万录朵 | 50 | 5.00f | 0 | 1300.00f | 15.00f | 10.00f | 1.00f | 60.00f | false | false | false | 0 |
| 先锋测试 | 先锋测试 | 6 | 120.00f | 80 | 23.00f | 0.80f | 3.50f | 6.00f | 15.00f | false | false | false | 0 |
| 光波 | 光波 | 26 | 1.00f | 0 | 13.00f | 1.00f | 4.10f | 2.00f | 30.00f | true | true | false | 0 |
| 净化 | 净化 | 20 | 40.00f | 100 | 8.00f | 2.50f | 5.00f | 8.00f | 18.00f | false | false | true | 0 |
| 圣光 | 圣光 | 18 | 50.00f | 200 | 12.00f | 1.50f | 4.00f | 8.00f | 25.00f | false | false | true | 0 |
| 地面射手 | 地面射手 | 20 | 20.00f | 0 | 10.00f | 0.40f | 4.00f | 3.00f | 20.00f | false | false | false | 0 |
| 坚守先锋 | 坚守先锋 | 9 | 210.00f | 500 | 13.00f | 1.10f | 3.50f | 6.00f | 15.00f | false | false | false | 0 |
| 奥术 | 奥术 | 35 | 22.00f | 0 | 55.00f | 1.80f | 4.00f | 8.00f | 28.00f | false | false | false | 0 |
| 女战士 | 女战士 | 21 | 250.00f | 6000 | 9.00f | 1.00f | 3.50f | 6.00f | 20.00f | false | false | false | 0 |
| 战术先锋 | 战术先锋 | 7 | 130.00f | 100 | 21.00f | 0.90f | 3.50f | 6.00f | 15.00f | false | false | false | 0 |
| 拳师 | 拳师 | 14 | 20.00f | 200 | 80.00f | 1.00f | 3.50f | 1.00f | 3.00f | false | false | false | 0 |
| 斥候先锋 | 斥候先锋 | 4 | 85.00f | 0 | 19.00f | 0.55f | 3.50f | 6.00f | 15.00f | false | false | false | 0 |
| 晶 | 晶 | 15 | 200.00f | 2000 | 35.00f | 1.00f | 3.50f | 6.00f | 5.00f | false | false | false | 0 |
| 武士 | 武士 | 16 | 200.00f | 4000 | 35.00f | 1.00f | 3.50f | 4.00f | 15.00f | false | false | false | 0 |
| 法师 | 法师 | 45 | 30.00f | 0 | 115.00f | 3.00f | 4.50f | 8.00f | 40.00f | false | false | false | 0 |
| 游击先锋 | 游击先锋 | 5 | 95.00f | 20 | 22.00f | 0.65f | 3.50f | 6.00f | 15.00f | false | false | false | 0 |
| 牧师 | 牧师 | 16 | 60.00f | 300 | 8.00f | 2.00f | 3.00f | 3.00f | 30.00f | true | false | true | 0 |
| 猎手先锋 | 猎手先锋 | 6 | 110.00f | 40 | 27.00f | 0.85f | 3.50f | 6.00f | 15.00f | false | false | false | 0 |
| 珑 | 珑 | 20 | 280.00f | 3500 | 15.00f | 1.00f | 3.50f | 2.00f | 20.00f | false | false | false | 0 |
| 突击先锋 | 突击先锋 | 3 | 70.00f | 0 | 21.00f | 0.60f | 3.50f | 6.00f | 15.00f | false | false | false | 0 |
| 荆棘 | 荆棘 | 22 | 1800.00f | 6000 | 25.00f | 1.20f | 1.00f | 6.00f | 30.00f | false | false | false | 0 |
| 近卫 | 近卫 | 9 | 1200.00f | 12 | 130.00f | 1.00f | 3.50f | 6.00f | 15.00f | false | false | false | 0 |
| 连弩 | 连弩 | 14 | 20.00f | 0 | 12.00f | 0.35f | 5.00f | 8.00f | 12.00f | false | false | false | 20 |
| 醒 | 醒 | 0 | 20000.00f | 5000 | 400.00f | 2.00f | 3.50f | 5.00f | 600.00f | false | false | false | 0 |
| 钩爪 | 钩爪 | 15 | 150.00f | 1000 | 22.00f | 1.50f | 2.50f | 5.00f | 20.00f | false | false | false | 0 |
| 铁壁 | 铁壁 | 18 | 2500.00f | 8000 | 12.00f | 1.50f | 1.00f | 6.00f | 40.00f | false | false | false | 0 |
| 风暴先锋 | 风暴先锋 | 7 | 135.00f | 60 | 18.00f | 0.50f | 3.50f | 6.00f | 15.00f | false | false | false | 0 |
| 驻防先锋 | 驻防先锋 | 8 | 175.00f | 350 | 15.00f | 1.00f | 3.50f | 6.00f | 15.00f | false | false | false | 0 |
| 鹰眼 | 鹰眼 | 16 | 25.00f | 0 | 28.00f | 0.70f | 6.00f | 8.00f | 18.00f | false | false | false | 25 |

### 天赋卡资产 (TalentCardData) (96 个资产)

| 资产名 | cardId | displayName | description | slotDiscount | effectValue | effectValue2 | secondaryEffectValue | secondaryEffectValue2 | targetOperatorDataId | purchaseCooldownPenalty | isGuardianRewindCard | triggerValue | isCurse | curseEffectValue | curseSecondaryEffectValue | curseRemovable |
|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|
| atk_berserk | atk_berserk | 狂战之怒 | 低血时攻击力+50%，攻击速度+20% | 0.00f | 50 | 0 | 20 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| atk_crit1 | atk_crit1 | 暴击强化1 | 暴击率+10% | 0.00f | 10 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| atk_crit2 | atk_crit2 | 暴击强化2 | 暴击率+20%，暴击伤害+20% | 0.00f | 20 | 0 | 20 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| atk_crit3 | atk_crit3 | 暴击强化3 | 暴击率+30%，暴击伤害+50% | 0.00f | 30 | 0 | 50 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| atk_killbuff | atk_killbuff | 杀戮律动 | 击杀后攻速+30%持续3秒，吸血8% | 0.00f | 30 | 3 | 8 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| atk_killstack | atk_killstack | 战意觉醒 | 每击杀+1攻击力(上限20)，吸血5% | 0.00f | 1 | 20 | 5 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| atk_lifesteal1 | atk_lifesteal1 | 吸血1 | 吸血8% | 0.00f | 8 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| atk_lifesteal2 | atk_lifesteal2 | 吸血2 | 吸血18%，最大生命+10% | 0.00f | 18 | 0 | 10 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| atk_pierce1 | atk_pierce1 | 破甲1 | 无视防御15% | 0.00f | 15 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| atk_pierce2 | atk_pierce2 | 破甲2 | 无视防御30%，攻击力+8% | 0.00f | 30 | 0 | 8 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| atk_power1 | atk_power1 | 攻击强化1 | 攻击力+10% | 0.00f | 10 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| atk_power2 | atk_power2 | 攻击强化2 | 攻击力+20% | 0.00f | 20 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| atk_power3 | atk_power3 | 攻击强化3 | 攻击力+35%，对精英怪伤害+15% | 0.00f | 35 | 0 | 15 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| atk_speed1 | atk_speed1 | 攻速提升1 | 攻击速度+12% | 0.00f | 12 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| atk_speed2 | atk_speed2 | 攻速提升2 | 攻击速度+25% | 0.00f | 25 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| atk_speed3 | atk_speed3 | 攻速提升3 | 攻击速度+40%，攻击力+10% | 0.00f | 40 | 0 | 10 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| curse_abyss | curse_abyss | 深渊凝视 | 守护点最大生命 -5。不可移除。深渊在凝视着你，它不会轻易放手。 | 0.00f | 0 | 0 | 0 | 0 | -1 | 0 | false | 0 | true | -5 | 0 | false |
| curse_bloodmoon | curse_bloodmoon | 血月诅咒 | 守护点最大生命 -3。血月之下，你的堡垒变得不堪一击。 | 0.00f | 0 | 0 | 0 | 0 | -1 | 0 | false | 0 | true | -3 | 0 | true |
| curse_decay | curse_decay | 腐朽铠甲 | 全体干员防御力 -20%。装备在不知不觉中开始锈蚀。 | 0.00f | 0 | 0 | 0 | 0 | -1 | 0 | false | 0 | true | -20 | 0 | true |
| curse_elite | curse_elite | 精英猎杀者 | 精英敌人全属性 +25%。更强的精英在等待着你的失误。 | 0.00f | 0 | 0 | 0 | 0 | -1 | 0 | false | 0 | true | -25 | 0 | true |
| curse_fate | curse_fate | 命运无常 | 暴击率 -20%。幸运女神背弃了你。 | 0.00f | 0 | 0 | 0 | 0 | -1 | 0 | false | 0 | true | -20 | 0 | true |
| curse_feeble | curse_feeble | 虚弱之触 | 全体干员最大生命值 -25%。你的部队变得弱不禁风。 | 0.00f | 0 | 0 | 0 | 0 | -1 | 0 | false | 0 | true | -25 | 0 | true |
| curse_greed | curse_greed | 贪婪之罚 | 击杀敌人获得的金币 -40%。财富似乎总是从你指缝间溜走。 | 0.00f | 0 | 0 | 0 | 0 | -1 | 0 | false | 0 | true | -40 | 0 | true |
| curse_guardian_frail | curse_guardian_frail | 守护点脆弱 | 守护点受到的所有伤害 +30%。命运对你的守护点格外残忍。 | 0.00f | 0 | 0 | 0 | 0 | -1 | 0 | false | 0 | true | -30 | 0 | true |
| curse_misfortune | curse_misfortune | 厄运缠身 | 免费重抽不可用，选卡选项数 -1（最低2个）。命运仿佛在和你作对。 | 0.00f | 0 | 0 | 0 | 0 | -1 | 0 | false | 0 | true | 0 | 0 | true |
| curse_sloth | curse_sloth | 怠惰之咒 | 全体干员攻击速度 -20%。你的部队变得迟缓而疲惫。 | 0.00f | 0 | 0 | 0 | 0 | -1 | 0 | false | 0 | true | -20 | 0 | true |
| curse_void | curse_void | 虚空虚弱 | 全体干员攻击力 -15%。你的部队仿佛被虚空削弱了。 | 0.00f | 0 | 0 | 0 | 0 | -1 | 0 | false | 0 | true | -15 | 0 | true |
| curse_warfever | curse_warfever | 战争狂热 | 击杀后攻速 +30%，但受到伤害 +20%。杀戮的快感让你失去理智。 | 0.00f | 0 | 0 | 0 | 0 | -1 | 0 | false | 0 | true | 30 | -20 | true |
| def_block | def_block | 格挡大师 | 防御力+30%，最大生命+20% | 0.00f | 30 | 0 | 20 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| def_def1 | def_def1 | 防御强化1 | 防御力+20% | 0.00f | 20 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| def_def2 | def_def2 | 防御强化2 | 防御力+40% | 0.00f | 40 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| def_def3 | def_def3 | 防御强化3 | 防御力+60%，最大生命+15% | 0.00f | 60 | 0 | 15 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| def_dodge1 | def_dodge1 | 灵活身法 | 攻击速度+15%，防御力+10% | 0.00f | 15 | 0 | 10 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| def_dodge2 | def_dodge2 | 疾风步 | 攻击速度+25%，攻击范围+1 | 0.00f | 25 | 0 | 1 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| def_fortify | def_fortify | 坚守阵地 | 防御力+30%，受精英怪伤害-15% | 0.00f | 30 | 0 | -15 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| def_hp1 | def_hp1 | 生命强化1 | 最大生命+15% | 0.00f | 15 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| def_hp2 | def_hp2 | 生命强化2 | 最大生命+30% | 0.00f | 30 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| def_hp3 | def_hp3 | 生命强化3 | 最大生命+50%，防御力+15% | 0.00f | 50 | 0 | 15 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| def_regen1 | def_regen1 | 快速恢复 | 最大生命+15%，吸血5% | 0.00f | 15 | 0 | 5 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| def_regen2 | def_regen2 | 不灭再生 | 最大生命+25%，吸血12% | 0.00f | 25 | 0 | 12 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| def_thorns1 | def_thorns1 | 荆棘护甲 | 防御力+25%，攻击力+8% | 0.00f | 25 | 0 | 8 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| def_thorns2 | def_thorns2 | 复仇反甲 | 防御力+35%，吸血8% | 0.00f | 35 | 0 | 8 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| def_undying | def_undying | 不屈意志 | 最大生命+25%，防御力+20% | 0.00f | 25 | 0 | 20 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_avatar1 | grd_avatar1 | 守护神降临 | 守护点伤害+1000，射程+3 | 0.00f | 1000 | 0 | 3 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_dmg1 | grd_dmg1 | 锐利防御 | 守护点伤害+100 | 0.00f | 100 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_dmg2 | grd_dmg2 | 守护增幅 | 守护点伤害+200，射程+1 | 0.00f | 200 | 0 | 1 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_eternal1 | grd_eternal1 | 永恒守护 | 守护点生命+5，战斗结束回满HP | 0.00f | 5 | 0 | 1 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_fortress1 | grd_fortress1 | 不灭要塞 | 守护点生命+8，单次受伤上限1 | 0.00f | 8 | 0 | 1 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_hp1 | grd_hp1 | 守护之心 | 守护点生命+3 | 0.00f | 3 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_hp2 | grd_hp2 | 加固壁垒 | 守护点生命+5，单次受伤上限2 | 0.00f | 5 | 0 | 2 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_multi1 | grd_multi1 | 守护风暴 | 守护点多目标+2，伤害+50 | 0.00f | 2 | 0 | 50 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_rage1 | grd_rage1 | 守护者之怒 | 守护点低血时伤害×200%，生命+2 | 0.00f | 200 | 0 | 2 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_regen1 | grd_regen1 | 自然恢复 | 守护点回血间隔-15秒 | 0.00f | 15 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_regen2 | grd_regen2 | 守护回春 | 守护点回血间隔-8秒 | 0.00f | 8 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_resonance1 | grd_resonance1 | 守护共鸣 | 每存活干员守护点HP+2，生命+2 | 0.00f | 2 | 0 | 2 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_rewind1 | grd_rewind1 | 时光延长 | 时光回溯额外回退3秒 | 0.00f | 3 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_rewind2 | grd_rewind2 | 二次回溯 | 时光回溯额外1次 | 0.00f | 1 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_shield1 | grd_shield1 | 绝对防御 | 守护点护盾2次，生命+3 | 0.00f | 2 | 0 | 3 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_teleport1 | grd_teleport1 | 传送加速 | 传送冷却-15秒 | 0.00f | 15 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_teleport2 | grd_teleport2 | 战术转移 | 传送冷却-20秒，传送后攻速+50%持续5秒 | 0.00f | 20 | 0 | 50 | 5 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| grd_timelord1 | grd_timelord1 | 时间领主 | 时光回溯额外2次，额外回退5秒 | 0.00f | 2 | 0 | 5 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| rar_ad | rar_ad | 攻防一体 | 攻击力+15%，防御力+20% | 0.00f | 15 | 0 | 20 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| rar_ag | rar_ag | 攻守同盟 | 攻击力+12%，守护点生命+5 | 0.00f | 12 | 0 | 5 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| rar_ah | rar_ah | 攻血兼备 | 攻击力+15%，最大生命+20% | 0.00f | 15 | 0 | 20 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| rar_all | rar_all | 全面发展 | 攻击力+10%，防御力+12% | 0.00f | 10 | 0 | 12 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| rar_as | rar_as | 战技合一 | 攻击力+18%，攻击速度+12% | 0.00f | 18 | 0 | 12 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| rar_dga | rar_dga | 坚壁守护 | 防御力+18%，守护点生命+5 | 0.00f | 18 | 0 | 5 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| rar_dh | rar_dh | 防血双修 | 防御力+20%，最大生命+20% | 0.00f | 20 | 0 | 20 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| rar_ds | rar_ds | 守技同心 | 防御力+20%，最大生命+15% | 0.00f | 20 | 0 | 15 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| rar_perfect | rar_perfect | 完美主义 | 攻击力+15%，防御力+15% | 0.00f | 15 | 0 | 15 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| rar_triple | rar_triple | 三位一体 | 攻击力+12%，防御力+15% | 0.00f | 12 | 0 | 15 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| rar_vanguard_speed | rar_vanguard_speed | 先锋疾行 | 先锋干员移动速度 +100%（加倍） | 0.00f | 100 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| skl_auto | skl_auto | 自动施放 | 暴击率+10%，攻击力+8% | 0.00f | 10 | 0 | 8 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| skl_berserk | skl_berserk | 狂战技能 | 低血时攻击力+40%，攻击速度+12% | 0.00f | 40 | 0 | 12 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| skl_cd1 | skl_cd1 | 冷却缩减 | 攻击速度+12%，防御力+8% | 0.00f | 12 | 0 | 8 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| skl_chain | skl_chain | 技能连锁 | AoE范围+20%，攻击速度+15% | 0.00f | 20 | 0 | 15 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| skl_charge1 | skl_charge1 | SP充能1 | 攻击速度+8%，攻击力+5% | 0.00f | 8 | 0 | 5 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| skl_charge2 | skl_charge2 | SP充能2 | 攻击速度+15%，攻击力+10% | 0.00f | 15 | 0 | 10 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| skl_charge3 | skl_charge3 | SP充能3 | 攻击速度+25%，暴击率+10% | 0.00f | 25 | 0 | 10 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| skl_duration1 | skl_duration1 | 永恒持续 | 攻击速度+10%，最大生命+12% | 0.00f | 10 | 0 | 12 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| skl_power1 | skl_power1 | 技能强化1 | 攻击力+10%，暴击伤害+15% | 0.00f | 10 | 0 | 15 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| skl_power2 | skl_power2 | 技能强化2 | 攻击力+18%，暴击伤害+25% | 0.00f | 18 | 0 | 25 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| skl_power3 | skl_power3 | 技能强化3 | 攻击力+25%，AoE范围+20% | 0.00f | 25 | 0 | 20 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| skl_reset | skl_reset | 技能重置 | 攻击速度+15%，吸血5% | 0.00f | 15 | 0 | 5 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| skl_vanguard_laststand | skl_vanguard_laststand | 先锋绝唱 | 先锋阵亡时自动触发技能返还部署点；无技能则返还撤退至守护点时的部署费用 | 0.00f | 0 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| spc_convert | spc_convert | 卡牌转化 | 吸血5%，防御力+10% | 0.00f | 5 | 0 | 10 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| spc_double | spc_double | 双倍效果 | 暴击伤害+20%，攻击力+10% | 0.00f | 20 | 0 | 10 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| spc_draw | spc_draw | 命运抉择 | 攻击力+8%，防御力+8% | 0.00f | 8 | 0 | 8 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| spc_evade | spc_evade | 疾影脱身 | 选择避让时不受接触伤害，移速瞬间暴涨 10 倍，持续 0.4 秒 | 0.00f | 0 | 0 | 0 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| spc_fortune | spc_fortune | 幸运女神 | 暴击率+15%，金币+20% | 0.00f | 15 | 0 | 20 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| spc_gold1 | spc_gold1 | 金币加成1 | 金币+25%，分数+15% | 0.00f | 25 | 0 | 15 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| spc_gold2 | spc_gold2 | 金币加成2 | 金币+50%，分数+30% | 0.00f | 50 | 0 | 30 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| spc_repair | spc_repair | 战场维修 | 守护点 HP +3
全体 HP +10%
每场胜利后守护点回满 | 0.00f | 3 | 0 | 10 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| spc_reroll | spc_reroll | 重新洗牌 | 攻击速度+8%，最大生命+8% | 0.00f | 8 | 0 | 8 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| spc_shop | spc_shop | 讨价还价 | 金币+20%，防御力+5% | 0.00f | 20 | 0 | 5 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |
| spc_skip | spc_skip | 速通模式 | 攻击速度+20%，金币+15% | 0.00f | 20 | 0 | 15 | 0 | -1 | 0 | false | 0 | false | 0 | 0 | true |

---

_本文件由 BalanceScanner 自动生成于 2026-07-31 18:51:47_

