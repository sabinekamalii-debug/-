using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TitleCharacterGallery : MonoBehaviour
{
    [Header("基础引用")]
    public CharacterGalleryData data;
    public GameObject rootPanel;

    [Header("显示控件")]
    public Image portraitImage;
    public Text nameText;
    public TMP_Text nameTextTMP;
    public Text descText;
    public TMP_Text descTextTMP;

    [Header("行为设置")]
    public bool loopPages = true;

    int _currentIndex;

    void Start() { if (rootPanel != null) rootPanel.SetActive(false); }

    public void Show()
    {
        if (rootPanel == null) return;
        _currentIndex = 0;
        rootPanel.SetActive(true);
        Refresh();
    }

    public void Hide() { if (rootPanel != null) rootPanel.SetActive(false); }

    public void Next()
    {
        if (!HasData()) return;
        _currentIndex++;
        if (_currentIndex >= data.entries.Count)
            _currentIndex = loopPages ? 0 : data.entries.Count - 1;
        Refresh();
    }

    public void Prev()
    {
        if (!HasData()) return;
        _currentIndex--;
        if (_currentIndex < 0)
            _currentIndex = loopPages ? data.entries.Count - 1 : 0;
        Refresh();
    }

    public void GoTo(int index)
    {
        if (!HasData()) return;
        if (index < 0 || index >= data.entries.Count) return;
        _currentIndex = index;
        Refresh();
    }

    public void Refresh()
    {
        if (!HasData()) { if (rootPanel != null) rootPanel.SetActive(false); return; }
        if (_currentIndex < 0 || _currentIndex >= data.entries.Count)
            _currentIndex = Mathf.Clamp(_currentIndex, 0, data.entries.Count - 1);

        var entry = data.entries[_currentIndex];
        if (portraitImage != null)
        {
            portraitImage.sprite = entry != null ? entry.portrait : null;
            portraitImage.enabled = portraitImage.sprite != null;
        }

        string displayName = entry != null ? entry.displayName : string.Empty;
        string description = entry != null ? entry.description : string.Empty;

        if (nameText != null) nameText.text = displayName;
        if (nameTextTMP != null) nameTextTMP.text = displayName;
        if (descText != null) { descText.text = description; descText.gameObject.SetActive(!string.IsNullOrEmpty(description)); }
        if (descTextTMP != null) { descTextTMP.text = description; descTextTMP.gameObject.SetActive(!string.IsNullOrEmpty(description)); }
    }

    bool HasData() => data != null && data.entries != null && data.entries.Count > 0;
}
