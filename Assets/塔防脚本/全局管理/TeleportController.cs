using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using static OperatorData;

/// <summary>
/// 按 R 进入传送模式：游戏暂停、地图变色，点击已部署干员再点击目标格子完成传送。
/// 传送阵可拖预制体或精灵图；近战不可点高台、墙不可点，与移动逻辑一致。
/// 50 秒冷却期间守护点显示小女孩图+冷却阴影（与干员画布一样自动生成，无需素材），结束后恢复原图。
/// </summary>
public class TeleportController : MonoBehaviour
{
    public static TeleportController Instance;

    [Header("守护点与精灵")]
    [Tooltip("守护点物体（用于切换精灵与冷却遮罩）")]
    public Transform defensePoint;
    [Tooltip("守护点身上的 SpriteRenderer，不填则自动从 defensePoint 取")]
    public SpriteRenderer defensePointSprite;
    [Tooltip("传送模式/冷却时显示的小女孩精灵图")]
    public Sprite defensePointGirlSprite;
    [Tooltip("冷却期间显示的图：直接拖拽精灵图到这里，冷却时会盖在守护点位置并随冷却缩短")]
    public Sprite defensePointCooldownOverlaySprite;
    [Tooltip("不填则自动查找「角色画布」或首个 Screen Space Canvas")]
    public Canvas defensePointCooldownParentCanvas;
    [Tooltip("冷却条显示尺寸(世界单位)。太大就调小此值（如 2~3）；精灵图的 Pixels Per Unit 不影响 UI 显示大小")]
    public float worldSpaceOverlayWorldUnits = 5f;
    [Tooltip("拖了冷却精灵图时，遮罩的透明度(0~1)。半透明可设 0.5~0.7")]
    [Range(0.1f, 1f)]
    public float cooldownOverlayAlpha = 0.6f;
    [Tooltip("冷却条相对守护点的显示层级，需大于守护点 Sprite 的 Order in Layer（如 53）才显示在前面")]
    public int cooldownOverlaySortOrder = 60;
    [Tooltip("一般不填；脚本会自动生成冷却遮罩，上面拖了精灵图就用你的图")]
    public Image defensePointCooldownOverlay;

    [Header("传送阵")]
    [Tooltip("直接拖拽精灵图：从 Project 里把传送阵的 Sprite 拖到这里")]
    public Sprite teleportPortalSprite;
    [Tooltip("可选：不用精灵图时，可拖入传送阵预制体")]
    public GameObject teleportPortalPrefab;
    [Tooltip("传送阵在干员右侧的偏移（世界单位）")]
    public float portalOffsetRight = 1.2f;

    [Header("动画时长")]
    public float operatorMoveOutDuration = 0.5f;
    public float portalShowAtDestinationDuration = 1f;

    [Header("冷却")]
    public float teleportCooldownDuration = 50f;

    [Header("检测")]
    [Tooltip("干员所在图层，与 DeploymentManager 一致，默认 My")]
    public LayerMask operatorLayer;

    private Sprite _originalDefensePointSprite;
    private bool _isTeleportMode;
    private bool _isOnCooldown;
    private float _cooldownTimer;
    private enum Phase { None, SelectOperator, SelectDestination }
    private Phase _phase;
    private OperatorUnit _selectedUnit;
    private Vector3 _originalPosition;
    private GameObject _portalAtSource;
    private GameObject _portalAtDest;
    /// <summary> 自动创建的冷却阴影画布（与干员卡片一致），冷却时显示、非冷却时隐藏。 </summary>
    private GameObject _defensePointCooldownCanvasGo;
    /// <summary> World Space 下挂在角色画布时的子画布（用于提高 Sort Order，让冷却条画在守护点前面）。 </summary>
    private Transform _cooldownOverlayWrapper;

    private void Reset()
    {
        operatorLayer = LayerMask.GetMask("My");
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (operatorLayer == 0)
            operatorLayer = LayerMask.GetMask("My");
        if (defensePoint != null && defensePointSprite == null)
            defensePointSprite = defensePoint.GetComponentInChildren<SpriteRenderer>();
        if (defensePointSprite != null)
            _originalDefensePointSprite = defensePointSprite.sprite;
        EnsureDefensePointCooldownOverlay();
    }

    /// <summary> 与 OperatorCard 一样：无素材时自动生成 1x1 白图遮罩 + Filled 竖向。自动查找角色画布挂上去，无需拖拽。 </summary>
    private void EnsureDefensePointCooldownOverlay()
    {
        if (defensePointCooldownOverlay != null) return;
        if (defensePoint == null) return;

        if (defensePointCooldownParentCanvas == null)
            defensePointCooldownParentCanvas = GetOrFindCooldownParentCanvas();
        if (defensePointCooldownParentCanvas != null && defensePointCooldownParentCanvas.renderMode == RenderMode.WorldSpace && defensePointCooldownParentCanvas.worldCamera == null && Camera.main != null)
            defensePointCooldownParentCanvas.worldCamera = Camera.main;

        Transform parentForOverlay;
        bool useScreenSpace = defensePointCooldownParentCanvas != null;

        if (useScreenSpace)
        {
            bool needWrapperCanvas = defensePointCooldownParentCanvas.renderMode == RenderMode.WorldSpace;
            if (needWrapperCanvas)
            {
                // 用子 Canvas 提高 Sort Order，使冷却条画在守护点（SpriteRenderer Order 53）前面
                var wrapperGo = new GameObject("CooldownOverlayWrapper");
                wrapperGo.transform.SetParent(defensePointCooldownParentCanvas.transform, false);
                var wrapperCanvas = wrapperGo.AddComponent<Canvas>();
                wrapperCanvas.overrideSorting = true;
                wrapperCanvas.sortingOrder = cooldownOverlaySortOrder;
                if (Camera.main != null) wrapperCanvas.worldCamera = Camera.main;
                var wrapperRt = wrapperGo.GetComponent<RectTransform>(); // Canvas 已自动加上 RectTransform，用 Get 不要 Add
                wrapperRt.anchorMin = new Vector2(0.5f, 0.5f);
                wrapperRt.anchorMax = new Vector2(0.5f, 0.5f);
                wrapperRt.pivot = new Vector2(0.5f, 0.5f);
                wrapperRt.anchoredPosition = Vector2.zero;
                wrapperRt.sizeDelta = Vector2.zero;
                wrapperRt.localScale = Vector3.one;
                _cooldownOverlayWrapper = wrapperGo.transform;
                parentForOverlay = wrapperGo.transform;
            }
            else
                parentForOverlay = defensePointCooldownParentCanvas.transform;
        }
        else
        {
            var canvasGo = new GameObject("DefensePointCooldownCanvas");
            canvasGo.transform.SetParent(defensePoint, false);
            canvasGo.transform.localPosition = Vector3.zero;
            canvasGo.transform.localScale = Vector3.one;
            canvasGo.SetActive(false);
            _defensePointCooldownCanvasGo = canvasGo;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            if (Camera.main != null) canvas.worldCamera = Camera.main;
            var rt = canvasGo.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(200f, 200f);
                rt.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            }
            parentForOverlay = canvasGo.transform;
        }

        var overlayGo = new GameObject("CooldownOverlay");
        overlayGo.transform.SetParent(parentForOverlay, false);
        var overlayRect = overlayGo.AddComponent<RectTransform>();
        overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
        overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
        overlayRect.pivot = new Vector2(0.5f, 0.5f);

        bool overlayIsWorldSpace = useScreenSpace
            && defensePointCooldownParentCanvas != null
            && defensePointCooldownParentCanvas.renderMode == RenderMode.WorldSpace;
        float size = overlayIsWorldSpace
            ? Mathf.Max(0.5f, worldSpaceOverlayWorldUnits)
            : (useScreenSpace ? 160f : 200f);
        overlayRect.sizeDelta = new Vector2(size, size);
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
        overlayRect.localScale = Vector3.one;
        // 防止父物体上的 Layout Group / Canvas Scaler 把子物体尺寸压成 0
        var layoutEl = overlayGo.AddComponent<LayoutElement>();
        layoutEl.ignoreLayout = true;
        layoutEl.preferredWidth = size;
        layoutEl.preferredHeight = size;

        var img = overlayGo.AddComponent<Image>();
        img.sprite = defensePointCooldownOverlaySprite != null ? defensePointCooldownOverlaySprite : GetWhiteSprite();
        img.color = defensePointCooldownOverlaySprite != null ? new Color(1f, 1f, 1f, cooldownOverlayAlpha) : new Color(0f, 0f, 0f, 0.55f);
        img.raycastTarget = false;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Vertical;
        img.fillOrigin = (int)Image.OriginVertical.Top;

        defensePointCooldownOverlay = img;
        if (useScreenSpace)
        {
            overlayGo.SetActive(false);
            overlayGo.transform.SetAsLastSibling();
            UpdateDefensePointOverlayPosition();
        }
        else
            overlayGo.SetActive(false);
    }

    /// <summary> 自动查找用于挂冷却阴影的 Canvas（与 OperatorCard 同套 UI）：先按名字「角色画布」，再找首个 Screen Space Canvas。 </summary>
    private static Canvas GetOrFindCooldownParentCanvas()
    {
        var byName = GameObject.Find("角色画布");
        if (byName != null)
        {
            var c = byName.GetComponent<Canvas>();
            if (c != null) return c;
            c = byName.GetComponentInChildren<Canvas>();
            if (c != null) return c;
        }
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.renderMode == RenderMode.ScreenSpaceCamera)
                return canvas;
        }
        return null;
    }

    /// <summary> 冷却时把遮罩对齐到守护点：World Space 画布用世界坐标，Screen Space 用屏幕坐标。 </summary>
    private void UpdateDefensePointOverlayPosition()
    {
        if (defensePoint == null || defensePointCooldownOverlay == null || defensePointCooldownParentCanvas == null) return;
        var overlayRect = defensePointCooldownOverlay.GetComponent<RectTransform>();
        if (overlayRect == null) return;

        if (defensePointCooldownParentCanvas.renderMode == RenderMode.WorldSpace)
        {
            Transform target = _cooldownOverlayWrapper != null ? _cooldownOverlayWrapper : overlayRect;
            target.position = defensePoint.position;
            target.rotation = defensePoint.rotation;
            return;
        }

        var canvasRect = defensePointCooldownParentCanvas.GetComponent<RectTransform>();
        if (canvasRect == null) canvasRect = defensePointCooldownParentCanvas.transform as RectTransform;
        if (canvasRect == null) return;
        Camera camForWorld = Camera.main;
        if (camForWorld == null) return;
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(camForWorld, defensePoint.position);
        bool isOverlay = defensePointCooldownParentCanvas.renderMode == RenderMode.ScreenSpaceOverlay;
        Camera camForRect = isOverlay ? null : (defensePointCooldownParentCanvas.worldCamera != null ? defensePointCooldownParentCanvas.worldCamera : camForWorld);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, camForRect, out Vector2 localPos);
        overlayRect.anchoredPosition = localPos;
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

    private void Update()
    {
        if (_isOnCooldown)
        {
            _cooldownTimer -= Time.deltaTime; // 用 deltaTime：按 R 暂停时冷却读条也会停
            UpdateCooldownOverlay();
            if (defensePointCooldownParentCanvas != null)
                UpdateDefensePointOverlayPosition();
            if (_cooldownTimer <= 0f)
            {
                _isOnCooldown = false;
                SetDefensePointGirlSprite(false);
                if (defensePointCooldownOverlay != null)
                {
                    defensePointCooldownOverlay.enabled = false;
                    defensePointCooldownOverlay.gameObject.SetActive(false); // 与 OperatorCard 一致：冷却结束隐藏
                }
                if (_defensePointCooldownCanvasGo != null)
                    _defensePointCooldownCanvasGo.SetActive(false);
            }
            return;
        }

        if (!_isTeleportMode) return;

        // 取消：ESC 或 鼠标/触摸 右键（手机一般无右键，可用 ESC 或不做取消）
        bool cancel = Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1);
        if (cancel)
        {
            CancelTeleportMode();
            return;
        }

        // 本帧是否有点击/触摸。手机版：先试鼠标（部分设备只上报为鼠标），再试所有触摸的 Began
        bool tapped = false;
        Vector2 tapScreenPos = Vector2.zero;
        if (Input.GetMouseButtonDown(0))
        {
            tapped = true;
            tapScreenPos = Input.mousePosition;
        }
        if (!tapped && Application.isMobilePlatform && Input.touchCount >= 1)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began)
                {
                    tapped = true;
                    tapScreenPos = t.position;
                    break;
                }
            }
        }

        if (_phase == Phase.SelectOperator && tapped)
        {
            Vector2 pos = Camera.main != null ? (Vector2)Camera.main.ScreenToWorldPoint(tapScreenPos) : Vector2.zero;
            Collider2D hit = Physics2D.OverlapPoint(pos, operatorLayer);
            if (hit != null)
            {
                var unit = hit.GetComponent<OperatorUnit>() ?? hit.GetComponentInParent<OperatorUnit>();
                if (unit != null)
                {
                    OnOperatorSelected(unit);
                    return;
                }
            }
        }

        if (_phase == Phase.SelectDestination && tapped)
        {
            Vector3 worldPos = Camera.main != null ? Camera.main.ScreenToWorldPoint(tapScreenPos) : Vector3.zero;
            worldPos.z = 0f;
            if (GridSystem.Instance != null && _selectedUnit != null && _selectedUnit.data != null)
            {
                if (GridSystem.Instance.IsCellOccupied(worldPos))
                {
                    if (SystemMessageUI.Instance != null)
                        SystemMessageUI.Instance.ShowMessage("目标位置已有干员", Color.red);
                    return;
                }
                if (IsValidTeleportDestination(worldPos, _selectedUnit.data))
                {
                    OnDestinationSelected(worldPos);
                    return;
                }
                if (SystemMessageUI.Instance != null)
                    SystemMessageUI.Instance.ShowMessage("近战只能选地面，远程只能选高台；墙不可选", Color.red);
            }
        }
    }

    private bool IsValidTeleportDestination(Vector3 worldPos, OperatorData data)
    {
        if (GridSystem.Instance == null) return false;
        Vector3Int cellPos = GridSystem.Instance.groundTilemap.WorldToCell(worldPos);
        bool hasGround = GridSystem.Instance.groundTilemap.HasTile(cellPos);
        bool hasWall = GridSystem.Instance.wallTilemap.HasTile(cellPos);
        bool hasHigh = GridSystem.Instance.highGroundTilemap.HasTile(cellPos);
        if (hasWall) return false;
        if (data.canStandOnGroundAndHighGround)
            return (hasGround && !hasWall) || hasHigh;
        if (data.opType == OperatorType.Ranged)
            return hasHigh;
        return hasGround && !hasWall;
    }

    /// <summary> R 键由外部调用（如 DeploymentManager），若当前可进入传送模式则进入并返回 true。 </summary>
    public bool TryEnterTeleportMode()
    {
        if (_isOnCooldown || _isTeleportMode) return false;
        EnterTeleportMode();
        return true;
    }

    /// <summary> 手机版长按角色时调用：直接进入传送并选中该干员，用户只需再点目标格子。避免手机“再点一次选干员”不识别的问题。 </summary>
    public bool TryEnterTeleportModeWithOperator(OperatorUnit unit)
    {
        if (unit == null || _isOnCooldown || _isTeleportMode) return false;
        _isTeleportMode = true;
        Time.timeScale = 0f;
        if (TilemapTinter.Instance != null) TilemapTinter.Instance.SetToBlue();
        if (HighGroundTinter.Instance != null) HighGroundTinter.Instance.SetToGold();
        SetDefensePointGirlSprite(true);
        OnOperatorSelected(unit);
        return true;
    }

    private void EnterTeleportMode()
    {
        _isTeleportMode = true;
        _phase = Phase.SelectOperator;
        _selectedUnit = null;
        Time.timeScale = 0f;
        if (TilemapTinter.Instance != null) TilemapTinter.Instance.SetToBlue();
        if (HighGroundTinter.Instance != null) HighGroundTinter.Instance.SetToGold();
        SetDefensePointGirlSprite(true);
        if (SystemMessageUI.Instance != null)
            SystemMessageUI.Instance.ShowMessage("点击要传送的干员，再点击目标格子", Color.cyan);
    }

    private void OnOperatorSelected(OperatorUnit unit)
    {
        _selectedUnit = unit;
        _originalPosition = unit.transform.position;
        if (GridSystem.Instance != null)
            GridSystem.Instance.SetCellOccupied(_originalPosition, false);

        _portalAtSource = CreatePortalAt(_originalPosition + Vector3.right * portalOffsetRight);

        StartCoroutine(MoveOperatorRightThenHide());
        _phase = Phase.SelectDestination;
        if (SystemMessageUI.Instance != null)
            SystemMessageUI.Instance.ShowMessage("点击目标格子完成传送（右键取消）", Color.cyan);
    }

    private IEnumerator MoveOperatorRightThenHide()
    {
        float elapsed = 0f;
        Vector3 startPos = _selectedUnit.transform.position;
        Vector3 endPos = startPos + Vector3.right * portalOffsetRight;
        while (elapsed < operatorMoveOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / operatorMoveOutDuration);
            _selectedUnit.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        _selectedUnit.gameObject.SetActive(false);
    }

    private void OnDestinationSelected(Vector3 worldPos)
    {
        StopAllCoroutines();
        if (_portalAtSource != null) { Destroy(_portalAtSource); _portalAtSource = null; }

        Vector3 cellCenter = GetTeleportCellCenter(worldPos);
        _selectedUnit.gameObject.SetActive(true);
        _selectedUnit.TeleportTo(cellCenter);

        _portalAtDest = CreatePortalAt(cellCenter + Vector3.right * portalOffsetRight);
        StartCoroutine(HidePortalAfterDelay());
    }

    private Vector3 GetTeleportCellCenter(Vector3 worldPos)
    {
        if (GridSystem.Instance == null) return worldPos;
        Vector3Int cellPos = GridSystem.Instance.groundTilemap.WorldToCell(worldPos);
        if (GridSystem.Instance.highGroundTilemap.HasTile(cellPos))
            return GridSystem.Instance.highGroundTilemap.GetCellCenterWorld(cellPos);
        if (GridSystem.Instance.groundTilemap.HasTile(cellPos))
            return GridSystem.Instance.groundTilemap.GetCellCenterWorld(cellPos);
        return worldPos;
    }

    private IEnumerator HidePortalAfterDelay()
    {
        yield return new WaitForSecondsRealtime(portalShowAtDestinationDuration);
        if (_portalAtDest != null) { Destroy(_portalAtDest); _portalAtDest = null; }
        ExitTeleportMode();
        StartCooldown();
    }

    private void CancelTeleportMode()
    {
        StopAllCoroutines();
        if (_portalAtSource != null) { Destroy(_portalAtSource); _portalAtSource = null; }
        if (_selectedUnit != null)
        {
            _selectedUnit.gameObject.SetActive(true);
            _selectedUnit.TeleportTo(_originalPosition);
            _selectedUnit = null;
        }
        ExitTeleportMode();
    }

    private void ExitTeleportMode()
    {
        _isTeleportMode = false;
        _phase = Phase.None;
        _selectedUnit = null;
        Time.timeScale = 1f;
        if (TilemapTinter.Instance != null) TilemapTinter.Instance.ResetColor();
        if (HighGroundTinter.Instance != null) HighGroundTinter.Instance.ResetColor();
        if (!_isOnCooldown) SetDefensePointGirlSprite(false);
        if (SystemMessageUI.Instance != null) SystemMessageUI.Instance.RestoreToDefault();
    }

    private void StartCooldown()
    {
        if (defensePointCooldownOverlay == null && defensePoint != null)
            EnsureDefensePointCooldownOverlay();

        _isOnCooldown = true;
        _cooldownTimer = teleportCooldownDuration;
        SetDefensePointGirlSprite(true);
        if (_defensePointCooldownCanvasGo != null)
            _defensePointCooldownCanvasGo.SetActive(true);
        if (defensePointCooldownOverlay != null)
        {
            defensePointCooldownOverlay.gameObject.SetActive(true);
            defensePointCooldownOverlay.enabled = true;
            defensePointCooldownOverlay.type = Image.Type.Filled;
            defensePointCooldownOverlay.fillMethod = Image.FillMethod.Vertical;
            defensePointCooldownOverlay.fillOrigin = (int)Image.OriginVertical.Top;
            defensePointCooldownOverlay.fillAmount = 1f;
            // 防止被父布局压成 sizeDelta=0，显示时强制恢复尺寸
            var rt = defensePointCooldownOverlay.GetComponent<RectTransform>();
            if (rt != null)
            {
                bool isWorld = defensePointCooldownParentCanvas != null && defensePointCooldownParentCanvas.renderMode == RenderMode.WorldSpace;
                float size = isWorld ? Mathf.Max(0.5f, worldSpaceOverlayWorldUnits) : 160f;
                rt.sizeDelta = new Vector2(size, size);
            }
        }
        if (defensePointCooldownParentCanvas != null)
            UpdateDefensePointOverlayPosition();
    }

    private void UpdateCooldownOverlay()
    {
        if (defensePointCooldownOverlay == null || !defensePointCooldownOverlay.enabled) return;
        defensePointCooldownOverlay.fillAmount = Mathf.Clamp01(_cooldownTimer / teleportCooldownDuration);
    }

    /// <summary> 优先用预制体（可拖 传送门 预制体），否则用精灵图生成。 </summary>
    private GameObject CreatePortalAt(Vector3 worldPos)
    {
        if (teleportPortalPrefab != null)
        {
            var go = Instantiate(teleportPortalPrefab);
            go.transform.position = worldPos;
            return go;
        }
        if (teleportPortalSprite != null)
        {
            var go = new GameObject("TeleportPortal");
            go.transform.position = worldPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = teleportPortalSprite;
            sr.sortingOrder = 100;
            return go;
        }
        return null;
    }

    private void SetDefensePointGirlSprite(bool useGirl)
    {
        if (defensePointSprite == null) return;
        defensePointSprite.sprite = useGirl && defensePointGirlSprite != null ? defensePointGirlSprite : _originalDefensePointSprite;
    }

    public bool IsInTeleportMode => _isTeleportMode;
    public bool IsOnCooldown => _isOnCooldown;
}
