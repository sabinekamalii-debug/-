# 关卡标准化推广提示词

你是一个 Unity 项目改造助手。我需要你将以下关卡场景全部标准化为与 level 1 一致的结构。目标关卡：level 2 ~ level 16（以及 level boss、level elite）。

## 背景

项目是一个塔防游戏，level 1 是完整参照标准，其他关卡都是残次品。level 2 已经手动完成标准化作为验证。现在需要把同样的改造推广到所有其他战斗关卡。

## 需要改造的关卡场景列表

- Assets/Scenes/level/level 3.unity
- Assets/Scenes/level/level 4.unity
- Assets/Scenes/level/level 5.unity
- Assets/Scenes/level/level 6.unity
- Assets/Scenes/level/level 7.unity
- Assets/Scenes/level/level 8.unity
- Assets/Scenes/level/level 9.unity
- Assets/Scenes/level/level 10.unity
- Assets/Scenes/level/level 11.unity ~ level 16.unity（这些是空壳，只有骨架）
- Assets/Scenes/level/level boss.unity
- Assets/Scenes/level/level elite.unity

参照标准：Assets/Scenes/level/level 1.unity

## 改造步骤（每个关卡都执行以下全部步骤）

### 步骤 1：用 C# 脚本从 level 1 复制 5 个根对象覆盖到目标关卡

以 additive 方式打开 level 1，找到以下 5 个根对象，删除目标关卡中的同名对象，然后从 level 1 完整复制（Instantiate）过来：

1. **遭遇战菜单** — 注意：复制后必须保持 `activeSelf=false`，`localScale=(0,0,0)`
2. **结束菜单** — 包含 Panel(颁奖台背景)、继续(抽卡)、重来(查看剧情碎片)、返回主菜单、卡片 等子对象
3. **角色画布** — 包含干员卡片（地面远程/牧师/法师/珑/晶 为 active，拳师/女战士/光波/武士/先锋测试 为 inactive）
4. **敌我信息canvas (1)** — 包含敌人数量/生命/money/新手教程/文本对话(头像/新手教程背景/新手教程（真）)/守护点精灵图/刷怪点精灵图
5. **角色商店处** — SpriteRenderer，位置 (7.88, 0, 0)，scale (0.12, 0.38, 1)，sprite = Assets/杂和卡图案/卡片上方图片与框架/生成人物边框 (70).png，sortingOrder=3

复制方法（C# 脚本示例逻辑）：
```
1. var scene1 = EditorSceneManager.OpenScene("Assets/Scenes/level/level 1.unity", OpenSceneMode.Additive);
2. 遍历 scene1.GetRootGameObjects() 找到这 5 个对象
3. 遍历 scene2.GetRootGameObjects() 删除同名对象
4. Object.Instantiate 复制，MoveGameObjectToScene 移入目标场景
5. 复制 transform.position/rotation/localScale 和 activeSelf
6. EditorSceneManager.CloseScene(scene1, false)
```

### 步骤 2：从 level 1 复制整个 Managers 对象覆盖

Managers 是最关键的对象，包含所有游戏逻辑脚本。level 2 之前的 Managers 缺少 GameSpeedBoost 组件，且所有脚本引用都是 null。

1. 删除目标关卡的 Managers
2. 从 level 1 完整复制 Managers 对象
3. 复制 transform（position、rotation、localScale）和 activeSelf

Managers 包含的组件（必须全部存在）：
- GameManager
- DeploymentManager
- SystemMessageUI
- OperatorInputController
- LevelEndMenu
- TeleportController
- LevelDebugSkipToPlot
- GameSpeedBoost（level 2 之前缺少这个！）

### 步骤 3：修复所有 Canvas 的 renderMode 和 worldCamera（关键！）

从 level 1 Instantiate 复制对象时，Canvas 的 worldCamera 引用会丢失（因为是跨场景引用），renderMode 也会变回 ScreenSpaceOverlay。必须手动修复：

找到目标关卡中 Managers 下的 Main Camera（`GameObject.Find("Managers").transform.Find("Main Camera")`），获取其 Camera 组件，然后设置：

| 对象名 | renderMode | sortingOrder | worldCamera | planeDistance |
|--------|-----------|-------------|-------------|---------------|
| 遭遇战菜单 | ScreenSpaceCamera | 100 | Main Camera | 100 |
| 结束菜单 | ScreenSpaceCamera | 0 | Main Camera | 100 |
| 角色画布 | WorldSpace | 0 | Main Camera | 100 |
| 敌我信息canvas (1) | ScreenSpaceCamera | 122 | Main Camera | 100 |

### 步骤 4：修复所有 Canvas 的 Transform（关键！）

复制后 Canvas 的 position 和 scale 也会错乱。必须设置：

| 对象名 | position | localScale |
|--------|----------|-----------|
| 遭遇战菜单 | (-0.0977805257, 0, 90) | (0.009259259, 0.009259259, 0.009259259) |
| 结束菜单 | (-0.0977805257, 0, 90) | (0.009259259, 0.009259259, 0.009259259) |
| 角色画布 | (0, 0, 90) | (1, 1, 1) |
| 敌我信息canvas (1) | (-0.0977805257, 0, 90) | (0.009259259, 0.009259259, 0.009259259) |
| 角色商店处 | (7.88, 0, 0) | (0.12, 0.38, 1) |

### 步骤 5：重新接线 Managers 上所有脚本的序列化引用（关键！）

从 level 1 复制 Managers 后，所有跨对象的引用都断了（因为 Instantiate 不保留场景内引用）。必须用反射手动重新接线：

```csharp
var mgr = GameObject.Find("Managers");
var scene2 = EditorSceneManager.GetActiveScene();
var roots = scene2.GetRootGameObjects();

// 找到目标场景中的对象
GameObject endMenu = null, infoCanvas = null, charCanvas = null, spawner = null;
foreach (var r in roots) {
    if (r.name == "结束菜单") endMenu = r;
    if (r.name == "敌我信息canvas (1)") infoCanvas = r;
    if (r.name == "角色画布") charCanvas = r;
    if (r.name == "Spawner") spawner = r;
}

// 1. LevelEndMenu.endMenuCanvas → 结束菜单
var lem = mgr.GetComponent("LevelEndMenu");
lem.GetType().GetField("endMenuCanvas").SetValue(lem, endMenu);

// 2. LevelEndMenu.spawner → Spawner
lem.GetType().GetField("spawner").SetValue(lem, spawner);

// 3. GameManager.uiController → 敌我信息canvas (1) 上的 UIController 组件
var gm = mgr.GetComponent("GameManager");
gm.GetType().GetField("uiController").SetValue(gm, infoCanvas.GetComponent("UIController"));

// 4. SystemMessageUI.messageText → 文本对话/新手教程（真）的 TextMeshProUGUI
var sm = mgr.GetComponent("SystemMessageUI");
var textDialogue = infoCanvas.transform.Find("文本对话");
var tutorialReal = textDialogue.Find("新手教程 （真）");
sm.GetType().GetField("messageText").SetValue(sm, tutorialReal.GetComponent<TextMeshProUGUI>());

// 5. TeleportController.defensePointCooldownParentCanvas → 角色画布的 Canvas 组件
var tc = mgr.GetComponent("TeleportController");
tc.GetType().GetField("defensePointCooldownParentCanvas").SetValue(tc, charCanvas.GetComponent<Canvas>());

// 6. Spawner.ui → 敌我信息canvas (1) 上的 UIController 组件
var sp = spawner.GetComponent<Spawner>();
typeof(Spawner).GetField("ui").SetValue(sp, infoCanvas.GetComponent("UIController"));
```

### 步骤 6：修复 GridSystem.defensePoint

在目标关卡的 Grid 对象上找到 GridSystem 组件，将其 defensePoint 字段指向场景中的「守护点」对象：

```csharp
var grid = GameObject.Find("Grid");
var gs = grid.GetComponent<GridSystem>();
var defensePoint = GameObject.Find("守护点");
gs.defensePoint = defensePoint.transform;
```

### 步骤 7：设置关卡特定参数

以下字段需要根据关卡编号 N 设置不同的值：

**LevelEndMenu.labelName**：设为 `"AfterLevelN"`（如 level 3 → "AfterLevel3"）
```csharp
var lem = mgr.GetComponent("LevelEndMenu");
lem.GetType().GetField("labelName").SetValue(lem, "AfterLevel" + levelNumber);
```

**GameManager.playerHealth**：
- level 1-4: 设为 1（与 level 1 一致）
- level 5+: 可设为 3（测试阶段给更多生命）

**DeploymentManager.currentDP**：
- level 1-4: 设为 60（与 level 1 一致）
- level 5+: 可设为 70

**TeleportController.teleportCooldownDuration**：设为 50（与 level 1 一致）

### 步骤 8：简化波数数据（WaveData）

开发测试阶段，敌人需要非常简单容易通关。

检查 Spawner.waves 数组：
1. 确保没有重复的 WaveData 条目
2. 将所有波数改为基础敌人类型：

| 波次 | 敌人类型(EnemyType枚举) | 数量 | 生成间隔(秒) | 波次前延迟(秒) | 路径索引 |
|------|----------------------|------|------------|-------------|---------|
| Wave 1 | Enemy | 3 | 3.0 | 3 | 0 |
| Wave 2 | Enemy | 4 | 2.5 | 5 | 1 |
| Wave 3 | Enemy | 3 | 3.0 | 5 | 2 |
| Wave 4 | GeBuLin | 2 | 3.0 | 5 | 0 |
| Wave 5 | Enemy | 4 | 2.5 | 6 | 3 |
| Wave 6 | 小骷髅 | 3 | 3.0 | 5 | 2 |
| Wave 7 | Enemy | 3 | 3.0 | 5 | 1 |
| Wave 8 | GeBuLin | 2 | 4.0 | 5 | 2 |

波数数据资产位置：`Assets/数据2/敌人波数/level N/Wave X.asset`

修改方法（C# 脚本）：
```csharp
string folder = $"Assets/数据2/敌人波数/level {levelNumber}";
string[] guids = AssetDatabase.FindAssets("", new[] { folder });
foreach (string guid in guids) {
    string path = AssetDatabase.GUIDToAssetPath(guid);
    if (!path.EndsWith(".asset")) continue;
    var wave = AssetDatabase.LoadAssetAtPath<WaveData>(path);
    if (wave == null) continue;
    // 按上面的表格设置 wave.enemyType, wave.enemiesPerWave, wave.spawnInterval, wave.delayBeforeWave, wave.pathIndex, wave.waveNumberDisplay
    EditorUtility.SetDirty(wave);
}
AssetDatabase.SaveAssets();
```

**注意**：如果关卡没有足够数量的 WaveData 资产文件，需要新建。WaveData 是 ScriptableObject，通过 `CreateAssetMenu(fileName = "WaveData", menuName = "Scriptable Objects/WaveData")` 创建。

### 步骤 9：删除目标关卡中不属于 level 1 的多余对象

检查并删除以下类型的多余对象（level 1 中不存在的）：
- 名为「醒」的根对象
- 角色画布下名为「万录朵」的子对象
- 其他与 level 1 不一致的根对象

### 步骤 10：验证

每个关卡改造完成后：
1. 保存场景
2. 进入 Play 模式
3. 检查：
   - 结束菜单**不应该**在游戏开始时显示（LevelEndMenu.Start() 会调用 ForceHideEndMenu() 隐藏它）
   - 角色画布右侧应显示 5 个干员卡片
   - 遭遇战菜单不应该显示（inactive）
   - 敌人应正常生成
   - 无控制台报错

## 对于空壳关卡（level 11 ~ level 16）的额外说明

这些关卡只有骨架（Path/Spawner/Grid 空壳），缺少：
- Tilemap tiles（Ground/Wall/HighGround 上没有 tiles）
- 对象池（Spawner 下的 ObjectPooler 子对象可能缺失）
- 干员系统（干员对象缺失）
- 灯光（Global Light 2D 可能存在在 Managers 下）

对于这些关卡，除了执行步骤 1-10 外，还需要：
1. 确认 Spawner 下有所有 ObjectPooler 子对象（EnemyPool/smallpool/bigpool/哥布林/骷髅/小骷髅/黑之魔王）
2. 确认 Grid 下有 Ground/Wall/HighGround 三个 Tilemap 且有 tiles
3. 确认有守护点对象且配有 DeployLightController + DefensePointShooter
4. 确认有 Path 对象且 WayPoint 位置正确
5. 如果缺少上述任何对象，从 level 1 复制对应对象过来

## 关键注意事项

1. **Instantiate 跨场景引用丢失问题**：从 level 1 Instantiate 复制对象时，所有指向场景内其他对象的引用都会变成 null。必须手动重新接线。这是整个改造中最容易遗漏的步骤。

2. **Canvas 设置被覆盖问题**：如果先修复 Canvas 再复制 Managers，Canvas 修复会被覆盖。正确顺序是：先复制所有对象 → 再修复 Canvas → 最后接线 Managers 引用。

3. **执行顺序**：步骤 1（复制 UI 对象）→ 步骤 2（复制 Managers）→ 步骤 3-4（修复 Canvas）→ 步骤 5（接线 Managers）→ 步骤 6（GridSystem）→ 步骤 7（关卡参数）→ 步骤 8（波数）→ 步骤 9（清理）→ 步骤 10（验证）

4. **每次只处理一个关卡**：不要同时打开多个目标关卡，避免混淆。

5. **保存**：每个步骤完成后都要 MarkSceneDirty + SaveScene。

6. **敌人测试难度**：当前处于开发测试阶段，所有波数数据必须非常简单（基础敌人、少量、慢速），让玩家非常容易通关。

7. **不要修改 level 1**：level 1 是参照标准，永远不要修改它。

8. **.unity 和 .scene 区别**：这个项目使用 Tuanjie 引擎，真正的场景文件是 .unity。.scene 文件会被 gitignore 忽略。
