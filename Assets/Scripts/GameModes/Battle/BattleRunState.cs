using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight runtime progression state for Battle Test Mode.
///
/// BattleDefinitionData still defines one battle. BattleRunState only tracks which
/// authored battle the current run is on. Player-army persistence, unit rewards,
/// and generated enemy armies can be added here later without changing the basic
/// battle progression contract.
/// </summary>
[Serializable]
public sealed class BattleRunState
{
    private readonly List<BattleDefinitionData> battles =
        new List<BattleDefinitionData>();

    private int currentBattleIndex = 0;
    private bool isComplete = false;

    public IReadOnlyList<BattleDefinitionData> Battles => battles;
    public int CurrentBattleIndex => currentBattleIndex;
    public int CurrentBattleNumber => HasCurrentBattle ? currentBattleIndex + 1 : 0;
    public int BattleCount => battles.Count;
    public bool HasCurrentBattle =>
        currentBattleIndex >= 0 &&
        currentBattleIndex < battles.Count;
    public bool IsFinalBattle =>
        HasCurrentBattle &&
        currentBattleIndex == battles.Count - 1;
    public bool IsComplete => isComplete;

    public BattleDefinitionData CurrentBattle =>
        HasCurrentBattle
            ? battles[currentBattleIndex]
            : null;

    public void Initialize(IReadOnlyList<BattleDefinitionData> battleSequence)
    {
        battles.Clear();

        if (battleSequence != null)
        {
            for (int index = 0; index < battleSequence.Count; index++)
            {
                BattleDefinitionData battle = battleSequence[index];

                if (battle != null)
                    battles.Add(battle);
            }
        }

        Reset();
    }

    public void Reset()
    {
        currentBattleIndex = 0;
        isComplete = battles.Count == 0;
    }

    public bool TryAdvance()
    {
        if (!HasCurrentBattle || isComplete)
            return false;

        if (IsFinalBattle)
        {
            isComplete = true;
            return false;
        }

        currentBattleIndex++;
        return true;
    }
}
