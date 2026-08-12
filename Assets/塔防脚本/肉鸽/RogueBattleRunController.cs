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

    [Header("战斗类型")]
    [Tooltip("普通 / 精英 / Boss，影响结算奖励")]
    [SerializeField] private BattleType battleType = BattleType.Normal;

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

        guardianMaxHp += TalentEffectApplier.GetGuardianHpBonus();

        // 跨场血量：从 RogueRuntimeState 继承；首战（GuardianMaxHp==0）则满血
        if (RogueRuntimeState.GuardianMaxHp > 0)
            guardianCurrentHp = Mathf.Clamp(RogueRuntimeState.GuardianCurrentHp, 1, guardianMaxHp);
        else
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

        RogueRuntimeState.ClearBattleOnlyEffects();

        // 从 GameManager 读取本场真实血量（而非本控制器的影子数值），修复无伤/满血评级
        int realGuardianHp = 0;
        int realGuardianMaxHp = guardianMaxHp; // fallback
        if (GameManager.Instance != null)
        {
            realGuardianHp    = GameManager.Instance.playerHealth;
            realGuardianMaxHp = GameManager.Instance.maxPlayerHealth;
        }

        // 写回跨场血量：spc_repair 在此时生效（战后回复固定量，对下一场可见）
        if (isWin)
            realGuardianHp = Mathf.Min(realGuardianHp + RogueRuntimeState.RepairAfterBattleAmount, realGuardianMaxHp);
        RogueRuntimeState.SetGuardianHp(realGuardianHp, realGuardianMaxHp);

        int stage = Mathf.Max(1, RogueRuntimeState.CurrentStage);
        bool firstClear = isWin && PlayerPrefs.GetInt($"Rogue.StageClear.{stage}", 0) == 0;
        if (firstClear) PlayerPrefs.SetInt($"Rogue.StageClear.{stage}", 1);

        bool usedEmergency = GameManager.Instance != null && GameManager.Instance.IsEmergencyProtocolUsed();

        var result = new RogueBattleResult
        {
            stage = stage,
            isWin = isWin,
            noHit = isWin && _noHit,
            guardianHpEnd = Mathf.Max(0, realGuardianHp),
            guardianHpMax = realGuardianMaxHp,
            firstClear = firstClear,
            betPlaced = betPlaced,
            battleType = battleType,
            usedEmergencyProtocol = usedEmergency
        };

        RogueRuntimeState.PublishBattleResult(result);

        if (_flow != null) _flow.EnterResultFromBattle();
    }
}
