using UnityEngine;

/// <summary>
/// RogueBattle 模板控制器：
/// - 管理本关基本状态（守护点血量、无伤标记）
/// - 胜/负时写入结果并跳转结算场景
/// </summary>
public class RogueBattleRunController : MonoBehaviour
{
    [Header("守护点")]
    [SerializeField] private int guardianMaxHp = 10;
    [SerializeField] private int guardianCurrentHp = 10;

    [Header("押注（占位）")]
    [SerializeField] private bool betPlaced;

    [Header("调试快捷键")]
    [SerializeField] private bool enableDebugHotkey = true;
    [SerializeField] private KeyCode debugWinKey = KeyCode.F9;
    [SerializeField] private KeyCode debugLoseKey = KeyCode.F10;

    private RogueFlowRouter _flow;
    private bool _noHit = true;
    private bool _finished;

    private void Awake()
    {
        RogueRuntimeState.InitIfNeeded();
        _flow = FindFirstObjectByType<RogueFlowRouter>();
    }

    private void Start()
    {
        if (!RogueRuntimeState.HasActiveRun)
            RogueRuntimeState.StartRunIfNeeded();

        guardianCurrentHp = guardianMaxHp;
        _noHit = true;
        _finished = false;
    }

    private void Update()
    {
        if (!enableDebugHotkey || _finished) return;
        if (Input.GetKeyDown(debugWinKey)) FinishBattle(true);
        if (Input.GetKeyDown(debugLoseKey)) FinishBattle(false);
    }

    public void ReportGuardianDamage(int damage)
    {
        if (_finished) return;
        if (damage > 0) _noHit = false;
        guardianCurrentHp = Mathf.Max(0, guardianCurrentHp - Mathf.Max(0, damage));
        if (guardianCurrentHp <= 0) FinishBattle(false);
    }

    public void FinishBattle(bool isWin)
    {
        if (_finished) return;
        _finished = true;

        int stage = Mathf.Max(1, RogueRuntimeState.CurrentStage);
        bool firstClear = isWin && PlayerPrefs.GetInt($"Rogue.StageClear.{stage}", 0) == 0;
        if (firstClear) PlayerPrefs.SetInt($"Rogue.StageClear.{stage}", 1);

        var result = new RogueBattleResult
        {
            stage = stage,
            isWin = isWin,
            noHit = isWin && _noHit,
            guardianHpEnd = Mathf.Max(0, guardianCurrentHp),
            firstClear = firstClear,
            betPlaced = betPlaced
        };

        RogueRuntimeState.PublishBattleResult(result);

        if (_flow != null) _flow.EnterResultFromBattle();
    }
}
