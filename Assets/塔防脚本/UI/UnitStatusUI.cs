using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UnitStatusUI : MonoBehaviour
{
    [Header("状态条引用")]
    [Tooltip("血条 Slider，显示当前血量/最大血量")]
    public Slider hpSlider;
    [Tooltip("蓝条 Slider，干员为技力/能量，敌人为攻击冷却等；不绑则仅显示血条")]
    public Slider mpSlider;
    private Image mpFillImage; // 蓝条填充图，SetMPColor 时用

    [Header("填充条缩放（可选）")]
    [Tooltip("勾选后：用下方 Fill Child Scale 强制缩放血条/蓝条填充子物体，解决某些 UI 比例不对")]
    public bool overrideFillChildScale = false;
    [Tooltip("对 Fill 子物体应用的 localScale，如 (4,4,1)")]
    public Vector3 fillChildScale = new Vector3(4f, 4f, 1f);
    [Tooltip("手动指定参与缩放的根节点（如 Fill Area/Fill）；不填则用各 Slider 的 fillRect")]
    public Transform manualFillRoot;
    [Tooltip("除自动收集的 Fill 子节点外，额外指定要应用 fillChildScale 的 Transform")]
    public Transform[] scaleTargets;

    private List<Transform> _fillChildren = new List<Transform>();

    void Start()
    {
        CollectFillChildren(hpSlider);
        CollectFillChildren(mpSlider);
        if (manualFillRoot != null)
        {
            for (int i = 0; i < manualFillRoot.childCount; i++)
            {
                Transform c = manualFillRoot.GetChild(i);
                if (c != null && !_fillChildren.Contains(c)) _fillChildren.Add(c);
            }
        }
        if (overrideFillChildScale)
        {
            StartCoroutine(ApplyFillScaleEndOfFrame());
            Application.onBeforeRender += OnBeforeRender;
        }
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRender;
    }

    private void OnDestroy()
    {
        Application.onBeforeRender -= OnBeforeRender;
    }

    private void OnBeforeRender()
    {
        ApplyScaleNow();
    }

    private void ApplyScaleNow()
    {
        if (!overrideFillChildScale) return;
        if (scaleTargets != null)
        {
            for (int i = 0; i < scaleTargets.Length; i++)
            {
                if (scaleTargets[i] != null)
                    scaleTargets[i].localScale = fillChildScale;
            }
        }
        for (int i = 0; i < _fillChildren.Count; i++)
        {
            if (_fillChildren[i] != null)
                _fillChildren[i].localScale = fillChildScale;
        }
    }

    private System.Collections.IEnumerator ApplyFillScaleEndOfFrame()
    {
        var wait = new WaitForEndOfFrame();
        while (true)
        {
            yield return wait;
            ApplyScaleNow();
            yield return null;
            ApplyScaleNow();
        }
    }

    private void CollectFillChildren(Slider slider)
    {
        if (slider == null || slider.fillRect == null) return;
        Transform fill = slider.fillRect;
        for (int i = 0; i < fill.childCount; i++)
        {
            Transform c = fill.GetChild(i);
            if (c != null && !_fillChildren.Contains(c)) _fillChildren.Add(c);
        }
    }

    /// <summary> 更新血条显示：current/max 为 0~1 比例。 </summary>
    public void UpdateHP(float current, float max)
    {
        if (hpSlider != null)
        {
            hpSlider.value = current / max; // 0~1 比例
        }
    }

    /// <summary> 更新蓝条显示：current/max 为 0~1 比例（干员技力/敌人攻击冷却等）。 </summary>
    public void UpdateMP(float current, float max)
    {
        if (mpSlider != null)
            mpSlider.value = current / max;
    }

    /// <summary> 设置蓝条填充颜色（若脚本内部有引用 mpFillImage）。 </summary>
    public void SetMPColor(Color color)
    {
        if (mpFillImage != null)
        {
            mpFillImage.color = color;
        }
    }

    void LateUpdate()
    {
        // 血条/蓝条始终正对相机，不随角色旋转
        transform.rotation = Quaternion.identity;
        ApplyScaleNow();
    }
}