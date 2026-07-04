using UnityEngine;
using TMPro;

public class UIController : MonoBehaviour
{
    [Header("文本显示")]
    public TMP_Text enemyCountText; // 敌人数量等
    public TMP_Text livesText;      // 生命值 Text
    public TMP_Text costText;       // 部署费用 Text
    private int _lastCostDisplayed = -1;

    [Header("关卡生命值")]
    public TMP_Text livesText2; // 关卡界面用的那个生命值文本

    [Header("守护点生命值（已弃用）")]
    [Tooltip("守护点血量请只在 GameManager 的 Player Health 上设置，此处不再覆盖。保留字段仅为兼容旧场景。")]
    public int guardianPointLives = 0;

    void Start()
    {
        // 守护点血量以 GameManager.playerHealth 为准，由 GameManager.Start() 调用 UpdateLivesUI 刷新显示，此处不再覆盖。
    }

    // --- 以下专供 GameManager 调用的方法 ---
    public void UpdateLivesUI2(int lives)
    {
        if (livesText2 != null)
        {
            livesText2.text = lives.ToString();
        }
    }
    // --- 1. 更新敌人数量 ---
    // Spawner 会直接调用，参数 1, 3 等
    // --- 2. 更新生命值（需要时改格式/逻辑）---
    public void UpdateEnemyUI(int current, int total)
    {
        if (enemyCountText != null)
            enemyCountText.text = $"敌人数量:{current}/{total}";
    }

    // --- 3. 更新生命值 ---
    public void UpdateLivesUI(int currentLives)
    {
        if (livesText != null)
            livesText.text = currentLives.ToString();
    }

    // --- 部署费用（DeploymentManager 调用）---
    public void UpdateCostUI(int current, int max)
    {
        if (costText == null) return;
        if (current == _lastCostDisplayed) return; // 数值未变不刷新，避免和别处"争夺"导致一闪一闪
        _lastCostDisplayed = current;
        costText.text = $"部署费用：{current}";
    }
}
