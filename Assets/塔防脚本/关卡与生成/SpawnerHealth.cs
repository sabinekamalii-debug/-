using UnityEngine;

public class SpawnerHealth : MonoBehaviour
{
    [Header("��������")]
    public int maxHealth = 200;
    public int currentHealth;

    // --- ����������Ƿ����� ---
    public bool isBroken = false;

    [Header("�Ӿ�����")]
    public UnitStatusUI statusUI;
    public SpriteRenderer spawnerSprite;
    public Sprite brokenSprite; // ���¹��ܡ��ѻ������ӵ�ͼƬ�ϵ����
    void Start()
    {
        currentHealth = maxHealth;
        isBroken = false; // ��ʼ����õ�
        if (spawnerSprite == null) spawnerSprite = GetComponent<SpriteRenderer>();
        if (statusUI == null) statusUI = GetComponentInChildren<UnitStatusUI>();
        RefreshHealthBar();
    }

    void RefreshHealthBar()
    {
        if (statusUI != null)
            statusUI.UpdateHP(currentHealth, maxHealth);
    }

    public void TakeDamage(int damage)
    {
        if (currentHealth <= 0) return;

        currentHealth -= damage;
        RefreshHealthBar();

        if (currentHealth <= 0)
        {
            BreakSpawner();
        }
    }

    void BreakSpawner()
    {
        isBroken = true; // ���ؼ������Ϊ��

        // --- �������޸ġ��л�ͼƬ ---
        if (spawnerSprite != null && brokenSprite != null)
        {
            // 1. ���ɻ�����ͼƬ
            spawnerSprite.sprite = brokenSprite;

            // 2. �������ɫ����Ϊ��ɫ (��ֹ֮ǰ��ɫ�߼����ţ���֤��ͼƬԭɫ��ʾ)
            spawnerSprite.color = Color.white;
        }
        // 3. �ر���ײ�� (������Ա�Ͳ������������Ѿ�����������)
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // 血条降为 0 后隐藏血条
        if (statusUI != null)
            statusUI.gameObject.SetActive(false);

        // 4. 关闭刷怪点攻击脚本（SpawnerShooter），防止被打爆后仍继续攻击干员
        var shooter = GetComponentInChildren<SpawnerShooter>(true);
        if (shooter != null)
            shooter.enabled = false;
    }
}