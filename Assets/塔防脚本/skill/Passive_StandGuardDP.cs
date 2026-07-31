using UnityEngine;

/// <summary>
/// 站桩回费被动（明日方舟 桃金娘一技能「持续回复」类）。
/// 挂在干员身上即可，不占用技能位、不需要技力。
/// 逻辑：当干员「站着不动」时（未处于移动状态），每隔 interval 秒返还 dpPerTick 点部署费用。
/// 一旦被玩家指挥移动（isMoving == true）则停止回费，鼓励「部署后驻守」的玩法。
/// 与击杀返费(dpOnKill) 完全独立，互不影响。
/// </summary>
[RequireComponent(typeof(OperatorUnit))]
public class Passive_StandGuardDP : MonoBehaviour
{
    [Header("站桩回费参数")]
    [Tooltip("每次回费的部署费用数量")]
    public int dpPerTick = 1;

    [Tooltip("回费间隔（秒）")]
    public float interval = 3f;

    [Tooltip("是否要求干员完全不移动才回费（桃金娘式）。取消勾选则只要存活就回费")]
    public bool requireStationary = true;

    [Header("回费时的视觉反馈（可选）")]
    [Tooltip("每次回费时短暂闪一下的颜色，留空可关闭")]
    public bool enableFlash = false;
    public Color flashColor = new Color(0.4f, 1f, 0.6f);
    public float flashDuration = 0.15f;

    private OperatorUnit owner;
    private float timer;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private float flashTimer;

    void Awake()
    {
        owner = GetComponent<OperatorUnit>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (spriteRenderers != null && spriteRenderers.Length > 0)
        {
            originalColors = new Color[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
                originalColors[i] = spriteRenderers[i].color;
        }
    }

    void Update()
    {
        if (owner == null || DeploymentManager.Instance == null) return;

        // 站桩判定：要求不动时，处于移动状态则不累计
        bool canRecover = !requireStationary || !owner.isMoving;

        if (canRecover)
        {
            timer += Time.deltaTime;
            if (timer >= interval)
            {
                timer -= interval;
                DeploymentManager.Instance.AddDP(dpPerTick);
                if (enableFlash) StartFlash();
            }
        }
        else
        {
            timer = 0f; // 一移动就重置计时，避免「走一步就到点白嫖」
        }

        UpdateFlash();
    }

    void StartFlash()
    {
        if (spriteRenderers == null) return;
        flashTimer = flashDuration;
        for (int i = 0; i < spriteRenderers.Length; i++)
            if (spriteRenderers[i] != null) spriteRenderers[i].color = flashColor;
    }

    void UpdateFlash()
    {
        if (flashTimer <= 0f) return;
        flashTimer -= Time.deltaTime;
        if (flashTimer <= 0f && spriteRenderers != null && originalColors != null)
        {
            for (int i = 0; i < spriteRenderers.Length && i < originalColors.Length; i++)
                if (spriteRenderers[i] != null) spriteRenderers[i].color = originalColors[i];
        }
    }
}
