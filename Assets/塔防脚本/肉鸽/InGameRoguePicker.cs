using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class InGameRoguePicker : MonoBehaviour
{
    private static InGameRoguePicker _instance;
    private GameObject _panel;
    private List<Button> _cardButtons = new List<Button>();
    private TalentCardData[] _currentOffers = new TalentCardData[3];
    
    [Header("卡池")]
    [Tooltip("在此处拖拽你的天赋卡片数据")]
    public TalentCardData[] cardPool;
    
    private TalentCardData[] _cardPool;

    public static void ShowPicker()
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("InGameRoguePicker");
            _instance = go.AddComponent<InGameRoguePicker>();
            _instance.InitUI();
        }
        _instance.DoShow();
    }

    private void InitUI()
    {
        if (cardPool != null && cardPool.Length > 0)
        {
            _cardPool = cardPool;
        }
        else
        {
            _cardPool = Resources.LoadAll<TalentCardData>("TalentCards");
        }

        // 不去寻找场景中已有的 Canvas，防止错误地挂载到敌人头顶的 WorldSpace 血条画布上
        // 将 InGameRoguePicker 自身变成一个最高层级的全屏覆盖 UI
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // 确保展示在最上层
        
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        gameObject.AddComponent<GraphicRaycaster>();

        // 半透明深色背景遮罩，挡住背后的其他 UI 进行沉浸式选卡
        _panel = new GameObject("RoguePicker_Panel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(canvas.transform, false);
        var panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        var panelImg = _panel.GetComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.85f); // 较深的黑色半透明
        panelImg.raycastTarget = true; // 阻挡射线，让背后的东西点不到

        var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(_panel.transform, false);
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -80f);
        titleRect.sizeDelta = new Vector2(800f, 60f);
        var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "<color=#B540FF>精英掉落</color> - 抽取一项局内天赋";
        titleTmp.fontSize = 40;
        titleTmp.alignment = TextAlignmentOptions.Center;

        const float cardW = 320f;
        const float cardH = 480f;
        const float gap = 80f;
        const float cardSlotY = 0f;
        float startX = -(cardW + gap);

        for (int i = 0; i < 3; i++)
        {
            var slot = CreateCardSlot(_panel.transform, i, new Vector2(startX + i * (cardW + gap), cardSlotY), new Vector2(cardW, cardH));
            _cardButtons.Add(slot);
        }

        // 添加跳过按钮
        float skipY = -(cardH / 2f + 80f);
        var skipBtn = CreateButton(_panel.transform, "放弃抽取", new Vector2(0f, skipY));
        var skipRect = skipBtn.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(0.5f, 0.5f);
        skipRect.anchorMax = new Vector2(0.5f, 0.5f);
        skipRect.pivot = new Vector2(0.5f, 0.5f);
        skipRect.anchoredPosition = new Vector2(0f, skipY);
        skipRect.sizeDelta = new Vector2(240f, 60f);
        skipBtn.onClick.AddListener(OnSkipPick);

        _panel.SetActive(false);
    }

    private Button CreateCardSlot(Transform parent, int index, Vector2 pos, Vector2 size)
    {
        var go = new GameObject($"CardSlot_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        
        var img = go.GetComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);

        float iconH = size.x;
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        var iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(1f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(0f, iconH);
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.color = Color.gray;
        iconImg.preserveAspect = true;

        var descGo = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
        descGo.transform.SetParent(go.transform, false);
        var descRect = descGo.GetComponent<RectTransform>();
        descRect.anchorMin = Vector2.zero;
        descRect.anchorMax = Vector2.one;
        descRect.offsetMin = new Vector2(16f, 16f);
        descRect.offsetMax = new Vector2(-16f, -(iconH + 16f));
        var descTmp = descGo.GetComponent<TextMeshProUGUI>();
        descTmp.fontSize = 20;
        descTmp.alignment = TextAlignmentOptions.TopLeft;
        descTmp.color = Color.white;
        descTmp.enableWordWrapping = true;

        var btn = go.GetComponent<Button>();
        int capture = index;
        btn.onClick.AddListener(() => OnPickCard(capture));
        return btn;
    }

    private Button CreateButton(Transform parent, string txt, Vector2 pos)
    {
        var go = new GameObject("Button_" + txt, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        var btn = go.GetComponent<Button>();

        var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
        var tmp = txtGo.GetComponent<TextMeshProUGUI>();
        tmp.text = txt;
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        return btn;
    }

    private void DoShow()
    {
        PickRandomOffers();
        RefreshVisuals();
        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();
    }

    private void PickRandomOffers()
    {
        var available = new List<TalentCardData>();
        if (_cardPool != null)
        {
            foreach (var c in _cardPool)
            {
                if (c == null) continue;
                available.Add(c);
            }
        }

        for (int i = 0; i < 3; i++) _currentOffers[i] = null;

        if (available.Count == 0) return;

        // 简记：即使不够3张，这里允许重复抽同一种卡补充槽位
        for (int i = 0; i < 3; i++)
        {
            int idx = UnityEngine.Random.Range(0, available.Count);
            _currentOffers[i] = available[idx];
            if (available.Count >= 3)
                available.RemoveAt(idx);
        }
    }

    private void RefreshVisuals()
    {
        for (int i = 0; i < 3; i++)
        {
            var btn = _cardButtons[i];
            var card = _currentOffers[i];
            if (btn == null) continue;

            var rootImg = btn.GetComponent<Image>();

            if (card != null)
            {
                if (rootImg != null && card.cardFront != null)
                {
                    rootImg.sprite = card.cardFront;
                    rootImg.enabled = true;
                }

                btn.interactable = true;
            }
            else
            {
                if (rootImg != null)
                {
                    rootImg.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
                    rootImg.enabled = true;
                }
                btn.interactable = false;
            }
        }
    }

    private void OnPickCard(int index)
    {
        var card = _currentOffers[index];
        if (card != null)
        {
            // 因为现在系统未实装，只是做效果验证。未来在这里：
            // RogueRuntimeState.SelectedTalentCardIds.Add(card.cardId);
        }

        ResumeGame();
    }

    private void OnSkipPick()
    {
        ResumeGame();
    }

    private void ResumeGame()
    {
        Time.timeScale = 1f;
        _panel.SetActive(false);
    }
}
