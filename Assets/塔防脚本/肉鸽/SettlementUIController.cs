using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 结算UI控制器：管理结算文字的显示、特效、失败背景等
/// 从原RogueResultController分离
/// </summary>
public class SettlementUIController : MonoBehaviour
{
    [Header("结算UI引用")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_Text gainText;
    [SerializeField] private TMP_Text totalText;
    
    [Header("简化结算显示")]
    [SerializeField] private int fullGuardianHpForGreatVictory = 10;
    
    [Header("结算文字出现特效")]
    [SerializeField] private float settlementTextFadeDuration = 0.26f;
    [SerializeField] private float settlementTextStaggerDelay = 0.06f;
    [SerializeField] private float settlementTextStartScale = 0.88f;
    [SerializeField] private bool useOutlinePulseEffect = true;
    [SerializeField] private float outlinePulsePeakWidth = 0.22f;
    [SerializeField] private Color outlinePulseColor = new Color(1f, 0.9f, 0.3f, 1f);
    [SerializeField] private float greatVictoryOutlinePulsePeakWidth = 0.22f;
    [SerializeField] private Color greatVictoryOutlinePulseColor = new Color(1f, 0.35f, 0.2f, 1f);
    [SerializeField] private Color victoryOutlinePulseColor = new Color(0.62f, 0.34f, 1f, 1f);
    [SerializeField] private float greatVictoryScalePulseMultiplier = 1.0f;
    [SerializeField] private bool useImpactShakeEffect = true;
    [SerializeField] private float impactScaleMultiplier = 1.12f;
    [SerializeField] private float impactScaleDuration = 0.1f;
    [SerializeField] private float impactShakeDuration = 0.16f;
    [SerializeField] private float impactShakeStrength = 22f;
    
    private GameObject _resultPanel;
    private bool _isBattleWin = true;
    private bool _useSimplifiedSettlementView = true;
    
    /// <summary>
    /// 缓存结果面板引用
    /// </summary>
    public void CacheResultPanel()
    {
        if (titleText != null && titleText.transform.parent != null)
            _resultPanel = titleText.transform.parent.gameObject;
    }
    
    /// <summary>
    /// 应用简化结算显示逻辑
    /// </summary>
    public void ApplySimplifiedSettlementDisplay(RogueBattleResult result, RogueSettlementSummary summary)
    {
        HideLegacyUnusedSettlementLabels();

        if (!result.isWin)
        {
            SetText(titleText, "止步");
            if (titleText != null) titleText.color = new Color32(255, 50, 50, 255); // 红色
            HideTextNode(detailText);
            HideTextNode(gainText);
            HideTextNode(totalText);
            
            // 保存战斗失败状态
            _isBattleWin = false;
            return;
        }

        bool isGreatVictory = result.noHit && result.guardianHpEnd >= fullGuardianHpForGreatVictory;
        string gradeText = isGreatVictory ? "大胜" : "胜利";
        Color gradeColor = isGreatVictory
            ? new Color32(255, 215, 0, 255)
            : new Color32(0, 220, 120, 255);

        SetText(titleText, gradeText);
        if (titleText != null) titleText.color = gradeColor;

        HideTextNode(detailText);
        ShowGainAndTotalBelowTitle(summary);
        
        // 保存战斗胜利状态
        _isBattleWin = true;
    }
    
    /// <summary>
    /// 显示收益和总点数在标题下方
    /// </summary>
    private void ShowGainAndTotalBelowTitle(RogueSettlementSummary summary)
    {
        if (gainText != null)
        {
            gainText.gameObject.SetActive(true);
            gainText.text = $"收益 +{summary.goldGain}";
        }

        if (totalText != null)
        {
            totalText.gameObject.SetActive(true);
            totalText.text = $"总点数 {RogueRuntimeState.RunGold}";
        }
    }
    
    /// <summary>
    /// 隐藏未使用的旧版结算标签
    /// </summary>
    private void HideLegacyUnusedSettlementLabels()
    {
        HideByName("结算详情");
        HideByName("详情");
        HideByName("详情文本");
    }
    
    /// <summary>
    /// 播放结算文字显示特效
    /// </summary>
    public IEnumerator PlaySettlementRevealEffectIfNeeded()
    {
        if (!_isBattleWin || !_useSimplifiedSettlementView) yield break;
        
        // 恢复结算文字可见性
        if (!_isBattleWin)
        {
            SetTextNodeActive(titleText, true);
            SetTextNodeActive(detailText, false);
            SetTextNodeActive(gainText, false);
            SetTextNodeActive(totalText, false);
        }
        else
        {
            SetTextNodeActive(titleText, true);
            SetTextNodeActive(detailText, false);
            SetTextNodeActive(gainText, true);
            SetTextNodeActive(totalText, true);
        }
        
        yield return AnimateVisibleSettlementText(titleText);
    }
    
    /// <summary>
    /// 播放结算文字动画
    /// </summary>
    private IEnumerator AnimateVisibleSettlementText(TMP_Text text)
    {
        if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text))
            yield break;

        var rect = text.rectTransform;
        if (rect == null) yield break;

        var cg = text.GetComponent<CanvasGroup>();
        if (cg == null) cg = text.gameObject.AddComponent<CanvasGroup>();

        Vector3 originalScale = rect.localScale;
        Vector3 startScale = originalScale * Mathf.Max(0.1f, settlementTextStartScale);
        cg.alpha = 0f;
        rect.localScale = startScale;

        float duration = Mathf.Max(0.05f, settlementTextFadeDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            cg.alpha = eased;
            rect.localScale = Vector3.LerpUnclamped(startScale, originalScale, eased);
            yield return null;
        }

        cg.alpha = 1f;
        rect.localScale = originalScale;

        if (useOutlinePulseEffect)
            yield return AnimateOutlinePulse(text);

        float delay = Mathf.Max(0f, settlementTextStaggerDelay);
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);
    }
    
    /// <summary>
    /// 播放描边脉冲特效
    /// </summary>
    private IEnumerator AnimateOutlinePulse(TMP_Text text)
    {
        if (text == null) yield break;
        var mat = text.fontMaterial;
        if (mat == null) yield break;
        if (!mat.HasProperty(ShaderUtilities.ID_OutlineWidth) || !mat.HasProperty(ShaderUtilities.ID_OutlineColor))
            yield break;

        float baseWidth = mat.GetFloat(ShaderUtilities.ID_OutlineWidth);
        Color baseColor = mat.GetColor(ShaderUtilities.ID_OutlineColor);
        bool isGreatVictory = string.Equals(text.text, "大胜", System.StringComparison.Ordinal);
        bool isVictory = string.Equals(text.text, "胜利", System.StringComparison.Ordinal);
        float peak = Mathf.Clamp(isGreatVictory ? greatVictoryOutlinePulsePeakWidth : outlinePulsePeakWidth, 0f, 1f);
        Color targetPulseColor = outlinePulseColor;
        if (isGreatVictory) targetPulseColor = greatVictoryOutlinePulseColor;
        else if (isVictory) targetPulseColor = victoryOutlinePulseColor;
        int count = 1;
        Vector3 baseScale = text.rectTransform != null ? text.rectTransform.localScale : Vector3.one;
        float scalePulse = Mathf.Max(1f, greatVictoryScalePulseMultiplier);

        for (int i = 0; i < count; i++)
        {
            float t = 0f;
            const float half = 0.1f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / half);
                float eased = 1f - Mathf.Pow(1f - p, 3f);
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, Mathf.Lerp(baseWidth, peak, eased));
                mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.Lerp(baseColor, targetPulseColor, eased));
                if (isGreatVictory && text.rectTransform != null)
                    text.rectTransform.localScale = Vector3.LerpUnclamped(baseScale, baseScale * scalePulse, eased);
                yield return null;
            }

            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / half);
                float eased = p * p * (3f - 2f * p);
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, Mathf.Lerp(peak, baseWidth, eased));
                mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.Lerp(targetPulseColor, baseColor, eased));
                if (isGreatVictory && text.rectTransform != null)
                    text.rectTransform.localScale = Vector3.LerpUnclamped(baseScale * scalePulse, baseScale, eased);
                yield return null;
            }
        }

        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, baseWidth);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, baseColor);
        if (text.rectTransform != null) text.rectTransform.localScale = baseScale;
        text.UpdateMeshPadding();

        if (useImpactShakeEffect)
            yield return PlayImpactScaleAndShake(text);
    }
    
    /// <summary>
    /// 播放冲击震荡特效
    /// </summary>
    private IEnumerator PlayImpactScaleAndShake(TMP_Text text)
    {
        if (text == null || text.rectTransform == null) yield break;
        var rt = text.rectTransform;
        Vector3 baseScale = rt.localScale;
        float scaleMul = Mathf.Max(1f, impactScaleMultiplier);
        float scaleDur = Mathf.Max(0.04f, impactScaleDuration);

        float t = 0f;
        while (t < scaleDur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / scaleDur);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            rt.localScale = Vector3.LerpUnclamped(baseScale, baseScale * scaleMul, eased);
            yield return null;
        }

        t = 0f;
        while (t < scaleDur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / scaleDur);
            float eased = p * p * (3f - 2f * p);
            rt.localScale = Vector3.LerpUnclamped(baseScale * scaleMul, baseScale, eased);
            yield return null;
        }
        rt.localScale = baseScale;

        Transform shakeTarget = _resultPanel != null ? _resultPanel.transform : rt.parent;
        if (shakeTarget == null) yield break;

        Vector3 basePos = shakeTarget.localPosition;
        float shakeDur = Mathf.Max(0.05f, impactShakeDuration);
        float strength = Mathf.Max(1f, impactShakeStrength);
        t = 0f;
        while (t < shakeDur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / shakeDur);
            float damper = 1f - p;
            Vector2 noise = UnityEngine.Random.insideUnitCircle * strength * damper;
            shakeTarget.localPosition = basePos + new Vector3(noise.x, noise.y, 0f);
            yield return null;
        }
        shakeTarget.localPosition = basePos;
    }
    
    /// <summary>
    /// 刷新总点数文本
    /// </summary>
    public void RefreshTotalText()
    {
        if (_useSimplifiedSettlementView) return;
        SetText(totalText,
            $"当前金币:{RogueRuntimeState.RunGold}\n" +
            $"当前天赋点:{TalentTreeState.TalentPoints}");
    }
    
    /// <summary>
    /// 刷新结算面板的所有文本
    /// </summary>
    public void RefreshResultPanelTexts()
    {
        EnsureSettlementTextsBound();
        RefreshTotalText();
        if (titleText != null) titleText.ForceMeshUpdate(true, true);
        if (detailText != null) detailText.ForceMeshUpdate(true, true);
        if (gainText != null) gainText.ForceMeshUpdate(true, true);
        if (totalText != null) totalText.ForceMeshUpdate(true, true);
    }
    
    /// <summary>
    /// 尝试按名称绑定结算文本（从原RogueResultController提取）
    /// </summary>
    private void EnsureSettlementTextsBound()
    {
        if (titleText == null || detailText == null || gainText == null || totalText == null)
            TryBindSettlementByContent();
    }
    
    /// <summary>
    /// 根据内容自动绑定结算文本
    /// </summary>
    private void TryBindSettlementByContent()
    {
        if (_resultPanel != null)
        {
            var tmps = _resultPanel.GetComponentsInChildren<TMP_Text>(true);
            if (tmps != null && tmps.Length >= 4)
            {
                System.Array.Sort(tmps, (a, b) =>
                {
                    float ay = a != null && a.rectTransform != null ? a.rectTransform.anchoredPosition.y : 0;
                    float by = b != null && b.rectTransform != null ? b.rectTransform.anchoredPosition.y : 0;
                    return by.CompareTo(ay);
                });
                if (titleText == null && tmps.Length > 0) titleText = tmps[0];
                if (detailText == null && tmps.Length > 1) detailText = tmps[1];
                if (gainText == null && tmps.Length > 2) gainText = tmps[2];
                if (totalText == null && tmps.Length > 3) totalText = tmps[3];
            }
        }
        
        var panels = UnityEngine.Object.FindObjectsOfType<TMP_Text>(true);
        if (panels == null) return;
        foreach (var t in panels)
        {
            if (t == null) continue;
            string txt = t.text ?? "";
            if (titleText == null && (txt.Contains("战斗") || txt.Contains("胜利") || txt.Contains("失败"))) titleText = t;
            if (detailText == null && (txt.Contains("详情") || txt.Contains("关卡"))) detailText = t;
            if (gainText == null && (txt.Contains("收益") || txt.Contains("获得"))) gainText = t;
            if (totalText == null && (txt.Contains("总点") || txt.Contains("本局点"))) totalText = t;
        }
    }
    
    /// <summary>
    /// 设置结算UI引用
    /// </summary>
    public void SetUIReferences(TMP_Text title, TMP_Text detail, TMP_Text gain, TMP_Text total, GameObject resultPanel)
    {
        titleText = title;
        detailText = detail;
        gainText = gain;
        totalText = total;
        _resultPanel = resultPanel;
    }
    
    // Helper methods
    private static void SetText(TMP_Text tmp, string value)
    {
        if (tmp != null) tmp.text = value;
    }
    
    private static void HideTextNode(TMP_Text text)
    {
        if (text == null) return;
        text.text = "";
        text.gameObject.SetActive(false);
    }
    
    private static void SetTextNodeActive(TMP_Text text, bool active)
    {
        if (text == null) return;
        text.gameObject.SetActive(active);
    }
    
    private static void HideByName(string objectName)
    {
        var go = GameObject.Find(objectName);
        if (go != null) go.SetActive(false);
    }
}