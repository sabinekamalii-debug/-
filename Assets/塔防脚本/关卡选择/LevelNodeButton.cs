using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public enum LevelType
{
    NormalBattle,
    Shop,
    Elite,
    Boss,
    RandomEvent,
    Rest
}

/// <summary>
/// 地图上的关卡节点：点击进入对应关卡。根据 LevelProgress 显示已解锁/未解锁。
/// 物体名带数字即对应关卡：按钮1→level 1，按钮2→level 2，…，按钮16→level 16（一一对应）。
/// </summary>
[RequireComponent(typeof(Button))]
public class LevelNodeButton : MonoBehaviour
{
    string _sceneName;
    string _displayName;
    Sprite _normalSprite;
    int _levelNumber;

    [Header("未解锁时的表现")]
    [SerializeField] Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Header("已通关时的表现")]
    [Tooltip("已通关的关卡只变暗、不可再次挑战，不变成封锁样式")]
    [SerializeField] Color completedDimColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    [Header("可选绑定")]
    [SerializeField] Image iconImage;
    [SerializeField] TMP_Text nameText;
    [SerializeField] Image frameImage;

    [Header("关卡类型配置")]
    public LevelTypeConfig levelTypeConfig;

    Button _button;

    void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.AddListener(OnClick);
            // Button 在 interactable=false 时会用 disabledColor 染 Target Graphic，默认 alpha 约 0.5 会发虚，改为完全不透明
            var colors = _button.colors;
            colors.disabledColor = new Color(colors.disabledColor.r, colors.disabledColor.g, colors.disabledColor.b, 1f);
            _button.colors = colors;
        }

        if (nameText == null) nameText = GetComponentInChildren<TMP_Text>(true);
        if (iconImage == null) iconImage = GetComponent<Image>();
        if (iconImage == null) iconImage = GetComponentInChildren<Image>(true);
        if (iconImage != null) _normalSprite = iconImage.sprite;

        // 根据物体名决定显示数字和关卡：按钮5 / LevelNode_5 → 显示 "5"，跳转 "level 5"
        string num = GetNumberFromGameObjectName(gameObject.name);
        if (!string.IsNullOrEmpty(num))
        {
            _displayName = num;
            _sceneName = "level " + num;
            if (int.TryParse(num, out int n))
            {
                _levelNumber = n;
            }
        }
        else
        {
            _displayName = "?";
            _sceneName = "";
            _levelNumber = 0;
        }
    }

    LevelType GetLevelType(int levelNum)
    {
        return LevelRandomizer.GetLevelType(levelNum);
    }

    void Start()
    {
        RefreshLockState();
    }

    void RefreshLockState()
    {
        bool unlocked = LevelProgress.IsUnlocked(_sceneName);
        bool completed = LevelProgress.IsCompleted(_sceneName);

        // 已通关：不可再次挑战，仅变暗（不显示为封锁）
        bool canEnter = unlocked && !completed;
        if (_button != null)
            _button.interactable = canEnter;

        // 强制不透明（alpha=1），避免图标/按钮/文字发虚
        Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        // 获取关卡类型对应的图标
        Sprite typeSprite = GetTypeSprite();

        if (iconImage != null)
        {
            if (completed)
            {
                iconImage.sprite = typeSprite != null ? typeSprite : _normalSprite;
                iconImage.color = Opaque(completedDimColor);
            }
            else if (unlocked)
            {
                iconImage.sprite = typeSprite != null ? typeSprite : _normalSprite;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.sprite = typeSprite != null ? typeSprite : _normalSprite;
                iconImage.color = Opaque(lockedColor);
            }
        }

        UpdateFrameAndText(unlocked, completed);
    }

    void LateUpdate()
    {
        if (iconImage != null && levelTypeConfig != null)
        {
            bool unlocked = LevelProgress.IsUnlocked(_sceneName);
            bool completed = LevelProgress.IsCompleted(_sceneName);
            
            // 确保图标正确，防止被Button组件覆盖
            Sprite typeSprite = GetTypeSprite();
            if (typeSprite != null && iconImage.sprite != typeSprite)
            {
                if (completed)
                {
                    iconImage.sprite = typeSprite;
                }
                else if (unlocked)
                {
                    iconImage.sprite = typeSprite;
                }
                else
                {
                    iconImage.sprite = typeSprite;
                }
            }
        }
    }

    Sprite GetTypeSprite()
    {
        if (levelTypeConfig == null) return null;

        LevelType type = GetLevelType(_levelNumber);
        switch (type)
        {
            case LevelType.Shop:
                return levelTypeConfig.shopIcon;
            case LevelType.Elite:
                return levelTypeConfig.eliteIcon;
            case LevelType.Boss:
                return levelTypeConfig.bossIcon;
            case LevelType.RandomEvent:
                return levelTypeConfig.randomEventIcon;
            case LevelType.Rest:
                return levelTypeConfig.restIcon;
            case LevelType.NormalBattle:
            default:
                return levelTypeConfig.normalBattleIcon;
        }
    }

    void UpdateFrameAndText(bool unlocked, bool completed)
    {
        Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);
        Color frameColor = completed ? completedDimColor : (unlocked ? Color.white : lockedColor);
        if (frameImage != null) frameImage.color = Opaque(frameColor);
        if (nameText != null)
        {
            nameText.color = Opaque(completed ? completedDimColor : (unlocked ? Color.white : lockedColor));
            if (!string.IsNullOrEmpty(_displayName)) nameText.text = _displayName;
        }
    }

    /// <summary> 从物体名里取出数字，如 "按钮3" -> "3"，"按钮16" -> "16"。 </summary>
    static string GetNumberFromGameObjectName(string goName)
    {
        if (string.IsNullOrEmpty(goName)) return null;
        int i = 0;
        while (i < goName.Length && !char.IsDigit(goName[i])) i++;
        if (i >= goName.Length) return null;
        int start = i;
        while (i < goName.Length && char.IsDigit(goName[i])) i++;
        return goName.Substring(start, i - start);
    }

    void OnClick()
    {
        if (!LevelProgress.IsUnlocked(_sceneName)) return;
        if (LevelProgress.IsCompleted(_sceneName)) return; // 已通关不可重复挑战
        if (string.IsNullOrEmpty(_sceneName)) return;
        // 先应用「进入本关会封锁哪些关」的路线规则，再跳转
        LevelProgress.OnEnterLevel(_sceneName);
        
        // 标记为从选关界面进入（保留选关进度）
        LevelSceneLoadContext.SetFromSelection();
        
        VideoSceneLoader.LoadScene(_sceneName);
    }
}
