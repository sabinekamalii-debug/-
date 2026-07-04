using UnityEngine;

[CreateAssetMenu(fileName = "WaveData", menuName = "Scriptable Objects/WaveData")]
public class WaveData : ScriptableObject
{
    [Header("UI ��ʾ����")]
    public int waveNumberDisplay; // ���������ֶ������ǵڼ���������Part1��Part2���� 1��

    public EnemyType enemyType;
    public float spawnInterval;
    public int enemiesPerWave;
    public float delayBeforeWave;

    [Header("·��")]
    [Tooltip("�� Spawner �ġ�·���б�����ѡ�ڼ���·�ߣ�0=��һ����1=�ڶ�������")]
    public int pathIndex = 0;
}
