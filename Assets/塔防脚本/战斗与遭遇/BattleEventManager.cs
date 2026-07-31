using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 局内随机事件管理器。
/// 挂在战斗场景的 Canvas 上，战斗中按概率随机触发事件弹窗。
/// 玩家必须在限时内做出选择，否则默认选第一个选项。
/// </summary>
public class BattleEventManager : MonoBehaviour
{
    public static BattleEventManager Instance { get; private set; }

    [Header("触发配置")]
    [Tooltip("每隔多少秒检查一次是否触发随机事件")]
    [SerializeField] private float checkInterval = 30f;
    [Tooltip("每场战斗最多触发几次事件")]
    [SerializeField] private int maxEventsPerBattle = 2;
    [Tooltip("战斗开始后多久才允许触发事件")]
    [SerializeField] private float initialDelay = 45f;

    [Header("UI 绑定（按名称自动查找）")]
    [SerializeField] private GameObject eventPanelRoot;
    private TMP_Text _titleText;
    private TMP_Text _descriptionText;
    private TMP_Text _timerText;
    private Transform _optionsRoot;

    [Header("超时设置")]
    [Tooltip("玩家选择超时秒数，0=不超时")]
    [SerializeField] private float choiceTimeout = 15f;

    private List<BattleEventData> _eventDatabase;
    private bool _databaseLoaded;
    private int _eventsTriggeredThisBattle;
    private float _timeSinceLastCheck;
    private float _battleStartTime;
    private bool _battleStarted;
    private bool _eventActive;
    private float _choiceTimer;
    private int _currentRunEventCount;
    private Dictionary<string, int> _runEventCounts = new Dictionary<string, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (eventPanelRoot == null)
            eventPanelRoot = CreateDefaultPanel();

        if (eventPanelRoot != null)
            eventPanelRoot.SetActive(false);

        BindUI();
        LoadEventDatabase();
    }

    /// <summary> 当前是否有事件弹窗正在等待玩家选择。供 GameSpeedBoost 判断以保持暂停。 </summary>
    public bool IsEventActive => _eventActive;

    /// <summary> 运行时自动创建事件弹窗（无需在场景里手动摆放 UI）。 </summary>
    private GameObject CreateDefaultPanel()
    {
        var canvasGo = new GameObject("BattleEventCanvas", typeof(Canvas), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var panel = new GameObject("EventPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var pRt = panel.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.5f, 0.5f);
        pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.sizeDelta = new Vector2(720, 500);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.96f);

        var title = new GameObject("EventTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(panel.transform, false);
        var tRt = title.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f); tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f); tRt.sizeDelta = new Vector2(-32, 64);
        tRt.anchoredPosition = new Vector2(0, -24);
        var tTmp = title.GetComponent<TMP_Text>();
        tTmp.text = ""; tTmp.fontSize = 34; tTmp.alignment = TextAlignmentOptions.Center; tTmp.color = Color.white;

        var desc = new GameObject("EventDescription", typeof(RectTransform), typeof(TextMeshProUGUI));
        desc.transform.SetParent(panel.transform, false);
        var dRt = desc.GetComponent<RectTransform>();
        dRt.anchorMin = new Vector2(0f, 1f); dRt.anchorMax = new Vector2(1f, 1f);
        dRt.pivot = new Vector2(0.5f, 1f); dRt.sizeDelta = new Vector2(-48, 150);
        dRt.anchoredPosition = new Vector2(0, -100);
        var dTmp = desc.GetComponent<TMP_Text>();
        dTmp.text = ""; dTmp.fontSize = 22; dTmp.alignment = TextAlignmentOptions.Top; dTmp.color = new Color(0.9f, 0.9f, 0.95f);

        var timer = new GameObject("TimerText", typeof(RectTransform), typeof(TextMeshProUGUI));
        timer.transform.SetParent(panel.transform, false);
        var tmRt = timer.GetComponent<RectTransform>();
        tmRt.anchorMin = new Vector2(1f, 1f); tmRt.anchorMax = new Vector2(1f, 1f);
        tmRt.pivot = new Vector2(1f, 1f); tmRt.sizeDelta = new Vector2(220, 36);
        tmRt.anchoredPosition = new Vector2(-16, -24);
        var tmTmp = timer.GetComponent<TMP_Text>();
        tmTmp.text = ""; tmTmp.fontSize = 20; tmTmp.alignment = TextAlignmentOptions.Right; tmTmp.color = Color.yellow;

        var options = new GameObject("OptionsList", typeof(RectTransform), typeof(VerticalLayoutGroup));
        options.transform.SetParent(panel.transform, false);
        var oRt = options.GetComponent<RectTransform>();
        oRt.anchorMin = new Vector2(0f, 0f); oRt.anchorMax = new Vector2(1f, 1f);
        oRt.sizeDelta = new Vector2(-48, -280);
        oRt.anchoredPosition = new Vector2(0, 130);
        var vlg = options.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 12; vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.padding = new RectOffset(16, 16, 16, 16);

        return canvasGo;
    }

    private void BindUI()
    {
        if (eventPanelRoot == null)
            eventPanelRoot = gameObject;

        _titleText = FindTmpInChildren(eventPanelRoot.transform, "EventTitle");
        _descriptionText = FindTmpInChildren(eventPanelRoot.transform, "EventDescription");
        _timerText = FindTmpInChildren(eventPanelRoot.transform, "TimerText");

        var optionsGo = eventPanelRoot.transform.Find("OptionsList");
        if (optionsGo != null)
            _optionsRoot = optionsGo;
    }

    private TMP_Text FindTmpInChildren(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t == null)
        {
            // 递归查找
            foreach (Transform child in parent)
            {
                var found = FindTmpInChildren(child, name);
                if (found != null) return found;
            }
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<TMP_Text>() : null;
        }
        return t.GetComponent<TMP_Text>();
    }

    #region Database

    private void LoadEventDatabase()
    {
        if (_databaseLoaded) return;
        _databaseLoaded = true;
        _eventDatabase = new List<BattleEventData>();

        var allEvents = Resources.LoadAll<BattleEventData>("BattleEvents");
        foreach (var evt in allEvents)
        {
            if (evt != null && !string.IsNullOrEmpty(evt.eventId))
                _eventDatabase.Add(evt);
        }

        Debug.Log($"[BattleEventManager] 加载了 {_eventDatabase.Count} 个局内随机事件");
    }

    #endregion

    #region Battle Lifecycle

    /// <summary> 战斗开始时由 GameManager 调用。 </summary>
    public void OnBattleStart()
    {
        _battleStarted = true;
        _battleStartTime = Time.time;
        _eventsTriggeredThisBattle = 0;
        _timeSinceLastCheck = 0f;
        _eventActive = false;
    }

    /// <summary> 战斗结束时调用。 </summary>
    public void OnBattleEnd()
    {
        _battleStarted = false;
        if (_eventActive)
            ForceCloseEvent();
    }

    private void Update()
    {
        if (!_battleStarted || _eventActive) return;

        // 初始延迟
        if (Time.time - _battleStartTime < initialDelay) return;

        // 已达本场上限
        if (_eventsTriggeredThisBattle >= maxEventsPerBattle) return;

        _timeSinceLastCheck += Time.deltaTime;
        if (_timeSinceLastCheck >= checkInterval)
        {
            _timeSinceLastCheck = 0f;
            TryTriggerEvent();
        }
    }

    #endregion

    #region Event Triggering

    private void TryTriggerEvent()
    {
        var candidates = new List<BattleEventData>();
        int currentStage = RogueRuntimeState.CurrentStage;

        foreach (var evt in _eventDatabase)
        {
            if (currentStage < evt.minStage) continue;
            if (evt.requireGuardianHpBelow > 0)
            {
                // 需要读取守护点当前HP（通过GameManager）
                if (GameManager.Instance != null &&
                    GameManager.Instance.playerHealth >= evt.requireGuardianHpBelow)
                    continue;
            }
            // 每局最大次数限制
            if (evt.maxPerRun > 0)
            {
                _runEventCounts.TryGetValue(evt.eventId, out int count);
                if (count >= evt.maxPerRun) continue;
            }
            candidates.Add(evt);
        }

        if (candidates.Count == 0) return;

        // 随机选一个，按概率判定
        var chosen = candidates[Random.Range(0, candidates.Count)];
        if (Random.value < chosen.triggerChance)
        {
            TriggerEvent(chosen);
            _eventsTriggeredThisBattle++;
            _runEventCounts.TryGetValue(chosen.eventId, out int c);
            _runEventCounts[chosen.eventId] = c + 1;
        }
    }

    private void TriggerEvent(BattleEventData evt)
    {
        _eventActive = true;
        Time.timeScale = 0f;

        if (eventPanelRoot != null)
            eventPanelRoot.SetActive(true);

        if (_titleText != null)
            _titleText.text = evt.title;
        if (_descriptionText != null)
            _descriptionText.text = evt.description;

        // 清除旧选项
        if (_optionsRoot != null)
        {
            for (int i = _optionsRoot.childCount - 1; i >= 0; i--)
                Destroy(_optionsRoot.GetChild(i).gameObject);
        }

        // 生成选项按钮
        if (_optionsRoot != null && evt.options != null)
        {
            for (int i = 0; i < evt.options.Count; i++)
            {
                int idx = i;
                var opt = evt.options[i];
                CreateOptionButton(opt, () => OnOptionChosen(opt, idx));
            }
        }

        _choiceTimer = choiceTimeout;
        if (choiceTimeout > 0)
            StartCoroutine(TimeoutRoutine());
    }

    private void CreateOptionButton(BattleEventOption option, System.Action onClick)
    {
        var go = new GameObject("OptionBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_optionsRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(550, 75);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.35f, 1f);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(12, 0);
        trt.offsetMax = new Vector2(-12, 0);
        var tmp = textGo.GetComponent<TMP_Text>();
        tmp.text = $"【{option.buttonText}】{option.description}";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 20;
        tmp.color = Color.white;

        go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
    }

    private IEnumerator TimeoutRoutine()
    {
        while (_choiceTimer > 0 && _eventActive)
        {
            if (_timerText != null)
                _timerText.text = $"剩余时间: {Mathf.CeilToInt(_choiceTimer)}秒";
            yield return new WaitForSecondsRealtime(1f);
            _choiceTimer -= 1f;
        }

        if (_eventActive)
        {
            // 超时：自动选第一个选项
            if (_optionsRoot != null && _optionsRoot.childCount > 0)
            {
                var firstBtn = _optionsRoot.GetChild(0).GetComponent<Button>();
                if (firstBtn != null)
                    firstBtn.onClick.Invoke();
            }
            else
            {
                ForceCloseEvent();
            }
        }
    }

    private void OnOptionChosen(BattleEventOption option, int optionIndex)
    {
        if (!_eventActive) return;
        _eventActive = false;
        StopAllCoroutines();
        ApplyEffect(option.effect);
        ForceCloseEvent();
    }

    private void ForceCloseEvent()
    {
        _eventActive = false;
        if (eventPanelRoot != null)
            eventPanelRoot.SetActive(false);
        Time.timeScale = 1f;
    }

    #endregion

    #region Effect Application

    private void ApplyEffect(BattleEventOptionEffect eff)
    {
        if (eff == null) return;

        // DP
        if (eff.dpCost > 0 && DeploymentManager.Instance != null)
        {
            DeploymentManager.Instance.currentDP = Mathf.Max(0,
                DeploymentManager.Instance.currentDP - eff.dpCost);
        }
        if (eff.dpGain > 0 && DeploymentManager.Instance != null)
        {
            DeploymentManager.Instance.AddDP(eff.dpGain);
        }

        // 金币
        if (eff.goldCost > 0)
            RogueRuntimeState.TryConsumeRunGold(eff.goldCost);

        // 守护点
        if (eff.guardianDamage > 0 && GameManager.Instance != null)
            GameManager.Instance.TakeDamage(eff.guardianDamage);
        if (eff.guardianHeal > 0 && GameManager.Instance != null)
            GameManager.Instance.HealGuardian(eff.guardianHeal);

        // 全体干员回复
        if (eff.allOperatorsHealPercent > 0)
        {
            var allOps = FindObjectsByType<OperatorUnit>(FindObjectsSortMode.None);
            foreach (var op in allOps)
            {
                if (op != null && op.data != null)
                {
                    int heal = Mathf.RoundToInt(op.data.maxHealth * eff.allOperatorsHealPercent / 100f);
                    op.Heal(heal);
                }
            }
        }

        // 冻结敌人
        if (eff.freezeAllEnemies > 0)
        {
            var allEnemies = FindObjectsByType<Enemy2>(FindObjectsSortMode.None);
            foreach (var enemy in allEnemies)
            {
                if (enemy != null && !enemy.IsDead())
                    enemy.ApplyFreeze(Mathf.RoundToInt(eff.freezeAllEnemies));
            }
        }

        // 伤害全场敌人
        if (eff.damageAllEnemies > 0)
        {
            var allEnemies = FindObjectsByType<Enemy2>(FindObjectsSortMode.None);
            foreach (var enemy in allEnemies)
            {
                if (enemy != null && !enemy.IsDead())
                    enemy.TakeDamage(eff.damageAllEnemies, true);
            }
        }

        // 击杀随机敌人
        if (eff.killRandomEnemies > 0)
        {
            var enemies = new List<Enemy2>(FindObjectsByType<Enemy2>(FindObjectsSortMode.None));
            enemies.RemoveAll(e => e == null || e.IsDead());
            Shuffle(enemies);
            int killed = 0;
            foreach (var enemy in enemies)
            {
                if (killed >= eff.killRandomEnemies) break;
                if (enemy != null && !enemy.IsDead())
                {
                    // 造成巨额伤害来"击杀"
                    enemy.TakeDamage(enemy.GetCurrentHealth() * 10, true);
                    killed++;
                }
            }
        }

        // 临时战斗buff
        if (eff.tempAttackPercent > 0)
            RogueRuntimeState.AddBattleTempAttackPercent(eff.tempAttackPercent);
        if (eff.tempAttackSpeedPercent > 0)
            RogueRuntimeState.AddBattleTempAttackSpeedPercent(eff.tempAttackSpeedPercent);

        // 诅咒
        if (eff.applyRandomCurse)
        {
            var curse = CurseManager.GetRandomCurse();
            if (curse != null)
                CurseManager.ApplyCurse(curse);
        }

        // 随机卡
        if (!string.IsNullOrEmpty(eff.gainRandomCardRarity))
        {
            var cards = Resources.LoadAll<TalentCardData>("TalentCards");
            var candidates = new List<TalentCardData>();
            foreach (var card in cards)
            {
                if (card == null || string.IsNullOrEmpty(card.cardId)) continue;
                if (card.isCurse) continue;
                if (RogueRuntimeState.IsCardOwned(card.cardId)) continue;
                if (eff.gainRandomCardRarity == "Any") candidates.Add(card);
                else if (eff.gainRandomCardRarity == "Common" && card.rarity == TalentCardRarity.Common) candidates.Add(card);
                else if (eff.gainRandomCardRarity == "Advanced" && card.rarity == TalentCardRarity.Advanced) candidates.Add(card);
                else if (eff.gainRandomCardRarity == "Rare" && card.rarity == TalentCardRarity.Rare) candidates.Add(card);
                else if (eff.gainRandomCardRarity == "Legendary" && card.rarity == TalentCardRarity.Legendary) candidates.Add(card);
            }
            if (candidates.Count > 0)
            {
                var picked = candidates[Random.Range(0, candidates.Count)];
                RogueRuntimeState.TryPickTalentCard(picked);
            }
        }

        if (!string.IsNullOrEmpty(eff.gainSpecificCardId))
        {
            var card = TalentEffectApplier.GetCardById(eff.gainSpecificCardId);
            if (card != null)
                RogueRuntimeState.TryPickTalentCard(card);
        }

        // 召唤友方NPC（占位：实际效果待 GameManager 接入）
        if (eff.spawnAlly > 0 && GameManager.Instance != null)
        {
            Debug.Log("[BattleEventManager] 召唤友方NPC（功能待接入具体NPC生成系统）");
        }

        // 额外刷敌人（占位）
        if (eff.spawnExtraEnemies > 0 && GameManager.Instance != null)
        {
            Debug.Log($"[BattleEventManager] 额外刷出 {eff.spawnExtraEnemies} 个敌人（功能待接入Spawner系统）");
        }

        Debug.Log("[BattleEventManager] 事件效果已应用");
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    #endregion

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
