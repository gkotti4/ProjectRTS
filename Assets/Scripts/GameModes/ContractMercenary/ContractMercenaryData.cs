using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// ContractMercenaryData
/// -----------------------------------------------------------------------------
///
/// Root authored definition for one Contract Mercenary rules/content set.
///
/// The scene controller owns runtime rules and battle coordination. This asset owns
/// the designer-authored content needed to create and operate a CM run so a complete
/// company setup can be assembled in the Unity Editor without changing code.
/// -----------------------------------------------------------------------------
[CreateAssetMenu(
    fileName = "ContractMercenaryData_",
    menuName = "Scriptable Objects/Game Modes/Contract Mercenary/Definition")]
public class ContractMercenaryData : ScriptableObject
{
    [Header("Company")]
    public string companyName = "Mercenary Company";

    [Header("Starting Resources")]
    public List<ContractMercenaryResourceAmount> startingResources =
        new List<ContractMercenaryResourceAmount>();

    [Header("Starting Army")]
    public List<ContractMercenaryStartingSquad> startingArmy =
        new List<ContractMercenaryStartingSquad>();

    [Header("Starting Progression")]
    [Min(0)]
    public int startingPrestige = 0;

    [Header("Economy")]
    [Tooltip("Base Gold cost for replacing one missing soldier. SquadData.reinforcementCostMultiplier scales this value per squad type.")]
    [Min(0)]
    public int replenishmentGoldCostPerSoldier = 20;

    [Header("Recruitment Catalog")]
    public List<ContractMercenaryRecruitOption> recruitmentOptions =
        new List<ContractMercenaryRecruitOption>();

    [Header("Upgrade Catalog")]
    public List<ContractMercenaryUpgradeShopOption> upgradeOptions =
        new List<ContractMercenaryUpgradeShopOption>();

    [Header("Contracts")]
    public List<ContractData> contracts =
        new List<ContractData>();

    void OnValidate()
    {
        startingPrestige = Mathf.Max(0, startingPrestige);
        replenishmentGoldCostPerSoldier = Mathf.Max(
            0,
            replenishmentGoldCostPerSoldier);

        ValidateStartingResources();
        ValidateStartingArmy();
        ValidateRecruitmentOptions();
        ValidateUpgradeOptions();
        ValidateContracts();
    }

    void ValidateStartingResources()
    {
        if (startingResources == null)
            return;

        for (int index = 0; index < startingResources.Count; index++)
        {
            ContractMercenaryResourceAmount resource = startingResources[index];

            if (resource != null)
                resource.amount = Mathf.Max(0, resource.amount);
        }
    }

    void ValidateStartingArmy()
    {
        if (startingArmy == null)
            return;

        for (int index = 0; index < startingArmy.Count; index++)
        {
            ContractMercenaryStartingSquad entry = startingArmy[index];

            if (entry == null)
                continue;

            entry.squadCount = Mathf.Max(1, entry.squadCount);

            if (entry.squadData == null)
            {
                Debug.LogWarning(
                    $"{name}: Starting Army entry {index} has no SquadData assigned.",
                    this);
            }
        }
    }

    void ValidateRecruitmentOptions()
    {
        if (recruitmentOptions == null)
            return;

        for (int index = 0; index < recruitmentOptions.Count; index++)
        {
            ContractMercenaryRecruitOption option = recruitmentOptions[index];

            if (option == null)
                continue;

            option.goldCost = Mathf.Max(0, option.goldCost);
            option.ironCost = Mathf.Max(0, option.ironCost);
            option.minimumPrestige = Mathf.Max(0, option.minimumPrestige);

            if (option.squadData == null)
            {
                Debug.LogWarning(
                    $"{name}: Recruitment option {index} has no SquadData assigned.",
                    this);
            }
        }
    }

    void ValidateUpgradeOptions()
    {
        if (upgradeOptions == null)
            return;

        for (int index = 0; index < upgradeOptions.Count; index++)
        {
            ContractMercenaryUpgradeShopOption option = upgradeOptions[index];

            if (option == null)
                continue;

            option.goldCost = Mathf.Max(0, option.goldCost);
            option.ironCost = Mathf.Max(0, option.ironCost);
            option.minimumPrestige = Mathf.Max(0, option.minimumPrestige);

            if (option.upgradeData == null)
            {
                Debug.LogWarning(
                    $"{name}: Upgrade option {index} has no UpgradeData assigned.",
                    this);
                continue;
            }

            if (option.upgradeData.scope != UpgradeScope.Faction)
            {
                Debug.LogWarning(
                    $"{name}: Upgrade option '{option.upgradeData.upgradeName}' uses " +
                    $"{option.upgradeData.scope} scope. Normal CM Upgrade Cards must use Faction scope.",
                    this);
            }
        }
    }

    void ValidateContracts()
    {
        if (contracts == null)
            return;

        HashSet<string> usedContractIds = new HashSet<string>();

        for (int index = 0; index < contracts.Count; index++)
        {
            ContractData contract = contracts[index];

            if (contract == null)
            {
                Debug.LogWarning(
                    $"{name}: Contract entry {index} is empty.",
                    this);
                continue;
            }

            if (contract.battleDefinition == null)
            {
                Debug.LogWarning(
                    $"{name}: Contract '{contract.contractName}' has no BattleDefinitionData assigned.",
                    this);
            }

            if (string.IsNullOrWhiteSpace(contract.contractId))
            {
                Debug.LogWarning(
                    $"{name}: Contract '{contract.contractName}' has no stable contractId.",
                    this);
                continue;
            }

            if (!usedContractIds.Add(contract.contractId))
            {
                Debug.LogWarning(
                    $"{name}: Duplicate contractId '{contract.contractId}' in the contract catalog.",
                    this);
            }
        }
    }
}
