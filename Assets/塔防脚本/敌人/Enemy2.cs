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

    private bool _isDead;
    private Vector3 _targetPosition;
    private int _currentWayPoint;
    private bool isBlocked = false;
    private UnitBlocker currentBlocker;
    private int currentHealth;
    private OperatorUnit targetOperator;
    private float attackTimer;
    private int maxHealth;

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
    }
    private void OnEnable()
    {
        if (data == null)
        {
            enabled = false;
            return;
        }

        currentHealth = data.lives;
        maxHealth = data.lives;
        _currentWayPoint = 0;
        if (currentPath == null)
        {
            return;
        }
        _targetPosition = GetNextPathTarget();
        if (statusUI != null) statusUI.UpdateHP(currentHealth, maxHealth);

        _isDead = false;
        isPurpleTalentEnemy = false;
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
        }
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;
    }
    private void Update()
    {
        if (_isDead) return;
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
        transform.position = Vector3.MoveTowards(transform.position, _targetPosition, data.speed * 0.75f * Time.deltaTime);

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

    public void TakeDamage(int damage, bool ignoreDefense = false)
    {
        int finalDamage = ignoreDefense ? damage : OperatorUnit.ApplyDefense(damage, data != null ? data.defense : 0);
        currentHealth -= finalDamage;
        
        if (statusUI != null)
            statusUI.UpdateHP(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (_isDead) return;
        _isDead = true;

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

        currentHealth = debuffedHealth;
        maxHealth = data.lives;

        if (statusUI != null)
        {
            statusUI.UpdateHP(currentHealth, maxHealth);
        }
    }
}
