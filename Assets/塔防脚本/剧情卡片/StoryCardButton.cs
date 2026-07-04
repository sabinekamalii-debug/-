using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Naninovel;

[RequireComponent(typeof(Button))]
public class StoryCardButton : MonoBehaviour
{
    public StoryCardData data;

    [Header("可选绑定")]
    public Image iconImage;
    public TMP_Text nameText;

    Button _button;
    string _runtimeScriptName;
    string _runtimeLabelName;
    static string PlotSceneName => "Title";

    void Awake()
    {
        TryAutoBind();
        _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(OnClick);
    }

    void OnValidate() => TryAutoBind();

    public void SetData(StoryCardData cardData, int fragmentIndex = 1)
    {
        TryAutoBind();
        data = cardData;
        _runtimeScriptName = null;
        _runtimeLabelName = null;
        if (nameText != null)
            nameText.text = "剧情碎片" + fragmentIndex;
        if (iconImage != null && data != null && data.icon != null)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = true;
        }
        else if (iconImage != null)
            iconImage.enabled = true;
    }

    void OnClick()
    {
        string script = "";
        string label = "";
        if (data != null)
        {
            script = string.IsNullOrEmpty(_runtimeScriptName) ? data.scriptName : _runtimeScriptName;
            script = ResolveScriptName(script, data);
            label = string.IsNullOrEmpty(_runtimeLabelName) ? data.labelName : _runtimeLabelName;
        }
        else
        {
            script = ResolveScriptFromNameText();
        }

        if (string.IsNullOrEmpty(script)) return;

        if (SceneManager.GetActiveScene().name == PlotSceneName && Engine.Initialized)
        {
            var player = Engine.GetService<IScriptPlayer>();
            if (player != null)
            {
                if (string.IsNullOrEmpty(label))
                    player.LoadAndPlay(script).Forget();
                else
                    player.LoadAndPlayAtLabel(script, label).Forget();
            }
        }
        else
        {
            NaninovelReturnRequest.Set(script, label ?? "");
            NaninovelReturnAutoPlayer.Ensure();
            VideoSceneLoader.LoadScene(PlotSceneName);
        }
    }

    string ResolveScriptFromNameText()
    {
        string title = nameText != null ? nameText.text : "";
        if (string.IsNullOrEmpty(title)) return "";
        int value = 0;
        for (int i = 0; i < title.Length; i++)
        {
            char c = title[i];
            if (c < '0' || c > '9') continue;
            value = value * 10 + (c - '0');
        }
        if (value <= 0) return "";
        return $"魔王 {value}";
    }

    static string ResolveScriptName(string script, StoryCardData cardData)
    {
        string s = (script ?? "").Trim();
        string id = cardData != null ? (cardData.cardId ?? "").Trim() : "";
        bool idIsNum = int.TryParse(id, out int idNum);

        if (string.IsNullOrEmpty(s) || s == "plot1")
        {
            if (idIsNum && idNum > 0)
                s = $"魔王 {idNum}";
        }

        if (s.StartsWith("魔王") && !s.StartsWith("魔王 "))
        {
            var suffix = s.Substring(2).Trim();
            if (!string.IsNullOrEmpty(suffix))
                s = $"魔王 {suffix}";
        }

        return s;
    }

    public void SetRuntimeTarget(string scriptName, string labelName = "")
    {
        _runtimeScriptName = scriptName;
        _runtimeLabelName = labelName;
    }

    void TryAutoBind()
    {
        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
            if (iconImage == null)
            {
                var icon = transform.Find("Icon");
                if (icon != null) iconImage = icon.GetComponent<Image>();
            }
        }

        if (nameText == null)
        {
            var name = transform.Find("Name");
            if (name != null) nameText = name.GetComponent<TMP_Text>();
        }
    }
}
