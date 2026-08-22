
using System;
using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// BattleSquadDeployment
/// -----------------------------------------------------------------------------
/// In-memory request for one squad entering a battle. externalSquadId is optional
/// and lets a higher-level mode match the spawned runtime squad back to its own
/// persistent record without making the battle engine game-mode aware.
[Serializable]
public sealed class BattleSquadDeployment
{
    public string externalSquadId;
    public SquadData squadData;

    [Min(1)]
    public int soldierCount = 1;

    public List<RuntimeUpgradeStackSnapshot> appliedUpgrades =
        new List<RuntimeUpgradeStackSnapshot>();
}

/// -----------------------------------------------------------------------------
/// BattleSquadResult
/// -----------------------------------------------------------------------------
/// Final manpower result for one participating squad. Successful routers count as
/// survivors even though their runtime SquadController has already left the map.
[Serializable]
public sealed class BattleSquadResult
{
    public string externalSquadId;
    public SquadData squadData;
    public int startingSoldierCount;
    public int survivingSoldierCount;
    public int casualtyCount;
    public bool routedOffField;
}

/// -----------------------------------------------------------------------------
/// BattleResult
/// -----------------------------------------------------------------------------
/// Reusable output from one completed battle. BattleGameModeController owns the
/// live battle; higher-level modes consume this result for progression.
[Serializable]
public sealed class BattleResult
{
    public BattleDefinitionData battleDefinition;
    public BattleGameState resultState;
    public float battleDuration;

    public List<BattleSquadResult> playerSquads =
        new List<BattleSquadResult>();

    public List<BattleSquadResult> enemySquads =
        new List<BattleSquadResult>();

    public bool PlayerWon => resultState == BattleGameState.Victory;
}


