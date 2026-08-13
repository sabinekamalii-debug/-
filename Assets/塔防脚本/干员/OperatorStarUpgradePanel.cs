using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 局内「干员升星」面板：战斗场景 / 商店场景中随时打开，花局内金币（RunGold）
/// 把本局阵容里的干员从 ★1 一路养到 ★maxStar，满星解锁职业专属被动。
///
/// 设计要点：
/// - UI 全部由代码动态创建，无需在任何场景里手工摆控件，挂上脚本即可用；
/// - 只显示「本局阵容内」的干员（OperatorStarRegistry 登记过的），
///   避免玩家给没带上场的干员花钱；
/// - 升星成功后立刻刷新场上同名干员的属性（RefreshStarState），
///   让玩家马上看到血条/攻击变强，而不是等下一场；
/// - 面板打开时暂停游戏（可关），关闭时恢复原时间缩放。
/// </summary>
public class OperatorStarUpgradePanel : MonoBehaviour
{
    [Header("行为设置")]
    [Tooltip("打开面板时是否暂停战斗（商店场景可关掉）")]
    public bool pauseWhileOpen = true;

    [Tooltip("是否自动在屏幕左上角生成一个「升星」开关按钮")]
    public bool createToggleButton = true;

    [Tooltip("开关按钮相对左上角的偏移")]
    public Vector2 toggleButtonOffset = new Vector2(120f, -20f);

    private static readonly Color[] StarColors =
    {
        new Color(0.40f, 0.40f, 0.40f),
        new Color(0.30f, 0.50f, 0.20f),
        new Color(0.20f, 0.40f, 0.80f),
        new Color(0.70f, 0.50f, 0.10f),
        new Color(0.90f, 0.75f, 0.15f),
    };

    private Canvas _canvas;
    private GameObject _panelRoot;
    private Transform _listContent;
    private TMP_Text _goldText;
    private TMP_Text _hintText;
    private Button _toggleButton;
    private TMP_Text _toggleButtonText;

    private List<OperatorData> _allOperators = new List<OperatorData>();
    private readonly List<RowRefs> _rows = new List<RowRefs>();
    private float _prevTimeScale = 1f;
    private bool _isOpen;

    /// <summary> 一行 UI 的引用集合，刷新时按行更新，避免整表重建。 </summary>
    private class RowRefs
    {
        public string key;
        public OperatorData data;
        public Image background;
        public TMP_Text nameText;
        public TMP_Text starText;
        public TMP_Text statText;
        public TMP_Text passiveText;
        public Button upgradeButton;
        public TMP_Text upgradeButtonText;
    }

    void Awake()
    {
        LoadAllOperators();
        EnsureCanvas();
        BuildPanel();
        if (createToggleButton) BuildToggleButton();
        SetOpen(false);
    }

    void OnEnable()
    {
        OperatorStarRegistry.OnStarChanged += OnStarChanged;
    }

    void OnDisable()
    {
        OperatorStarRegistry.OnStarChanged -= OnStarChanged;
        // 面板随场景卸载时，确保不把 timeScale 卡在 0
        if (_isOpen && pauseWhileOpen) Time.timeScale = _prevTimeScale;
    }

    private void OnStarChanged(string key)
    {
        if (_isOpen) RefreshAll();
        RefreshToggleButtonText();
    }

    // ─────────────────────────────────────────────
    //  数据
    // ─────────────────────────────────────────────

    private void LoadAllOperators()
    {
        _allOperators = new List<OperatorData>();
#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:OperatorData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<OperatorData>(path);
            if (data != null && !string.IsNullOrEmpty(data.operatorName))
                _allOperators.Add(data);
        }
#else
        _allOperators = Resources.LoadAll<OperatorData>("")
            .Where(o => o != null && !string.IsNullOrEmpty(o.operatorName)).ToList();
#endif
    }

    /// <summary> 本局阵容中的干员数据（按 OperatorStarRegistry 登记顺序）。 </summary>
    private List<OperatorData> GetRosterOperators()
    {
        var result = new List<OperatorData>();
        foreach (var key in OperatorStarRegistry.GetRosterKeys())
        {
            var data = _allOperators.FirstOrDefault(o => o.RegistryKey == key);
            if (data != null) result.Add(data);
        }
        return result;
    }

    // ─────────────────────────────────────────────
    //  交互
    // ─────────────────────────────────────────────

    public void Toggle() => SetOpen(!_isOpen);

    public void SetOpen(bool open)
    {
        _isOpen = open;
        if (_panelRoot != null) _panelRoot.SetActive(open);

        if (pauseWhileOpen)
        {
            if (open)
            {
                _prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = _prevTimeScale;
            }
        }

        if (open) RebuildList();
        RefreshToggleButtonText();
    }

    private void OnUpgradeClicked(RowRefs row)
    {
        if (row == null || row.data == null) return;
        // 升星改为纯「获得」驱动：战斗胜利重复获得同名干员才会自动升星，
        // 面板不再支持主动金币升星。点击仅给出说明。
        int star = OperatorStarRegistry.GetStar(row.key);
        int max = OperatorStarRegistry.GetMaxStarCached(row.key);
        if (star >= max)
            ShowHint($"{row.data.operatorName} 已满星（★{star}）");
        else
            ShowHint($"{row.data.operatorName} 当前 ★{star}：升星需战斗胜利重复获得该干员");
        RefreshAll();
    }

    /// <summary> 让场上已部署的同名干员即时应用新星级属性与被动。 </summary>
    private void RefreshDeployedUnits(string key)
    {
        var units = Object.FindObjectsByType<OperatorUnit>(FindObjectsSortMode.None);
        foreach (var u in units)
        {
            if (u == null || u.data == null) continue;
            if (u.data.RegistryKey != key) continue;
            u.RefreshStarState();
        }
    }

    private void ShowHint(string msg)
    {
        if (_hintText != null) _hintText.text = msg;
    }

    // ─────────────────────────────────────────────
    //  UI 刷新
    // ─────────────────────────────────────────────

    private void RefreshAll()
    {
        if (_goldText != null)
            _goldText.text = $"局内金币：{RogueRuntimeState.RunGold}";

        foreach (var row in _rows) RefreshRow(row);
    }

    private void RefreshRow(RowRefs row)
    {
        if (row == null || row.data == null) return;

        int star = OperatorStarRegistry.GetStar(row.key);
        int maxStar = OperatorStarRegistry.GetMaxStar(row.key, _allOperators);
        bool isMax = star >= maxStar;

        if (row.background != null)
            row.background.color = StarColors[Mathf.Clamp(star - 1, 0, StarColors.Length - 1)] * new Color(1f, 1f, 1f, 0.55f);

        if (row.nameText != null)
            row.nameText.text = row.data.operatorName;

        if (row.starText != null)
            row.starText.text = new string('★', star) + new string('☆', Mathf.Max(0, maxStar - star));

        if (row.statText != null)
        {
            float mul = StatMultiplier(maxStar, star);
            int hp  = Mathf.RoundToInt(row.data.maxHealth * mul);
            int atk = Mathf.RoundToInt(row.data.attackDamage * mul);
            int def = Mathf.RoundToInt(row.data.defense * mul);

            if (!isMax)
            {
                float nextMul = StatMultiplier(maxStar, star + 1);
                int nHp  = Mathf.RoundToInt(row.data.maxHealth * nextMul);
                int nAtk = Mathf.RoundToInt(row.data.attackDamage * nextMul);
                int nDef = Mathf.RoundToInt(row.data.defense * nextMul);
                row.statText.text = $"HP {hp}→{nHp}   ATK {atk}→{nAtk}   DEF {def}→{nDef}";
            }
            else
            {
                row.statText.text = $"HP {hp}   ATK {atk}   DEF {def}";
            }
        }

        if (row.passiveText != null)
        {
            if (isMax)
            {
                row.passiveText.color = new Color(1f, 0.85f, 0.3f);
                row.passiveText.text = "满星被动已激活：" + GetPassiveDesc(row.data);
            }
            else
            {
                row.passiveText.color = new Color(0.65f, 0.65f, 0.65f);
                row.passiveText.text = $"★{maxStar} 解锁：" + GetPassiveDesc(row.data);
            }
        }

        if (row.upgradeButton != null)
        {
            // 升星改为纯「获得」驱动，面板不再提供主动升星按钮
            row.upgradeButton.interactable = false;
            var img = row.upgradeButton.GetComponent<Image>();
            if (img != null)
            {
                img.color = isMax ? new Color(0.35f, 0.30f, 0.10f)
                                  : new Color(0.30f, 0.30f, 0.30f);
            }
        }
        if (row.upgradeButtonText != null)
        {
            row.upgradeButtonText.text = isMax ? "已满星" : "升星靠获得";
        }
    }

    private static float StatMultiplier(int maxStar, int star)
    {
        int idxMax  = Mathf.Clamp(maxStar, 1, BalanceConfig.BaseStatMultiplier.Length - 1);
        int idxStar = Mathf.Clamp(star, 1, BalanceConfig.StarGrowth.Length - 1);
        return BalanceConfig.BaseStatMultiplier[idxMax] * BalanceConfig.StarGrowth[idxStar];
    }

    /// <summary> 职业满星被动文案（与 OperatorUnit.ApplyStarPassive 的 7 个被动一一对应）。 </summary>
    private static string GetPassiveDesc(OperatorData data)
    {
        if (data == null) return "";
        if (!string.IsNullOrEmpty(data.starPassiveDesc)) return data.starPassiveDesc;

        switch (data.opType)
        {
            case OperatorData.OperatorType.Vanguard:   return "部署即返还部署费用，技力回复 +30%";
            case OperatorData.OperatorType.Guard:      return "专注强化，攻击力 +30%";
            case OperatorData.OperatorType.Defender:   return "防御 +40%，且致命伤时保留 1 点生命（每场 1 次）";
            case OperatorData.OperatorType.Sniper:     return "暴击率 +25%";
            case OperatorData.OperatorType.Caster:     return "法术穿透：无视 50% 防御，攻击力 +20%";
            case OperatorData.OperatorType.Medic:      return "治疗量 +30%";
            case OperatorData.OperatorType.Specialist: return "再部署更划算（撤退返还 +50%）";
            default:                                   return "职业专属被动";
        }
    }

    // ─────────────────────────────────────────────
    //  UI 构建
    // ─────────────────────────────────────────────

    private void EnsureCanvas()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null) return;

        // 复用场景里已有的 Overlay Canvas，避免多层 Canvas 叠加
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) { _canvas = c; break; }
        }
        if (_canvas != null) return;

        var go = new GameObject("StarUpgradeCanvas");
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 500;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        go.AddComponent<GraphicRaycaster>();
    }

    private void BuildToggleButton()
    {
        var go = new GameObject("StarUpgradeToggleButton");
        go.transform.SetParent(_canvas.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(160f, 56f);
        rect.anchoredPosition = toggleButtonOffset;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.55f, 0.40f, 0.10f, 0.95f);

        _toggleButton = go.AddComponent<Button>();
        _toggleButton.onClick.AddListener(Toggle);

        _toggleButtonText = CreateText(go.transform, "Text", 22, TextAlignmentOptions.Center);
        StretchFull(_toggleButtonText.rectTransform);
        RefreshToggleButtonText();
    }

    private void RefreshToggleButtonText()
    {
        if (_toggleButtonText == null) return;
        _toggleButtonText.text = _isOpen ? "关闭升星" : "干员升星";
    }

    private void BuildPanel()
    {
        _panelRoot = new GameObject("StarUpgradePanel");
        _panelRoot.transform.SetParent(_canvas.transform, false);
        var rootRect = _panelRoot.AddComponent<RectTransform>();
        StretchFull(rootRect);

        // 半透明遮罩，吃掉点击避免误触战场
        var dim = _panelRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.75f);

        // 主窗口
        var win = new GameObject("Window");
        win.transform.SetParent(_panelRoot.transform, false);
        var winRect = win.AddComponent<RectTransform>();
        winRect.anchorMin = new Vector2(0.5f, 0.5f);
        winRect.anchorMax = new Vector2(0.5f, 0.5f);
        winRect.pivot = new Vector2(0.5f, 0.5f);
        winRect.sizeDelta = new Vector2(1100f, 760f);
        var winImg = win.AddComponent<Image>();
        winImg.color = new Color(0.11f, 0.11f, 0.13f, 0.98f);

        // 标题
        var title = CreateText(win.transform, "Title", 34, TextAlignmentOptions.Center);
        title.text = "干员升星养成";
        title.color = new Color(1f, 0.87f, 0.45f);
        var tRect = title.rectTransform;
        tRect.anchorMin = new Vector2(0f, 1f);
        tRect.anchorMax = new Vector2(1f, 1f);
        tRect.pivot = new Vector2(0.5f, 1f);
        tRect.sizeDelta = new Vector2(0f, 56f);
        tRect.anchoredPosition = new Vector2(0f, -12f);

        // 金币显示
        _goldText = CreateText(win.transform, "GoldText", 24, TextAlignmentOptions.MidlineLeft);
        _goldText.color = new Color(1f, 0.9f, 0.4f);
        var gRect = _goldText.rectTransform;
        gRect.anchorMin = new Vector2(0f, 1f);
        gRect.anchorMax = new Vector2(0.6f, 1f);
        gRect.pivot = new Vector2(0f, 1f);
        gRect.sizeDelta = new Vector2(0f, 34f);
        gRect.anchoredPosition = new Vector2(28f, -70f);

        // 提示行
        _hintText = CreateText(win.transform, "HintText", 20, TextAlignmentOptions.MidlineLeft);
        _hintText.color = new Color(0.7f, 0.85f, 0.7f);
        var hRect = _hintText.rectTransform;
        hRect.anchorMin = new Vector2(0f, 0f);
        hRect.anchorMax = new Vector2(1f, 0f);
        hRect.pivot = new Vector2(0f, 0f);
        hRect.sizeDelta = new Vector2(0f, 34f);
        hRect.anchoredPosition = new Vector2(28f, 16f);

        // 关闭按钮
        var closeGo = new GameObject("CloseButton");
        closeGo.transform.SetParent(win.transform, false);
        var cRect = closeGo.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(1f, 1f);
        cRect.anchorMax = new Vector2(1f, 1f);
        cRect.pivot = new Vector2(1f, 1f);
        cRect.sizeDelta = new Vector2(110f, 46f);
        cRect.anchoredPosition = new Vector2(-18f, -14f);
        var cImg = closeGo.AddComponent<Image>();
        cImg.color = new Color(0.45f, 0.18f, 0.18f);
        var cBtn = closeGo.AddComponent<Button>();
        cBtn.onClick.AddListener(() => SetOpen(false));
        var cTxt = CreateText(closeGo.transform, "Text", 22, TextAlignmentOptions.Center);
        cTxt.text = "关闭";
        StretchFull(cTxt.rectTransform);

        // 滚动列表
        var scrollGo = new GameObject("Scroll");
        scrollGo.transform.SetParent(win.transform, false);
        var sRect = scrollGo.AddComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0f, 0f);
        sRect.anchorMax = new Vector2(1f, 1f);
        sRect.offsetMin = new Vector2(24f, 58f);
        sRect.offsetMax = new Vector2(-24f, -110f);
        var sImg = scrollGo.AddComponent<Image>();
        sImg.color = new Color(0.06f, 0.06f, 0.08f, 0.9f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.scrollSensitivity = 40f;
        scrollGo.AddComponent<RectMask2D>();

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var vRect = viewport.AddComponent<RectTransform>();
        StretchFull(vRect);
        scroll.viewport = vRect;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRect;
        _listContent = content.transform;
    }

    private void RebuildList()
    {
        if (_listContent == null) return;

        for (int i = _listContent.childCount - 1; i >= 0; i--)
            Destroy(_listContent.GetChild(i).gameObject);
        _rows.Clear();

        var roster = GetRosterOperators();
        if (roster.Count == 0)
        {
            var empty = CreateText(_listContent, "Empty", 22, TextAlignmentOptions.Center);
            empty.color = new Color(0.7f, 0.7f, 0.7f);
            empty.text = OperatorStarRegistry.IsRunActive
                ? "本局阵容为空，无法升星。"
                : "尚未开始一局游戏（请先在入口选择干员阵容）。";
            var le = empty.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 60f;
        }
        else
        {
            foreach (var op in roster) _rows.Add(CreateRow(op));
        }

        RefreshAll();
    }

    private RowRefs CreateRow(OperatorData op)
    {
        var row = new RowRefs { key = op.RegistryKey, data = op };

        var go = new GameObject("Row_" + op.operatorName);
        go.transform.SetParent(_listContent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 112f);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 112f;
        le.preferredHeight = 112f;
        row.background = go.AddComponent<Image>();

        // 名称
        row.nameText = CreateText(go.transform, "Name", 24, TextAlignmentOptions.MidlineLeft);
        var nRect = row.nameText.rectTransform;
        nRect.anchorMin = new Vector2(0f, 0.5f);
        nRect.anchorMax = new Vector2(0.28f, 1f);
        nRect.offsetMin = new Vector2(16f, 0f);
        nRect.offsetMax = new Vector2(0f, -8f);

        // 星级
        row.starText = CreateText(go.transform, "Star", 26, TextAlignmentOptions.MidlineLeft);
        row.starText.color = new Color(1f, 0.85f, 0.3f);
        var stRect = row.starText.rectTransform;
        stRect.anchorMin = new Vector2(0.28f, 0.5f);
        stRect.anchorMax = new Vector2(0.6f, 1f);
        stRect.offsetMin = Vector2.zero;
        stRect.offsetMax = new Vector2(0f, -8f);

        // 属性对比
        row.statText = CreateText(go.transform, "Stat", 19, TextAlignmentOptions.MidlineLeft);
        row.statText.color = new Color(0.85f, 0.92f, 1f);
        var sRect = row.statText.rectTransform;
        sRect.anchorMin = new Vector2(0f, 0.22f);
        sRect.anchorMax = new Vector2(0.78f, 0.55f);
        sRect.offsetMin = new Vector2(16f, 0f);
        sRect.offsetMax = Vector2.zero;

        // 被动说明
        row.passiveText = CreateText(go.transform, "Passive", 17, TextAlignmentOptions.MidlineLeft);
        var pRect = row.passiveText.rectTransform;
        pRect.anchorMin = new Vector2(0f, 0f);
        pRect.anchorMax = new Vector2(0.78f, 0.24f);
        pRect.offsetMin = new Vector2(16f, 4f);
        pRect.offsetMax = Vector2.zero;

        // 升星按钮
        var btnGo = new GameObject("UpgradeButton");
        btnGo.transform.SetParent(go.transform, false);
        var bRect = btnGo.AddComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.8f, 0.14f);
        bRect.anchorMax = new Vector2(0.985f, 0.86f);
        bRect.offsetMin = Vector2.zero;
        bRect.offsetMax = Vector2.zero;
        btnGo.AddComponent<Image>().color = new Color(0.2f, 0.45f, 0.2f);
        row.upgradeButton = btnGo.AddComponent<Button>();
        row.upgradeButton.onClick.AddListener(() => OnUpgradeClicked(row));
        row.upgradeButtonText = CreateText(btnGo.transform, "Text", 20, TextAlignmentOptions.Center);
        StretchFull(row.upgradeButtonText.rectTransform);

        return row;
    }

    // ── 小工具 ──

    private static TMP_Text CreateText(Transform parent, string name, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
