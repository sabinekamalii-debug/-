using System.Collections.Generic;
using UnityEngine;

public class UnitBlocker : MonoBehaviour
{
    [Header("�赲����")]
    public int maxBlockCount = 2; // ����赲����������װ��3���ȷ���2��

    public List<Enemy2> blockedEnemies = new List<Enemy2>();
    public List<SpawnerHealth> contactSpawners = new List<SpawnerHealth>(); // ��ǰ�赲�ĵ����б�

    // 所属干员
    private OperatorUnit owner;

    void Awake()
    {
        owner = GetComponent<OperatorUnit>();
    }

    // ��һ������Ҫ�������˽����Ա�Ĺ�����Χ/�赲��Χ
    // ע�⣺�������ͱ���� Collider2D��������������� 2D
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            SpawnerHealth spawner = other.GetComponent<SpawnerHealth>();
            if (spawner != null)
            {
                if (!spawner.isBroken && !contactSpawners.Contains(spawner))
                    contactSpawners.Add(spawner);
                return;
            }

            Enemy2 enemy = other.GetComponentInParent<Enemy2>();
            if (enemy != null)
            {
                if (owner != null && owner.IsStandingOnHighGround())
                    return;

                // 规则：如果自己还在“路上移动”，且当前这个格子上已经有别的干员站着，
                // 那么这次新产生的碰撞暂时无效（不开始阻挡）。
                if (owner != null && owner.isMoving &&
                    OperatorUnit.IsCellOccupiedByStandingOperator(owner.transform.position, owner))
                {
                    return;
                }

                // ����赲����
                if (owner != null && owner.IsEvading())
                    return;

                if (blockedEnemies.Count < maxBlockCount)
                {
                    BlockEnemy(enemy); // �赲��
                }
                else
                {
                }
            }
        }
    }

    // 持续碰撞：用于处理“在有干员的格子上开始碰撞，移动到空格子后仍保持碰撞”的情况
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Enemy")) return;

        Enemy2 enemy = other.GetComponentInParent<Enemy2>();
        if (enemy == null) return;
        if (owner != null && owner.IsStandingOnHighGround()) return; // 高台干员不阻挡
        if (blockedEnemies.Contains(enemy)) return; // 已经在列表里了

        // 若仍在有干员站立的格子上，则碰撞依旧无效
        if (owner != null && owner.isMoving &&
            OperatorUnit.IsCellOccupiedByStandingOperator(owner.transform.position, owner))
        {
            return;
        }

        // 一旦走到“没有人站着”的格子上，且仍与敌人保持碰撞，就开始真正阻挡
        if (owner != null && owner.IsEvading())
            return;

        if (blockedEnemies.Count < maxBlockCount)
        {
            BlockEnemy(enemy);
        }
    }
    // �赲���˵��߼�
    private void OnTriggerExit2D(Collider2D other)
    {
        SpawnerHealth spawner = other.GetComponent<SpawnerHealth>();
        if (spawner != null)
            contactSpawners.Remove(spawner);
    }

    public bool HasBlockedSpawner()
    {
        contactSpawners.RemoveAll(s => s == null || s.isBroken);
        return contactSpawners.Count > 0;
    }

    public SpawnerHealth GetFirstBlockedSpawner()
    {
        contactSpawners.RemoveAll(s => s == null || s.isBroken);
        return contactSpawners.Count > 0 ? contactSpawners[0] : null;
    }

    private void BlockEnemy(Enemy2 enemy)
    {
        blockedEnemies.Add(enemy);
        enemy.SetBlocked(true, this); // ���ߵ��ˣ��㱻�ҵ�ס�ˣ�ͣ�£�
    }

    // �ͷŵ��˵��߼�������������ʱ�ɵ��˽ű����ã�
    public void ReleaseEnemy(Enemy2 enemy)
    {
        if (blockedEnemies.Contains(enemy))
        {
            blockedEnemies.Remove(enemy);

            // �����߼�����ѡ����
            // �����ʱ��Χ�ﻹ����Ϊ��Ա��û���赲�ĵ��ˣ���������ֶ�ȥ����һ���µ�
        }
    }
    // --- ��������Ա����ʱ�������� ---
    public void ReleaseAllEnemies()
    {
        // �������б��赲�ĵ��ˣ�����������
        foreach (var enemy in blockedEnemies)
        {
            if (enemy != null)
            {
                enemy.SetBlocked(false, null);
            }
        }
        blockedEnemies.Clear();
    }
}