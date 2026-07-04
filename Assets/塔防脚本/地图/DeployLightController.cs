using UnityEngine;

public class DeployLightController : MonoBehaviour
{
    public static DeployLightController Instance;

    [Header("把拼好的大组 DeployLightGroup 拖进来")]
    public GameObject deployLightGroup; 
    
    [Header("把守护点自己拖进来（作为圆心）")]
    public Transform defensePoint;

    [Header("高光方格的颜色配置")]
    public Color highlightColor = new Color(1f, 1f, 1f, 0.5f); // 默认半透明白色，你可以在面板自己调

    void Awake()
    {
        Instance = this;
        if (deployLightGroup != null) deployLightGroup.SetActive(false);
    }

    // 显示部署范围
    public void ShowRange(OperatorData opData)
    {
        if (deployLightGroup == null || defensePoint == null || opData == null) return;

        // 1. 先把整个大组激活
        deployLightGroup.SetActive(true);

        // 2. 获取该干员的半径
        float radius = opData.deployRadius;

        // 3. 挨个点名里面的每一个小方块
        foreach (Transform block in deployLightGroup.transform)
        {
            // 算物理距离（无视网格对不齐的问题，容错率拉满）
            float dx = Mathf.Abs(block.position.x - defensePoint.position.x);
            float dy = Mathf.Abs(block.position.y - defensePoint.position.y);

            // 只要 X 或 Y 偏差在 0.6 以内，就当作是正对齐的
            bool isAlignedX = dx < 0.6f; 
            bool isAlignedY = dy < 0.6f; 

            // 判断是否在十字范围内（给 0.5 的误差宽容）
            bool inRange = (isAlignedX && dy <= radius + 0.5f) || 
                           (isAlignedY && dx <= radius + 0.5f);

            // 控制方块显隐
            block.gameObject.SetActive(inRange);

            // 【完全符合你的要求】：直接修改方块身上 SpriteRenderer 组件的 Color 属性
            if (inRange)
            {
                SpriteRenderer sr = block.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = highlightColor; // 直接给组件配置赋值！
                }
            }
        }

    }

    // 隐藏部署范围
    public void HideRange()
    {
        if (deployLightGroup != null) deployLightGroup.SetActive(false);
    }
}