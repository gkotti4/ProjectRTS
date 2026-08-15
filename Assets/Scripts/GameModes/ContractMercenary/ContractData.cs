using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// ContractData
/// -----------------------------------------------------------------------------
///
/// Authored definition for one Contract Mercenary job.
/// The contract points at the reusable battle definition and describes strategic
/// rewards/progression earned when that battle is completed successfully.
/// -----------------------------------------------------------------------------
[CreateAssetMenu(
    fileName = "ContractData_",
    menuName = "Scriptable Objects/Game Modes/Contract Mercenary/Contract")]
public class ContractData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable contract key used by run progression and future saves.")]
    public string contractId;

    public string contractName = "Contract";

    [TextArea]
    public string contractDescription;

    [Header("Battle")]
    public BattleDefinitionData battleDefinition;

    [Header("Difficulty / Progression")]
    [Range(1, 5)]
    public int threatRating = 1;

    [Min(0)]
    public int prestigeReward = 1;

    [Tooltip("If disabled, completing this contract once prevents selecting it again during the same run.")]
    public bool repeatable = false;

    [Header("Rewards")]
    public List<ContractMercenaryResourceAmount> rewards =
        new List<ContractMercenaryResourceAmount>();
}