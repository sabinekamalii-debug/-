using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class OperatorCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("配置")]
    public OperatorData operatorData;   // 卡片对应的干员数据
    public GameObject operatorPrefab;   // 卡片对应的干员预制体
    public Image characterIcon;         // 干员立绘/头像

    [Header("冷却表现（二选一或同时用）")]
    [Tooltip("冷却时立绘保持的固定灰色，结束瞬间恢复原色，方便分清谁在冷却。不渐变。")]
    public Color cooldownColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [Tooltip("勾选则自动生成冷却进度条（无需图片素材），在立绘上方显示半透明遮罩随冷却减少而缩短。")]
    public bool useCooldownOverlayWithoutImage = true;
    [Tooltip("可选：若已有图片可拖入；不拖且上面勾选则用代码生成纯色遮罩。")]
    public Image cooldownOverlay;

    private bool isValidDrag = false;
    private Image _runtimeOverlay;  // 无素材时自动创建的遮罩

    // --- 方向路由：竖直拖拽→滚动列表，水平拖拽→部署到地图 ---
    private OperatorShopScroll _shopScroll;
    private bool _directionPending = false;   // 等待判定方向
    private bool _isScrolling = false;         // 本次拖拽已判定为滚动
    private Vector2 _dragStartScreenPos;
    private const float DirectionThreshold = 8f; // 判定方向的屏幕像素阈值

    // --- 费用与冷却相关 ---
    private bool isOnCooldown = false;  // 是否处于购买冷却中

    /// <summary> 本卡片部署该干员时的实际费用（数据费用 + 本卡片上的 OperatorCardStatBonus 或预制体上的 OperatorStatBonus）。 </summary>
    public int GetDeployCost()
    {
        int cost = operatorData != null ? operatorData.cost : 0;
        var cardBonus = GetComponent<OperatorCardStatBonus>();
        if (cardBonus != null)
            cost += cardBonus.deployCostBonus;
        else if (operatorPrefab != null)
        {
            var prefabBonus = operatorPrefab.GetComponent<OperatorStatBonus>();
            if (prefabBonus != null) cost += prefabBonus.GetDeployCostBonus();
        }
        return cost < 0 ? 0 : cost;
    }

    /// <summary>
    /// 战场上是否已有同名干员存活（每个干员场上同时只能存在一个）。
    /// 依据 OperatorUnit.AllOperators（死亡/撤退销毁时自动移除）。
    /// </summary>
    public bool IsOperatorOnField()
    {
        if (operatorData == null) return false;
        string name = operatorData.operatorName;
        for (int i = 0; i < OperatorUnit.AllOperators.Count; i++)
        {
            var op = OperatorUnit.AllOperators[i];
            if (op != null && op.data != null && op.data.operatorName == name)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 将本卡片重新绑定到 roster 中的某个干员（开局自选阵容消费端使用）。
    /// 会把卡片指向的干员数据、部署预制体、立绘一并替换为目标干员，
    /// 这样战斗里拖出的就是你选的人，而不是场景里原本静态摆放的干员。
    /// </summary>
    public void ApplyRosterOperator(OperatorData data)
    {
        if (data == null) return;
        operatorData = data;
        operatorPrefab = data.unitPrefab;
        if (characterIcon != null && data.icon != null)
            characterIcon.sprite = data.icon;
        RefreshStarBadge();
    }

    // --- 星级角标：让玩家在部署卡上直接看到该干员当前养到几星 ---
    private TMPro.TMP_Text _starBadge;

    void OnEnable()
    {
        OperatorStarRegistry.OnStarChanged += OnStarChanged;
        RefreshStarBadge();
    }

    void OnDisable()
    {
        OperatorStarRegistry.OnStarChanged -= OnStarChanged;
    }

    private void OnStarChanged(string key) => RefreshStarBadge();

    /// <summary> 在卡片左上角显示 ★n（无美术资源，纯代码生成）。 </summary>
    public void RefreshStarBadge()
    {
        if (operatorData == null) return;
        if (!OperatorStarRegistry.IsRunActive) return;
        if (!OperatorStarRegistry.IsInRoster(operatorData.RegistryKey)) return;

        if (_starBadge == null)
        {
            var go = new GameObject("StarBadge");
            go.transform.SetParent(transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 22f);
            rect.anchoredPosition = new Vector2(0f, -2f);

            _starBadge = go.AddComponent<TMPro.TextMeshProUGUI>();
            _starBadge.fontSize = 18f;
            _starBadge.alignment = TMPro.TextAlignmentOptions.Center;
            _starBadge.color = new Color(1f, 0.85f, 0.25f);
            _starBadge.raycastTarget = false;
            _starBadge.enableWordWrapping = false;
        }

        int star = OperatorStarRegistry.GetStar(operatorData.RegistryKey);
        _starBadge.text = new string('★', Mathf.Clamp(star, 1, 5));
    }
    private float cooldownTimer = 0f;   // 冷却剩余时间
    private float cooldownDuration = 0f;// 冷却总时长（从 OperatorData 读取）

    private Color originalColor = Color.white;
    private bool initialized = false;

    void Update()
    {
        if (operatorData == null || characterIcon == null || DeploymentManager.Instance == null) return;

        Image overlay = cooldownOverlay != null ? cooldownOverlay : _runtimeOverlay;

        if (!initialized)
        {
            initialized = true;
            cooldownDuration = Mathf.Max(0f, operatorData.purchaseCooldown);
            originalColor = characterIcon.color;
            if (overlay == null && useCooldownOverlayWithoutImage && cooldownDuration > 0f)
                overlay = GetOrCreateRuntimeOverlay();
            if (overlay != null)
            {
                overlay.type = Image.Type.Filled;
                overlay.fillMethod = Image.FillMethod.Vertical;
                overlay.fillOrigin = (int)Image.OriginVertical.Top;
                overlay.gameObject.SetActive(cooldownDuration > 0f);
            }
            overlay = cooldownOverlay != null ? cooldownOverlay : _runtimeOverlay;
        }

        // 冷却中：固定灰色 + 可选 overlay 显示剩余比例
        if (isOnCooldown && cooldownDuration > 0f)
        {
            cooldownTimer -= Time.deltaTime;

            characterIcon.color = cooldownColor;

            if (overlay != null)
            {
                overlay.enabled = true;
                overlay.fillAmount = Mathf.Clamp01(cooldownTimer / cooldownDuration);
            }

            if (cooldownTimer <= 0f)
            {
                isOnCooldown = false;
                if (overlay != null) overlay.enabled = false;
                if (DeploymentManager.Instance.currentDP < GetDeployCost())
                    characterIcon.color = Color.yellow;
                else
                    characterIcon.color = originalColor;
            }
            return;
        }

        if (overlay != null && !isOnCooldown)
            overlay.enabled = false;

        // 不在冷却：按实际部署费用显示原色或黄色
        if (DeploymentManager.Instance.currentDP < GetDeployCost())
            characterIcon.color = Color.yellow;
        else
            characterIcon.color = originalColor;
    }

    /// <summary> 无图片素材时：在立绘上创建一个纯色遮罩（1x1 白图），用于冷却进度。 </summary>
    private Image GetOrCreateRuntimeOverlay()
    {
        if (_runtimeOverlay != null) return _runtimeOverlay;
        if (characterIcon == null) return null;

        var go = new GameObject("CooldownOverlay");
        go.transform.SetParent(characterIcon.transform, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        var img = go.AddComponent<Image>();
        img.sprite = GetWhiteSprite();
        img.color = new Color(0f, 0f, 0f, 0.55f);  // 半透明黑
        img.raycastTarget = false;

        _runtimeOverlay = img;
        return _runtimeOverlay;
    }

    private static Sprite _sharedWhiteSprite;
    private static Sprite GetWhiteSprite()
    {
        if (_sharedWhiteSprite != null) return _sharedWhiteSprite;
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _sharedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        return _sharedWhiteSprite;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 如果在可滚动商店面板内，直接进入方向待定模式（不检查部署条件）
        if (_shopScroll == null)
            _shopScroll = GetComponentInParent<OperatorShopScroll>();

        if (_shopScroll != null)
        {
            isValidDrag = true;
            _directionPending = true;
            _isScrolling = false;
            _dragStartScreenPos = eventData.position;
            return;
        }

        // 无面板时，保持原行为：检查部署条件后立即部署
        if (DeploymentManager.Instance == null) return;
        if (Time.timeScale == 0f) 
        {
            isValidDrag = false;
            return;
        }

        if (IsOperatorOnField())
        {
            isValidDrag = false;
            if (SystemMessageUI.Instance != null)
            {
                SystemMessageUI.Instance.ShowMessage("该干员仍在场上，需等待其消失后才能再次部署", Color.red);
            }
            return;
        }

        if (isOnCooldown)
        {
            isValidDrag = false;
            if (SystemMessageUI.Instance != null)
            {
                SystemMessageUI.Instance.ShowMessage("此干员购买冷却中", Color.yellow);
            }
            return;
        }

        if (!initialized)
        {
            initialized = true;
            cooldownDuration = Mathf.Max(0f, operatorData != null ? operatorData.purchaseCooldown : 0f);
            originalColor = characterIcon != null ? characterIcon.color : Color.white;
        }

        if (DeploymentManager.Instance.currentDP < GetDeployCost())
        {
            isValidDrag = false;
            if (SystemMessageUI.Instance != null)
            {
                SystemMessageUI.Instance.ShowMessage("部署费用不足！", Color.red);
            }
            return;
        }

        isValidDrag = true;
        _dragStartScreenPos = eventData.position;

        // 如果在可滚动商店面板内，先不立即部署，等待方向判定
        if (_shopScroll == null)
            _shopScroll = GetComponentInParent<OperatorShopScroll>();

        if (_shopScroll != null)
        {
            _directionPending = true;
            _isScrolling = false;
            return;
        }

        // 无面板时，保持原行为：立即开始部署
        StartDeploy();
    }

    private void StartDeploy()
    {
        if (operatorData != null && operatorPrefab != null)
        {
            DeploymentManager.Instance.StartDrag(operatorPrefab, operatorData, this);
            if (DeployLightController.Instance != null)
                DeployLightController.Instance.ShowRange(operatorData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isValidDrag) return;

        // 方向待定：判断是竖直滚动还是水平部署
        if (_directionPending)
        {
            float dx = Mathf.Abs(eventData.position.x - _dragStartScreenPos.x);
            float dy = Mathf.Abs(eventData.position.y - _dragStartScreenPos.y);

            if (dx > DirectionThreshold || dy > DirectionThreshold)
            {
                if (dy > dx)
                {
                    // 竖直拖拽 → 滚动列表
                    _isScrolling = true;
                    _directionPending = false;
                    if (_shopScroll != null)
                        _shopScroll.BeginScroll(eventData.position);
                }
                else
                {
                    // 水平拖拽 → 检查部署条件后开始部署
                    _directionPending = false;
                    if (DeploymentManager.Instance == null) { isValidDrag = false; return; }
                    if (Time.timeScale == 0f) { isValidDrag = false; return; }
                    if (IsOperatorOnField())
                    {
                        isValidDrag = false;
                        if (SystemMessageUI.Instance != null)
                            SystemMessageUI.Instance.ShowMessage("该干员仍在场上，需等待其消失后才能再次部署", Color.red);
                        return;
                    }
                    if (isOnCooldown)
                    {
                        isValidDrag = false;
                        if (SystemMessageUI.Instance != null)
                            SystemMessageUI.Instance.ShowMessage("此干员购买冷却中", Color.yellow);
                        return;
                    }
                    if (DeploymentManager.Instance.currentDP < GetDeployCost())
                    {
                        isValidDrag = false;
                        if (SystemMessageUI.Instance != null)
                            SystemMessageUI.Instance.ShowMessage("部署费用不足！", Color.red);
                        return;
                    }
                    StartDeploy();
                }
            }
            return;
        }

        if (_isScrolling)
        {
            if (_shopScroll != null)
                _shopScroll.UpdateScroll(eventData.position);
            return;
        }

        if (DeploymentManager.Instance == null) return;
        DeploymentManager.Instance.OnDragging();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isValidDrag) return;

        // 滚动结束
        if (_isScrolling)
        {
            _isScrolling = false;
            _directionPending = false;
            if (_shopScroll != null)
                _shopScroll.EndScroll();
            isValidDrag = false;
            return;
        }

        // 待定状态结束（未判定方向就松手，什么都不做）
        if (_directionPending)
        {
            _directionPending = false;
            isValidDrag = false;
            return;
        }

        // 部署结束
        if (DeploymentManager.Instance != null)
            DeploymentManager.Instance.EndDrag(operatorPrefab);
        isValidDrag = false;

        if (DeployLightController.Instance != null)
        {
            DeployLightController.Instance.HideRange();
        }
    }

    /// <summary>
    /// 部署成功后由 DeploymentManager 回调，开始冷却
    /// </summary>
    public void OnDeployedSuccessfully(int usedCost)
    {
        // 开始冷却
        if (cooldownDuration > 0f)
        {
            isOnCooldown = true;
            cooldownTimer = cooldownDuration;
        }
    }
}