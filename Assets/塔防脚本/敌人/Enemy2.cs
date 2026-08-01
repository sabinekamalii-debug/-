using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Enemy2 : MonoBehaviour
{
    [SerializeField] private EnemyData2 data;
    [SerializeField] private Path currentPath;
    [Header("死亡时费用提示")]
    [Tooltip("敌人身上的文本框，死亡时显示「费用+X」，3秒后消失")]
    [SerializeField] private TMP_Text deathRewardText;

    [HideInInspector] public bool isPurpleTalentEnemy = false;
    [HideInInspector] public bool isElite = false;

    private bool _isDead;
    private Vector3 _targetPosition;
    private int _currentWayPoint;
    private bool isBlocked = false;
    private UnitBlocker currentBlocker;
    private int currentHealth;
    private OperatorUnit targetOperator;
    private float attackTimer;
    private int maxHealth;
    private float _runtimeMoveSpeedMultiplier = 1f;

    // ===== 先锋侦察标记 =====
    [HideInInspector] public bool isMarked = false;   // 是否被先锋侦察标记
    private float _markTimer = 0f;                     // 剩余标记时间
    private GameObject _reconRing;                     // 青色侦察环子物体
    [Tooltip("被标记时受到的伤害倍率（1.3 = +30% 增伤）")]
    public float markedDamageMultiplier = 1.3f;

    public static int ActiveEnemyCount { get; private set; }

    public static void ResetActiveEnemyCountForNewLevel()
    {
        ActiveEnemyCount = 0;
    }

    public float GetAttackRangeFromData() => data != null ? data.attackRange : 0f;

    private UnitStatusUI statusUI;
    private void Awake()
    {
        statusUI = GetComponentInChildren<UnitStatusUI>();
        if (deathRewardText == null)
        {
            foreach (var tmp in GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp == null) continue;
                if (statusUI != null && (tmp.transform == statusUI.transform || tmp.transform.IsChildOf(statusUI.transform)))
                    continue;
                deathRewardText = tmp;
                break;
            }
        }
    }
    void Start()
    {
        statusUI = GetComponentInChildren<UnitStatusUI>();

        if (statusUI != null) statusUI.UpdateHP(currentHealth, maxHealth);

        // === 数据驱动自动挂组件（无需手挂） ===
        if (data != null)
        {
            // 术师怪：攻击无视干员防御
            if (data.penetrateDefense && GetComponent<IgnoreDefenseAttacker>() == null)
                gameObject.AddComponent<IgnoreDefenseAttacker>();

            // 远程怪：站桩远程攻击
            if (data.rangedAttack && GetComponent<EnemyRangedAttacker>() == null)
                gameObject.AddComponent<EnemyRangedAttacker>();

            // 奶妈怪：周期治疗周围敌人
            if (data.healRadius > 0f && data.healInterval > 0f && GetComponent<EnemyHealerTrait>() == null)
                gameObject.AddComponent<EnemyHealerTrait>();
        }
    }
    private void OnEnable()
    {
        if (!_allEnemiesForRewind.Contains(this))
            _allEnemiesForRewind.Add(this);

        if (data == null)
        {
            enabled = false;
            return;
        }

        currentHealth = Mathf.RoundToInt(data.lives * LevelRunModifiers.EnemyHpMultiplier);
        maxHealth = currentHealth;
        _runtimeMoveSpeedMultiplier = LevelRunModifiers.EnemySpeedMultiplier;
        
        _currentWayPoint = 0;
        if (currentPath == null)
        {
            return;
        }
        _targetPosition = GetNextPathTarget();
        if (statusUI != null) statusUI.UpdateHP(currentHealth, maxHealth);

        _isDead = false;
        // 重置先锋侦察标记（对象池复用时）
        isMarked = false;
        _markTimer = 0f;
        if (_reconRing != null) _reconRing.SetActive(false);
        isPurpleTalentEnemy = false;
        isElite = false;
        _eliteDefenseBonus = 0;
        _eliteBonusDP = 0;
        if (deathRewardText != null) deathRewardText.gameObject.SetActive(false);
        if (_runtimeDeathRewardCanvas != null) _runtimeDeathRewardCanvas.SetActive(false);
        if (statusUI != null)
        {
            statusUI.gameObject.SetActive(true);
            for (int i = 0; i < statusUI.transform.childCount; i++)
            {
                Transform child = statusUI.transform.GetChild(i);
                if (deathRewardText != null && (child == deathRewardText.transform || deathRewardText.transform.IsChildOf(child)))
                    continue;
                child.gameObject.SetActive(true);
            }
        }
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (deathRewardText != null && sr.transform.IsChildOf(deathRewardText.transform)) continue;
            sr.enabled = true;
            sr.color = Color.white; // 重置精英染色
        }
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;
    }
    private void Update()
    {
        if (_isDead) return;
        UpdateReconMark();
        if (_isStunned) return;
        if (currentPath == null || currentPath.wayPoint == null) return;
        if (!isBlocked && statusUI != null)
            statusUI.UpdateMP(0f, 1f);
        if (currentPath.wayPoint.Length == 0)
        {
            ReachedEndAndDisappear();
            if (GameManager.Instance != null)
                GameManager.Instance.TakeDamage(data.damageforplayer);
            return;
        }
        if (isBlocked)
        {
            // 远程怪不近战：被阻挡时仅由 EnemyRangedAttacker 输出，停在原地当炮台
            if (data != null && data.rangedAttack)
                return;

            if (targetOperator != null)
            {
                float distToOp = Vector3.Distance(transform.position, targetOperator.transform.position);
                if (distToOp > 1.2f)
                {
                    if (currentBlocker != null)
                        currentBlocker.ReleaseEnemy(this);
                    SetBlocked(false, null);
                    return;
                }

                attackTimer += Time.deltaTime;
                float attackInterval = data.attackInterval > 0f ? data.attackInterval : 1f;
                if (statusUI != null)
                    statusUI.UpdateMP(attackTimer, attackInterval);
                if (attackTimer >= attackInterval)
                {
                    bool ignoreDefense = GetComponent<IgnoreDefenseAttacker>() != null;
                    targetOperator.TakeDamage(data.damage, ignoreDefense);
                    attackTimer = 0f;
                }
            }
            return;
        }
        transform.position = Vector3.MoveTowards(transform.position, _targetPosition, data.speed * 0.75f * _runtimeMoveSpeedMultiplier * Time.deltaTime);

        float relativeDistance = (transform.position - _targetPosition).magnitude;
        if (relativeDistance < 0.1f)
        {
            if (_currentWayPoint < currentPath.wayPoint.Length - 1)
            {
                _currentWayPoint++;
                _targetPosition = GetNextPathTarget();
            }
            else
            {
                ReachedEndAndDisappear();
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.TakeDamage(data.damageforplayer);
                }
            }
        }
        if (currentPath.wayPoint != null && _currentWayPoint >= currentPath.wayPoint.Length)
        {
            ReachedEndAndDisappear();
        }
    }

    private void ReachedEndAndDisappear()
    {
        int damage = data != null ? data.damageforplayer : 1;
        RecordReachEnd(damage);

        isBlocked = false;
        currentBlocker = null;
        targetOperator = null;
        ActiveEnemyCount--;
        gameObject.SetActive(false);
    }

    private Vector3 GetNextPathTarget()
    {
        if (currentPath == null || currentPath.wayPoint == null || currentPath.wayPoint.Length == 0)
            return transform.position;
        bool isLast = _currentWayPoint >= currentPath.wayPoint.Length - 1;
        if (isLast)
        {
            if (GridSystem.Instance != null && GridSystem.Instance.defensePoint != null)
                return GridSystem.Instance.defensePoint.position;
            int lastIndex = currentPath.wayPoint.Length - 1;
            if (currentPath.wayPoint[lastIndex] != null)
                return currentPath.GetPosition(lastIndex);
            return transform.position;
        }
        return currentPath.GetPosition(_currentWayPoint);
    }

    public void SetPath(Path path)
    {
        if (path != null)
        {
            currentPath = path;
            ActiveEnemyCount++;
        }
    }

    public int GetContactDamage()
    {
        return data != null ? data.damage : 0;
    }

    public void SetBlocked(bool blocked, UnitBlocker blockerScript)
    {
        isBlocked = blocked;
        currentBlocker = blockerScript;

        if (blocked && blockerScript != null)
        {
            targetOperator = blockerScript.GetComponent<OperatorUnit>();
        }
        else
        {
            targetOperator = null;
        }
    }

    public void TakeDamage(int damage, bool ignoreDefense = false, int penetrationPercent = 0)
    {
        int defense = (data != null ? data.defense : 0) + _eliteDefenseBonus;
        if (penetrationPercent > 0 && !ignoreDefense)
        {
            defense = Mathf.RoundToInt(defense * (1f - Mathf.Min(penetrationPercent, 100) / 100f));
        }
        // 先锋侦察标记：被标记的敌人受到所有来源的额外增伤（先算增伤，再走防御计算）
        if (isMarked && markedDamageMultiplier > 1f)
            damage = Mathf.RoundToInt(damage * markedDamageMultiplier);

        int finalDamage = ignoreDefense ? damage : OperatorUnit.ApplyDefense(damage, defense);
        int oldHealth = currentHealth;
        currentHealth -= finalDamage;
        string enemyName = data != null ? data.name : name;
        
        
        if (statusUI != null)
            statusUI.UpdateHP(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>治疗（奶妈怪用）。增量、封顶 maxHealth，并更新血条。不触发死亡。</summary>
    public void Heal(int amount)
    {
        if (amount <= 0 || _isDead) return;
        int oldHealth = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        string enemyName = data != null ? data.name : name;
        
        if (statusUI != null) statusUI.UpdateHP(currentHealth, maxHealth);
    }

    /// <summary>当前血量上限（奶妈怪读取用）。</summary>
    public int MaxHealth => maxHealth;

    /// <summary>敌人数据（奶妈怪等 Trait 只读访问用）。</summary>
    public EnemyData2 Data => data;

    // ===== 先锋侦察标记 =====
    /// <summary>
    /// 被先锋侦察标记（或刷新标记时长）。被标记的敌人显示青色侦察环，并在标记期间受到额外增伤。
    /// </summary>
    public void MarkForRecon(float duration)
    {
        if (_isDead) return;
        isMarked = true;
        _markTimer = Mathf.Max(_markTimer, duration); // 再次路过刷新（取较大值，避免缩短）
        ShowReconRing(true);
    }

    private void UpdateReconMark()
    {
        if (!isMarked) return;
        _markTimer -= Time.deltaTime;

        // 青环轻微脉动，强化「被扫描/锁定」的视觉反馈
        if (_reconRing != null)
        {
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 6f);
            _reconRing.transform.localScale = Vector3.one * pulse;
        }

        if (_markTimer <= 0f)
        {
            isMarked = false;
            ShowReconRing(false);
        }
    }

    private void ShowReconRing(bool show)
    {
        if (!show)
        {
            if (_reconRing != null) _reconRing.SetActive(false);
            return;
        }

        if (_reconRing == null)
        {
            _reconRing = new GameObject("ReconRing");
            _reconRing.transform.SetParent(transform, false);
            _reconRing.transform.localPosition = Vector3.zero;
            var sr = _reconRing.AddComponent<SpriteRenderer>();
            sr.sprite = GetReconRingSprite();
            // 尽量渲染在敌人身上层
            var enemySR = GetComponent<SpriteRenderer>();
            if (enemySR != null)
            {
                sr.sortingLayerID = enemySR.sortingLayerID;
                sr.sortingOrder = enemySR.sortingOrder + 1;
            }
        }
        // 每次显示都重设青色（OnEnable 会把所有 SpriteRenderer 刷白，这里覆盖回来）
        var ringSR = _reconRing.GetComponent<SpriteRenderer>();
        if (ringSR != null) ringSR.color = new Color(0.3f, 0.9f, 1f, 0.9f); // 青色（cyan/teal）侦察环
        _reconRing.SetActive(true);
    }

    // 运行时生成的圆环 Sprite（全体敌人共享，避免依赖美术资源）
    private static Sprite _reconRingSpriteCache;
    private static Sprite GetReconRingSprite()
    {
        if (_reconRingSpriteCache != null) return _reconRingSpriteCache;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float cx = size / 2f, cy = size / 2f;
        float outer = size / 2f - 2f;
        float inner = outer - 12f; // 环宽约 12px
        var clear = new Color(0f, 0f, 0f, 0f);
        var white = Color.white;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                tex.SetPixel(x, y, (d <= outer && d >= inner) ? white : clear);
            }
        }
        tex.Apply();
        _reconRingSpriteCache = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        return _reconRingSpriteCache;
    }

    void Die()
    {
        if (_isDead) return;
        _isDead = true;

        RecordDeathSnapshot();

        if (isPurpleTalentEnemy && GameManager.Instance != null)
        {
            GameManager.Instance.OnPurpleEnemyKilled();
        }

        if (isBlocked && currentBlocker != null)
        {
            currentBlocker.ReleaseEnemy(this);
        }
        isBlocked = false;
        currentBlocker = null;
        int reward = (data != null) ? data.dpOnKill : 0;
        reward += _eliteBonusDP;
        if (data != null && DeploymentManager.Instance != null)
            DeploymentManager.Instance.AddDP(reward);

        ActiveEnemyCount--;

        ShowDeathRewardText(reward);

        if (statusUI != null)
        {
            bool deathTextOnSameCanvas = deathRewardText != null && deathRewardText.transform.IsChildOf(statusUI.transform);
            if (deathTextOnSameCanvas)
            {
                foreach (Transform child in statusUI.transform)
                {
                    if (child != deathRewardText.transform && !deathRewardText.transform.IsChildOf(child))
                        child.gameObject.SetActive(false);
                }
            }
            else
                statusUI.gameObject.SetActive(false);
        }

        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (deathRewardText != null && sr.transform.IsChildOf(deathRewardText.transform)) continue;
            sr.enabled = false;
        }
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        if (GameManager.Instance != null)
            GameManager.Instance.DelayThenSetInactive(gameObject, deathRewardDisplayDuration);
        else
            gameObject.SetActive(false);
    }

    [Header("死亡费用文字动画")]
    [Tooltip("总显示时长（秒）")]
    [SerializeField] private float deathRewardDisplayDuration = 3f;
    [Tooltip("前多少秒内做上浮动画")]
    [SerializeField] private float deathRewardRiseDuration = 1f;
    [Tooltip("上浮高度（世界单位），不大但能看出上浮")]
    [SerializeField] private float deathRewardRiseHeight = 0.35f;

    private void ShowDeathRewardText(int reward)
    {
        string text = "费用+" + reward;

        if (deathRewardText != null)
        {
            deathRewardText.text = text;
            deathRewardText.gameObject.SetActive(true);
            Transform t = deathRewardText.transform.parent;
            while (t != null && t != transform)
            {
                t.gameObject.SetActive(true);
                t = t.parent;
            }
            if (gameObject.activeInHierarchy)
                StartCoroutine(AnimateDeathRewardRise(deathRewardText.transform));
            return;
        }

        if (_runtimeDeathRewardCanvas == null)
            CreateRuntimeDeathRewardText();
        if (_runtimeDeathRewardCanvas != null)
        {
            _runtimeDeathRewardCanvas.gameObject.SetActive(true);
            _runtimeDeathRewardCanvas.transform.position = transform.position + Vector3.up * 0.6f;
            if (_runtimeDeathRewardTMP != null)
                _runtimeDeathRewardTMP.text = text;
            if (gameObject.activeInHierarchy)
                StartCoroutine(AnimateDeathRewardRise(_runtimeDeathRewardCanvas.transform));
        }
    }

    private IEnumerator AnimateDeathRewardRise(Transform rewardTransform)
    {
        if (rewardTransform == null) yield break;
        Vector3 startPos = rewardTransform.position;
        float elapsed = 0f;
        while (elapsed < deathRewardRiseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / deathRewardRiseDuration);
            rewardTransform.position = startPos + Vector3.up * (deathRewardRiseHeight * t);
            yield return null;
        }
        rewardTransform.position = startPos + Vector3.up * deathRewardRiseHeight;
    }

    private GameObject _runtimeDeathRewardCanvas;
    private TMP_Text _runtimeDeathRewardTMP;

    private void CreateRuntimeDeathRewardText()
    {
        var go = new GameObject("DeathRewardText");
        go.transform.SetParent(transform);
        go.transform.localPosition = new Vector3(0, 0.6f, 0);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(2f, 0.5f);
            rt.localScale = new Vector3(0.02f, 0.02f, 0.02f);
        }

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.offsetMin = textRt.offsetMax = Vector2.zero;
        textRt.sizeDelta = new Vector2(2f, 0.5f);
        textRt.localScale = Vector3.one;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "费用+0";
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.yellow;

        _runtimeDeathRewardCanvas = go;
        _runtimeDeathRewardTMP = tmp;
        go.SetActive(false);
    }

    public void ApplySpawnDebuff(float healthMultiplier)
    {
        int debuffedHealth = (int)(data.lives * healthMultiplier);

        int oldHealth = currentHealth;
        currentHealth = debuffedHealth;
        maxHealth = data.lives;
        

        if (statusUI != null)
        {
            statusUI.UpdateHP(currentHealth, maxHealth);
        }
    }

    // ── 精英关卡 buff ──
    private int _eliteDefenseBonus = 0;
    private int _eliteBonusDP = 0;

    public void ApplyEliteBuff(float hpMult, int defBonus, int bonusDP)
    {
        isElite = true;
        int oldHealth = currentHealth;
        int buffedHealth = (int)(data.lives * hpMult);
        currentHealth = buffedHealth;
        maxHealth = buffedHealth;
        _eliteDefenseBonus = defBonus;
        _eliteBonusDP = bonusDP;
        

        if (statusUI != null)
            statusUI.UpdateHP(currentHealth, maxHealth);
    }

    public int GetEliteBonusDP() => _eliteBonusDP;
    public int GetEliteDefenseBonus() => _eliteDefenseBonus;

    public float GetHealthRatio()
    {
        if (maxHealth <= 0) return 0f;
        return Mathf.Clamp01((float)currentHealth / maxHealth);
    }

    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    public bool IsDead()
    {
        return _isDead;
    }

    public void ApplyFreeze(int seconds)
    {
        if (_isDead) return;
        StartCoroutine(FreezeCoroutine(seconds));
    }

    private System.Collections.IEnumerator FreezeCoroutine(int seconds)
    {
        float originalMult = _runtimeMoveSpeedMultiplier;
        _runtimeMoveSpeedMultiplier = 0f;
        yield return new WaitForSeconds(seconds);
        if (!_isDead)
        {
            _runtimeMoveSpeedMultiplier = originalMult;
        }
    }

    private bool _isStunned = false;

    public bool IsStunned()
    {
        return _isStunned;
    }

    public void ApplyStun(float seconds)
    {
        if (_isDead) return;
        StartCoroutine(StunCoroutine(seconds));
    }

    private System.Collections.IEnumerator StunCoroutine(float seconds)
    {
        _isStunned = true;
        yield return new WaitForSeconds(seconds);
        if (!_isDead)
        {
            _isStunned = false;
        }
    }

    public static void StunAllEnemies(float seconds)
    {
        foreach (var enemy in _allEnemiesForRewind)
        {
            if (enemy != null && !enemy._isDead && enemy.gameObject.activeSelf)
            {
                enemy.ApplyStun(seconds);
            }
        }
        _allEnemiesForRewind.RemoveAll(e => e == null);
    }

    // ── 时光回溯：位置历史记录 ──
    private struct EnemySnapshot
    {
        public float timestamp;
        public Vector3 position;
        public int waypointIndex;
        public Vector3 targetPosition;
        public int health;
        public bool isBlocked;
        public bool isDead;
        public bool reachedEnd;
        public int damageCaused;
    }

    private const float RewindHistoryDuration = 5f;
    private List<EnemySnapshot> _positionHistory = new List<EnemySnapshot>();

    private static List<Enemy2> _allEnemiesForRewind = new List<Enemy2>();

    private void OnDisable()
    {
        _allEnemiesForRewind.Remove(this);
    }

    private void LateUpdate()
    {
        if (currentPath == null) return;

        float now = Time.time;
        _positionHistory.Add(new EnemySnapshot
        {
            timestamp = now,
            position = transform.position,
            waypointIndex = _currentWayPoint,
            targetPosition = _targetPosition,
            health = currentHealth,
            isBlocked = isBlocked,
            isDead = _isDead,
            reachedEnd = false,
            damageCaused = 0
        });

        while (_positionHistory.Count > 0 && now - _positionHistory[0].timestamp > RewindHistoryDuration)
        {
            _positionHistory.RemoveAt(0);
        }
    }

    public void RewindToSecondsAgo(float seconds)
    {
        bool wasDeadOrGone = _isDead || !gameObject.activeSelf;

        if (_positionHistory.Count == 0)
        {
            if (wasDeadOrGone) return;
            return;
        }

        float targetTime = Time.time - seconds;
        EnemySnapshot snapshot = _positionHistory[0];
        bool found = false;

        for (int i = 0; i < _positionHistory.Count; i++)
        {
            if (_positionHistory[i].timestamp >= targetTime)
            {
                if (i > 0)
                {
                    float t = (targetTime - _positionHistory[i - 1].timestamp) /
                              (_positionHistory[i].timestamp - _positionHistory[i - 1].timestamp);
                    if (float.IsNaN(t) || float.IsInfinity(t)) t = 0f;
                    t = Mathf.Clamp01(t);
                    snapshot = new EnemySnapshot
                    {
                        position = Vector3.Lerp(_positionHistory[i - 1].position, _positionHistory[i].position, t),
                        waypointIndex = _positionHistory[i - 1].waypointIndex,
                        targetPosition = _positionHistory[i - 1].targetPosition,
                        health = Mathf.RoundToInt(Mathf.Lerp(_positionHistory[i - 1].health, _positionHistory[i].health, t)),
                        isBlocked = _positionHistory[i - 1].isBlocked,
                        isDead = _positionHistory[i - 1].isDead,
                        reachedEnd = _positionHistory[i - 1].reachedEnd,
                        damageCaused = Mathf.RoundToInt(Mathf.Lerp(_positionHistory[i - 1].damageCaused, _positionHistory[i].damageCaused, t))
                    };
                }
                else
                {
                    snapshot = _positionHistory[i];
                }
                found = true;
                break;
            }
            snapshot = _positionHistory[i];
        }

        if (!found && _positionHistory.Count > 0)
            snapshot = _positionHistory[_positionHistory.Count - 1];

        if (wasDeadOrGone)
        {
        _isDead = false;
        _runtimeMoveSpeedMultiplier = LevelRunModifiers.EnemySpeedMultiplier;
            gameObject.SetActive(true);
            ActiveEnemyCount++;

            if (statusUI != null)
                statusUI.gameObject.SetActive(true);

            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.enabled = true;
            }
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = true;
        }

        transform.position = snapshot.position;
        _currentWayPoint = snapshot.waypointIndex;
        _targetPosition = snapshot.targetPosition;
        currentHealth = Mathf.Max(snapshot.health, 1);
        maxHealth = Mathf.Max(maxHealth, currentHealth);

        if (isBlocked && currentBlocker != null)
        {
            currentBlocker.ReleaseEnemy(this);
        }
        isBlocked = false;
        currentBlocker = null;
        targetOperator = null;

        if (statusUI != null)
            statusUI.UpdateHP(currentHealth, maxHealth);

        _positionHistory.Clear();
    }

    public static void RewindAllEnemies(float seconds)
    {
        int totalDamageRewound = 0;

        foreach (var enemy in _allEnemiesForRewind)
        {
            if (enemy == null) continue;

            bool wasDead = enemy._isDead || !enemy.gameObject.activeSelf;
            bool hasHistory = enemy._positionHistory != null && enemy._positionHistory.Count > 0;
            if (!hasHistory && !wasDead) continue;

            if (wasDead && hasHistory)
            {
                float timeSinceDeath = Time.time - enemy._positionHistory[enemy._positionHistory.Count - 1].timestamp;
                if (timeSinceDeath > seconds) continue;
            }

            if (!wasDead && !hasHistory) continue;

            int damageBefore = 0;
            for (int i = enemy._positionHistory.Count - 1; i >= 0; i--)
            {
                if (Time.time - enemy._positionHistory[i].timestamp <= seconds)
                {
                    damageBefore += enemy._positionHistory[i].damageCaused;
                }
            }

            enemy.RewindToSecondsAgo(seconds);
            totalDamageRewound += damageBefore;
        }

        _allEnemiesForRewind.RemoveAll(e => e == null);

        if (totalDamageRewound > 0 && GameManager.Instance != null)
        {
            GameManager.Instance.HealGuardian(totalDamageRewound);
        }
    }

    public void RecordReachEnd(int damage)
    {
        float now = Time.time;
        _positionHistory.Add(new EnemySnapshot
        {
            timestamp = now,
            position = transform.position,
            waypointIndex = _currentWayPoint,
            targetPosition = _targetPosition,
            health = currentHealth,
            isBlocked = isBlocked,
            isDead = true,
            reachedEnd = true,
            damageCaused = damage
        });
    }

    private void RecordDeathSnapshot()
    {
        float now = Time.time;
        _positionHistory.Add(new EnemySnapshot
        {
            timestamp = now,
            position = transform.position,
            waypointIndex = _currentWayPoint,
            targetPosition = _targetPosition,
            health = 0,
            isBlocked = isBlocked,
            isDead = true,
            reachedEnd = false,
            damageCaused = 0
        });
    }
}
