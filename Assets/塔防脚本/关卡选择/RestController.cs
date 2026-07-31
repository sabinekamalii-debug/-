using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 休息节点场景控制器：
/// - 场景中需预置 UI 对象（RestCanvas 及子物体），本脚本按名称查找绑定
/// - 参考 Slay the Spire 休息点设计：玩家只能选择一个动作
/// - 休整：恢复守护点 30% 生命
/// - 研习：获得 1 次抽卡次数
/// - 锻造：永久强化一张已拥有的天赋卡（效果 ×1.5，本局持续）
/// - 净化：移除一个随机诅咒（有诅咒时可用）
/// - 打劫路人：进入风险战斗，胜利得 100 额外金币
/// </summary>
public class RestController : MonoBehaviour
{
    private int _maxHp;
    private int _currentHp;

    // ── 场景绑定对象 ──
    private Canvas _canvas;
    private TMP_Text _hpText;
    private TMP_Text _goldText;
    private TMP_Text _drawText;
    private TMP_Text _curseText;
    private TMP_Text _resultText;
    private Button _purifyBtn;
    private Button _forgeBtn;
    private readonly List<Button> _optionButtons = new List<Button>();
    private GameObject _forgePanel;
    private Transform _forgeListRoot;

    // ── 选项按钮色 ──
    private static readonly Color RestColor    = new Color(0.25f, 0.50f, 0.25f);
    private static readonly Color StudyColor   = new Color(0.20f, 0.35f, 0.55f);
    private static readonly Color ForgeColor   = new Color(0.50f, 0.35f, 0.20f);
    private static readonly Color ForgeLockColor = new Color(0.15f, 0.15f, 0.18f);
    private static readonly Color PurifyColor  = new Color(0.35f, 0.20f, 0.50f);
    private static readonly Color PurifyLockColor = new Color(0.15f, 0.15f, 0.18f);
    private static readonly Color AmbushColor  = new Color(0.50f, 0.15f, 0.12f);

    private void Awake()
    {
        RogueRuntimeState.InitIfNeeded();
        LoadGuardianHp();
        BindSceneObjects();
    }

    private void Start()
    {
        RefreshInfo();
        BindButtons();
        RefreshOptionLockStates();
    }

    private void LoadGuardianHp()
    {
        if (RogueRuntimeState.GuardianMaxHp > 0)
        {
            _maxHp     = RogueRuntimeState.GuardianMaxHp;
            _currentHp = RogueRuntimeState.GuardianCurrentHp;
        }
        else
        {
            _maxHp     = 5 + TalentEffectApplier.GetGuardianHpBonus();
            _currentHp = _maxHp;
        }
    }

    // ════════════════════════════════════════
    //  场景绑定（GameObject.Find 模式）
    // ════════════════════════════════════════

    private void BindSceneObjects()
    {
        var canvasGo = GameObject.Find("RestCanvas");
        if (canvasGo != null)
            _canvas = canvasGo.GetComponent<Canvas>();

        _hpText     = FindTmp("HpText");
        _goldText   = FindTmp("GoldText");
        _drawText   = FindTmp("DrawText");
        _curseText  = FindTmp("CurseText");
        _resultText = FindTmp("ResultText");

        _forgeBtn    = FindButton("Btn_Forge");
        _purifyBtn   = FindButton("Btn_Purify");

        _optionButtons.Clear();
        _optionButtons.Add(FindButton("Btn_Rest"));
        _optionButtons.Add(FindButton("Btn_Study"));
        if (_forgeBtn != null)  _optionButtons.Add(_forgeBtn);
        if (_purifyBtn != null) _optionButtons.Add(_purifyBtn);
        _optionButtons.Add(FindButton("Btn_Leave"));

        // ForgePanel 默认 inactive，GameObject.Find 无法找到，需通过 Canvas transform 搜索
        // 新结构: ForgePanel > ForgeFrame > (ForgeTitle, ForgeListRoot, Btn_ForgeClose)
        if (_canvas != null)
        {
            var forgePanelTr = _canvas.transform.Find("ForgePanel");
            if (forgePanelTr != null)
            {
                _forgePanel = forgePanelTr.gameObject;
                _forgePanel.SetActive(false);

                var forgeFrameTr = forgePanelTr.Find("ForgeFrame");
                var listTr = forgeFrameTr != null ? forgeFrameTr.Find("ForgeListRoot") : forgePanelTr.Find("ForgeListRoot");
                if (listTr != null)
                {
                    ClearChildren(listTr);
                    _forgeListRoot = listTr;
                }
            }
        }
    }

    private void BindButtons()
    {
        BindBtn("Btn_Rest",    OnRestHeal);
        BindBtn("Btn_Study",   OnRestAlchemy);
        BindBtn("Btn_Forge",   OnRestForge);
        BindBtn("Btn_Purify",  OnRestPurify);
        BindBtn("Btn_Leave",   OnRestAmbush);

        // Btn_ForgeClose 是 ForgePanel/ForgeFrame 的子对象（inactive），GameObject.Find 找不到
        if (_forgePanel != null)
        {
            var forgeFrameTr = _forgePanel.transform.Find("ForgeFrame");
            var closeTr = forgeFrameTr != null ? forgeFrameTr.Find("Btn_ForgeClose") : _forgePanel.transform.Find("Btn_ForgeClose");
            if (closeTr != null)
            {
                var closeBtn = closeTr.GetComponent<Button>();
                if (closeBtn != null)
                {
                    closeBtn.onClick.RemoveAllListeners();
                    closeBtn.onClick.AddListener(CloseForgePanel);
                }
            }
        }
    }

    private void BindBtn(string name, UnityEngine.Events.UnityAction action)
    {
        var btn = FindButton(name);
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }
    }

    private void RefreshInfo()
    {
        if (_hpText != null)
            _hpText.text = $"❤ 守护点  {_currentHp} / {_maxHp}";

        if (_goldText != null)
            _goldText.text = $"💰 金币  {RogueRuntimeState.RunGold}";

        if (_drawText != null)
        {
            int atkPct = RogueRuntimeState.BattleTempAttackPercent;
            int spdPct = RogueRuntimeState.BattleTempAttackSpeedPercent;
            if (atkPct > 0 || spdPct > 0)
                _drawText.text = $"战斗增益  攻击+{atkPct}%  攻速+{spdPct}%";
            else
                _drawText.text = "战斗增益  无";
        }

        int curseCount = CurseManager.HasCurses ? CurseManager.ActiveCurses.Count : 0;
        if (_curseText != null)
        {
            _curseText.text = $"☠ 诅咒  {curseCount}";
            _curseText.gameObject.SetActive(curseCount > 0);
        }
    }

    // ════════════════════════════════════════
    //  选项行为
    // ════════════════════════════════════════

    private void OnRestHeal()
    {
        int healAmount = Mathf.Max(1, Mathf.RoundToInt(_maxHp * 0.3f));
        int newHp = Mathf.Min(_currentHp + healAmount, _maxHp);

        RogueRuntimeState.SetGuardianHp(newHp, _maxHp);
        _currentHp = newHp;

        if (_hpText != null)
            _hpText.text = $"❤ 守护点  {_currentHp} / {_maxHp}";
        ShowResult($"已恢复 {healAmount} 点生命！", new Color(0.5f, 0.95f, 0.5f));
        DisableAllOptionButtons();
        Invoke(nameof(ReturnToPlot), 1.2f);
    }

    private void OnRestAlchemy()
    {
        const int hpCost = 2;
        const int goldGain = 80;

        if (_currentHp <= hpCost)
        {
            ShowResult($"生命值不足！至少需要 {hpCost} 点生命才能炼金。", new Color(0.9f, 0.5f, 0.4f));
            return;
        }

        int newHp = _currentHp - hpCost;
        RogueRuntimeState.SetGuardianHp(newHp, _maxHp);
        RogueRuntimeState.AddRunGold(goldGain);
        _currentHp = newHp;

        if (_hpText != null)
            _hpText.text = $"❤ 守护点  {_currentHp} / {_maxHp}";
        if (_goldText != null)
            _goldText.text = $"💰 金币  {RogueRuntimeState.RunGold}";

        ShowResult($"炼金成功！消耗 {hpCost} 点生命，获得 {goldGain} 金币。", new Color(0.95f, 0.75f, 0.3f));
        DisableAllOptionButtons();
        Invoke(nameof(ReturnToPlot), 1.8f);
    }

    // ── 锻造：营地里永久强化一张已拥有的天赋卡 ──

    private void OnRestForge()
    {
        if (_forgePanel == null || _forgeListRoot == null) return;
        if (_forgePanel.activeSelf) return;
        SetOptionButtonsVisible(false);
        _forgePanel.SetActive(true);

        ClearChildren(_forgeListRoot);

        var owned = RogueRuntimeState.SelectedTalentCardIds;
        float startY = 0.74f;
        float rowH = 0.11f;
        for (int i = 0; i < owned.Count; i++)
        {
            string id = owned[i];
            var card = TalentEffectApplier.GetCardById(id);
            if (card == null) continue;
            float mult = RogueRuntimeState.GetCardMultiplier(id);
            string label = $"{card.displayName}    当前 ×{mult:0.##}";
            var btn = CreateForgeCardButton(_forgeListRoot, label, new Color(0.3f, 0.42f, 0.52f),
                startY - rowH * i, () => ForgeCard(id, card, mult));
        }
    }

    private Button CreateForgeCardButton(Transform parent, string label, Color color, float yAnchor, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("ForgeCardBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, yAnchor);
        rt.anchorMax = new Vector2(0.5f, yAnchor);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(520, 64);

        var txtGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(go.transform, false);
        var tmp = txtGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(onClick);
        var colors = btn.colors;
        colors.normalColor = color;
        colors.highlightedColor = color * 1.3f;
        colors.pressedColor = color * 0.8f;
        btn.colors = colors;

        return btn;
    }

    private void ForgeCard(string cardId, TalentCardData card, float currentMult)
    {
        float newMult = currentMult * 1.5f;
        RogueRuntimeState.ApplyCardMultiplier(cardId, newMult);

        if (_forgePanel != null) _forgePanel.SetActive(false);

        ShowResult($"「{card.displayName}」已强化！\n效果 ×{currentMult:0.##} → ×{newMult:0.##}",
            new Color(0.95f, 0.7f, 0.4f));
        DisableAllOptionButtons();
        Invoke(nameof(ReturnToPlot), 1.8f);
    }

    private void CloseForgePanel()
    {
        if (_forgePanel != null) _forgePanel.SetActive(false);
        SetOptionButtonsVisible(true);
        RefreshOptionLockStates();
    }

    private void SetOptionButtonsVisible(bool visible)
    {
        foreach (var b in _optionButtons) b.interactable = visible;
    }

    /// <summary> 重新应用条件禁用的选项（无卡禁锻造、无诅咒禁净化）。 </summary>
    private void RefreshOptionLockStates()
    {
        if (_forgeBtn != null)
        {
            bool hasCards = RogueRuntimeState.SelectedTalentCardIds.Count > 0;
            _forgeBtn.interactable = hasCards;
            var img = _forgeBtn.GetComponent<Image>();
            if (img != null)
                img.color = hasCards ? ForgeColor : ForgeLockColor;
        }

        if (_purifyBtn != null)
        {
            bool hasCurses = CurseManager.HasCurses;
            _purifyBtn.interactable = hasCurses;
            var img = _purifyBtn.GetComponent<Image>();
            if (img != null)
                img.color = hasCurses ? PurifyColor : PurifyLockColor;
        }
    }

    private void OnRestPurify()
    {
        string removed = CurseManager.RemoveRandomCurse();
        if (!string.IsNullOrEmpty(removed))
        {
            int curseCount = CurseManager.ActiveCurses.Count;
            if (_curseText != null)
            {
                _curseText.text = $"☠ 诅咒  {curseCount}";
                _curseText.gameObject.SetActive(curseCount > 0);
            }
            ShowResult($"诅咒解除: {removed}", new Color(0.8f, 0.6f, 0.95f));
        }
        else
        {
            ShowResult("没有可移除的诅咒", new Color(0.7f, 0.7f, 0.7f));
        }
        DisableAllOptionButtons();
        Invoke(nameof(ReturnToPlot), 1.8f);
    }

    private void OnRestAmbush()
    {
        // 先标记休息节点完成（玩家已选择进入战斗，无论输赢节点都消耗）
        int restLevel = RestReturnContext.GetAndClear();
        if (restLevel > 0)
        {
            LevelProgress.MarkCompleted("level " + restLevel);
        }
        else
        {
            // 异常：找不到休息节点编号，安全回退
            ShowResult("前方无人，悻悻而归…", new Color(0.7f, 0.7f, 0.7f));
            DisableAllOptionButtons();
            Invoke(nameof(ReturnToPlot), 1.5f);
            return;
        }

        // 设置打劫赏金：战斗胜利后额外获得 100 金币
        RogueRuntimeState.SetAmbushMode(100);

        // 优先使用新架构：LevelConfig + BattleScene（避免加载空壳关卡场景）
        LevelConfig ambushConfig = TryLoadLevelConfig(restLevel);
        if (ambushConfig == null || !IsPlayableLevelConfig(ambushConfig))
        {
            // 当前关卡配置不可用（空壳），回退到已知可用的 level 1 配置
            ambushConfig = TryLoadLevelConfig(1);
        }

        if (ambushConfig != null && IsPlayableLevelConfig(ambushConfig))
        {
            LevelSceneLoadContext.SetLevelConfig(ambushConfig, restLevel, "level " + restLevel);
            VideoSceneLoader.LoadScene(SceneNames.BattleScene);
        }
        else
        {
            // 无 LevelConfig，直接加载已知在 Build Settings 中的关卡场景
            VideoSceneLoader.LoadScene("level 1");
        }
    }

    // ════════════════════════════════════════
    //  关卡配置加载（新架构：LevelConfig + BattleScene）
    // ════════════════════════════════════════

    private static LevelConfig TryLoadLevelConfig(int levelNumber)
    {
        string[] possibleNames = {
            $"Level_{levelNumber:D2}_Battle",
            $"Level_{levelNumber}_Battle",
            $"LevelConfig_{levelNumber}",
        };
        foreach (var name in possibleNames)
        {
            var config = Resources.Load<LevelConfig>($"LevelConfigs/{name}");
            if (config != null) return config;
        }
        return null;
    }

    private static bool IsPlayableLevelConfig(LevelConfig config)
    {
        if (config == null) return false;
        if (config.gridData == null || config.gridData.Length < config.gridWidth * config.gridHeight) return false;
        if (config.waveGroups == null || config.waveGroups.Length == 0) return false;

        bool hasWave = false;
        foreach (var group in config.waveGroups)
        {
            if (group == null || group.entries == null) continue;
            foreach (var entry in group.entries)
            {
                if (entry != null && entry.count > 0) { hasWave = true; break; }
            }
            if (hasWave) break;
        }
        if (!hasWave) return false;

        var paths = config.GetAllPaths();
        if (paths == null) return false;
        bool hasPath = false;
        foreach (var path in paths)
        {
            if (path != null && path.Length > 0) { hasPath = true; break; }
        }
        return hasPath;
    }

    // ════════════════════════════════════════
    //  辅助方法
    // ════════════════════════════════════════

    private void ShowResult(string text, Color color)
    {
        if (_resultText != null)
        {
            _resultText.text = text;
            _resultText.color = color;
        }
    }

    private void DisableAllOptionButtons()
    {
        if (_canvas != null)
        {
            var buttons = _canvas.GetComponentsInChildren<Button>();
            foreach (var b in buttons) b.interactable = false;
        }
    }

    private void ReturnToPlot()
    {
        int restLevel = RestReturnContext.GetAndClear();
        if (restLevel > 0)
        {
            LevelProgress.MarkCompleted("level " + restLevel);
        }
        LevelSceneLoadContext.SetFromVictory();
        VideoSceneLoader.LoadScene(SceneNames.Plot);
    }

    // ── 场景查找工具 ──

    private static TMP_Text FindTmp(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<TMP_Text>() : null;
    }

    private static Button FindButton(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}
