using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// RogueEntry 场景干员选择面板。
/// UI 对象在场景中静态创建（编辑器可见），此脚本负责运行时数据绑定和交互。
/// </summary>
public class OperatorSelectionPanel : MonoBehaviour
{
    public const int StarBudget = BalanceConfig.StarBudget;

    [Header("场景引用（自动查找）")]
    [SerializeField] private Image _portraitImage;
    [SerializeField] private TMP_Text _quoteText;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _starText;
    [SerializeField] private TMP_Text _statsText;
    [SerializeField] private TMP_Text _budgetText;
    [SerializeField] private Transform _headListContent;
    [SerializeField] private Button _selectButton;
    [SerializeField] private TMP_Text _selectButtonText;
    [SerializeField] private Button _upgradeStarButton;     // 升星按钮（花局内 RunGold）
    [SerializeField] private TMP_Text _upgradeStarButtonText;
    [SerializeField] private ScrollRect _headScroll;

    private List<OperatorData> _allOperators;
    private List<OperatorData> _selectedOperators = new List<OperatorData>();
    private Dictionary<string, int> _rosterStars = new Dictionary<string, int>(); // 选中干员的当前星级（局内养成）
    private OperatorData _previewing;

    private static readonly Color[] StarColors =
    {
        new Color(0.4f, 0.4f, 0.4f),
        new Color(0.3f, 0.5f, 0.2f),
        new Color(0.2f, 0.4f, 0.8f),
        new Color(0.7f, 0.5f, 0.1f),
        new Color(0.9f, 0.75f, 0.15f),
    };

    private static readonly float[] BaseStatMultiplier = { 0f, 0.6f, 0.8f, 1.0f, 1.3f, 1.6f };
    private static readonly float[] StarGrowth = { 0f, 1.0f, 1.3f, 1.7f, 2.2f, 3.0f };
    private static Sprite _placeholderSprite;

    void Awake()
    {
        BindReferences();
        LoadAllOperators();
    }

    void Start()
    {
        PopulateHeadList();
        // 列表生成后强制滚动到顶部（已解锁区域可见）
        Canvas.ForceUpdateCanvases();
        if (_headScroll != null)
        {
            _headScroll.normalizedPosition = new Vector2(0, 1);
            // 延迟再设一次，确保 Layout 已计算完毕
            StartCoroutine(ResetScrollNextFrame());
        }
        if (_allOperators.Count > 0)
        {
            var firstUnlocked = _allOperators.FirstOrDefault(o => o.isInitialAvailable);
            PreviewOperator(firstUnlocked ?? _allOperators[0]);
        }
        RefreshBudgetDisplay();
    }

    private void BindReferences()
    {
        var t = transform;
        if (_budgetText == null)
            _budgetText = t.Find("BudgetBar/BudgetText")?.GetComponent<TMP_Text>();
        if (_portraitImage == null)
            _portraitImage = t.Find("PortraitArea/PortraitImage")?.GetComponent<Image>();
        if (_nameText == null)
            _nameText = t.Find("PortraitArea/NameText")?.GetComponent<TMP_Text>();
        if (_starText == null)
            _starText = t.Find("PortraitArea/StarInfo")?.GetComponent<TMP_Text>();
        if (_quoteText == null)
            _quoteText = t.Find("PortraitArea/QuoteText")?.GetComponent<TMP_Text>();
        if (_statsText == null)
            _statsText = t.Find("PortraitArea/StatsText")?.GetComponent<TMP_Text>();
        if (_selectButton == null)
            _selectButton = t.Find("PortraitArea/SelectButton")?.GetComponent<Button>();
        if (_selectButtonText == null)
            _selectButtonText = _selectButton?.transform.Find("BtnText")?.GetComponent<TMP_Text>();
        if (_headListContent == null)
            _headListContent = t.Find("HeadListArea/HeadScroll/Viewport/Content");
        if (_headScroll == null)
            _headScroll = t.Find("HeadListArea/HeadScroll")?.GetComponent<ScrollRect>();

        // 滚轮灵敏度 ×10
        if (_headScroll != null)
            _headScroll.scrollSensitivity = 100f;

        if (_selectButton != null)
            _selectButton.onClick.AddListener(OnSelectButtonClicked);
        if (_upgradeStarButton == null)
            _upgradeStarButton = t.Find("PortraitArea/UpgradeStarButton")?.GetComponent<Button>();
        if (_upgradeStarButton != null)
            _upgradeStarButton.onClick.AddListener(OnUpgradeStarClicked);
    }

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
        _allOperators = Resources.LoadAll<OperatorData>("").ToList();
        _allOperators = _allOperators.FindAll(o => !string.IsNullOrEmpty(o.operatorName));
#endif
        _allOperators.Sort((a, b) => b.maxStarRating.CompareTo(a.maxStarRating));
        Debug.Log($"[OperatorSelectionPanel] Loaded {_allOperators.Count} operators");
    }

    private void PopulateHeadList()
    {
        if (_headListContent == null) return;

        // 清除编辑器占位项
        for (int i = _headListContent.childCount - 1; i >= 0; i--)
            Destroy(_headListContent.GetChild(i).gameObject);

        var unlocked = _allOperators.Where(o => o.isInitialAvailable)
            .OrderByDescending(o => o.maxStarRating).ToList();
        var locked = _allOperators.Where(o => !o.isInitialAvailable)
            .OrderByDescending(o => o.maxStarRating).ToList();

        if (unlocked.Count > 0)
        {
            CreateSectionLabel("已解锁 (" + unlocked.Count + ")");
            foreach (var op in unlocked) CreateHeadButton(op);
        }
        if (locked.Count > 0)
        {
            CreateSectionLabel("未解锁 (" + locked.Count + ")");
            foreach (var op in locked) CreateHeadButton(op);
        }
    }

    private void CreateSectionLabel(string text)
    {
        var go = new GameObject("Section_" + text);
        go.transform.SetParent(_headListContent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 25);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 14;
        tmp.color = new Color(0.6f, 0.55f, 0.45f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = text;
        tmp.raycastTarget = false;
    }

    private void CreateHeadButton(OperatorData op)
    {
        var itemGo = new GameObject("Head_" + op.operatorName);
        itemGo.transform.SetParent(_headListContent, false);
        var rect = itemGo.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 70);

        var img = itemGo.AddComponent<Image>();
        bool unlocked = op.isInitialAvailable;
        img.color = unlocked ? StarColors[Mathf.Clamp(op.maxStarRating - 1, 0, 4)] : new Color(0.15f, 0.15f, 0.15f, 0.6f);

        var btn = itemGo.AddComponent<Button>();
        btn.onClick.AddListener(() => PreviewOperator(op));

        // Icon
        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(itemGo.transform, false);
        var iRect = iconGo.AddComponent<RectTransform>();
        iRect.anchorMin = new Vector2(0f, 0f);
        iRect.anchorMax = new Vector2(0f, 1f);
        iRect.sizeDelta = new Vector2(60, 0);
        iRect.anchoredPosition = new Vector2(35, 0);
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.sprite = op.icon != null ? op.icon : GetPlaceholderSprite();
        iconImg.color = unlocked ? Color.white : new Color(0.2f, 0.2f, 0.2f);
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // Info text
        var infoGo = new GameObject("Info");
        infoGo.transform.SetParent(itemGo.transform, false);
        var infoRect = infoGo.AddComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0.15f, 0f);
        infoRect.anchorMax = new Vector2(0.85f, 1f);
        infoRect.offsetMin = Vector2.zero;
        infoRect.offsetMax = Vector2.zero;
        var infoTmp = infoGo.AddComponent<TextMeshProUGUI>();
        infoTmp.fontSize = 16;
        infoTmp.color = unlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f);
        infoTmp.alignment = TextAlignmentOptions.MidlineLeft;
        string starStr = new string('★', op.maxStarRating);
        infoTmp.text = unlocked ? $"{op.operatorName} {starStr}" : "??? 未解锁";
        infoTmp.raycastTarget = false;

        // Checkmark
        var checkGo = new GameObject("Checkmark");
        checkGo.transform.SetParent(itemGo.transform, false);
        var cRect = checkGo.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(1f, 0.5f);
        cRect.anchorMax = new Vector2(1f, 0.5f);
        cRect.sizeDelta = new Vector2(30, 30);
        cRect.anchoredPosition = new Vector2(-20, 0);
        var checkTmp = checkGo.AddComponent<TextMeshProUGUI>();
        checkTmp.alignment = TextAlignmentOptions.Center;
        checkTmp.fontSize = 24;
        checkTmp.color = new Color(0.9f, 0.75f, 0.15f);
        checkTmp.text = "";
        checkTmp.raycastTarget = false;
        checkGo.SetActive(false);
    }

    private void PreviewOperator(OperatorData op)
    {
        _previewing = op;
        if (op == null) return;

        if (_portraitImage != null)
        {
            _portraitImage.sprite = op.icon != null ? op.icon : GetPlaceholderSprite();
            _portraitImage.color = op.isInitialAvailable ? Color.white : new Color(0.3f, 0.3f, 0.3f);
        }
        if (_quoteText != null)
            _quoteText.text = op.isInitialAvailable ? (string.IsNullOrEmpty(op.selectQuote) ? "" : $"\"{op.selectQuote}\"") : "\"尚未解锁\"";
        if (_nameText != null)
        {
            _nameText.text = op.isInitialAvailable ? op.operatorName : "???";
            _nameText.color = op.isInitialAvailable ? Color.white : new Color(0.4f, 0.4f, 0.4f);
        }
        if (_starText != null)
        {
            string maxStars = new string('★', op.maxStarRating);
            if (op.isInitialAvailable)
            {
                int cur = GetRosterStar(op);
                _starText.text = $"当前: ★{cur}  上限: {maxStars}";
                if (cur >= op.maxStarRating)
                    _starText.text += "  (满星·被动已激活)";
            }
            else
                _starText.text = "";
        }
        if (_statsText != null)
        {
            if (op.isInitialAvailable)
            {
                int cur = GetRosterStar(op);
                float baseMul = BaseStatMultiplier[Mathf.Clamp(op.maxStarRating, 1, BaseStatMultiplier.Length - 1)];
                float grow = StarGrowth[Mathf.Clamp(cur, 1, StarGrowth.Length - 1)];
                float mult = baseMul * grow;
                var sb = new System.Text.StringBuilder();
                sb.Append($"HP:{Mathf.RoundToInt(op.maxHealth * mult)}  ATK:{Mathf.RoundToInt(op.attackDamage * mult)}  费用:{op.cost}");
                if (cur >= op.maxStarRating && !string.IsNullOrEmpty(op.starPassiveDesc))
                    sb.Append($"\n[满星被动] {op.starPassiveDesc}");
                _statsText.text = sb.ToString();
            }
            else
                _statsText.text = "";
        }
        UpdateSelectButtonText();
        UpdateUpgradeStarButton();
    }

    /// <summary> 获取某干员当前预览星级（选中时为养成星级，未选中默认 1）。 </summary>
    private int GetRosterStar(OperatorData op)
    {
        if (op == null) return 1;
        if (_rosterStars.TryGetValue(op.RegistryKey, out int s)) return s;
        return 1;
    }

    private void UpdateSelectButtonText()
    {
        if (_selectButtonText == null || _previewing == null) return;

        if (!_previewing.isInitialAvailable)
        {
            _selectButtonText.text = "未解锁";
            _selectButtonText.color = new Color(0.4f, 0.4f, 0.4f);
            _selectButton.interactable = false;
            return;
        }

        _selectButton.interactable = true;
        bool isSelected = _selectedOperators.Contains(_previewing);
        if (isSelected)
        {
            _selectButtonText.text = "取消选中";
            _selectButtonText.color = new Color(0.9f, 0.4f, 0.3f);
        }
        else
        {
            int used = GetUsedStars();
            if (used + 1 > StarBudget)
            {
                _selectButtonText.text = "星数已满";
                _selectButtonText.color = new Color(0.5f, 0.5f, 0.3f);
                _selectButton.interactable = false;
            }
            else
            {
                _selectButtonText.text = "选中";
                _selectButtonText.color = new Color(0.9f, 0.85f, 0.7f);
            }
        }
    }

    private void OnSelectButtonClicked()
    {
        if (_previewing == null || !_previewing.isInitialAvailable) return;

        if (_selectedOperators.Contains(_previewing))
        {
            _selectedOperators.Remove(_previewing);
            _rosterStars.Remove(_previewing.RegistryKey);
        }
        else
        {
            // 新加入的干员默认占 1 星，加入后总星数不得超过预算
            if (GetUsedStars() + 1 > StarBudget) return;
            _selectedOperators.Add(_previewing);
            if (!_rosterStars.ContainsKey(_previewing.RegistryKey))
                _rosterStars[_previewing.RegistryKey] = 1;
        }

        // 同步阵容到养成状态：升星接口要求干员已在 roster 内登记，
        // 否则选人阶段点「升星」会因「不在阵容」而静默失败。
        SyncRosterToRegistry();

        UpdateSelectButtonText();
        RefreshBudgetDisplay();
        UpdateHeadListCheckmarks();
        UpdateUpgradeStarButton();
    }

    /// <summary>
    /// 把当前勾选的干员登记进 OperatorStarRegistry（保留已预升的星级），
    /// 使选人阶段的升星按钮立即可用，且与开战后的养成状态是同一份数据。
    /// </summary>
    private void SyncRosterToRegistry()
    {
        var keys = _selectedOperators.Select(o => o.RegistryKey).ToList();
        OperatorStarRegistry.BeginRun(keys, _allOperators);
        foreach (var op in _selectedOperators)
        {
            if (_rosterStars.TryGetValue(op.RegistryKey, out int s))
                OperatorStarRegistry.SetStar(op.RegistryKey, s);
        }
    }

    /// <summary>
    /// 升星按钮：花局内 RunGold 把当前预览干员升 1 星（仅当选中且未达上限）。
    /// 升星消耗写入 RogueRuntimeState.OperatorStarRegistry，局内实时生效。
    /// </summary>
    private void OnUpgradeStarClicked()
    {
        if (_previewing == null || !_previewing.isInitialAvailable) return;
        if (!_selectedOperators.Contains(_previewing)) return; // 只有选中（进阵容）的干员才能升星

        // 升星会占用星数预算：升后总星数 +1 不得超过预算（除非该干员已预升到更高星，预算本就够）
        int projectedUsed = GetUsedStars() - _rosterStars.GetValueOrDefault(_previewing.RegistryKey, 1) + (_rosterStars.GetValueOrDefault(_previewing.RegistryKey, 1) + 1);
        if (projectedUsed > StarBudget)
        {
            if (_upgradeStarButtonText != null)
                _upgradeStarButtonText.text = $"星数预算不足(剩{StarBudget - GetUsedStars()})";
            return;
        }

        int cost;
        bool ok = RogueRuntimeState.TryUpgradeOperatorStar(_previewing.RegistryKey, _allOperators, out cost);
        if (ok)
        {
            _rosterStars[_previewing.RegistryKey] = OperatorStarRegistry.GetStar(_previewing.RegistryKey);
            RefreshBudgetDisplay();
            PreviewOperator(_previewing);
        }
        else
        {
            // 金币不足或已满星：短暂提示
            if (_upgradeStarButtonText != null)
                StartCoroutine(FlashUpgradeButton(cost));
        }
    }

    private IEnumerator FlashUpgradeButton(int neededCost)
    {
        if (_upgradeStarButtonText == null) yield break;
        string old = _upgradeStarButtonText.text;
        _upgradeStarButtonText.text = neededCost >= int.MaxValue ? "已满星" : $"金币不足(需{neededCost})";
        yield return new WaitForSeconds(1f);
        UpdateUpgradeStarButton();
    }

    /// <summary> 刷新升星按钮文案与可交互状态。 </summary>
    private void UpdateUpgradeStarButton()
    {
        if (_upgradeStarButton == null) return;
        if (_upgradeStarButtonText == null)
            _upgradeStarButtonText = _upgradeStarButton.transform.Find("BtnText")?.GetComponent<TMP_Text>();

        bool selected = _previewing != null && _selectedOperators.Contains(_previewing);
        _upgradeStarButton.interactable = selected;

        if (_upgradeStarButtonText == null) return;
        if (!selected)
        {
            _upgradeStarButtonText.text = "先选中再升星";
            return;
        }
        int cost = RogueRuntimeState.PreviewStarUpgradeCost(_previewing.RegistryKey, _allOperators);
        if (cost >= int.MaxValue)
            _upgradeStarButtonText.text = "已满星";
        else
            _upgradeStarButtonText.text = $"升星 (需{cost}金)";
    }

    private void RefreshBudgetDisplay()
    {
        int used = GetUsedStars();
        if (_budgetText != null)
        {
            int remain = StarBudget - used;
            _budgetText.text = $"星数预算: {used}/{StarBudget}（剩 {remain}）  已选: {_selectedOperators.Count}人";
            _budgetText.color = remain < 0 ? new Color(0.9f, 0.3f, 0.25f)
                : remain == 0 ? new Color(0.4f, 0.85f, 0.4f)
                : new Color(0.9f, 0.75f, 0.15f);
        }
        UpdateSelectButtonText();
    }

    private void UpdateHeadListCheckmarks()
    {
        if (_headListContent == null) return;
        for (int i = 0; i < _headListContent.childCount; i++)
        {
            var child = _headListContent.GetChild(i);
            var checkmark = child.Find("Checkmark");
            if (checkmark == null) continue;

            var opName = child.name.Replace("Head_", "");
            var op = _allOperators.Find(o => o.operatorName == opName);
            if (op != null && _selectedOperators.Contains(op))
            {
                checkmark.gameObject.SetActive(true);
                var idx = _selectedOperators.IndexOf(op) + 1;
                var tmp = checkmark.GetComponent<TextMeshProUGUI>();
                if (tmp != null) tmp.text = idx.ToString();
            }
            else
                checkmark.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 已占用星数 = 阵容中每个干员的【当前星级】之和。
    /// 这才是真正的「星数预算」策略：带 7 个 ★1 = 7 点，带 1 个 ★5 + 2 个 ★1 = 7 点，
    /// 带 1 个 ★5 + 3 个 ★1 = 8 点则超预算。升星会直接占用预算。
    /// </summary>
    private int GetUsedStars()
    {
        int total = 0;
        foreach (var op in _selectedOperators)
        {
            int star = _rosterStars.TryGetValue(op.name, out int s) ? s : 1;
            total += star;
        }
        return total;
    }

    public List<OperatorData> GetSelectedOperators() => _selectedOperators;
    public bool IsBudgetFull() => GetUsedStars() >= StarBudget;

    /// <summary>
    /// 是否允许开战：星数预算不超（且至少占满 1 点）、人数在 [RosterMinCount, RosterMaxCount] 区间内。
    /// </summary>
    public bool CanStart()
    {
        int used = GetUsedStars();
        int count = _selectedOperators.Count;
        return used >= 1
            && used <= StarBudget
            && count >= BalanceConfig.RosterMinCount
            && count <= BalanceConfig.RosterMaxCount;
    }

    /// <summary>
    /// 确认阵容并开始本局：把选中的干员 + 各自养成星级写入 RogueRuntimeState，
    /// 供战斗部署时按星级应用属性与满星被动。由 RogueEntryController 在开战前调用。
    /// </summary>
    public void ConfirmRoster()
    {
        var keys = _selectedOperators.Select(o => o.RegistryKey).ToList();
        RogueRuntimeState.SetSelectedRoster(keys, _allOperators);
        // 把选人阶段预升的星级写入养成状态
        foreach (var op in _selectedOperators)
        {
            if (_rosterStars.TryGetValue(op.RegistryKey, out int s))
                OperatorStarRegistry.SetStar(op.RegistryKey, s);
        }
    }

    private IEnumerator ResetScrollNextFrame()
    {
        yield return null;
        if (_headScroll != null)
            _headScroll.normalizedPosition = new Vector2(0, 1);
    }

    private static Sprite GetPlaceholderSprite()
    {
        if (_placeholderSprite != null) return _placeholderSprite;
        var tex = new Texture2D(64, 64);
        var pixels = new Color[64 * 64];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0.25f, 0.22f, 0.3f, 1f);
        tex.SetPixels(pixels);
        tex.Apply();
        _placeholderSprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        return _placeholderSprite;
    }
}
