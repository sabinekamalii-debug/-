using UnityEngine;

public enum EnemyType 
{
    Enemy,
    Smallboss,
    Bigboss,
    GeBuLin,
    KuLou,
    小骷髅,
    黑之魔王,
    黑之魔王分身,
    火之魔王,
    石头怪,
    蜜蜂,
    万录朵,
    空之魔王,

    // ===== 敌人七原型（设计文档见 塔防脚本/敌人/敌人种类设计.txt）=====
    杂兵潮,      // Swarm：低血低防、量大、中速。考狙击清杂 + 重装多挡
    重甲兵,      // HeavyArmor：高防慢速。考术师/破防近卫破甲
    疾跑者,      // Runner：超高速低血、绕战线。考重装卡位/特种推拉
    术师怪,      // Caster：攻击无视干员防御。考医疗（重装防御失效）
    远程怪,      // Ranged：站桩点后排。考狙击对狙/近卫抢杀
    重锤兵,      // Hammer：高攻低频厚血。考重装+医疗/特种推离
    奶妈怪,      // Healer：给周围敌人回血。考狙击超射程点杀/先锋标记集火
}
