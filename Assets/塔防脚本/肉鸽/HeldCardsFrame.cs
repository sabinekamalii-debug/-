using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 持卡栏：在 plot（剧情/过关）场景中，于左侧显示一个卡片框，
/// 列出玩家本局当前持有的所有卡片（来自 RogueRuntimeState.SelectedTalentCardIds），
/// 方便玩家过关时回看自己已获得了哪些卡。
///
/// UI 结构在场景/Prefab 中预先搭建，本脚本只负责运行时填充动态内容（卡片行、悬停预览）。
/// 需要将以下字段在 Inspector 中拖入引用：
/// - titleText: 标题文字
/// - cardContent: 卡片列表容器（ScrollRect 的 Content）
/// </summary>
public class HeldCardsFrame : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("标题文字（显示 持有卡片 (数量)）")]
    public TMP_Text titleText;

    [Tooltip("卡片列表容器（ScrollRect 的 Content，子物体将作为卡片行挂载点）")]
    public Transform cardContent;

    [Tooltip("详情弹窗的父 Canvas（用于层级最上层显示）")]
    public Canvas parentCanvas;

    [Header("卡片行样式")]
    public Vector2 cardRowSize = new Vector2(280, 280);
    public float cardIconSize = 260f;

    [Header("剧情卡片面板切换")]
    [Tooltip("切换到剧情卡片面板的按钮")]
    public Button showStoryCardButton;
    [Tooltip("剧情卡片面板根节点")]
    public GameObject storyCardPanelRoot;

    // ── 运行时状态 ──
    private List<TalentCardData> _allCards = new List<TalentCardData>();
    private GameObject _tooltipGo;
    private ScrollRect _scrollRect;
    private RectTransform _viewportRect;
    private bool _isDragging;
    private Vector2 _lastDragPos;

    // 测试模式：设置为 true 时显示示例卡片
    public static bool TestMode = false;

    /// <summary> Inspector 右键菜单：填充测试卡片 </summary>
    [ContextMenu("填充测试卡片")]
    public void FillTestCards()
    {
        TestMode = true;
        Refresh();
    }

    /// <summary> Inspector 右键菜单：清空测试卡片 </summary>
    [ContextMenu("清空测试卡片")]
    public void ClearTestCards()
    {
        TestMode = false;
        Refresh();
    }

    private void Awake()
    {
        if (titleText == null)
            titleText = FindChildRecursive(transform, "TitleText")?.GetComponent<TMP_Text>();
        if (cardContent == null)
            cardContent = FindChildRecursive(transform, "Content");
        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>();
        _scrollRect = GetComponent<ScrollRect>();
        if (_scrollRect != null)
        {
            _scrollRect.scrollSensitivity = 0; // 禁用滚轮（用自定义拖拽替代）
            if (_scrollRect.viewport != null)
                _viewportRect = _scrollRect.viewport;
        }
    }

    private void Start()
    {
        BindStoryCardSwitch();
        Refresh();
    }

    private void OnEnable()
    {
        // 场景加载后延迟刷新一次，确保 Naninovel 等叠加场景已完成加载
        Invoke(nameof(Refresh), 0.1f);
    }

    private void BindStoryCardSwitch()
    {
        if (showStoryCardButton == null)
        {
            var t = FindChildRecursive(transform, "BtnStoryCard");
            if (t != null) showStoryCardButton = t.GetComponent<Button>();
        }
        if (showStoryCardButton != null)
            showStoryCardButton.onClick.AddListener(ShowStoryCardPanel);
    }

    public void ShowStoryCardPanel()
    {
        if (storyCardPanelRoot == null) return;
        gameObject.SetActive(false);
        storyCardPanelRoot.SetActive(true);
        var panel = storyCardPanelRoot.GetComponent<StoryCardPanel>();
        if (panel != null) panel.Refresh();
    }

    // ─────────────────────────────────────────────
    //  按住拖拽滑动（绕过 InputSystemUIInputModule 滚轮失效问题）
    // ─────────────────────────────────────────────

    private void Update()
    {
        if (_scrollRect == null || _scrollRect.content == null || !gameObject.activeInHierarchy) return;
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.isPressed)
        {
            Vector2 curPos = mouse.position.ReadValue();

            if (!_isDragging)
            {
                // 仅在开始拖拽时检查鼠标是否在 Viewport 范围内
                if (IsPointerOverViewport())
                {
                    _isDragging = true;
                    _lastDragPos = curPos;
                    _scrollRect.velocity = Vector2.zero;
                }
            }
            else
            {
                // 拖拽中不再检查位置，只要鼠标按住就持续滑动
                Vector2 delta = curPos - _lastDragPos;
                _lastDragPos = curPos;
                var content = _scrollRect.content;
                float maxScroll = content.rect.height - _viewportRect.rect.height;
                if (maxScroll > 0)
                {
                    float newPos = content.anchoredPosition.y + delta.y;
                    newPos = Mathf.Clamp(newPos, 0, maxScroll);
                    content.anchoredPosition = new Vector2(content.anchoredPosition.x, newPos);
                }
            }
        }
        else
        {
            _isDragging = false;
        }
    }

    private bool IsPointerOverViewport()
    {
        if (_viewportRect == null || parentCanvas == null) return false;
        var cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
        return RectTransformUtility.RectangleContainsScreenPoint(_viewportRect, mousePos, cam);
    }

    // ─────────────────────────────────────────────
    //  数据刷新
    // ─────────────────────────────────────────────

    /// <summary> 重新收集卡片数据并渲染列表。 </summary>
    public void Refresh()
    {
        _allCards.Clear();

        var ids = RogueRuntimeState.SelectedTalentCardIds;
        if (ids != null)
        {
            foreach (var id in ids)
            {
                var card = TalentEffectApplier.GetCardById(id);
                if (card != null) _allCards.Add(card);
            }
        }

        // 测试模式：显示一些测试卡片
        if (TestMode)
        {
            var testIds = new[] { "atk_power1", "atk_speed1", "def_hp1", "grd_hp1", "spc_gold1", "skl_power1" };
            foreach (var id in testIds)
            {
                var card = TalentEffectApplier.GetCardById(id);
                if (card != null && !_allCards.Contains(card))
                    _allCards.Add(card);
            }
        }

        RenderCards();
    }

    // ─────────────────────────────────────────────
    //  渲染卡片列表
    // ─────────────────────────────────────────────

    private void RenderCards()
    {
        if (cardContent == null) return;

        // URP 下 UnityEngine.UI.Mask (stencil) 可能不裁剪，用 RectMask2D 替代
        if (_scrollRect != null && _scrollRect.viewport != null)
        {
            var vp = _scrollRect.viewport;
            if (vp.GetComponent<Mask>() != null && vp.GetComponent<RectMask2D>() == null)
            {
                Destroy(vp.GetComponent<Mask>());
                vp.gameObject.AddComponent<RectMask2D>();
            }
        }

        HideTooltip();

        // 清空旧卡片
        for (int i = cardContent.childCount - 1; i >= 0; i--)
            Destroy(cardContent.GetChild(i).gameObject);

        // 移除旧的 VerticalLayoutGroup（如果存在）
        var vlg = cardContent.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) Destroy(vlg);

        // 更新标题
        if (titleText != null)
            titleText.text = "持有卡片 (" + _allCards.Count + ")";

        if (_allCards.Count == 0)
        {
            AddEmpty(cardContent);
            return;
        }

        // 确保 Content 有 GridLayoutGroup（双列网格）
        var grid = cardContent.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = cardContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.spacing = new Vector2(8, 8);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.cellSize = new Vector2(280, 280);
            grid.childAlignment = TextAnchor.UpperCenter;
        }

        // 确保 Content 有 ContentSizeFitter（垂直自适应，否则 ScrollRect 无法滚动）
        var csf = cardContent.GetComponent<ContentSizeFitter>();
        if (csf == null)
            csf = cardContent.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (var card in _allCards)
            CreateCardRow(cardContent, card);
    }

    // ─────────────────────────────────────────────
    //  空状态
    // ─────────────────────────────────────────────

    private void AddEmpty(Transform parent)
    {
        var t = new GameObject("Empty");
        t.transform.SetParent(parent, false);
        var rt = t.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 40);
        var txt = t.AddComponent<TextMeshProUGUI>();
        txt.text = "（暂无持有卡片）";
        txt.fontSize = 20;
        txt.color = new Color(1, 1, 1, 0.5f);
        txt.alignment = TextAlignmentOptions.Center;
    }

    // ─────────────────────────────────────────────
    //  卡片行（运行时动态创建）
    // ─────────────────────────────────────────────

    private void CreateCardRow(Transform parent, TalentCardData card)
    {
        var row = new GameObject("Card_" + card.cardId);
        row.transform.SetParent(parent, false);

        var rowImg = row.AddComponent<Image>();
        rowImg.color = RarityBg(card.rarity);

        var rowBtn = row.AddComponent<Button>();
        var cb = rowBtn.colors;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
        cb.pressedColor = new Color(0f, 0f, 0f, 0.15f);
        rowBtn.colors = cb;
        rowBtn.targetGraphic = rowImg;

        // ── 图标（填满卡片宽度，16:9 比例） ──
        var icon = new GameObject("Icon");
        icon.transform.SetParent(row.transform, false);
        var irt = icon.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(0, 1);
        irt.anchorMax = new Vector2(1, 1);
        irt.pivot = new Vector2(0.5f, 1);
        irt.anchoredPosition = new Vector2(0, -8);
        irt.sizeDelta = new Vector2(-16, cardIconSize * 9f / 16f);
        var iimg = icon.AddComponent<Image>();
        if (card.icon != null)
        {
            iimg.sprite = card.icon;
            iimg.color = Color.white;
        }
        else
        {
            iimg.color = RarityAccent(card.rarity);
        }
        iimg.preserveAspect = true;
        iimg.raycastTarget = false;

        // ── 名称（图标下方，居中） ──
        var name = new GameObject("Name");
        name.transform.SetParent(row.transform, false);
        var nrt = name.AddComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0, 0);
        nrt.anchorMax = new Vector2(1, 0);
        nrt.pivot = new Vector2(0.5f, 0);
        nrt.anchoredPosition = new Vector2(0, 16);
        nrt.sizeDelta = new Vector2(-16, 56);
        var nt = name.AddComponent<TextMeshProUGUI>();
        nt.text = card.displayName ?? "???";
        nt.fontSize = 36;
        nt.color = Color.white;
        nt.alignment = TextAlignmentOptions.Center;
        nt.fontStyle = FontStyles.Bold;
        nt.enableWordWrapping = true;
        nt.overflowMode = TextOverflowModes.Ellipsis;
        nt.raycastTarget = false;

        // 悬停显示大卡片预览
        var trigger = row.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowTooltip(card, null));
        trigger.triggers.Add(enter);
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => HideTooltip());
        trigger.triggers.Add(exit);
    }

    // ─────────────────────────────────────────────
    //  悬停提示（运行时动态创建）
    // ─────────────────────────────────────────────

    private void ShowTooltip(TalentCardData card, RectTransform anchor)
    {
        HideTooltip();
        if (card == null) return;

        var canvas = parentCanvas != null ? parentCanvas : GetComponentInParent<Canvas>();
        if (canvas == null) return;

        _tooltipGo = new GameObject("HeldCardPreview");
        _tooltipGo.transform.SetParent(canvas.transform, false);
        _tooltipGo.transform.SetAsLastSibling();

        // 大卡片面板，屏幕居中显示
        float panelW = 420f;
        float panelH = 680f;
        var cardRt = _tooltipGo.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(panelW, panelH);

        var cardBg = _tooltipGo.AddComponent<Image>();
        cardBg.color = new Color(0.06f, 0.06f, 0.12f, 0.98f);
        cardBg.raycastTarget = false;

        // 顶部色条
        var topStrip = new GameObject("TopStrip");
        topStrip.transform.SetParent(_tooltipGo.transform, false);
        var tsRt = topStrip.AddComponent<RectTransform>();
        tsRt.anchorMin = new Vector2(0, 1);
        tsRt.anchorMax = new Vector2(1, 1);
        tsRt.pivot = new Vector2(0.5f, 1);
        tsRt.anchoredPosition = Vector2.zero;
        tsRt.sizeDelta = new Vector2(0, 8);
        var tsImg = topStrip.AddComponent<Image>();
        tsImg.color = RarityAccent(card.rarity);
        tsImg.raycastTarget = false;

        // 大图标 — 占面板宽度的大部分
        float iconSize = 340f;
        float iconY = -24f;
        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(_tooltipGo.transform, false);
        var iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 1);
        iconRt.anchorMax = new Vector2(0.5f, 1);
        iconRt.pivot = new Vector2(0.5f, 1);
        iconRt.anchoredPosition = new Vector2(0, iconY);
        iconRt.sizeDelta = new Vector2(iconSize, iconSize);
        var iconImg = iconGo.AddComponent<Image>();
        if (card.icon != null)
        {
            iconImg.sprite = card.icon;
            iconImg.color = Color.white;
        }
        else
        {
            iconImg.color = RarityAccent(card.rarity);
        }
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        float curY = iconY - iconSize - 16f; // below icon

        // 卡名
        var nameGo = new GameObject("CardName");
        nameGo.transform.SetParent(_tooltipGo.transform, false);
        var nameRt = nameGo.AddComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0, 1);
        nameRt.anchorMax = new Vector2(1, 1);
        nameRt.pivot = new Vector2(0.5f, 1);
        nameRt.anchoredPosition = new Vector2(0, curY);
        nameRt.sizeDelta = new Vector2(-32, 48);
        var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
        nameTxt.text = card.displayName ?? "???";
        nameTxt.fontSize = 40;
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.color = Color.white;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.raycastTarget = false;
        curY -= 56f;

        // 类型
        var tagGo = new GameObject("Tag");
        tagGo.transform.SetParent(_tooltipGo.transform, false);
        var tagRt = tagGo.AddComponent<RectTransform>();
        tagRt.anchorMin = new Vector2(0, 1);
        tagRt.anchorMax = new Vector2(1, 1);
        tagRt.pivot = new Vector2(0.5f, 1);
        tagRt.anchoredPosition = new Vector2(0, curY);
        tagRt.sizeDelta = new Vector2(-32, 32);
        var tagTxt = tagGo.AddComponent<TextMeshProUGUI>();
        tagTxt.text = CardTypeDisplay(card.cardType);
        tagTxt.fontSize = 26;
        tagTxt.color = new Color(0.7f, 0.7f, 0.75f, 1f);
        tagTxt.alignment = TextAlignmentOptions.Center;
        tagTxt.raycastTarget = false;
        curY -= 40f;

        // 分隔线
        var lineGo = new GameObject("Divider");
        lineGo.transform.SetParent(_tooltipGo.transform, false);
        var lineRt = lineGo.AddComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0, 1);
        lineRt.anchorMax = new Vector2(1, 1);
        lineRt.pivot = new Vector2(0.5f, 1);
        lineRt.anchoredPosition = new Vector2(0, curY);
        lineRt.sizeDelta = new Vector2(-32, 2);
        var lineImg = lineGo.AddComponent<Image>();
        lineImg.color = new Color(0.3f, 0.3f, 0.4f, 0.6f);
        lineImg.raycastTarget = false;
        curY -= 14f;

        // 描述
        string descText = string.IsNullOrEmpty(card.description) ? "" : card.description;
        string effectText = FormatEffectText(card);

        if (!string.IsNullOrEmpty(descText))
        {
            var descGo = new GameObject("Description");
            descGo.transform.SetParent(_tooltipGo.transform, false);
            var descRt = descGo.AddComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0, 1);
            descRt.anchorMax = new Vector2(1, 1);
            descRt.pivot = new Vector2(0.5f, 1);
            descRt.anchoredPosition = new Vector2(0, curY);
            descRt.sizeDelta = new Vector2(-32, 80);
            var descTxt = descGo.AddComponent<TextMeshProUGUI>();
            descTxt.text = descText;
            descTxt.fontSize = 28;
            descTxt.color = new Color(0.92f, 0.92f, 0.95f, 1f);
            descTxt.alignment = TextAlignmentOptions.TopLeft;
            descTxt.enableWordWrapping = true;
            descTxt.overflowMode = TextOverflowModes.Ellipsis;
            descTxt.raycastTarget = false;
            curY -= 90f;
        }

        // 效果摘要
        if (!string.IsNullOrEmpty(effectText))
        {
            var effGo = new GameObject("Effects");
            effGo.transform.SetParent(_tooltipGo.transform, false);
            var effRt = effGo.AddComponent<RectTransform>();
            effRt.anchorMin = new Vector2(0, 1);
            effRt.anchorMax = new Vector2(1, 1);
            effRt.pivot = new Vector2(0.5f, 1);
            effRt.anchoredPosition = new Vector2(0, curY);
            effRt.sizeDelta = new Vector2(-32, 80);
            var effTxt = effGo.AddComponent<TextMeshProUGUI>();
            effTxt.text = effectText;
            effTxt.fontSize = 26;
            effTxt.color = new Color(0.85f, 0.85f, 0.9f, 1f);
            effTxt.alignment = TextAlignmentOptions.TopLeft;
            effTxt.enableWordWrapping = true;
            effTxt.overflowMode = TextOverflowModes.Ellipsis;
            effTxt.raycastTarget = false;
        }
    }

    private void HideTooltip()
    {
        if (_tooltipGo != null)
        {
            Destroy(_tooltipGo);
            _tooltipGo = null;
        }
    }

    private void OnDisable()
    {
        HideTooltip();
    }

    // ─────────────────────────────────────────────
    //  效果文本格式化
    // ─────────────────────────────────────────────

    private static string FormatEffectText(TalentCardData card)
    {
        var sb = new System.Text.StringBuilder();
        if (card.effectType != TalentEffectType.None)
        {
            sb.AppendLine("◆ " + EffectTypeDisplay(card.effectType, card.effectValue, card.effectValue2));
        }
        if (card.secondaryEffectType != TalentEffectType.None)
        {
            sb.AppendLine("◆ " + EffectTypeDisplay(card.secondaryEffectType, card.secondaryEffectValue, card.secondaryEffectValue2));
        }
        if (card.cardScope == CardScope.PerBattle)
            sb.AppendLine("◇ 仅本次战斗生效");
        if (card.isGuardianRewindCard && card.triggerType != GuardianRewindTriggerType.None)
            sb.AppendLine("◇ 触发: " + TriggerTypeDisplay(card.triggerType, card.triggerValue));
        if (card.purchaseCooldownPenalty > 0)
            sb.AppendLine("◇ 干员购买冷却 +" + card.purchaseCooldownPenalty + "s");
        if (card.effectTarget != CardEffectTarget.Global)
        {
            if (card.effectTarget == CardEffectTarget.ByClass)
                sb.AppendLine("◇ 职业限定: " + card.targetOperatorType);
            else if (card.effectTarget == CardEffectTarget.ByOperator)
                sb.AppendLine("◇ 专属卡");
        }
        return sb.Length > 0 ? sb.ToString().TrimEnd() : "";
    }

    private static string EffectTypeDisplay(TalentEffectType type, int v1, int v2)
    {
        switch (type)
        {
            case TalentEffectType.AttackBonus:         return $"攻击力 +{v1}";
            case TalentEffectType.DefenseBonus:        return $"防御力 +{v1}";
            case TalentEffectType.GuardianHpBonus:     return $"守护点生命 +{v1}";
            case TalentEffectType.AttackPercent:       return $"攻击力 +{v1}%";
            case TalentEffectType.DefensePercent:      return $"防御力 +{v1}%";
            case TalentEffectType.GoldBonus:           return $"击杀金币 +{v1}%";
            case TalentEffectType.ScoreBonus:          return $"击杀分数 +{v1}%";
            case TalentEffectType.AttackSpeedPercent:  return $"攻速 +{v1}%";
            case TalentEffectType.AttackRangeBonus:    return $"攻击范围 +{v1}格";
            case TalentEffectType.DefensePenetration:  return $"无视防御 {v1}%";
            case TalentEffectType.CritChanceBonus:     return $"暴击率 +{v1}%";
            case TalentEffectType.CritDamageBonus:     return $"暴击伤害 +{v1}%";
            case TalentEffectType.LifeStealPercent:    return $"吸血 +{v1}%";
            case TalentEffectType.EliteDamageBonus:    return $"对精英怪伤害 +{v1}%";
            case TalentEffectType.MaxHpPercent:        return $"最大生命 +{v1}%";
            case TalentEffectType.KillStackAttack:     return $"击杀叠加攻击 +{v1}（上限{(v2 == 0 ? "∞" : v2.ToString())}）";
            case TalentEffectType.LowHpAttackBonus:    return $"低血攻击 +{v1}%";
            case TalentEffectType.KillAttackSpeedBuff: return $"击杀后攻速 +{v1}%（{v2}s）";
            case TalentEffectType.AoeRangePercent:     return $"范围攻击 +{v1}%";
            case TalentEffectType.GuardianRegenInterval: return $"守护点回血间隔 {v1}s";
            case TalentEffectType.GuardianDamageBonus: return $"守护点伤害 +{v1}";
            case TalentEffectType.GuardianRangeBonus:  return $"守护点射程 +{v1}";
            case TalentEffectType.GuardianMultiTarget: return $"守护点多目标 +{v1}";
            case TalentEffectType.GuardianAttackSpeedPercent: return $"守护点攻速 +{v1}%";
            case TalentEffectType.GuardianRewindExtraTime: return $"回溯额外时间 +{v1}s";
            case TalentEffectType.GuardianRewindExtraCount: return $"回溯额外次数 +{v1}";
            case TalentEffectType.TeleportCooldownReduction: return $"传送冷却 -{v1}s";
            case TalentEffectType.GuardianShieldCount: return $"守护点护盾 +{v1}";
            case TalentEffectType.GuardianResonancePerOp: return $"每干员守护点HP +{v1}";
            case TalentEffectType.GuardianDamageReductionMax: return $"守护点单次受伤上限 {v1}";
            case TalentEffectType.GuardianLowHpDamageMultiplier: return $"守护点低血伤害 x{v1}%";
            case TalentEffectType.GuardianBattleEndHeal: return v1 == 1 ? "战斗结束回满守护点HP" : "守护点战后回血";
            case TalentEffectType.TeleportAttackSpeedBuff: return $"传送后攻速 +{v1}%（{v2}s）";
            case TalentEffectType.RewindAttackSpeedBuff: return $"回溯后攻速 +{v1}%（{v2}s）";
            default: return type.ToString();
        }
    }

    private static string TriggerTypeDisplay(GuardianRewindTriggerType t, int v)
    {
        switch (t)
        {
            case GuardianRewindTriggerType.InstantDP:               return $"立即获得 {v} 部署点";
            case GuardianRewindTriggerType.InstantGuardianHeal:     return $"守护点回血 {v}";
            case GuardianRewindTriggerType.InstantAttackBuff:      return $"本场攻击 +{v}%";
            case GuardianRewindTriggerType.InstantAttackSpeedBuff:  return $"本场攻速 +{v}%";
            case GuardianRewindTriggerType.InstantAllOperatorsHeal:return "全体干员回血";
            case GuardianRewindTriggerType.InstantFreezeAllEnemies:return $"冻结全场 {v}s";
            case GuardianRewindTriggerType.InstantDamageAllEnemies:return $"全场敌人 {v} 伤害";
            case GuardianRewindTriggerType.InstantKillWeakest:     return "击杀最弱敌人";
            default: return t.ToString();
        }
    }

    // ─────────────────────────────────────────────
    //  显示文本辅助
    // ─────────────────────────────────────────────

    private static string CardTypeShort(TalentCardType t)
    {
        switch (t)
        {
            case TalentCardType.Attack:   return "攻击";
            case TalentCardType.Defense:  return "防御";
            case TalentCardType.Guardian: return "守护";
            case TalentCardType.Rare:     return "稀有";
            case TalentCardType.Skill:    return "技能";
            default:                      return "特殊";
        }
    }

    private static string CardTypeDisplay(TalentCardType t)
    {
        switch (t)
        {
            case TalentCardType.Special:  return "特殊";
            case TalentCardType.Attack:   return "攻击";
            case TalentCardType.Defense:  return "防御";
            case TalentCardType.Guardian: return "守护";
            case TalentCardType.Rare:     return "稀有";
            case TalentCardType.Skill:    return "技能";
            default:                      return t.ToString();
        }
    }

    private static string RarityDisplay(TalentCardRarity r)
    {
        switch (r)
        {
            case TalentCardRarity.Common:   return "普通";
            case TalentCardRarity.Advanced:  return "进阶";
            case TalentCardRarity.Rare:      return "稀有";
            case TalentCardRarity.Legendary: return "传奇";
            default:                         return r.ToString();
        }
    }

    // ─────────────────────────────────────────────
    //  颜色
    // ─────────────────────────────────────────────

    private static Color RarityBg(TalentCardRarity r)
    {
        switch (r)
        {
            case TalentCardRarity.Advanced:  return new Color(0.12f, 0.22f, 0.42f, 0.92f);
            case TalentCardRarity.Rare:      return new Color(0.30f, 0.16f, 0.42f, 0.92f);
            case TalentCardRarity.Legendary: return new Color(0.45f, 0.30f, 0.10f, 0.94f);
            default:                         return new Color(0.18f, 0.18f, 0.22f, 0.92f);
        }
    }

    private static Color RarityAccent(TalentCardRarity r)
    {
        switch (r)
        {
            case TalentCardRarity.Advanced:  return new Color(0.35f, 0.65f, 1f, 1f);
            case TalentCardRarity.Rare:      return new Color(0.75f, 0.45f, 1f, 1f);
            case TalentCardRarity.Legendary: return new Color(1f, 0.78f, 0.25f, 1f);
            default:                         return new Color(0.62f, 0.62f, 0.62f, 1f);
        }
    }

    // ─────────────────────────────────────────────
    //  工具
    // ─────────────────────────────────────────────

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name) return child;
            var found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
