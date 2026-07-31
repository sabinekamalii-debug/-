using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("")]
    [Tooltip("守护点最大生命值")]
    public int maxPlayerHealth = 5;
    [Tooltip("守护点当前生命值，可在 Inspector 中自定义")]
    public int playerHealth = 5;
    public bool isGameOver = false;
    
    [Header("UI")]
    public UIController uiController;

    private bool _isMidGameDropProcessing = false;
    private bool _guardianRewindUsed = false;
    private bool _isGuardianRewindProcessing = false;

    [Header("应急协议（时光回溯）")]
    [Tooltip("是否开启应急协议：守护点生命≤1时触发，选择战术支援并回退敌人5秒")]
    public bool enableEmergencyProtocol = true;
    [Tooltip("回退秒数（时光倒流长）")]
    public float rewindSeconds = 5f;
    [Tooltip("触发阈值：守护点生命≤此值时触发应急协议")]
    public int emergencyProtocolThreshold = 1;
    [Tooltip("回溯后敌人眩晕秒数（给玩家喘息时间）")]
    public float postRewindStunSeconds = 2f;

    [Header("守护点血条（可选）")]
    [Tooltip("若血条画布不是守护点的子物体，拖入这里；GameOver 时会自动隐藏")]
    public GameObject guardPointHealthBarCanvas;

    [Header("死亡特效")]
    [Tooltip("死亡时的红色全屏覆盖层 Prefab，不填则自动创建")]
    public GameObject deathOverlay;
    private Canvas _deathCanvas;
    private GameObject _deathOverlayObject;
    private GameObject _deathTextObject;
    private bool _isListeningForClick = false;

    [Header("时光回溯特效")]
    [Tooltip("时光回溯视觉特效 Prefab，不填则代码自动创建")]
    public GameObject rewindEffectPrefab;

    public void DelayThenSetInactive(GameObject target, float delay)
    {
        if (target == null) return;
        StartCoroutine(DelayRoutine(target, delay));
    }

    private IEnumerator DelayRoutine(GameObject target, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (target != null) target.SetActive(false);
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 确保 GameSpeedBoost 始终存在（Ctrl 加速）
        if (GetComponent<GameSpeedBoost>() == null)
            gameObject.AddComponent<GameSpeedBoost>();
    }

    public void Start()
    {
        int guardianBonus = TalentEffectApplier.GetGuardianHpBonus();
        maxPlayerHealth = 5 + guardianBonus;

        // 跨场血量：首战满血，后续战斗承接上场残余血量
        if (RogueRuntimeState.GuardianMaxHp > 0)
        {
            playerHealth = Mathf.Clamp(RogueRuntimeState.GuardianCurrentHp, 1, maxPlayerHealth);
        }
        else
        {
            playerHealth = maxPlayerHealth;
        }

        if (uiController != null)
        {
            uiController.UpdateLivesUI(playerHealth);
        }

        // 通知 BattleEventManager 战斗开始
        if (BattleEventManager.Instance != null)
            BattleEventManager.Instance.OnBattleStart();
    }

    public void TakeDamage(int damageAmount)
    {
        if (isGameOver) return;
        if (_isGuardianRewindProcessing) return;

        int newHealth = playerHealth - damageAmount;

        if (enableEmergencyProtocol && !_guardianRewindUsed && playerHealth > emergencyProtocolThreshold && newHealth <= emergencyProtocolThreshold)
        {
            int oldHealth = playerHealth;
            playerHealth = emergencyProtocolThreshold;
            
            if (uiController != null)
            {
                uiController.UpdateLivesUI(playerHealth);
            }
            TriggerEmergencyProtocol();
            return;
        }

        int preDamageHealth = playerHealth;
        playerHealth = newHealth;
        

        if (uiController != null)
        {
            uiController.UpdateLivesUI(playerHealth);
        }

        if (playerHealth <= 0)
        {
            GameOver();
        }
    }

    public void OnPurpleEnemyKilled()
    {
        if (isGameOver || _isMidGameDropProcessing) 
        {
            return;
        }
        
        _isMidGameDropProcessing = true;
        
        Time.timeScale = 0f;

        RogueResultController.IsMidGameDrop = true;
        SceneManager.LoadScene(SceneNames.RogueResult, LoadSceneMode.Additive);
    }

    public void ResetMidGameDropFlag()
    {
        _isMidGameDropProcessing = false;
    }

    public bool IsPurpleEnemyDropProcessing()
    {
        return _isMidGameDropProcessing;
    }

    public void TriggerEmergencyProtocol()
    {
        if (_guardianRewindUsed || _isGuardianRewindProcessing) return;

        _guardianRewindUsed = true;
        _isGuardianRewindProcessing = true;

        maxPlayerHealth = Mathf.Max(1, maxPlayerHealth - 1);
        if (playerHealth > maxPlayerHealth)
            playerHealth = maxPlayerHealth;

        Time.timeScale = 0f;

        RogueResultController.IsGuardianRewindDrop = true;
        RogueResultController.IsMidGameDrop = true;
        SceneManager.LoadScene(SceneNames.RogueResult, LoadSceneMode.Additive);
    }

    public void OnEmergencyProtocolComplete()
    {
        StartCoroutine(EmergencyProtocolCompleteCoroutine());
    }

    private IEnumerator EmergencyProtocolCompleteCoroutine()
    {
        yield return PlayRewindVisualEffect();

        _isGuardianRewindProcessing = false;

        OperatorUnit.RewindAllOperatorsHealth(rewindSeconds);

        Enemy2.RewindAllEnemies(rewindSeconds);

        Enemy2.StunAllEnemies(postRewindStunSeconds);

        Time.timeScale = 1f;
    }

    private IEnumerator PlayRewindVisualEffect()
    {
        Canvas canvas = null;
        GameObject overlayGo = null;
        TMPro.TextMeshProUGUI countTmp = null;
        UnityEngine.UI.Image img = null;

        if (rewindEffectPrefab != null)
        {
            // 使用 Prefab 实例化
            var effectInstance = Instantiate(rewindEffectPrefab);
            canvas = effectInstance.GetComponent<Canvas>();
            var overlayTransform = effectInstance.transform.Find("RewindOverlay");
            if (overlayTransform != null)
            {
                overlayGo = overlayTransform.gameObject;
                img = overlayGo.GetComponent<UnityEngine.UI.Image>();
            }
            var countdownTransform = effectInstance.transform.Find("CountdownText");
            if (countdownTransform != null)
                countTmp = countdownTransform.GetComponent<TMPro.TextMeshProUGUI>();
        }
        else
        {
            // 回退：代码创建
            var canvasGo = new GameObject("EmergencyProtocol_Effect", typeof(Canvas));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            overlayGo = new GameObject("RewindOverlay", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            overlayGo.transform.SetParent(canvas.transform, false);
            var rect = overlayGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            img = overlayGo.GetComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.3f, 0.2f, 0.8f, 0f);

            var countdownGo = new GameObject("CountdownText", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            countdownGo.transform.SetParent(canvas.transform, false);
            var countRect = countdownGo.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0.5f, 0.5f);
            countRect.anchorMax = new Vector2(0.5f, 0.5f);
            countRect.pivot = new Vector2(0.5f, 0.5f);
            countRect.sizeDelta = new Vector2(600, 200);
            countRect.anchoredPosition = Vector2.zero;
            countTmp = countdownGo.GetComponent<TMPro.TextMeshProUGUI>();
            countTmp.alignment = TMPro.TextAlignmentOptions.Center;
            countTmp.fontSize = 120;
            countTmp.color = new Color(0.8f, 0.8f, 1f, 1f);
            countTmp.text = "时光回溯中...";
        }

        try
        {
            float totalDuration = 1.5f;
            float elapsed = 0f;
            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / totalDuration;
                float alpha = Mathf.Sin(t * Mathf.PI) * 0.5f;
                if (img != null) img.color = new Color(0.3f, 0.2f, 0.8f, alpha);
                yield return null;
            }

            int displaySeconds = Mathf.CeilToInt(rewindSeconds);
            for (int i = displaySeconds; i >= 1; i--)
            {
                if (countTmp != null)
                {
                    countTmp.text = i.ToString();
                    countTmp.transform.localScale = Vector3.one * 1.5f;
                }
                float countTime = 0.3f;
                float countElapsed = 0f;
                while (countElapsed < countTime)
                {
                    countElapsed += Time.unscaledDeltaTime;
                    float ct = countElapsed / countTime;
                    if (countTmp != null)
                    {
                        countTmp.transform.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one * 0.8f, ct);
                        countTmp.color = new Color(0.8f, 0.8f, 1f, 1f - ct * 0.3f);
                    }
                    yield return null;
                }
            }

            if (countTmp != null)
            {
                countTmp.text = "—— 时间重定向 ——";
                countTmp.fontSize = 60;
                countTmp.color = new Color(1f, 0.9f, 0.5f, 1f);
                countTmp.transform.localScale = Vector3.one;
            }

            float fadeTime = 0.5f;
            float fadeElapsed = 0f;
            while (fadeElapsed < fadeTime)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                float ft = fadeElapsed / fadeTime;
                if (img != null) img.color = new Color(0.3f, 0.2f, 0.8f, 0.5f * (1f - ft));
                if (countTmp != null) countTmp.color = new Color(1f, 0.9f, 0.5f, 1f - ft);
                yield return null;
            }
        }
        finally
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }

    public bool IsEmergencyProtocolUsed()
    {
        return _guardianRewindUsed;
    }

    public void ResetEmergencyProtocolForNewRun()
    {
        _guardianRewindUsed = false;
        _isGuardianRewindProcessing = false;
    }

    public void HealGuardian(int amount)
    {
        if (isGameOver) return;
        int oldHealth = playerHealth;
        playerHealth = Mathf.Min(playerHealth + amount, maxPlayerHealth);
        
        if (uiController != null)
        {
            uiController.UpdateLivesUI(playerHealth);
        }
    }

    public void AddDeploymentPoints(int amount)
    {
        if (DeploymentManager.Instance != null)
        {
            DeploymentManager.Instance.AddDP(amount);
        }
    }

    private void GameOver()
    {
        // 死亡时写回残血到跨场状态（HP 为 0）
        RogueRuntimeState.SetGuardianHp(0, maxPlayerHealth);

        // 通知 BattleEventManager 战斗结束
        if (BattleEventManager.Instance != null)
            BattleEventManager.Instance.OnBattleEnd();

        isGameOver = true;
        Time.timeScale = 0;

        if (GridSystem.Instance != null && GridSystem.Instance.defensePoint != null)
        {
            var dp = GridSystem.Instance.defensePoint;
            foreach (var statusUI in dp.GetComponentsInChildren<UnitStatusUI>(true))
            {
                if (statusUI != null && statusUI.gameObject.activeSelf)
                    statusUI.gameObject.SetActive(false);
            }
            foreach (var shooter in dp.GetComponentsInChildren<DefensePointShooter>(true))
            {
                if (shooter != null) shooter.enabled = false;
            }
        }
        if (guardPointHealthBarCanvas != null && guardPointHealthBarCanvas.activeSelf)
            guardPointHealthBarCanvas.SetActive(false);
        
        StartCoroutine(ShowDeathEffect());
    }
    
    private IEnumerator ShowDeathEffect()
    {
        Time.timeScale = 0f;
        
        CreateDeathOverlay();
        
        float duration = 1.5f;
        float elapsed = 0f;
        
        UnityEngine.UI.Image redImage = null;
        if (_deathOverlayObject != null)
        {
            redImage = _deathOverlayObject.GetComponent<UnityEngine.UI.Image>();
        }
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            
            if (redImage != null)
            {
                redImage.color = new Color(1f, 0f, 0f, t * 0.8f);
            }
            
            yield return null;
        }
        
        if (_deathTextObject != null)
        {
            _deathTextObject.SetActive(true);
            
            float textGrowDuration = 0.8f;
            float textElapsed = 0f;
            RectTransform textRt = _deathTextObject.GetComponent<RectTransform>();
            
            while (textElapsed < textGrowDuration)
            {
                textElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(textElapsed / textGrowDuration);
                float easeT = t * t * (3f - 2f * t);
                float scale = 0.5f + easeT * 0.5f;
                
                if (textRt != null)
                {
                    textRt.localScale = new Vector3(scale, scale, 1f);
                }
                
                yield return null;
            }
        }
        
        _isListeningForClick = true;
    }
    
    private void CreateDeathOverlay()
    {
        _isListeningForClick = true;

        if (deathOverlay != null)
        {
            _deathOverlayObject = Instantiate(deathOverlay);
            _deathOverlayObject.SetActive(true);
            return;
        }
        
        _deathCanvas = new GameObject("DeathCanvas").AddComponent<Canvas>();
        _deathCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _deathCanvas.sortingOrder = 9999;
        
        _deathOverlayObject = new GameObject("DeathOverlay", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        _deathOverlayObject.transform.SetParent(_deathCanvas.transform, false);
        
        RectTransform overlayRt = _deathOverlayObject.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        
        UnityEngine.UI.Image overlayImage = _deathOverlayObject.GetComponent<UnityEngine.UI.Image>();
        overlayImage.color = new Color(1f, 0f, 0f, 0f);
        overlayImage.raycastTarget = true;
        
        UnityEngine.UI.Button overlayBtn = _deathOverlayObject.AddComponent<UnityEngine.UI.Button>();
        overlayBtn.transition = UnityEngine.UI.Selectable.Transition.None;
        overlayBtn.onClick.AddListener(OnDeathOverlayClicked);
        
        _deathTextObject = new GameObject("DeathText", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        _deathTextObject.transform.SetParent(_deathOverlayObject.transform, false);
        
        RectTransform textRt = _deathTextObject.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.5f, 0.5f);
        textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.sizeDelta = new Vector2(800f, 300f);
        textRt.localScale = new Vector3(0.5f, 0.5f, 1f);
        
        TMPro.TextMeshProUGUI textTmp = _deathTextObject.GetComponent<TMPro.TextMeshProUGUI>();
        textTmp.text = "死亡";
        textTmp.fontSize = 240;
        textTmp.alignment = TMPro.TextAlignmentOptions.Center;
        textTmp.color = new Color(0.8f, 0f, 1f);
        textTmp.raycastTarget = false;
        
        _deathTextObject.SetActive(false);
    }
    
    private void OnDeathOverlayClicked()
    {
        HandleDeathClick();
    }

    private void Update()
    {
        if (_isListeningForClick && (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)))
        {
            HandleDeathClick();
        }
    }
    
    private void HandleDeathClick()
    {
        _isListeningForClick = false;

        Time.timeScale = 1f;

        if (_deathCanvas != null)
        {
            Destroy(_deathCanvas.gameObject);
        }
        
        VideoSceneLoader.LoadScene(SceneNames.RogueResult);
    }
}
