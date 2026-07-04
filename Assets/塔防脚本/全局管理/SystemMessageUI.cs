using UnityEngine;
using TMPro;
using System.Collections;

public class SystemMessageUI : MonoBehaviour
{
    public static SystemMessageUI Instance;

    [Header("文本框")]
    public TextMeshProUGUI messageText;

    [Header("默认与停留")]
    [Tooltip("无其他提示时显示的文字，如「新手教程」")]
    public string defaultText = "新手教程";
    [Tooltip("临时提示（ShowMessage）的显示时长（秒）")]
    public float displayDuration = 2.0f;

    [Header("新手教程开关")]
    [Tooltip("勾选：拖拽角色未部署时显示「将角色拖拽到白色方块区域」；不勾选：不显示")]
    public bool showDragHint = true;

    void Awake()
    {
        Instance = this;
        if (messageText != null)
        {
            messageText.text = defaultText;
            messageText.gameObject.SetActive(true);
        }
    }

    public void ShowMessage(string msg, Color? color = null)
    {
        if (messageText == null) return;

        StopAllCoroutines();

        messageText.text = msg;
        messageText.color = color ?? Color.yellow;
        messageText.gameObject.SetActive(true);

        StartCoroutine(HideAfterDelay());
    }

    /// <summary> 拖拽开始时调用，勾选 showDragHint 时显示提示。 </summary>
    public void ShowDragHint()
    {
        if (messageText == null || !showDragHint) return;
        StopAllCoroutines();
        messageText.text = "将角色拖拽到白色方块区域";
        messageText.color = Color.yellow;
        messageText.gameObject.SetActive(true);
    }

    /// <summary> 恢复为默认文字（无其他提示时）。 </summary>
    public void RestoreToDefault()
    {
        if (messageText == null) return;
        StopAllCoroutines();
        messageText.text = defaultText;
        messageText.color = Color.yellow;
        messageText.gameObject.SetActive(true);
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(displayDuration);
        RestoreToDefault();
    }
}