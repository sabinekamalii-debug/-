using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 「碎裂之镜」剧情碎片收藏面板
/// - 自动从 Resources/StoryCards/ 加载所有 StoryCardData
/// - 按 fragmentSetId 自动分组（无 setId 的按 category 分组）
/// - 网格布局：按 category 分区，每区按套系分行
/// - 点击碎片 → 播放 Naninovel 脚本
/// - 显示每个套系的进度（已解锁/总数）
/// </summary>
public class StoryMirrorPanel : MonoBehaviour
{
    public static StoryMirrorPanel Instance { get; private set; }

    [Header("碎片数据（自动加载）")]
    [Tooltip("可选：手动指定卡片列表。留空则自动从 Resources/StoryCards/ 加载全部")]
    public List<StoryCardData> cardDatabase = new List<StoryCardData>();

    [Tooltip("可选：套系覆盖列表。用于自定义套系显示名/图标，无需手动创建")]
    public List<StorySetData> setDatabase = new List<StorySetData>();

    [Header("碎片预制体（需带 FragmentShard 组件）")]
    public GameObject shardPrefab;

    [Header("镜面容器")]
    public RectTransform mirrorRoot;
    [Tooltip("镜面纹理（圆形或方形，带裂纹视觉）")]
    public Image mirrorBackground;

    [Header("自动布局参数")]
    [Tooltip("碎片水平间距")]
    public float shardSpacingX = 110f;
    [Tooltip("碎片垂直间距（行高，含标签）")]
    public float shardSpacingY = 150f;
    [Tooltip("套系标签与碎片的垂直偏移")]
    public float labelOffsetY = 35f;

    [Header("镜头推近")]
    [Tooltip("推近时的缩放倍率")]
    public float zoomScale = 2.5f;
    [Tooltip("推近动画时长")]
    public float zoomDuration = 0.4f;
    [Tooltip("未选中时的默认缩放")]
    public float defaultScale = 1f;

    [Header("套系进度条")]
    [Tooltip("套系进度条预制体（环形进度指示器）")]
    public GameObject setProgressPrefab;
    [Tooltip("进度条挂载容器")]
    public Transform progressContainer;

    [Header("返回按钮")]
    public Button returnButton;
    public GameObject heldCardsPanelRoot;

    [Header("奖励提示")]
    public TMP_Text rewardToastText;
    public float rewardToastDuration = 2f;

    // ── 内部状态 ──
    readonly List<FragmentShard> _shards = new List<FragmentShard>();
    readonly List<GameObject> _progressBars = new List<GameObject>();
    readonly List<GameObject> _groupLabels = new List<GameObject>();
    FragmentShard _zoomedShard;
    bool _isZoomed;
    float _rewardToastTimer;

    readonly Dictionary<string, StoryCardData> _cardLookup = new Dictionary<string, StoryCardData>();
    readonly Dictionary<string, StorySetData> _setOverrideLookup = new Dictionary<string, StorySetData>();
    readonly List<CardGroup> _groups = new List<CardGroup>();

    // ── 自动分组结构 ──
    class CardGroup
    {
        public string groupKey;
        public StoryCardCategory category;
        public List<StoryCardData> cards = new List<StoryCardData>();
        public string displayName;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        BuildSetOverrideLookup();
        BuildCardLookup();
        DiscoverGroups();
        BuildMirror();
        RefreshAll();

        StoryCardButton.OnRewardGranted += OnRewardGranted;
        if (returnButton != null)
            returnButton.onClick.AddListener(ReturnToHeldCards);
    }

    void OnDisable()
    {
        StoryCardButton.OnRewardGranted -= OnRewardGranted;
    }

    void Update()
    {
        if (_rewardToastTimer > 0f)
        {
            _rewardToastTimer -= Time.unscaledDeltaTime;
            if (_rewardToastTimer <= 0f && rewardToastText != null)
                rewardToastText.gameObject.SetActive(false);
        }
    }

    // ═══════════════════════════════════════════════
    //  数据加载 & 自动分组
    // ═══════════════════════════════════════════════

    void BuildSetOverrideLookup()
    {
        _setOverrideLookup.Clear();
        foreach (var set in setDatabase)
        {
            if (set != null && !string.IsNullOrEmpty(set.setId))
                _setOverrideLookup[set.setId] = set;
        }
    }

    void BuildCardLookup()
    {
        _cardLookup.Clear();

        // 先从手动指定的 cardDatabase 加载
        foreach (var card in cardDatabase)
        {
            if (card != null && !string.IsNullOrEmpty(card.cardId))
                _cardLookup[card.cardId] = card;
        }

        // 再从 Resources/StoryCards/ 自动补充
        foreach (var card in Resources.LoadAll<StoryCardData>("StoryCards"))
        {
            if (card != null && !string.IsNullOrEmpty(card.cardId) && !_cardLookup.ContainsKey(card.cardId))
                _cardLookup[card.cardId] = card;
        }
    }

    void DiscoverGroups()
    {
        _groups.Clear();
        var groupMap = new Dictionary<string, CardGroup>();

        foreach (var card in _cardLookup.Values)
        {
            if (card == null) continue;

            // 有 fragmentSetId 的用 setId 分组，没有的按 category 分组
            string key = string.IsNullOrEmpty(card.fragmentSetId)
                ? $"_{card.category}"
                : card.fragmentSetId;

            if (!groupMap.TryGetValue(key, out var group))
            {
                group = new CardGroup { groupKey = key, category = card.category };
                groupMap[key] = group;
            }

            group.cards.Add(card);
        }

        // 组内按 setIndex 排序
        foreach (var g in groupMap.Values)
            g.cards.Sort((a, b) => a.setIndex.CompareTo(b.setIndex));

        // 组间排序：先按 category（Main < Side < Character < Event），再按 groupKey
        _groups.AddRange(groupMap.Values);
        _groups.Sort((a, b) =>
        {
            int c = a.category.CompareTo(b.category);
            return c != 0 ? c : string.Compare(a.groupKey, b.groupKey, System.StringComparison.Ordinal);
        });

        // 派生显示名
        foreach (var g in _groups)
        {
            if (_setOverrideLookup.TryGetValue(g.groupKey, out var setData) && setData != null)
                g.displayName = setData.displayName;
            else
                g.displayName = DeriveDisplayName(g);
        }
    }

    static string DeriveDisplayName(CardGroup group)
    {
        // 无 fragmentSetId 的按 category 命名
        if (group.groupKey.StartsWith("_"))
        {
            return group.category switch
            {
                StoryCardCategory.Main => "主线碎片",
                StoryCardCategory.Side => "支线碎片",
                StoryCardCategory.Character => "角色碎片",
                StoryCardCategory.Event => "事件碎片",
                _ => "碎片"
            };
        }

        // 有 fragmentSetId 的：尝试从 key 派生可读名称
        // 如 "act1_main" → "第一幕 · 主线", "act1_spring" → "第一幕 · 残影之泉"
        var parts = group.groupKey.Split('_');
        if (parts.Length >= 2 && parts[0].StartsWith("act"))
        {
            string actName = parts[0] switch
            {
                "act1" => "第一幕",
                "act2" => "第二幕",
                "act3" => "第三幕",
                "act4" => "第四幕",
                "act5" => "第五幕",
                _ => parts[0]
            };
            string subName = parts[1] switch
            {
                "main" => "主线",
                "side" => "支线",
                "spring" => "残影之泉",
                _ => parts[1]
            };
            return $"{actName} · {subName}";
        }

        // 兜底：把下划线换成中点
        return group.groupKey.Replace("_", " · ");
    }

    // ═══════════════════════════════════════════════
    //  镜面构建（自动布局）
    // ═══════════════════════════════════════════════

    void BuildMirror()
    {
        foreach (var s in _shards)
            if (s != null) Destroy(s.gameObject);
        _shards.Clear();

        foreach (var lbl in _groupLabels)
            if (lbl != null) Destroy(lbl);
        _groupLabels.Clear();

        Transform root = mirrorRoot != null ? mirrorRoot : transform;
        float rootHeight = mirrorRoot != null ? mirrorRoot.rect.height : 800f;

        // 从顶部开始向下排列
        float yCursor = rootHeight * 0.5f - shardSpacingY * 0.5f;

        foreach (var group in _groups)
        {
            int count = group.cards.Count;
            if (count == 0) continue;

            // 创建套系标签
            var labelGo = CreateGroupLabel(root, group.displayName, new Vector2(0, yCursor + labelOffsetY));
            _groupLabels.Add(labelGo);

            // 居中排列碎片
            float rowWidth = (count - 1) * shardSpacingX;
            float startX = -rowWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var card = group.cards[i];
                Vector2 pos = new Vector2(startX + i * shardSpacingX, yCursor);

                var go = shardPrefab != null
                    ? Instantiate(shardPrefab, root, false)
                    : CreateFallbackShard(root);

                var shard = go.GetComponent<FragmentShard>();
                if (shard == null)
                    shard = go.AddComponent<FragmentShard>();

                shard.Init(card, null, pos, i, OnShardClicked);
                _shards.Add(shard);
            }

            yCursor -= shardSpacingY;
        }
    }

    GameObject CreateGroupLabel(Transform parent, string text, Vector2 pos)
    {
        var go = new GameObject($"Label_{text}");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(600, 40);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 26;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.9f, 0.85f, 0.6f, 0.9f);
        return go;
    }

    GameObject CreateFallbackShard(Transform parent)
    {
        var go = new GameObject("Shard_Fallback");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(80f, 100f);
        var img = go.AddComponent<Image>();
        img.color = Color.gray;
        return go;
    }

    // ═══════════════════════════════════════════════
    //  套系进度条
    // ═══════════════════════════════════════════════

    void BuildSetProgressBars()
    {
        foreach (var pb in _progressBars)
            if (pb != null) Destroy(pb);
        _progressBars.Clear();

        if (progressContainer == null) return;

        foreach (var group in _groups)
        {
            // 直接从组内卡片计算进度，不依赖 StoryCardUnlockState 的 setId 查询
            int unlocked = 0;
            int total = 0;
            bool keyUnlocked = false;

            foreach (var card in group.cards)
            {
                if (card.isKeyFragment)
                    keyUnlocked = StoryCardUnlockState.IsUnlocked(card.cardId);
                else
                {
                    total++;
                    if (StoryCardUnlockState.IsUnlocked(card.cardId))
                        unlocked++;
                }
            }

            if (total == 0 && !keyUnlocked) continue;

            GameObject go;
            if (setProgressPrefab != null)
            {
                go = Instantiate(setProgressPrefab, progressContainer, false);

                var fillImage = FindChildImage(go.transform, "Fill");
                if (fillImage != null)
                    fillImage.fillAmount = total > 0 ? (float)unlocked / total : 0f;

                var progressText = FindChildTMP(go.transform, "ProgressText");
                if (progressText != null)
                    progressText.text = $"{unlocked}/{Mathf.Max(1, total)}";

                var nameText = FindChildTMP(go.transform, "SetName");
                if (nameText != null)
                    nameText.text = group.displayName;
            }
            else
            {
                // 文字回退
                go = new GameObject($"Progress_{group.displayName}");
                go.transform.SetParent(progressContainer, false);
                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(400, 30);
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 20;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.color = new Color(0.8f, 0.8f, 0.85f, 0.9f);
                tmp.text = $"{group.displayName}  {unlocked}/{Mathf.Max(1, total)}" + (keyUnlocked ? " ★" : "");
            }

            _progressBars.Add(go);
        }
    }

    // ═══════════════════════════════════════════════
    //  碎片点击 → 播放剧情
    // ═══════════════════════════════════════════════

    void OnShardClicked(FragmentShard shard)
    {
        if (shard == null || shard.CardData == null) return;

        var data = shard.CardData;
        if (!StoryCardUnlockState.IsUnlocked(data.cardId)) return;

        if (!_isZoomed)
            ZoomToShard(shard);
        else if (_zoomedShard == shard)
            ZoomOut();

        string script = string.IsNullOrEmpty(data.scriptName) ? "plot1" : data.scriptName;
        string label = data.labelName ?? "";

        NaninovelReturnRequest.Set(script, label);
        NaninovelReturnAutoPlayer.Ensure();

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Title")
        {
            VideoSceneLoader.LoadScene(SceneNames.Title);
        }
    }

    void ZoomToShard(FragmentShard shard)
    {
        if (_zoomedShard != null && _zoomedShard != shard)
            _zoomedShard.SetZoomed(false);

        _zoomedShard = shard;
        _isZoomed = true;
        shard.SetZoomed(true);
    }

    void ZoomOut()
    {
        if (_zoomedShard != null)
            _zoomedShard.SetZoomed(false);
        _zoomedShard = null;
        _isZoomed = false;
    }

    // ═══════════════════════════════════════════════
    //  刷新 & 返回
    // ═══════════════════════════════════════════════

    public void RefreshAll()
    {
        StoryCardUnlockState.RefreshCache();
        BuildCardLookup();
        DiscoverGroups();

        // 更新碎片状态
        foreach (var shard in _shards)
        {
            if (shard == null || shard.CardData == null) continue;
            var data = shard.CardData;
            bool unlocked = StoryCardUnlockState.IsUnlocked(data.cardId);
            bool viewed = unlocked && StoryCardUnlockState.IsViewed(data.cardId);
            shard.RefreshState(unlocked, viewed);
        }

        BuildSetProgressBars();
    }

    public void UnlockAndShow(string cardId)
    {
        StoryCardUnlockState.Unlock(cardId);
        RefreshAll();
    }

    void ReturnToHeldCards()
    {
        gameObject.SetActive(false);
        if (heldCardsPanelRoot != null)
            heldCardsPanelRoot.SetActive(true);
    }

    void OnRewardGranted(StoryCardData cardData, int reward)
    {
        if (rewardToastText != null && cardData != null)
        {
            rewardToastText.text = $"+{reward} 天赋点（{cardData.displayName}）";
            rewardToastText.gameObject.SetActive(true);
            _rewardToastTimer = rewardToastDuration;
        }
    }

    // ═══════════════════════════════════════════════
    //  辅助
    // ═══════════════════════════════════════════════

    static Image FindChildImage(Transform parent, string name)
    {
        var child = parent.Find(name);
        return child != null ? child.GetComponent<Image>() : null;
    }

    static TMP_Text FindChildTMP(Transform parent, string name)
    {
        var child = parent.Find(name);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    /// <summary>
    /// 将套系数据转换为面板可用的单个卡片（方便与旧 StoryCardPanel 共享 cardDatabase 引用）。
    /// </summary>
    public void SyncFromSetDatabase()
    {
        // 自动模式下无需手动同步，保留方法兼容外部调用
        cardDatabase.Clear();
        foreach (var card in _cardLookup.Values)
        {
            if (card != null)
                cardDatabase.Add(card);
        }
    }
}
