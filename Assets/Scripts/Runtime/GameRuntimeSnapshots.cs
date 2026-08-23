using System;
using System.Collections.Generic;

/// -----------------------------------------------------------------------------
/// GameRuntimeSnapshots
/// -----------------------------------------------------------------------------
///
/// Read-only-by-convention handoff models describing the currently instantiated
/// game runtime at one moment in time.
///
/// These snapshots intentionally keep direct ScriptableObject references because
/// they live in memory and are consumed by GameSession/game-mode systems. They are
/// NOT permanent save DTOs. Permanent saves should convert references to stable IDs.
///
/// Design role:
/// Clean boundary from GameManager/live Unity objects into session/progression code.
/// -----------------------------------------------------------------------------
[Serializable]
public sealed class GameRuntimeSnapshot
{
    public FactionRuntimeSnapshot playerFaction;
    public List<FactionRuntimeSnapshot> factions =
        new List<FactionRuntimeSnapshot>();
    public List<SquadRuntimeSnapshot> playerArmy =
        new List<SquadRuntimeSnapshot>();
}

[Serializable]
public sealed class FactionRuntimeSnapshot
{
    public FactionData factionData;
    public int factionId = -1;
    public int teamId = 0;
    public bool isPlayerControlled = false;

    public ResourceCost resources;
    public int currentPopulation = 0;
    public int populationCap = 0;

    public List<RuntimeUpgradeStackSnapshot> appliedUpgrades =
        new List<RuntimeUpgradeStackSnapshot>();
}

[Serializable]
public sealed class SquadRuntimeSnapshot
{
    public SquadData squadData;
    public string squadId;
    public int livingSoldierCount = 0;
    public int existingSoldierCount = 0;

    public List<RuntimeUpgradeStackSnapshot> appliedUpgrades =
        new List<RuntimeUpgradeStackSnapshot>();
}

[Serializable]
public sealed class RuntimeUpgradeStackSnapshot
{
    public UpgradeData upgradeData;
    public string upgradeId;
    public int stackCount = 0;
}
