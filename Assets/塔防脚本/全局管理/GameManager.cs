using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("")]
    [Tooltip("守护点初始生命值，可在 Inspector 中自定义")]
    public int playerHealth = 5;
    public bool isGameOver = false;
    
    [Header("UI")]
    public UIController uiController;

    private bool _isMidGameDropProcessing = false;

    [Header("守护点血条（可选）")]
    [Tooltip("若血条画布不是守护点的子物体，拖入这里；GameOver 时会自动隐藏")]
    public GameObject guardPointHealthBarCanvas;

    [Header("死亡特效")]
    [Tooltip("死亡时的红色全屏覆盖层，不填则自动创建")]
    public GameObject deathOverlay;
    private Canvas _deathCanvas;
    private GameObject _deathOverlayObject;
    private GameObject _deathTextObject;
    private bool _isDeathTransitionComplete = false;
    private bool _isListeningForClick = false;

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
    }

    public void Start()
    {
        playerHealth += TalentEffectApplier.GetGuardianHpBonus();

        if (uiController != null)
        {
            uiController.UpdateLivesUI(playerHealth);
        }
    }
    
    public void TakeDamage(int damageAmount)
    {
        if (isGameOver) return;

        playerHealth -= damageAmount;

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
        SceneManager.LoadScene("RogueResult", LoadSceneMode.Additive);
    }

    public void ResetMidGameDropFlag()
    {
        _isMidGameDropProcessing = false;
    }

    public bool IsPurpleEnemyDropProcessing()
    {
        return _isMidGameDropProcessing;
    }

    private void GameOver()
    {
        Debug.Log("[GameManager] GameOver 被调用！");
        isGameOver = true;
        Time.timeScale = 0;
        Debug.Log("[GameManager] Time.timeScale 已设为 0");

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
        
        _isDeathTransitionComplete = true;
        
        _isListeningForClick = true;
        Debug.Log("[GameManager] 死亡动画完成，点击监听已启用");
    }
    
    private void CreateDeathOverlay()
    {
        Debug.Log("[GameManager] CreateDeathOverlay 开始创建死亡覆盖层");
        
        _isListeningForClick = true;
        Debug.Log("[GameManager] 点击监听已启用");
        
        if (deathOverlay != null)
        {
            Debug.Log("[GameManager] 使用自定义死亡覆盖层");
            _deathOverlayObject = Instantiate(deathOverlay);
            _deathOverlayObject.SetActive(true);
            return;
        }
        
        _deathCanvas = new GameObject("DeathCanvas").AddComponent<Canvas>();
        _deathCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _deathCanvas.sortingOrder = 9999;
        DontDestroyOnLoad(_deathCanvas.gameObject);
        
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
        Debug.Log("[GameManager] OnDeathOverlayClicked 被调用！");
        HandleDeathClick();
    }
    
    private void Update()
    {
        if (_isListeningForClick && (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)))
        {
            Debug.Log("[GameManager] Update 检测到点击！");
            HandleDeathClick();
        }
    }
    
    private void HandleDeathClick()
    {
        Debug.Log("[GameManager] HandleDeathClick 开始执行...");
        _isListeningForClick = false;
        
        Time.timeScale = 1f;
        Debug.Log("[GameManager] Time.timeScale 已设为 1");
        
        if (_deathCanvas != null)
        {
            Debug.Log("[GameManager] 销毁死亡画布");
            Destroy(_deathCanvas.gameObject);
        }
        
        Debug.Log("[GameManager] 开始加载 RogueResult 场景");
        VideoSceneLoader.LoadScene("RogueResult");
    }
}
