using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct BattleSquadEntry
{
    public SquadData squadData;

    [Min(1)]
    public int squadCount;

    public static BattleSquadEntry Create(SquadData data, int count = 1)
    {
        return new BattleSquadEntry
        {
            squadData = data,
            squadCount = Mathf.Max(1, count)
        };
    }
}

[CreateAssetMenu(
    fileName = "BattleDefinitionData_",
    menuName = "Scriptable Objects/Game Modes/Battle Definition")]
public class BattleDefinitionData : ScriptableObject
{
    [Header("Identity")]
    public string battleName = "Battle";
    [TextArea] public string battleDescription;
    
    [Header("Player Army")]
    public List<BattleSquadEntry> playerArmy =
        new List<BattleSquadEntry>();

    [Header("Enemy Army")]
    public List<BattleSquadEntry> enemyArmy =
        new List<BattleSquadEntry>();

    [Header("Post-Battle Upgrade Pool")]
    public List<UpgradeData> rewardUpgradePool =
        new List<UpgradeData>();
}