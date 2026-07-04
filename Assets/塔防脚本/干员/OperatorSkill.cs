using UnityEngine;

// 这是所有技能的父类
public abstract class OperatorSkill : MonoBehaviour
{
    [Header("技能基础配置")]
    public string skillName = "未命名技能";
    public float maxSP = 10f;          // 需要多少技力
    public float duration = 10f;       // 持续时间

    // 记录技能属于哪个干员
    protected OperatorUnit owner;

    // 初始化（干员脚本会自动调用这个）
    public virtual void Initialize(OperatorUnit unit)
    {
        owner = unit;
    }

    // --- 必须由子类实现的三个方法 ---

    // 1. 技能开启时发生什么
    public abstract void OnSkillStart();

    // 2. 技能进行时发生什么 (每帧调用，比如持续回血)
    public virtual void OnSkillUpdate() { }

    // 3. 技能结束时发生什么 (还原属性)
    public abstract void OnSkillEnd();
}