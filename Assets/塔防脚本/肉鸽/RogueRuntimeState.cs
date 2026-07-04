using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 肉鸽全局运行时状态（简化版）：
/// - AvailablePoint：局外可用点（入口显示）
/// - RunPoint：当前 run 点数（局内）
/// - PermanentPoint：永久点
/// - SelectedTalentCardIds：本局已选天赋卡 ID，选卡界面追加、结束本局时清空。
/// </summary>
public static class RogueRuntimeState
{
    private const int FullGuardianHpForGreatVictory = 10;
    private const string KeyAvailable = "Rogue.AvailablePoint";
    private const string KeyPermanent = "Rogue.PermanentPoint";
    private const string KeyFirstInit = "Rogue.FirstInit.Done";
    private const string KeyHasActiveRun = "Rogue.HasActiveRun";
    private const string KeyCurrentStage = "Rogue.CurrentStage";
    private const string KeyRunPoint = "Rogue.RunPoint";

    private static bool _initialized;

    public static int AvailablePoint { get; private set; }
    public static int RunPoint { get; private set; }
    public static int PermanentPoint { get; private set; }

    public static bool HasActiveRun { get; private set; }
    public static int CurrentStage { get; private set; } = 1;
    public static bool AutoStartBattleOnEntry { get; set; }

    public static RogueBattleResult LastBattleResult { get; private set; }
    public static bool HasPendingBattleResult { get; private set; }

    /// <summary> 本局已选天赋卡 ID 列表，用于选卡去重与后续效果。EndRun / StartRun 时清空。 </summary>
    private static List<string> _selectedTalentIds = new List<string>();
    public static IReadOnlyList<string> SelectedTalentCardIds => _selectedTalentIds;

    /// <summary> 仅测试用：清空已选卡，方便直接跑 RogueResult 场景时每次 Play 都能看到 3 张卡。 </summary>
    public static void ClearSelectedTalentCardsForTesting()
    {
        _selectedTalentIds.Clear();
    }

    public static void InitIfNeeded()
    {
        if (_initialized) return;
        _initialized = true;

        if (PlayerPrefs.GetInt(KeyFirstInit, 0) == 0)
        {
            PlayerPrefs.SetInt(KeyAvailable, 5);
            PlayerPrefs.SetInt(KeyPermanent, 0);
            PlayerPrefs.SetInt(KeyFirstInit, 1);
            PlayerPrefs.Save();
        }

        AvailablePoint = Mathf.Max(0, PlayerPrefs.GetInt(KeyAvailable, 5));
        PermanentPoint = Mathf.Max(0, PlayerPrefs.GetInt(KeyPermanent, 0));

        // 恢复爬塔中途存档（杀塔式：中途退出后可继续）
        if (PlayerPrefs.GetInt(KeyHasActiveRun, 0) != 0)
        {
            HasActiveRun = true;
            CurrentStage = Mathf.Max(1, PlayerPrefs.GetInt(KeyCurrentStage, 1));
            RunPoint = Mathf.Max(0, PlayerPrefs.GetInt(KeyRunPoint, 0));
        }
    }

    public static void StartRunIfNeeded()
    {
        InitIfNeeded();
        if (HasActiveRun) return;

        HasActiveRun = true;
        CurrentStage = 1;
        _selectedTalentIds.Clear();

        // 初始 run 点 = 基础 5 + 局外可用点。
        RunPoint = 5 + AvailablePoint;
        AvailablePoint = 0;
        SavePersistent();
    }

    public static void ContinueToNextStage()
    {
        if (!HasActiveRun) return;
        CurrentStage = Mathf.Max(1, CurrentStage + 1);
        SavePersistent();
    }

    public static void EndRunAndBackToEntry()
    {
        InitIfNeeded();
        AvailablePoint += Mathf.Max(0, RunPoint);
        RunPoint = 0;
        HasActiveRun = false;
        CurrentStage = 1;
        _selectedTalentIds.Clear();
        SavePersistent();
    }

    /// <summary> 选卡时调用：扣 RunPoint 并记录本局已选。返回是否成功（点数足够且未重复选同一张）。 </summary>
    public static bool TryPickTalentCard(TalentCardData card)
    {
        if (card == null || !HasActiveRun) return false;
        if (RunPoint < card.costRunPoint) return false;
        if (_selectedTalentIds.Contains(card.cardId)) return false;

        RunPoint -= card.costRunPoint;
        _selectedTalentIds.Add(card.cardId);
        return true;
    }

    public static void AddFreeTalentCard(TalentCardData card)
    {
        if (card != null && !_selectedTalentIds.Contains(card.cardId))
        {
            _selectedTalentIds.Add(card.cardId);
        }
    }

    public static bool TryExchangeAvailableToPermanent()
    {
        InitIfNeeded();
        if (AvailablePoint < 5) return false;

        AvailablePoint -= 5;
        PermanentPoint += 1;
        SavePersistent();
        return true;
    }

    public static void PublishBattleResult(RogueBattleResult result)
    {
        LastBattleResult = result;
        HasPendingBattleResult = true;
    }

    public static bool TryConsumeBattleResult(out RogueBattleResult result)
    {
        result = LastBattleResult;
        if (!HasPendingBattleResult) return false;
        HasPendingBattleResult = false;
        return true;
    }

    public static RogueSettlementSummary ApplySettlement(RogueBattleResult result)
    {
        InitIfNeeded();

        bool isGreatVictory = result.isWin && result.noHit && result.guardianHpEnd >= FullGuardianHpForGreatVictory;
        int gain = 0;
        if (result.isWin)
            gain = isGreatVictory ? 5 : 4;

        int permanentGain = 0;
        string betOutcome = "无押注";

        if (result.betPlaced)
        {
            float roll = Random.value;
            if (result.noHit)
            {
                if (roll < 0.5f) betOutcome = "押注返还+额外天赋";
                else if (roll < 0.95f) betOutcome = "押注返还";
                else betOutcome = "押注消失";
            }
            else
            {
                if (roll < 0.4f)
                {
                    gain *= 2;
                    betOutcome = "押注返还+双倍点数";
                }
                else if (roll < 0.9f) betOutcome = "押注返还";
                else betOutcome = "押注消失";
            }
        }

        RunPoint += Mathf.Max(0, gain);
        PermanentPoint += Mathf.Max(0, permanentGain);
        SavePersistent();

        return new RogueSettlementSummary
        {
            runPointGain = gain,
            permanentPointGain = permanentGain,
            betOutcome = betOutcome
        };
    }

    private static void SavePersistent()
    {
        PlayerPrefs.SetInt(KeyAvailable, Mathf.Max(0, AvailablePoint));
        PlayerPrefs.SetInt(KeyPermanent, Mathf.Max(0, PermanentPoint));
        PlayerPrefs.SetInt(KeyHasActiveRun, HasActiveRun ? 1 : 0);
        PlayerPrefs.SetInt(KeyCurrentStage, Mathf.Max(1, CurrentStage));
        PlayerPrefs.SetInt(KeyRunPoint, Mathf.Max(0, RunPoint));
        PlayerPrefs.Save();
    }

    /// <summary> 退出游戏时由 RogueStatePersistence 调用，确保爬塔中途存档被写入。 </summary>
    public static void SaveRunStateIfNeeded()
    {
        if (!_initialized) return;
        SavePersistent();
    }
}

public struct RogueBattleResult
{
    public int stage;
    public bool isWin;
    public bool noHit;
    public int guardianHpEnd;
    public bool firstClear;
    public bool betPlaced;
}

public struct RogueSettlementSummary
{
    public int runPointGain;
    public int permanentPointGain;
    public string betOutcome;
}
