using System;
using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// GameRuntimeSetup
/// -----------------------------------------------------------------------------
///
/// In-memory description of the shared game runtime that GameManager should create.
/// GameSession/game-mode code authors the setup; GameManager consumes it and owns
/// the resulting live FactionInstances.
///
/// This is NOT permanent save data. It intentionally keeps direct ScriptableObject
/// references because it only crosses the session -> live-runtime boundary in memory.
/// -----------------------------------------------------------------------------
[Serializable]
public sealed class GameRuntimeSetup
{
    public FactionRuntimeSetup playerFaction = new FactionRuntimeSetup
    {
        teamId = 1,
        isPlayerControlled = true
    };

    public FactionRuntimeSetup enemyFaction = new FactionRuntimeSetup
    {
        teamId = 2,
        isPlayerControlled = false
    };

    public bool IsValid =>
        playerFaction != null &&
        playerFaction.factionData != null &&
        enemyFaction != null &&
        enemyFaction.factionData != null;
}

[Serializable]
public sealed class FactionRuntimeSetup
{
    public FactionData factionData;
    public int teamId = 0;
    public bool isPlayerControlled = false;

    [Min(0)]
    public int startingResources = 0;

    [Min(0)]
    public int startingPopulationCap = 0;

    public List<UpgradeData> startingUpgrades =
        new List<UpgradeData>();
}