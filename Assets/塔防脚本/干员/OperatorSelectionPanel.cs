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
    [SerializeField] private ScrollRect _headScroll;

    private List<OperatorData> _allOperators;
    private List<OperatorData> _selectedOperators = new List<OperatorData>();
    private Dictionary<string, int> _rosterStars = new Dictionary<string, int>(); // 选中干员的当前星级（局内养成）
    private OperatorData _previewing;
    private Coroutine _blockedHintCo; // 「开始本局被拦截」提示的恢复协程

    private static readonly Color[] StarColors =
    {
        new Color(0.4f, 0.4f, 0.4f),
        new Color(0.3f, 0.5f, 0.2f),
        new Color(0.2f, 0.4f, 0.8f),
        new Color(0.7f, 0.5f, 0.1f),
        new Color(0.9f, 0.75f, 0.15f),
    };

    /// <summary> 预览中（未选中）的头像边框色：柔和白色 </summary>
    private static readonly Color PreviewFrameColor = new Color(1f, 1f, 1f, 0.55f);
    /// <summary> 已选中（进入阵容）的头像边框色：金色 </summary>
    private static readonly Color SelectedFrameColor = new Color(1f, 0.84f, 0.25f, 1f);

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

        // 清除编辑器占位项（编辑态下 Destroy 报错，改用 DestroyImmediate 以免误删静态占位节点）
        bool playing = Application.isPlaying;
        for (int i = _headListContent.childCount - 1; i >= 0; i--)
        {
            var childGo = _headListContent.GetChild(i).gameObject;
            if (playing) Destroy(childGo);
            else DestroyImmediate(childGo);
        }

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

        // 选中/预览高亮框（比条目略大，形成描边效果，画在头像之下）
        var frameGo = new GameObject("Highlight");
        frameGo.transform.SetParent(itemGo.transform, false);
        var fRect = frameGo.AddComponent<RectTransform>();
        fRect.anchorMin = Vector2.zero;
        fRect.anchorMax = Vector2.one;
        fRect.offsetMin = new Vector2(-3, -3);
        fRect.offsetMax = new Vector2(3, 3);
        var frameImg = frameGo.AddComponent<Image>();
        frameImg.color = Color.clear;
        frameImg.raycastTarget = false;

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
        UpdateHeadListVisualState();
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
            if (used + Mathf.Clamp(_previewing.maxStarRating, 1, 5) > StarBudget)
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
            // 新加入的干员按其星级上限占用预算，加入后总上限之和不得超过预算
            if (GetUsedStars() + Mathf.Clamp(_previewing.maxStarRating, 1, 5) > StarBudget) return;
            _selectedOperators.Add(_previewing);
            if (!_rosterStars.ContainsKey(_previewing.RegistryKey))
                _rosterStars[_previewing.RegistryKey] = 1;
        }

        // 同步阵容到养成状态：升星接口要求干员已在 roster 内登记，
        // 否则选人阶段点「升星」会因「不在阵容」而静默失败。
        SyncRosterToRegistry();

        UpdateSelectButtonText();
        RefreshBudgetDisplay();
        UpdateHeadListVisualState();
    }

    /// <summary>
    /// 把当前勾选的干员登记进 OperatorStarRegistry（保留已预升的星级），
    /// 使选人阶段的升星按钮立即可用，且与开战后的养成状态是同一份数据。
    /// </summary>
    private void SyncRosterToRegistry()
    {
        var keys = _selectedOperators.Select(o => o.RegistryKey).ToList();
        // 选人阶段不允许预升星：阵容登记后全员强制 ★1（由 SetSelectedRoster 的 forceReset 保证）。
        OperatorStarRegistry.BeginRun(keys, _allOperators, forceReset: true);
    }

    private void RefreshBudgetDisplay()
    {
        int used = GetUsedStars();
        if (_budgetText != null)
        {
            int remain = StarBudget - used;
            _budgetText.text = $"强度上限预算: {used}/{StarBudget}（剩 {remain}）  已选: {_selectedOperators.Count}人";
            _budgetText.color = remain < 0 ? new Color(0.9f, 0.3f, 0.25f)
                : remain == 0 ? new Color(0.4f, 0.85f, 0.4f)
                : new Color(0.9f, 0.75f, 0.15f);
        }
        UpdateSelectButtonText();
    }

    /// <summary>
    /// 在预算文本处闪红显示无法开战的原因，约 2.5 秒后自动恢复为预算信息。
    /// 供「开始本局」按钮被阵容校验拦截时给出明确反馈。
    /// </summary>
    public void FlashStartBlockedHint(string reason)
    {
        if (_budgetText == null) return;
        _budgetText.text = reason;
        _budgetText.color = new Color(0.95f, 0.25f, 0.2f);
        if (_blockedHintCo != null) StopCoroutine(_blockedHintCo);
        _blockedHintCo = StartCoroutine(RecoverBudgetText());
    }

    private IEnumerator RecoverBudgetText()
    {
        yield return new WaitForSecondsRealtime(2.5f);
        _blockedHintCo = null;
        RefreshBudgetDisplay();
    }

    /// <summary>
    /// 刷新头像列表的完整视觉状态：选中条目显示金色边框+顺序角标，
    /// 正在预览的条目显示白色边框，其余恢复默认（无色框、无角标）。
    /// </summary>
    private void UpdateHeadListVisualState()
    {
        if (_headListContent == null) return;
        for (int i = 0; i < _headListContent.childCount; i++)
        {
            var child = _headListContent.GetChild(i);
            var checkmark = child.Find("Checkmark");
            var highlight = child.Find("Highlight");
            if (checkmark == null && highlight == null) continue; // Section 标签

            var opName = child.name.Replace("Head_", "");
            var op = _allOperators.Find(o => o.operatorName == opName);

            bool isSelected = op != null && _selectedOperators.Contains(op);
            bool isPreviewing = op != null && _previewing == op;

            if (highlight != null)
            {
                var hlImg = highlight.GetComponent<Image>();
                if (hlImg != null)
                    hlImg.color = isSelected ? SelectedFrameColor
                        : isPreviewing ? PreviewFrameColor
                        : Color.clear;
            }

            if (checkmark == null) continue;
            if (isSelected)
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
    /// 已占用星数预算 = 阵容中每个干员的【星级上限 maxStarRating】之和。
    /// 「7 星预算」的本意是：预算锁的是你带的干员本身的强度档位，
    /// 例如 1 个 ★5 + 1 个 ★2 = 7（占满），而非按养成进度累加。
    /// 选人阶段预升星只改变局内初始星级，不再额外占用预算。
    /// </summary>
    private int GetUsedStars()
    {
        int total = 0;
        foreach (var op in _selectedOperators)
            total += Mathf.Clamp(op.maxStarRating, 1, 5);
        return total;
    }

    public List<OperatorData> GetSelectedOperators() => _selectedOperators;
    public bool IsBudgetFull() => GetUsedStars() >= StarBudget;

    /// <summary>
    /// 返回无法开战的具体原因（中文提示），阵容合法返回 null。
    /// 供 RogueEntryController 在玩家点击「开始本局」被拦截时展示明确反馈。
    /// </summary>
    public string GetStartBlockedReason()
    {
        int used = GetUsedStars();
        int count = _selectedOperators.Count;
        if (count == 0)
            return "请先在右侧选中至少 1 名干员（建议选满 3 人）再开始本局";
        if (used < 1)
            return "阵容强度预算需至少占用 1 点才能开战";
        if (count < BalanceConfig.RosterMinCount)
            return $"阵容人数不足：至少需要 {BalanceConfig.RosterMinCount} 名干员（当前 {count} 人）";
        if (count > BalanceConfig.RosterMaxCount)
            return $"阵容人数过多：最多 {BalanceConfig.RosterMaxCount} 名干员（当前 {count} 人）";
        if (used > StarBudget)
            return $"强度预算超限：{used}/{StarBudget}（请移除高星干员）";
        return null;
    }

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
