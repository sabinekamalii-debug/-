using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 随机事件场景控制器。
/// 加载 Resources/RandomEvents/ 下的事件数据，随机选取一个符合条件的事件展示。
/// 玩家选择选项后执行结果，返回关卡地图。
/// 
/// Naninovel 集成：支持进场/选项触发 Naninovel 剧本（播完自动返回事件场景）。
/// </summary>
public class RandomEventController : MonoBehaviour
{
    [Header("场景绑定（按名称自动查找）")]
    private TMP_Text _titleText;
    private TMP_Text _descriptionText;
    private TMP_Text _goldText;
    private Image _backgroundImage;
    private Transform _optionsRoot;

    [Header("选项按钮预制体（不填则代码生成）")]
    [SerializeField] private GameObject optionButtonPrefab;

    private RandomEventData _currentEvent;
    private List<RandomEventData> _availableEvents;
    private bool _resolved;
    private int _currentLevelNum;

    // ── Naninovel 返回状态（静态，跨场景保持）──
    // 注：进场触发剧情已移除，本组状态仅服务"选项触发剧情"流程
    //   s_returnedFromNani=true 时表示"选了某选项后跳 Naninovel 播剧本，播完回来"
    //   s_eventWasResolved 必为 true（选项已选）
    private static string s_pendingEventId;
    private static bool s_returnedFromNani;
    private static bool s_eventWasResolved;      // true = 选项已选，回来显示结果
    private static string s_pendingResultText;    // 选项结果文本（选后展示）
    private static string s_pendingResultLog;     // 结果执行的日志（ApplyPendingOutcomes 时生成）
    private static List<RandomEventOutcome> s_pendingOutcomes; // 待执行的结果（选项脚本触发时暂存）

    // Domain Reload 禁用时，每次 Enter Play Mode 手动重置全部静态状态，
    // 避免 Awake() 直接进入错误的"从 Naninovel 返回"分支。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetStaticStateOnPlaymodeEnter()
    {
        s_pendingEventId = null;
        s_returnedFromNani = false;
        s_eventWasResolved = false;
        s_pendingResultText = null;
        s_pendingResultLog = null;
        s_pendingOutcomes = null;
    }

    private void Awake()
    {
        RogueRuntimeState.InitIfNeeded();
        _currentLevelNum = RogueRuntimeState.CurrentStage;
        BindSceneObjects();

        // 从 Naninovel 剧本返回？恢复状态（仅在"选项触发剧本"流程下出现）
        if (s_returnedFromNani)
        {
            s_returnedFromNani = false;
            // 选项已选 → 执行暂存的结果 + 显示
            ApplyPendingOutcomes();
            ShowPendingResult();
            return;
        }

        LoadAvailableEvents();
        PickAndDisplayEvent();
    }

    #region UI Binding

    private void BindSceneObjects()
    {
        _titleText = FindTmp("EventTitle");
        _descriptionText = FindTmp("EventDescription");
        _goldText = FindTmp("GoldCount");
        _backgroundImage = FindImage("EventBackground");

        // 移除场景中的 Btn_Leave（奇遇关卡不允许直接离开）
        var leaveGo = GameObject.Find("Btn_Leave");
        if (leaveGo != null)
            Destroy(leaveGo);

        var optionsGo = GameObject.Find("OptionsList");
        if (optionsGo != null)
        {
            ClearChildren(optionsGo.transform);
            _optionsRoot = optionsGo.transform;
        }
    }

    private TMP_Text FindTmp(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<TMP_Text>() : null;
    }

    private Image FindImage(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<Image>() : null;
    }

    private void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    #endregion

    #region Event Selection

    private void LoadAvailableEvents()
    {
        _availableEvents = new List<RandomEventData>();
        var allEvents = Resources.LoadAll<RandomEventData>("RandomEvents");
        foreach (var evt in allEvents)
        {
            if (evt == null || string.IsNullOrEmpty(evt.eventId)) continue;
            if (evt.options == null || evt.options.Count < 2) continue;
            // 关卡范围筛选
            if (evt.minStage > 0 && _currentLevelNum < evt.minStage) continue;
            if (evt.maxStage > 0 && _currentLevelNum > evt.maxStage) continue;
            // 一次性事件：本局已遇过则跳过
            if (evt.oneShot && RogueRuntimeState.HasEncounteredEvent(evt.eventId)) continue;
            _availableEvents.Add(evt);
        }
    }

    private void PickAndDisplayEvent()
    {
        if (_availableEvents == null || _availableEvents.Count == 0)
        {
            DisplayFallbackEvent();
            return;
        }

        // 加权随机选一个
        int totalWeight = 0;
        foreach (var evt in _availableEvents) totalWeight += evt.weight;

        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;
        _currentEvent = _availableEvents[0];
        foreach (var evt in _availableEvents)
        {
            cumulative += evt.weight;
            if (roll < cumulative)
            {
                _currentEvent = evt;
                break;
            }
        }

        DisplayEvent(_currentEvent);
    }

    private void DisplayEvent(RandomEventData evt)
    {
        // 注：进场触发剧情已移除（曾基于 evt.playNaniOnEnter + evt.naniEntryScriptName 跳 Title）。
        //   仅保留"选项触发"路径：选项被点击后，若该选项配了 naniScriptName，再跳剧本。
        RenderEventUI(evt);
    }

    /// <summary> 纯 UI 渲染（不含 Naninovel 跳转判断） </summary>
    private void RenderEventUI(RandomEventData evt)
    {
        if (_titleText != null)
            _titleText.text = evt.title;
        if (_descriptionText != null)
            _descriptionText.text = evt.description;
        if (_backgroundImage != null && evt.backgroundImage != null)
            _backgroundImage.sprite = evt.backgroundImage;
        if (_goldText != null)
            _goldText.text = $"金币: {RogueRuntimeState.RunGold}";

        // 生成选项按钮
        if (_optionsRoot != null && evt.options != null)
        {
            foreach (var option in evt.options)
            {
                CreateOptionButton(option);
            }
        }
    }

    private void DisplayFallbackEvent()
    {
        if (_titleText != null)
            _titleText.text = "平静的一关";
        if (_descriptionText != null)
            _descriptionText.text = "这里什么都没有发生。你稍作休整后继续前进。";
        _resolved = true;

        if (_optionsRoot != null)
        {
            var leaveBtn = CreateSimpleButton("继续前进", () => ReturnToMap());
        }
    }

    private void CreateOptionButton(RandomEventOption option)
    {
        GameObject btnGo;
        if (optionButtonPrefab != null)
        {
            btnGo = Instantiate(optionButtonPrefab, _optionsRoot);
        }
        else
        {
            btnGo = new GameObject("OptionBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(_optionsRoot, false);
            var rt = btnGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600, 80);
            var btnImg = btnGo.GetComponent<Image>();
            btnImg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(btnGo.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(16, 0);
            textRt.offsetMax = new Vector2(-16, 0);
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 22;
            tmp.color = Color.white;
        }

        var buttonText = btnGo.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            string text = option.buttonText;
            if (option.goldCost >= 99999)
                text += $" (消耗全部金币)";
            else if (option.goldCost > 0)
                text += $" (-{option.goldCost}金币)";
            buttonText.text = text;
        }

        var btn = btnGo.GetComponent<Button>();
        if (btn != null)
        {
            var capturedOption = option;
            btn.onClick.AddListener(() => OnOptionChosen(capturedOption));
        }

        // 检查条件是否满足（99999=全部金币的特殊处理）
        bool canChoose = true;
        if (option.goldCost >= 99999)
        {
            if (RogueRuntimeState.RunGold <= 0) canChoose = false;
        }
        else if (option.goldCost > 0 && RogueRuntimeState.RunGold < option.goldCost)
        {
            canChoose = false;
        }
        if (!string.IsNullOrEmpty(option.requiredCardId) && !RogueRuntimeState.IsCardOwned(option.requiredCardId))
            canChoose = false;
        if (btn != null)
            btn.interactable = canChoose;
    }

    private GameObject CreateSimpleButton(string text, System.Action onClick)
    {
        var go = new GameObject("SimpleBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_optionsRoot, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 80);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.4f, 1f);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 24;
        tmp.color = Color.white;

        go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
        return go;
    }

    #endregion

    #region Outcome Resolution

    private void OnOptionChosen(RandomEventOption option)
    {
        if (_resolved) return;
        _resolved = true;

        // 扣金币（99999 表示"全部金币"）
        if (option.goldCost >= 99999)
        {
            RogueRuntimeState.TryConsumeRunGold(RogueRuntimeState.RunGold);
        }
        else if (option.goldCost > 0)
        {
            RogueRuntimeState.TryConsumeRunGold(option.goldCost);
        }

        // 标记一次性事件
        if (_currentEvent != null && _currentEvent.oneShot)
            RogueRuntimeState.MarkEventEncountered(_currentEvent.eventId);

        // ── 选项有 Naninovel 剧本 → 先存结果，跳 Title 播剧情 ──
        if (!string.IsNullOrEmpty(option.naniScriptName))
        {
            s_pendingEventId = _currentEvent != null ? _currentEvent.eventId : "";
            s_returnedFromNani = true;
            s_eventWasResolved = true;
            s_pendingResultText = $"【{option.buttonText}】\n\n{option.resultText}";
            s_pendingOutcomes = new List<RandomEventOutcome>(option.outcomes ?? new List<RandomEventOutcome>());

            JumpToNaninovel(option.naniScriptName, option.naniLabel);
            return;
        }

        // ── 普通模式：执行结果 + 显示 ──
        string resultLog = "";
        foreach (var outcome in option.outcomes)
        {
            resultLog += ResolveOutcome(outcome) + "\n";
        }

        ShowOptionResult(option, resultLog);
    }

    /// <summary> 显示选项结果（普通模式） </summary>
    private void ShowOptionResult(RandomEventOption option, string resultLog)
    {
        if (_descriptionText != null)
            _descriptionText.text = $"【{option.buttonText}】\n\n{option.resultText}\n\n{resultLog}";

        if (_optionsRoot != null)
            ClearChildren(_optionsRoot);

        if (_optionsRoot != null)
            CreateSimpleButton("继续前进", () => ReturnToMap());

        if (_goldText != null)
            _goldText.text = $"金币: {RogueRuntimeState.RunGold}";
    }

    /// <summary> 从 Naninovel 回来后执行暂存的结果 </summary>
    private void ApplyPendingOutcomes()
    {
        if (s_pendingOutcomes == null || s_pendingOutcomes.Count == 0)
        {
            s_pendingResultLog = "";
            return;
        }
        s_pendingResultLog = "";
        foreach (var outcome in s_pendingOutcomes)
        {
            s_pendingResultLog += ResolveOutcome(outcome) + "\n";
        }
        s_pendingOutcomes = null;
    }

    /// <summary> 从 Naninovel 回来后显示结果 </summary>
    private void ShowPendingResult()
    {
        if (_descriptionText != null)
            _descriptionText.text = $"{s_pendingResultText}\n\n{s_pendingResultLog}";

        if (_optionsRoot != null)
            ClearChildren(_optionsRoot);

        if (_optionsRoot != null)
            CreateSimpleButton("继续前进", () => ReturnToMap());

        if (_goldText != null)
            _goldText.text = $"金币: {RogueRuntimeState.RunGold}";

        s_pendingResultText = null;
        s_pendingResultLog = null;
    }

    /// <summary> 跳转到 Naninovel 剧本播放 </summary>
    private void JumpToNaninovel(string scriptName, string label)
    {
        string currentScene = SceneManager.GetActiveScene().name;
        NaninovelReturnRequest.Set(scriptName, label ?? "", currentScene);
        NaninovelReturnAutoPlayer.Ensure();
        VideoSceneLoader.LoadScene(SceneNames.Title);
    }

    private string ResolveOutcome(RandomEventOutcome outcome)
    {
        if (outcome.outcomeType == RandomEventOutcomeType.None)
            return "";

        // 概率判定
        bool success = outcome.successChance >= 1f || Random.value < outcome.successChance;
        RandomEventOutcomeType effectiveType = success ? outcome.outcomeType : outcome.failureOutcome;
        int effectiveValue = success ? outcome.value : outcome.failureValue;

        if (effectiveType == RandomEventOutcomeType.None)
            return success ? ApplySingleOutcome(outcome.outcomeType, outcome.value, outcome.stringParam)
                           : "";

        return ApplySingleOutcome(effectiveType, effectiveValue, outcome.stringParam);
    }

    private string ApplySingleOutcome(RandomEventOutcomeType type, int value, string stringParam)
    {
        switch (type)
        {
            case RandomEventOutcomeType.GainGold:
                RogueRuntimeState.AddRunGold(value);
                return $"获得 {value} 金币";

            case RandomEventOutcomeType.LoseGold:
                int lost = Mathf.Min(value, RogueRuntimeState.RunGold);
                RogueRuntimeState.TryConsumeRunGold(lost);
                return $"失去 {lost} 金币";

            case RandomEventOutcomeType.GainTalentPoints:
                TalentTreeState.AddTalentPoints(value);
                return $"获得 {value} 天赋点";

            case RandomEventOutcomeType.GainCardDraw:
                RogueRuntimeState.AddCardDraw(value);
                return $"获得 {value} 次抽卡机会";

            case RandomEventOutcomeType.AddRandomCard:
                {
                    var card = PickRandomCard(stringParam);
                    if (card != null)
                    {
                        RogueRuntimeState.TryPickTalentCard(card);
                        return $"获得天赋卡: {card.displayName}";
                    }
                    return "";
                }

            case RandomEventOutcomeType.AddSpecificCard:
                {
                    var specificCard = TalentEffectApplier.GetCardById(stringParam);
                    if (specificCard != null)
                    {
                        RogueRuntimeState.TryPickTalentCard(specificCard);
                        return $"获得天赋卡: {specificCard.displayName}";
                    }
                    return "(未找到指定卡牌)";
                }

            case RandomEventOutcomeType.RemoveRandomCardToGold:
                {
                    int gold = RogueRuntimeState.ConvertRandomOwnedCardToGold();
                    if (gold > 0) return $"随机出售一张卡，获得 {gold} 金币";
                    return "(没有可出售的卡)";
                }

            case RandomEventOutcomeType.HealGuardian:
                RogueRuntimeState.HealGuardian(value);
                return $"守护点回复 {value} 点生命";

            case RandomEventOutcomeType.DamageGuardian:
                RogueRuntimeState.DamageGuardian(value);
                return $"守护点受到 {value} 点伤害";

            case RandomEventOutcomeType.AddCurse:
                {
                    var curse = CurseManager.GetRandomCurse();
                    if (curse != null)
                    {
                        CurseManager.ApplyCurse(curse);
                        return $"被诅咒: {curse.displayName}";
                    }
                    return "(无可用诅咒)";
                }

            case RandomEventOutcomeType.RemoveRandomCurse:
                {
                    string removed = CurseManager.RemoveRandomCurse();
                    if (!string.IsNullOrEmpty(removed))
                        return $"诅咒解除: {removed}";
                    return "(没有诅咒可解除)";
                }

            case RandomEventOutcomeType.EnemyBuffPercent:
                RogueRuntimeState.SetNextBattleEnemyModifier(value);
                return $"下场战斗敌人全属性 +{value}%";

            case RandomEventOutcomeType.EnemyDebuffPercent:
                RogueRuntimeState.SetNextBattleEnemyModifier(-value);
                return $"下场战斗敌人全属性 -{value}%";

            case RandomEventOutcomeType.NextBattleGoldBonus:
                RogueRuntimeState.SetNextBattleGoldBonus(value);
                return $"下场战斗金币 +{value}%";

            case RandomEventOutcomeType.GainReroll:
                RogueRuntimeState.GrantFreeReroll();
                return "获得 1 次免费重抽机会";

            case RandomEventOutcomeType.RevealMap:
                // 标记为已揭示，地图界面读取该标记
                RogueRuntimeState.SetMapRevealed(true);
                return "地图上的节点类型已全部揭示";

            case RandomEventOutcomeType.SkipNextBattle:
                RogueRuntimeState.GrantSkipBattle();
                return "获得 1 次跳过普通战斗的机会";

            case RandomEventOutcomeType.DuplicateRandomCard:
                {
                    string dupCard = RogueRuntimeState.DuplicateRandomOwnedCard();
                    if (!string.IsNullOrEmpty(dupCard))
                        return $"复制了一张已有卡";
                    return "(没有可复制的卡)";
                }

            case RandomEventOutcomeType.GainGuardianMaxHp:
                RogueRuntimeState.AddGuardianMaxHp(value);
                return $"守护点最大生命 +{value}";

            case RandomEventOutcomeType.LoseGuardianMaxHp:
                RogueRuntimeState.AddGuardianMaxHp(-value);
                return $"守护点最大生命 -{value}";

            default:
                return "";
        }
    }

    private TalentCardData PickRandomCard(string rarityFilter)
    {
        var allCards = Resources.LoadAll<TalentCardData>("TalentCards");
        var candidates = new List<TalentCardData>();

        foreach (var card in allCards)
        {
            if (card == null || string.IsNullOrEmpty(card.cardId)) continue;
            if (RogueRuntimeState.IsCardOwned(card.cardId)) continue;
            if (card.isGuardianRewindCard) continue;

            if (!string.IsNullOrEmpty(rarityFilter))
            {
                if (rarityFilter == "Common" && card.rarity == TalentCardRarity.Common) candidates.Add(card);
                else if (rarityFilter == "Advanced" && card.rarity == TalentCardRarity.Advanced) candidates.Add(card);
                else if (rarityFilter == "Rare" && card.rarity == TalentCardRarity.Rare) candidates.Add(card);
                else if (rarityFilter == "Legendary" && card.rarity == TalentCardRarity.Legendary) candidates.Add(card);
                else if (rarityFilter == "Any") candidates.Add(card);
            }
            else
            {
                candidates.Add(card);
            }
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    #endregion

    #region Navigation

    private void ReturnToMap()
    {
        LevelProgress.MarkCompleted($"level {_currentLevelNum}");
        VideoSceneLoader.LoadScene(SceneNames.Plot);
    }

    #endregion
}
