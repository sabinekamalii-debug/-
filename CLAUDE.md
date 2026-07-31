# 项目记忆规则

## Debug 的含义（死死记住）

当用户说"加 Debug"时，指的是：
1. 在代码里写 `Debug.Log("...")` 语句
2. Unity 控制台（Console 窗口）在运行时能打印出来
3. **我自己主动读取控制台信息，看哪里出错了**

不是写文件日志、不是搞 DebugLogger 类、不是用 execute_csharp_script 返回值、不是任何其他形式的日志。就是纯粹的 `Debug.Log(...)` → 控制台显示 → 我自己读 Console 定位问题。

测试时流程：按 Play → 进游戏测试 → 切到 Unity Console 窗口 → 看打印的日志找问题。

## 创建对象的规则（死死记住）

**永远不要用代码在运行时创建对象。** 用户要的是场景层级面板里真实存在的 GameObject，可以直接在编辑器里选中、拖拽、调整参数。

- ✅ 用 `unity_gameobject` 工具的 `create` / `create_batch` 在场景里创建真实 GameObject
- ✅ 用 `unity_menu` 执行 "GameObject/Create Empty" 等菜单命令
- ❌ 不要写 `new GameObject(...)` 之类的运行时代码
- ❌ 不要写 `[SerializeField] private GameObject xxx` 然后在 Awake/Start 里 Instantiate
- ❌ 不要创建只在 Play 模式才出现的对象

核心原则：用户要能在 Hierarchy 窗口看到、点选、编辑的对象。

## 引擎说明

本项目使用团结引擎（Tuanjie），GUID 格式为 base64 风格（如 `WisftyOpBno...`），不是标准 Unity 的 32 位 hex 格式。不要手动替换场景文件里的 GUID。
