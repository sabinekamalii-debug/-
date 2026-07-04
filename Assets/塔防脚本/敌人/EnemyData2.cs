using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "NewData", menuName = "MyGame/EnemyData")]
public class EnemyData2 : ScriptableObject
{
    public int lives;
    [Tooltip("防御值：每 100 点获得 1% 减伤，最多 99900（99% 减伤）")]
    public int defense = 0;
    public int damage;
    public int damageforplayer;
    public float speed;

    [Header("攻击（近战与远程通用）")]
    [Tooltip("攻击间隔（秒），近战被阻挡时与远程攻击均使用此值，默认 1 秒")]
    public float attackInterval = 1f;
    [Header("远程专用")]
    [Tooltip("攻击范围（世界单位），仅远程敌人在意")]
    public float attackRange = 4f;

    [Header("击杀奖励")]
    [Tooltip("该敌人被击杀时我方获得的部署费用，按敌人种类在对应敌人数据里填写")]
    public int dpOnKill = 0;
}
