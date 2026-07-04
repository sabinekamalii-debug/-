using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MiniStoryCardPanel : MonoBehaviour
{
    public static MiniStoryCardPanel Instance { get; private set; }

    public RectTransform cardContainer;
    public Vector2 miniCardSize = new Vector2(45f, 80f);
    public float cardSpacing = 10f;
    public int cardsPerRow = 2;

    public bool showDefaultCardsOnStart = true;
    public int defaultCardCount = 2;

    public List<TalentCardData> allTalentCards = new List<TalentCardData>();

    private List<GameObject> _miniCards = new List<GameObject>();
    private List<TalentCardData> _availableCards = new List<TalentCardData>();

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
        Transform root = cardContainer != null ? cardContainer : transform;
        if (root != null)
        {
            root.SetAsLastSibling();
            EnsureAutoLayout(root);
            FixCanvasCameraMode(root);
        }

        LoadTalentCards();
        
        if (showDefaultCardsOnStart)
        {
            if (_availableCards.Count > 0)
            {
                for (int i = 0; i < defaultCardCount && i < _availableCards.Count; i++)
                {
                    CreateCardSimple(_availableCards[i], i);
                }
            }
        }
    }

    void FixCanvasCameraMode(Transform root)
    {
        var canvas = root.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            if (canvas.worldCamera == null)
            {
                canvas.worldCamera = Camera.main;
            }

            if (canvas.worldCamera != null)
            {
                var cam = canvas.worldCamera;
                var cullingMask = cam.cullingMask;
                int uiLayer = LayerMask.NameToLayer("UI");
                if (!((cullingMask & (1 << uiLayer)) != 0))
                {
                    cam.cullingMask |= (1 << uiLayer);
                }
            }
        }
    }

    List<Vector2> BuildPageSlots(RectTransform rootRect, int count)
    {
        var result = new List<Vector2>(count);
        float cardW = Mathf.Max(45f, miniCardSize.x);
        float cardH = Mathf.Max(80f, miniCardSize.y);

        const float spacingX = 10f;
        const float spacingY = 10f;
        int cols = Mathf.Min(4, Mathf.Max(1, count));
        int rows = count > 4 ? 2 : 1;
        float totalW = cols * cardW + (cols - 1) * spacingX;
        float startX = -totalW * 0.5f + cardW * 0.5f;

        float startY = 0f;
        for (int i = 0; i < count; i++)
        {
            int row = i / 4;
            int col = i % 4;
            float x = startX + col * (cardW + spacingX);
            float y = startY - row * (cardH + spacingY);
            result.Add(new Vector2(x, y));
        }
        return result;
    }


    void EnsureAutoLayout(Transform root)
    {
        var grid = root.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = root.gameObject.AddComponent<GridLayoutGroup>();
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
        }
        grid.cellSize = miniCardSize;
        grid.spacing = new Vector2(cardSpacing, cardSpacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cardsPerRow;
    }

    void LoadTalentCards()
    {
        _availableCards.Clear();
        
        if (allTalentCards != null && allTalentCards.Count > 0)
        {
            foreach (var card in allTalentCards)
            {
                if (card != null)
                {
                    _availableCards.Add(card);
                }
            }
        }
        else
        {
            TalentCardData[] cards = Resources.LoadAll<TalentCardData>("TalentCards");
            foreach (var card in cards)
            {
                if (card != null)
                {
                    _availableCards.Add(card);
                }
            }
        }
    }

    void CreateCardSimple(TalentCardData cardData, int index)
    {
        if (cardContainer == null)
        {
            return;
        }

        GameObject cardGo = new GameObject("MiniCard_" + cardData.displayName);
        cardGo.layer = LayerMask.NameToLayer("UI");
        cardGo.transform.SetParent(cardContainer, false);
        cardGo.transform.SetAsLastSibling();

        RectTransform rect = cardGo.AddComponent<RectTransform>();
        rect.sizeDelta = miniCardSize;

        Image img = cardGo.AddComponent<Image>();
        
        if (cardData.cardBack != null)
        {
            img.sprite = cardData.cardBack;
            img.color = Color.white;
        }
        else
        {
            img.color = Color.yellow;
        }
        
        img.raycastTarget = false;
        img.maskable = true;

        cardGo.SetActive(true);
        img.enabled = true;
        img.gameObject.SetActive(true);

        _miniCards.Add(cardGo);
    }

    public void AddMiniCard(TalentCardData cardData)
    {
        CreateCardSimple(cardData, _miniCards.Count);
    }

    public void ClearAllCards()
    {
        foreach (var card in _miniCards)
        {
            if (card != null)
            {
                Destroy(card);
            }
        }
        _miniCards.Clear();
    }
}
