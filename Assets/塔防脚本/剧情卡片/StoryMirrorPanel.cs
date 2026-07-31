using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 「碎裂之镜」剧情碎片收藏面板
/// - 加载所有 StorySetData + StoryCardData
/// - 将碎片渲染到镜面上的对应位置
/// - 点击碎片 → 播放 Naninovel 脚本
/// - 显示每个套系的环形进度（已解锁/总数）
/// </summary>
public class StoryMirrorPanel : MonoBehaviour
{
    public static StoryMirrorPanel Instance { get; private set; }

    [Header("套系数据库")]
    public List<StorySetData> setDatabase = new List<StorySetData>();
    public List<StoryCardData> cardDatabase = new List<StoryCardData>();

    [Header("碎片预制体（需带 FragmentShard 组件）")]
    public GameObject shardPrefab;

    [Header("镜面容器")]
    public RectTransform mirrorRoot;
    [Tooltip("镜面纹理（圆形或方形，带裂纹视觉）")]
    public Image mirrorBackground;

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
    FragmentShard _zoomedShard;
    bool _isZoomed;
    float _rewardToastTimer;

    readonly Dictionary<string, StoryCardData> _cardLookup = new Dictionary<string, StoryCardData>();

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
        BuildCardLookup();
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

        // 脉冲动画由单个 FragmentShard.Update 自行处理
    }

    // ═══════════════════════════════════════════════
    //  构建
    // ═══════════════════════════════════════════════

    void BuildCardLookup()
    {
        _cardLookup.Clear();
        foreach (var card in cardDatabase)
        {
            if (card != null && !string.IsNullOrEmpty(card.cardId))
                _cardLookup[card.cardId] = card;
        }
        // 也尝试从 Resources 补充
        foreach (var card in Resources.LoadAll<StoryCardData>(""))
        {
            if (card != null && !string.IsNullOrEmpty(card.cardId) && !_cardLookup.ContainsKey(card.cardId))
                _cardLookup[card.cardId] = card;
        }
    }

    void BuildMirror()
    {
        // 清理旧碎片
        foreach (var s in _shards)
            if (s != null) Destroy(s.gameObject);
        _shards.Clear();

        Transform root = mirrorRoot != null ? mirrorRoot : transform;

        foreach (var set in setDatabase)
        {
            if (set == null || set.fragmentCardIds.Count == 0) continue;

            var cardsInSet = new List<StoryCardData>();
            foreach (var cid in set.fragmentCardIds)
            {
                if (_cardLookup.TryGetValue(cid, out var cd))
                    cardsInSet.Add(cd);
            }

            if (cardsInSet.Count == 0) continue;

            // 计算该套系中每个碎片在镜面上的位置
            var positions = CalculateShardPositions(set, cardsInSet.Count);

            for (int i = 0; i < cardsInSet.Count; i++)
            {
                var card = cardsInSet[i];
                var pos = positions[i];

                var go = shardPrefab != null
                    ? Instantiate(shardPrefab, root, false)
                    : CreateFallbackShard(root);

                var shard = go.GetComponent<FragmentShard>();
                if (shard == null)
                    shard = go.AddComponent<FragmentShard>();

                shard.Init(card, set, pos, i, OnShardClicked);
                _shards.Add(shard);
            }
        }
    }

    List<Vector2> CalculateShardPositions(StorySetData set, int count)
    {
        var result = new List<Vector2>();

        if (count <= 1)
        {
            // 单碎片居中
            result.Add(new Vector2(
                (set.mirrorCenter.x - 0.5f) * mirrorRoot.rect.width,
                (set.mirrorCenter.y - 0.5f) * mirrorRoot.rect.height
            ));
            return result;
        }

        float radius = set.mirrorRadius * Mathf.Min(mirrorRoot.rect.width, mirrorRoot.rect.height) * 0.5f;
        float startAngle = -set.mirrorArcDegrees * 0.5f;
        float angleStep = count > 1 ? set.mirrorArcDegrees / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            float angle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
            float x = Mathf.Sin(angle) * radius + (set.mirrorCenter.x - 0.5f) * mirrorRoot.rect.width;
            float y = Mathf.Cos(angle) * radius + (set.mirrorCenter.y - 0.5f) * mirrorRoot.rect.height;
            result.Add(new Vector2(x, y));
        }

        return result;
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

        foreach (var set in setDatabase)
        {
            if (set == null) continue;
            var (unlocked, total, keyUnlocked) = StoryCardUnlockState.GetSetProgress(set.setId);

            var go = setProgressPrefab != null
                ? Instantiate(setProgressPrefab, progressContainer, false)
                : null;

            if (go != null)
            {
                _progressBars.Add(go);

                var fillImage = FindChildImage(go.transform, "Fill");
                if (fillImage != null)
                    fillImage.fillAmount = total > 0 ? (float)unlocked / total : 0f;

                var progressText = FindChildTMP(go.transform, "ProgressText");
                if (progressText != null)
                    progressText.text = $"{unlocked}/{total}";

                var nameText = FindChildTMP(go.transform, "SetName");
                if (nameText != null)
                    nameText.text = set.displayName;
            }
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

        // 推近到该碎片
        if (!_isZoomed)
            ZoomToShard(shard);
        else if (_zoomedShard == shard)
            ZoomOut();

        // 播放剧情
        string script = string.IsNullOrEmpty(data.scriptName) ? "plot1" : data.scriptName;
        string label = data.labelName ?? "";

        NaninovelReturnRequest.Set(script, label);
        NaninovelReturnAutoPlayer.Ensure();

        // 在 Title 场景播放
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
        cardDatabase.Clear();
        foreach (var set in setDatabase)
        {
            if (set == null) continue;
            foreach (var cid in set.fragmentCardIds)
            {
                if (_cardLookup.TryGetValue(cid, out var card) && !cardDatabase.Contains(card))
                    cardDatabase.Add(card);
            }
        }
    }
}
