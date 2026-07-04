using UnityEngine;
using UnityEngine.EventSystems;
using static OperatorData;

public class DeploymentManager : MonoBehaviour
{
    public static DeploymentManager Instance;
    public LayerMask operatorLayer;

    [Header("全局设置")]
    public Transform basePoint;
    public LayerMask groundLayer;
    public int maxDP = 500;          // 费用上限（默认 500，可在 Inspector 覆盖）
    public float dpRecoverRate = 2f; // 每秒回复多少点费用（2代表1秒回2点）
    private float dpTimer = 0f;      // 内部计时器
    [Header("资源管理")]
    [Tooltip("当前部署费用。Inspector/控制台里设多少，开局就是多少，不会被别的值覆盖。")]
    public int currentDP;

    [Header("状态监控")]
    public bool isGamePaused = false;
    public bool isRetreatMode = false;
    private OperatorBrain pendingOperator;
    private float inputCooldown = 0f;
    private float retreatCooldown = 0f;
    public float retreatCooldownDuration = 2.0f;

    private OperatorData currentDraggingData;
    private OperatorCard currentDraggingCard; // 发起部署的卡片，用于回调冷却
    private OperatorCard _pendingDeployCard;  // 本次部署待选目的地完成后回调的卡片（EndDrag 里已清空 currentDraggingCard，用这个保留引用）
    private GameObject currentGhost;
    private bool isValidPlacement = false;
    private Vector3 deployPosition;
    public Color validColor = new Color(0, 1, 0, 0.5f);
    public Color invalidColor = new Color(1, 0, 0, 0.5f);
    private SpriteRenderer ghostRenderer;

    [Header("部署范围微调")]
    [Tooltip("在原有部署半径基础上额外增加的世界单位（相当于把十字每条边稍微延长一点）。")]
    public float deployRangeExtra = 0.5f;
    [Tooltip("十字中心粗细的额外裕量（世界单位），避免因为格子划分差异导致边缘格子判定不到。")]
    public float deployCrossThicknessExtra = 0.5f;

    [Header("手机版：双击屏幕代替 R 键")]
    [Tooltip("两次点击之间的最大时间间隔（秒），用于识别双击。")]
    public float mobileDoubleTapInterval = 0.35f;
    [Tooltip("两次点击位置允许的最大距离（像素），太远视为不同位置的点击。")]
    public float mobileDoubleTapMaxMovePixels = 80f;
    private float _mobileLastTapTime = -1f;
    private Vector2 _mobileLastTapPos;

#if UNITY_EDITOR
    [Header("编辑器：鼠标双击模拟 R 键")]
    [Tooltip("仅在 Unity 编辑器下生效，用鼠标双击 Game 窗口来模拟手机双击。")]
    public float editorDoubleClickInterval = 0.35f;
    [Tooltip("编辑器中两次点击位置允许的最大距离（像素）。")]
    public float editorDoubleClickMaxMovePixels = 80f;
    private float _editorLastClickTime = -1f;
    private Vector2 _editorLastClickPos;
#endif

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // 若未在 Inspector 里拖入守护点，则自动按名字查找（否则拖拽松手后 isValidPlacement 一直为 false，预制体不会出现）
        if (basePoint == null)
        {
            var go = GameObject.Find("守护点");
            if (go != null) basePoint = go.transform;
        }
        // 以 Inspector/控制台里设置的 Current DP 为准，不覆盖；只做范围限制并刷新 UI
        currentDP = Mathf.Clamp(currentDP, 0, maxDP);
        Time.timeScale = 1f;
        UpdateCostUI();
    }

    void Update()
    {
        if (inputCooldown > 0) inputCooldown -= Time.unscaledDeltaTime;
        if (retreatCooldown > 0) retreatCooldown -= Time.deltaTime;

        // 电脑版：R 键进入传送或撤退。
        if (!Application.isMobilePlatform && Input.GetKeyDown(KeyCode.R))
        {
            if (TeleportController.Instance != null && TeleportController.Instance.TryEnterTeleportMode())
                return;
            ToggleRetreatMode();
        }

#if UNITY_EDITOR
        // 编辑器：鼠标双击 Game 窗口，模拟手机双击（等同于按 R）
        if (!Application.isMobilePlatform && Input.GetMouseButtonDown(0))
        {
            float now = Time.unscaledTime;
            Vector2 pos = Input.mousePosition;

            bool isDoubleClick = false;
            if (_editorLastClickTime >= 0f)
            {
                float dt = now - _editorLastClickTime;
                float distSqr = (pos - _editorLastClickPos).sqrMagnitude;
                float maxDistSqr = editorDoubleClickMaxMovePixels * editorDoubleClickMaxMovePixels;
                if (dt <= editorDoubleClickInterval && distSqr <= maxDistSqr)
                    isDoubleClick = true;
            }

            _editorLastClickTime = now;
            _editorLastClickPos = pos;

            if (isDoubleClick)
            {
                // 已注释：不再用双击触发传送/撤退，避免误触
                // if (TeleportController.Instance != null && TeleportController.Instance.TryEnterTeleportMode())
                //     return;
                // ToggleRetreatMode();
                return;
            }
        }
#endif

        // --- 手机版：双击屏幕等同于按下 R 键（附带详细 Debug） ---
        if (Application.isMobilePlatform && Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Ended)
            {
                float now = Time.unscaledTime;
                Vector2 pos = t.position;

                bool isDoubleTap = false;
                if (_mobileLastTapTime >= 0f)
                {
                    float dt = now - _mobileLastTapTime;
                    float distSqr = (pos - _mobileLastTapPos).sqrMagnitude;
                    float maxDistSqr = mobileDoubleTapMaxMovePixels * mobileDoubleTapMaxMovePixels;
                    if (dt <= mobileDoubleTapInterval && distSqr <= maxDistSqr)
                    {
                        isDoubleTap = true;
                    }
                }

                _mobileLastTapTime = now;
                _mobileLastTapPos = pos;

                if (isDoubleTap)
                {
                    // 已注释：不再用双触触发传送/撤退，避免误触
                    // if (TeleportController.Instance != null)
                    // {
                    //     if (TeleportController.Instance.TryEnterTeleportMode()) { return; }
                    //     else { ToggleRetreatMode(); return; }
                    // }
                    // else { ToggleRetreatMode(); return; }
                    return;
                }
            }
        }

        if (isRetreatMode)
        {
            HandleRetreatInput();
        }
        else if (isGamePaused && pendingOperator != null)
        {
            if (inputCooldown <= 0) HandleDestinationSelection();
        }
        // 【新增】：自动回复费用逻辑
        if (currentDP < maxDP && !isGamePaused)
        {
            dpTimer += Time.deltaTime;
            if (dpTimer >= (1f / dpRecoverRate))
            {
                currentDP++;
                dpTimer = 0f;
                UpdateCostUI();
            }
        }
    }

    public void StartDrag(GameObject ghostPrefab, OperatorData data = null, OperatorCard sourceCard = null)
    {
        if (isRetreatMode) return;
        isGamePaused = false;
        currentDraggingData = data;
        currentDraggingCard = sourceCard;
        Time.timeScale = 0f;
        isGamePaused = true;

        if (SystemMessageUI.Instance != null) SystemMessageUI.Instance.ShowDragHint();

        if (ghostPrefab != null)
        {
            currentGhost = Instantiate(ghostPrefab);
            ghostRenderer = currentGhost.GetComponentInChildren<SpriteRenderer>();
            if (ghostRenderer == null) ghostRenderer = currentGhost.GetComponent<SpriteRenderer>();
        }
        isValidPlacement = false;

        // ==========================================
        // 【新增联动】：开始拖拽干员，点亮部署范围！
        // ==========================================
        if (DeployLightController.Instance != null && data != null)
        {
            DeployLightController.Instance.ShowRange(data);
        }
    }

    public void OnDragging()
    {
        if (currentGhost == null) return;
        if (basePoint == null)
        {
            if (ghostRenderer != null) ghostRenderer.color = invalidColor;
            isValidPlacement = false;
            return;
        }
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;

        float rangeLimit = 4.0f;
        if (currentDraggingData != null) rangeLimit = currentDraggingData.deployRadius;

        // 在原有部署半径和十字宽度基础上略微放宽，避免与你地图格子划分的细小差异导致“明明在格子上却判定不到”。
        float effectiveRange = rangeLimit + deployRangeExtra;
        float crossHalfThickness = 0.6f + deployCrossThicknessExtra;

        bool isWalkable = false;
        if (GridSystem.Instance != null && currentDraggingData != null)
        {
            Vector3Int cellPos = GridSystem.Instance.groundTilemap.WorldToCell(mousePos);
            bool hasGround = GridSystem.Instance.groundTilemap.HasTile(cellPos);
            bool hasWall = GridSystem.Instance.wallTilemap.HasTile(cellPos);
            bool isOccupied = GridSystem.Instance.IsCellOccupied(mousePos);

            isWalkable = hasGround && !hasWall && !isOccupied;
        }

        float diffX = Mathf.Abs(mousePos.x - basePoint.position.x);
        float diffY = Mathf.Abs(mousePos.y - basePoint.position.y);
        bool inRange = (diffY < crossHalfThickness && diffX <= effectiveRange) ||
                       (diffX < crossHalfThickness && diffY <= effectiveRange);

        if (inRange && isWalkable)
        {
            // 不再强制把拖拽位置吸附到守护点中心，而是保留玩家当前指向的格子中心。
            currentGhost.transform.position = mousePos;
            ghostRenderer.color = validColor;
            isValidPlacement = true;
            deployPosition = mousePos;
        }
        else
        {
            currentGhost.transform.position = mousePos;
            ghostRenderer.color = invalidColor;
            isValidPlacement = false;
        }
    }

    public void EndDrag(GameObject operatorPrefab)
    {
        if (currentGhost != null) Destroy(currentGhost);
        // 先保存，再清空，否则下面给 newUnit.data 赋值时已经为 null
        OperatorData dataToInject = currentDraggingData;
        OperatorCard cardToCallback = currentDraggingCard;
        currentDraggingData = null;
        currentDraggingCard = null;

        if (SystemMessageUI.Instance != null) SystemMessageUI.Instance.RestoreToDefault();

        if (DeployLightController.Instance != null)
        {
            DeployLightController.Instance.HideRange();
        }

        if (!isValidPlacement || operatorPrefab == null)
        {
            Time.timeScale = 1f; 
            isGamePaused = false;
            return;
        }

        GameObject newOpObj = Instantiate(operatorPrefab, deployPosition, Quaternion.identity);
        OperatorUnit newUnit = newOpObj.GetComponent<OperatorUnit>();
        // 从卡片拖拽部署时自动注入干员数据，无需在预制体上拖拽 Data
        if (newUnit != null && dataToInject != null)
        {
            newUnit.data = dataToInject;
            newUnit.SyncRuntimeFromData();
        }

        // 若卡片上挂了 OperatorCardStatBonus，把加成应用到刚生成的干员身上
        if (newUnit != null && cardToCallback != null)
        {
            var cardBonus = cardToCallback.GetComponent<OperatorCardStatBonus>();
            if (cardBonus != null)
            {
                var unitBonus = newUnit.gameObject.AddComponent<OperatorStatBonus>();
                unitBonus.attackBonus = cardBonus.attackBonus;
                unitBonus.defenseBonus = cardBonus.defenseBonus;
                unitBonus.deployCostBonus = cardBonus.deployCostBonus;
                unitBonus.healthBonus = cardBonus.healthBonus;
                unitBonus.ApplyNow();
            }
        }

        pendingOperator = newOpObj.GetComponent<OperatorBrain>();
        _pendingDeployCard = cardToCallback;

        if (pendingOperator != null)
        {
            inputCooldown = 0.2f;
            if (SystemMessageUI.Instance != null) SystemMessageUI.Instance.ShowMessage("请点击图中变色区域移动角色");
            if (TilemapTinter.Instance != null) TilemapTinter.Instance.SetToBlue();
            if (HighGroundTinter.Instance != null) HighGroundTinter.Instance.SetToGold();

            if (newUnit != null)
            {
                OperatorData data = newUnit.data;
                if (data != null && data.opType == OperatorType.Ranged && GridSystem.Instance != null)
                    GridSystem.Instance.ShowHighGroundHighlights();
            }
        }
        else
        {
            Time.timeScale = 1f;
            isGamePaused = false;
            if (_pendingDeployCard != null)
            {
                int cost = (dataToInject != null ? dataToInject.cost : 0);
                _pendingDeployCard.OnDeployedSuccessfully(cost);
                _pendingDeployCard = null;
            }
        }
    }

    void HandleDestinationSelection()
    {
        if (GridSystem.Instance == null || pendingOperator == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;

            if (GridSystem.Instance.IsCellOccupied(mousePos))
            {
                if (SystemMessageUI.Instance != null) SystemMessageUI.Instance.ShowMessage("目标位置已安排其他干员", Color.red);
                return;
            }

            OperatorUnit unit = pendingOperator.GetComponent<OperatorUnit>();
            if (unit == null || unit.data == null) return;
            OperatorData data = unit.data;
            Vector3Int cellPos = GridSystem.Instance.groundTilemap.WorldToCell(mousePos);
            bool isValidDest = false;

            // 特殊干员：既能上地面又能上高台
            if (data.canStandOnGroundAndHighGround)
            {
                bool isGround = GridSystem.Instance.groundTilemap.HasTile(cellPos);
                bool isWall = GridSystem.Instance.wallTilemap.HasTile(cellPos);
                bool isHigh = GridSystem.Instance.highGroundTilemap.HasTile(cellPos);

                // 地面：有地面且不是墙；高台：有高台砖
                isValidDest = (isGround && !isWall) || isHigh;
                if (!isValidDest && SystemMessageUI.Instance != null)
                {
                    SystemMessageUI.Instance.ShowMessage("此干员只能部署在地面或高台", Color.red);
                }
            }
            else if (data.opType == OperatorType.Ranged)
            {
                // 普通远程：只能高台
                isValidDest = GridSystem.Instance.highGroundTilemap.HasTile(cellPos);
                if (!isValidDest && SystemMessageUI.Instance != null)
                    SystemMessageUI.Instance.ShowMessage("远程干员目的地只能是高台", Color.red);
            }
            else
            {
                // 普通近战：只能地面且非墙
                bool isGround = GridSystem.Instance.groundTilemap.HasTile(cellPos);
                bool isWall = GridSystem.Instance.wallTilemap.HasTile(cellPos);
                isValidDest = isGround && !isWall;
                if (!isValidDest && SystemMessageUI.Instance != null)
                    SystemMessageUI.Instance.ShowMessage("近战干员目的地只能是地面", Color.red);
            }

            if (isValidDest)
            {
                GridSystem.Instance.SetCellOccupied(mousePos, true);
                unit.MoveToDestination(mousePos); 

                // 按实际费用扣费（含 OperatorStatBonus 的 deployCostBonus），并记录到 unit.deployCost，撤退时按此退还
                unit.deployCost = unit.GetDeployCost();

                currentDP -= unit.deployCost;
                UpdateCostUI();

                Time.timeScale = 1f; 
                isGamePaused = false;
                pendingOperator = null;
                if (_pendingDeployCard != null)
                {
                    _pendingDeployCard.OnDeployedSuccessfully(unit.deployCost);
                    _pendingDeployCard = null;
                }

                if (TilemapTinter.Instance != null) TilemapTinter.Instance.ResetColor();
                if (HighGroundTinter.Instance != null) HighGroundTinter.Instance.ResetColor();
            }
        }
        else if (Input.GetMouseButtonDown(1))
        {
            if (pendingOperator != null) Destroy(pendingOperator.gameObject);
            pendingOperator = null;
            _pendingDeployCard = null;
            Time.timeScale = 1f; 
            isGamePaused = false;

            if (TilemapTinter.Instance != null) TilemapTinter.Instance.ResetColor();
            if (HighGroundTinter.Instance != null) HighGroundTinter.Instance.ResetColor();
        }
    }

    void ToggleRetreatMode()
    {
        if (pendingOperator != null) return;
        if (retreatCooldown > 0) return;

        if (!isRetreatMode)
        {
            isRetreatMode = true;
            Time.timeScale = 0f; 
            // 新手提示：进入撤退模式时，引导玩家点击已有友方角色
            if (SystemMessageUI.Instance != null)
                SystemMessageUI.Instance.ShowMessage("点击已有友方角色");
        }
        else ExitRetreatMode();
    }

    void HandleRetreatInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Collider2D hit = Physics2D.OverlapPoint(mousePos, operatorLayer);

            if (hit != null)
            {
                OperatorUnit unit = hit.GetComponent<OperatorUnit>();
                if (unit == null) unit = hit.GetComponentInParent<OperatorUnit>();
                if (unit != null) RetreatOperator(unit);
            }
        }
        if (Input.GetMouseButtonDown(1)) ExitRetreatMode();
    }

    void RetreatOperator(OperatorUnit unit)
    {
        if (unit.data != null)
        {
            // 撤退时返还本次部署所花费用（若没有记录则退回基础费用）
            int refund = unit.deployCost > 0 ? unit.deployCost : unit.data.cost;
            currentDP += refund;
            UpdateCostUI();
        }
        if (unit.blocker != null) unit.blocker.ReleaseAllEnemies();
        Destroy(unit.gameObject);
        ExitRetreatMode();
        retreatCooldown = retreatCooldownDuration;
    }

    void ExitRetreatMode()
    {
        isRetreatMode = false;
        Time.timeScale = 1f;
        // 撤退模式结束后恢复默认新手提示
        if (SystemMessageUI.Instance != null)
            SystemMessageUI.Instance.RestoreToDefault();
    }

    /// <summary> 增加部署费用（如击杀敌人获得），自动不超过上限并刷新 UI。 </summary>
    public void AddDP(int amount)
    {
        if (amount <= 0) return;
        currentDP = Mathf.Min(currentDP + amount, maxDP);
        UpdateCostUI();
    }

    void UpdateCostUI()
    {
        // 只让“当前单例”刷新 UI，避免场景里误挂多个 DeploymentManager 时用旧 currentDP 覆盖，导致数字闪一下又回去
        if (this != Instance) return;
        if (GameManager.Instance != null && GameManager.Instance.uiController != null)
            GameManager.Instance.uiController.UpdateCostUI(currentDP, maxDP);
    }
}