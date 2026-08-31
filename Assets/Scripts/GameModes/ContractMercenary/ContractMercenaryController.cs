using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// -----------------------------------------------------------------------------
/// ContractMercenaryController
/// -----------------------------------------------------------------------------
///
/// Game-mode rules/controller for the Contract Mercenary company phase.
///
/// This component is intentionally NOT persistent. GameSession owns the run state;
/// this controller may be destroyed/recreated as menu/battle scenes change.
/// ContractMercenaryData owns designer-authored CM content/configuration.
///
/// First playable responsibilities:
/// - create a new mercenary run from ContractMercenaryData
/// - expose authored contracts/recruitment/upgrades to the Hub UI
/// - replenish and recruit persistent squads
/// - purchase persistent broad company upgrades
/// - synchronize those upgrades into the live player FactionInstance before battle
/// - bind persistent squad Equipment/Veterancy into spawned runtime squads
/// - award committed squad Veterancy XP after victorious contracts
/// - accept/start a contract through the existing BattleGameModeController
/// - deploy the persistent company army at its current manpower
/// - consume BattleResult and commit victory casualties/survivors
/// - award contract resources/prestige on victory exactly once
/// - preserve defeat as a retry/abandon decision
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
public class ContractMercenaryController : MonoBehaviour
{
    public static ContractMercenaryController Instance { get; private set; }

    public event Action<ContractMercenaryRunState> OnRunStarted;
    public event Action<ContractData> OnContractStarted;
    public event Action<ContractData, bool> OnContractResolved;
    public event Action<ContractMercenaryRunState> OnRunStateChanged;
    public event Action<ContractMercenaryContractResult> OnContractResultReady;

    #region Definition

    [Header("Contract Mercenary Definition")]
    [Tooltip("Root authored content/rules asset for this Contract Mercenary mode.")]
    [SerializeField] private ContractMercenaryData contractMercenaryData;

    #endregion

    #region Battle Integration

    [FormerlySerializedAs("battleGameModeController")]
    [Header("Battle Integration")]
    [Tooltip("Optional for the current same-scene prototype. If empty, BattleGameModeController.Instance is resolved at runtime.")]
    [SerializeField] private BattleGameModeController battleController;

    #endregion

    #region Runtime

    private bool isSubscribedToBattleController = false;
    private bool currentContractVictoryCommitted = false;

    public ContractMercenaryData Data => contractMercenaryData;

    public string CompanyName =>
        contractMercenaryData != null &&
        !string.IsNullOrWhiteSpace(contractMercenaryData.companyName)
            ? contractMercenaryData.companyName
            : "Mercenary Company";

    public ContractMercenaryRunState RunState =>
        GameSession.Instance != null
            ? GameSession.Instance.ContractMercenaryRunState
            : null;

    public IReadOnlyList<ContractData> AvailableContracts
    {
        get
        {
            if (contractMercenaryData == null || contractMercenaryData.contracts == null)
                return Array.Empty<ContractData>();

            return contractMercenaryData.contracts;
        }
    }

    public IReadOnlyList<ContractMercenaryRecruitOption> RecruitmentOptions
    {
        get
        {
            if (contractMercenaryData == null || contractMercenaryData.recruitmentOptions == null)
                return Array.Empty<ContractMercenaryRecruitOption>();

            return contractMercenaryData.recruitmentOptions;
        }
    }

    public IReadOnlyList<ContractMercenaryUpgradeShopOption> UpgradeShopOptions
    {
        get
        {
            if (contractMercenaryData == null || contractMercenaryData.upgradeOptions == null)
                return Array.Empty<ContractMercenaryUpgradeShopOption>();

            return contractMercenaryData.upgradeOptions;
        }
    }

    public IReadOnlyList<ContractMercenaryEquipmentOption> EquipmentOptions
    {
        get
        {
            if (contractMercenaryData == null || contractMercenaryData.equipmentOptions == null)
                return Array.Empty<ContractMercenaryEquipmentOption>();

            return contractMercenaryData.equipmentOptions;
        }
    }

    public bool HasRun => RunState != null;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveBattleController();
    }

    void OnEnable()
    {
        ResolveBattleController();
        SubscribeToBattleController();
    }

    void Start()
    {
        ResolveBattleController();
        SubscribeToBattleController();
    }

    void OnDisable()
    {
        UnsubscribeFromBattleController();
    }

    void OnDestroy()
    {
        UnsubscribeFromBattleController();

        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region Run Lifecycle

    public bool StartNewRun()
    {
        if (GameSession.Instance == null)
        {
            Debug.LogError(
                $"{name}: Contract Mercenary requires GameSession.Instance.",
                this);
            return false;
        }

        if (contractMercenaryData == null)
        {
            Debug.LogError(
                $"{name}: Contract Mercenary requires a ContractMercenaryData definition before a run can start.",
                this);
            return false;
        }

        ContractMercenaryRunState runState =
            new ContractMercenaryRunState();

        runState.Initialize(
            contractMercenaryData.startingResources,
            contractMercenaryData.startingArmy,
            contractMercenaryData.startingPrestige);

        GameSession.Instance.SetContractMercenaryRunState(runState);

        currentContractVictoryCommitted = false;

        OnRunStarted?.Invoke(runState);
        OnRunStateChanged?.Invoke(runState);

        Debug.Log(
            $"{name}: Started Contract Mercenary run. " +
            $"Army={runState.Army.Count}, " +
            $"Gold={runState.GetResource(ContractMercenaryResourceType.Gold)}, " +
            $"Iron={runState.GetResource(ContractMercenaryResourceType.Iron)}, " +
            $"Prestige={runState.Prestige}.",
            this);

        return true;
    }

    public void EndCurrentRun()
    {
        if (GameSession.Instance == null)
            return;

        GameSession.Instance.ClearContractMercenaryRunState();
        currentContractVictoryCommitted = false;
    }

    #endregion

    #region Company Phase

    public int GetMissingSoldierCount(
        ContractMercenarySquadState squadState)
    {
        ContractMercenaryRunState runState = RunState;

        return runState != null
            ? runState.GetMissingSoldierCount(squadState)
            : 0;
    }

    public int GetReplenishmentGoldCost(
        ContractMercenarySquadState squadState)
    {
        if (squadState == null || squadState.squadData == null)
            return 0;

        int missingSoldiers = GetMissingSoldierCount(squadState);

        if (missingSoldiers <= 0)
            return 0;

        int baseGoldCostPerSoldier = contractMercenaryData != null
            ? Mathf.Max(0, contractMercenaryData.replenishmentGoldCostPerSoldier)
            : 0;

        float squadCostMultiplier = Mathf.Max(
            0f,
            squadState.squadData.reinforcementCostMultiplier);

        return Mathf.CeilToInt(
            missingSoldiers *
            baseGoldCostPerSoldier *
            squadCostMultiplier);
    }

    public bool CanReplenishSquad(
        ContractMercenarySquadState squadState)
    {
        ContractMercenaryRunState runState = RunState;

        if (runState == null ||
            runState.HasActiveContract ||
            squadState == null ||
            squadState.squadData == null)
        {
            return false;
        }

        int missingSoldiers = runState.GetMissingSoldierCount(squadState);

        if (missingSoldiers <= 0)
            return false;

        return runState.CanAfford(
            ContractMercenaryResourceType.Gold,
            GetReplenishmentGoldCost(squadState));
    }

    public bool ReplenishSquadToFull(
        ContractMercenarySquadState squadState)
    {
        if (!CanReplenishSquad(squadState))
            return false;

        ContractMercenaryRunState runState = RunState;
        int goldCost = GetReplenishmentGoldCost(squadState);

        if (!runState.TrySpendResource(
                ContractMercenaryResourceType.Gold,
                goldCost))
        {
            return false;
        }

        int replenishedCount = runState.ReplenishSquadToFull(squadState);

        if (replenishedCount <= 0)
        {
            runState.AddResource(
                ContractMercenaryResourceType.Gold,
                goldCost);
            return false;
        }

        OnRunStateChanged?.Invoke(runState);
        return true;
    }

    public bool CanRecruit(ContractMercenaryRecruitOption recruitOption)
    {
        ContractMercenaryRunState runState = RunState;

        if (runState == null ||
            runState.HasActiveContract ||
            recruitOption == null ||
            recruitOption.squadData == null)
        {
            return false;
        }

        if (runState.Prestige < Mathf.Max(0, recruitOption.minimumPrestige))
            return false;

        return runState.CanAfford(
                   ContractMercenaryResourceType.Gold,
                   Mathf.Max(0, recruitOption.goldCost)) &&
               runState.CanAfford(
                   ContractMercenaryResourceType.Iron,
                   Mathf.Max(0, recruitOption.ironCost));
    }

    public bool RecruitSquad(ContractMercenaryRecruitOption recruitOption)
    {
        if (!CanRecruit(recruitOption))
            return false;

        ContractMercenaryRunState runState = RunState;
        int goldCost = Mathf.Max(0, recruitOption.goldCost);
        int ironCost = Mathf.Max(0, recruitOption.ironCost);

        if (!runState.TrySpendResource(
                ContractMercenaryResourceType.Gold,
                goldCost))
        {
            return false;
        }

        if (!runState.TrySpendResource(
                ContractMercenaryResourceType.Iron,
                ironCost))
        {
            runState.AddResource(
                ContractMercenaryResourceType.Gold,
                goldCost);
            return false;
        }

        ContractMercenarySquadState recruitedSquad =
            runState.AddSquad(
                recruitOption.squadData,
                recruitOption.squadData.ResolvedStartingSoldierCount);

        if (recruitedSquad == null)
        {
            runState.AddResource(
                ContractMercenaryResourceType.Gold,
                goldCost);
            runState.AddResource(
                ContractMercenaryResourceType.Iron,
                ironCost);
            return false;
        }

        OnRunStateChanged?.Invoke(runState);
        return true;
    }

    public bool CanPurchaseUpgrade(
        ContractMercenaryUpgradeShopOption shopOption)
    {
        ContractMercenaryRunState runState = RunState;

        if (runState == null ||
            runState.HasActiveContract ||
            shopOption == null ||
            shopOption.upgradeData == null ||
            shopOption.upgradeData.scope != UpgradeScope.Faction)
        {
            return false;
        }

        if (runState.Prestige < Mathf.Max(0, shopOption.minimumPrestige))
            return false;

        if (!runState.CanApplyCompanyUpgrade(shopOption.upgradeData))
            return false;

        return runState.CanAfford(
                   ContractMercenaryResourceType.Gold,
                   Mathf.Max(0, shopOption.goldCost)) &&
               runState.CanAfford(
                   ContractMercenaryResourceType.Iron,
                   Mathf.Max(0, shopOption.ironCost));
    }

    public bool PurchaseUpgrade(
        ContractMercenaryUpgradeShopOption shopOption)
    {
        if (!CanPurchaseUpgrade(shopOption))
            return false;

        ContractMercenaryRunState runState = RunState;
        int goldCost = Mathf.Max(0, shopOption.goldCost);
        int ironCost = Mathf.Max(0, shopOption.ironCost);

        if (!runState.TrySpendResource(
                ContractMercenaryResourceType.Gold,
                goldCost))
        {
            return false;
        }

        if (!runState.TrySpendResource(
                ContractMercenaryResourceType.Iron,
                ironCost))
        {
            runState.AddResource(
                ContractMercenaryResourceType.Gold,
                goldCost);
            return false;
        }

        if (!runState.ApplyCompanyUpgrade(shopOption.upgradeData))
        {
            runState.AddResource(
                ContractMercenaryResourceType.Gold,
                goldCost);
            runState.AddResource(
                ContractMercenaryResourceType.Iron,
                ironCost);
            return false;
        }

        OnRunStateChanged?.Invoke(runState);
        return true;
    }


    public bool CanPurchaseEquipment(
        ContractMercenarySquadState squadState,
        ContractMercenaryEquipmentOption equipmentOption)
    {
        ContractMercenaryRunState runState = RunState;

        if (runState == null ||
            runState.HasActiveContract ||
            !runState.OwnsSquad(squadState) ||
            squadState.squadData == null ||
            equipmentOption == null ||
            equipmentOption.equipmentData == null)
        {
            return false;
        }

        EquipmentData equipmentData = equipmentOption.equipmentData;

        if (!equipmentData.CanEquipTo(squadState.squadData))
            return false;

        if (squadState.GetEquippedEquipment(equipmentData.slot) == equipmentData)
            return false;

        if (runState.Prestige < Mathf.Max(0, equipmentOption.minimumPrestige))
            return false;

        return runState.CanAfford(
                   ContractMercenaryResourceType.Gold,
                   Mathf.Max(0, equipmentOption.goldCost)) &&
               runState.CanAfford(
                   ContractMercenaryResourceType.Iron,
                   Mathf.Max(0, equipmentOption.ironCost));
    }

    public bool PurchaseEquipment(
        ContractMercenarySquadState squadState,
        ContractMercenaryEquipmentOption equipmentOption)
    {
        if (!CanPurchaseEquipment(squadState, equipmentOption))
            return false;

        ContractMercenaryRunState runState = RunState;
        int goldCost = Mathf.Max(0, equipmentOption.goldCost);
        int ironCost = Mathf.Max(0, equipmentOption.ironCost);

        if (!runState.TrySpendResource(
                ContractMercenaryResourceType.Gold,
                goldCost))
        {
            return false;
        }

        if (!runState.TrySpendResource(
                ContractMercenaryResourceType.Iron,
                ironCost))
        {
            runState.AddResource(
                ContractMercenaryResourceType.Gold,
                goldCost);
            return false;
        }

        if (!runState.EquipSquadEquipment(
                squadState,
                equipmentOption.equipmentData))
        {
            runState.AddResource(
                ContractMercenaryResourceType.Gold,
                goldCost);
            runState.AddResource(
                ContractMercenaryResourceType.Iron,
                ironCost);
            return false;
        }

        OnRunStateChanged?.Invoke(runState);
        return true;
    }

    #endregion

    #region Contract Selection / Battle

    public bool CanStartContract(ContractData contract)
    {
        if (contract == null || contract.battleDefinition == null)
            return false;

        ContractMercenaryRunState runState = RunState;

        if (runState == null || runState.HasActiveContract)
            return false;

        if (!HasDeployableArmy(runState))
            return false;

        if (!contract.repeatable && runState.IsContractCompleted(contract))
            return false;

        if (!runState.MeetsContractProgressionRequirements(contract))
            return false;

        ResolveBattleController();

        return battleController != null &&
               !battleController.IsBattleActive;
    }

    public bool StartContract(ContractData contract)
    {
        if (!CanStartContract(contract))
            return false;

        ContractMercenaryRunState runState = RunState;

        // Broad Upgrade Cards are persistent strategic state. Mirror their exact
        // purchased stack counts into the live player faction before battle squads
        // are spawned so RuntimeStatResolver sees them immediately.
        if (!EnsureCompanyUpgradesAppliedToBattleFaction(runState))
            return false;

        if (runState == null || !runState.BeginContract(contract))
            return false;

        currentContractVictoryCommitted = false;

        battleController.SetBattleDefinition(
            contract.battleDefinition);

        battleController.SetPlayerArmyDeployments(
            BuildPlayerArmyDeployments(runState));

        battleController.StartBattle();

        OnContractStarted?.Invoke(contract);
        OnRunStateChanged?.Invoke(runState);

        Debug.Log(
            $"{name}: Started contract '{contract.contractName}'.",
            this);

        return true;
    }

    public bool RetryCurrentContract()
    {
        ContractMercenaryRunState runState = RunState;

        if (runState == null || runState.CurrentContract == null)
            return false;

        ResolveBattleController();

        if (battleController == null ||
            battleController.IsBattleActive)
        {
            return false;
        }

        if (!EnsureCompanyUpgradesAppliedToBattleFaction(runState))
            return false;

        currentContractVictoryCommitted = false;

        battleController.SetBattleDefinition(
            runState.CurrentContract.battleDefinition);

        battleController.SetPlayerArmyDeployments(
            BuildPlayerArmyDeployments(runState));

        battleController.StartBattle();
        return true;
    }

    public bool AbandonCurrentContract()
    {
        ContractMercenaryRunState runState = RunState;

        if (runState == null || !runState.HasActiveContract)
            return false;

        if (battleController != null &&
            battleController.IsBattleActive)
        {
            return false;
        }

        ContractData abandonedContract = runState.CurrentContract;
        runState.AbandonCurrentContract();
        currentContractVictoryCommitted = false;
        battleController?.ClearPlayerArmyDeployments();

        OnContractResolved?.Invoke(abandonedContract, false);
        OnRunStateChanged?.Invoke(runState);
        return true;
    }

    #endregion

    #region Army Deployment

    bool HasDeployableArmy(ContractMercenaryRunState runState)
    {
        if (runState == null)
            return false;

        for (int index = 0; index < runState.Army.Count; index++)
        {
            ContractMercenarySquadState squadState = runState.Army[index];

            if (squadState != null &&
                squadState.squadData != null &&
                squadState.currentSoldierCount > 0)
            {
                return true;
            }
        }

        return false;
    }

    List<BattleSquadDeployment> BuildPlayerArmyDeployments(
        ContractMercenaryRunState runState)
    {
        List<BattleSquadDeployment> deployments =
            new List<BattleSquadDeployment>();

        if (runState == null)
            return deployments;

        for (int index = 0; index < runState.Army.Count; index++)
        {
            ContractMercenarySquadState squadState = runState.Army[index];

            if (squadState == null ||
                squadState.squadData == null ||
                squadState.currentSoldierCount <= 0)
            {
                continue;
            }

            BattleSquadDeployment deployment = new BattleSquadDeployment
            {
                externalSquadId = squadState.companySquadId,
                squadData = squadState.squadData,
                soldierCount = squadState.currentSoldierCount
            };

            // Preserve the separate squad-specific UpgradeData channel for future
            // authored squad progression. Equipment has its own persistent loadout
            // and is bound to the runtime squad at battle start. Broad CM Upgrade
            // Cards are synchronized to the player FactionInstance instead.
            if (squadState.appliedUpgrades != null)
            {
                for (int upgradeIndex = 0;
                     upgradeIndex < squadState.appliedUpgrades.Count;
                     upgradeIndex++)
                {
                    ContractMercenaryUpgradeStack stack =
                        squadState.appliedUpgrades[upgradeIndex];

                    if (stack == null ||
                        stack.upgradeData == null ||
                        stack.stackCount <= 0)
                    {
                        continue;
                    }

                    deployment.appliedUpgrades.Add(
                        new RuntimeUpgradeStackSnapshot
                        {
                            upgradeData = stack.upgradeData,
                            upgradeId = stack.upgradeData.upgradeId,
                            stackCount = stack.stackCount
                        });
                }
            }

            deployments.Add(deployment);
        }

        return deployments;
    }

    #endregion

    #region Company Upgrade Runtime Sync

    bool EnsureCompanyUpgradesAppliedToBattleFaction(
        ContractMercenaryRunState runState)
    {
        if (runState == null)
            return false;

        if (GameManager.Instance == null ||
            GameManager.Instance.PlayerFaction == null)
        {
            Debug.LogError(
                $"{name}: Cannot start Contract Mercenary battle because the live player FactionInstance is unavailable.",
                this);
            return false;
        }

        FactionInstance playerFaction = GameManager.Instance.PlayerFaction;

        for (int stackIndex = 0;
             stackIndex < runState.CompanyUpgrades.Count;
             stackIndex++)
        {
            ContractMercenaryUpgradeStack persistentStack =
                runState.CompanyUpgrades[stackIndex];

            if (persistentStack == null ||
                persistentStack.upgradeData == null ||
                persistentStack.stackCount <= 0)
            {
                continue;
            }

            UpgradeData upgradeData = persistentStack.upgradeData;

            if (upgradeData.scope != UpgradeScope.Faction)
            {
                Debug.LogError(
                    $"{name}: Persistent company upgrade '{upgradeData.upgradeName}' is not Faction scope.",
                    this);
                return false;
            }

            int desiredStackCount = Mathf.Max(0, persistentStack.stackCount);
            int runtimeStackCount = playerFaction.GetUpgradeStackCount(upgradeData);

            while (runtimeStackCount < desiredStackCount)
            {
                if (!GameManager.Instance.TryApplyFactionUpgrade(
                        upgradeData,
                        playerFaction,
                        UpgradeGrantSource.CampaignProgression))
                {
                    Debug.LogError(
                        $"{name}: Could not synchronize company upgrade '{upgradeData.upgradeName}' " +
                        $"to the live player faction ({runtimeStackCount}/{desiredStackCount} stacks applied).",
                        this);
                    return false;
                }

                runtimeStackCount++;
            }
        }

        return true;
    }

    #endregion

    #region Battle Binding

    void ResolveBattleController()
    {
        if (battleController == null)
            battleController = BattleGameModeController.Instance;
    }

    void SubscribeToBattleController()
    {
        if (battleController == null ||
            isSubscribedToBattleController)
        {
            return;
        }

        battleController.OnBattleResolved +=
            HandleBattleResolved;
        battleController.OnArmiesSpawned +=
            HandleArmiesSpawned;

        isSubscribedToBattleController = true;
    }

    void UnsubscribeFromBattleController()
    {
        if (battleController == null ||
            !isSubscribedToBattleController)
        {
            return;
        }

        battleController.OnBattleResolved -=
            HandleBattleResolved;
        battleController.OnArmiesSpawned -=
            HandleArmiesSpawned;

        isSubscribedToBattleController = false;
    }

    void HandleArmiesSpawned(
        IReadOnlyList<SquadController> playerArmy,
        IReadOnlyList<SquadController> enemyArmy)
    {
        ContractMercenaryRunState runState = RunState;

        if (runState == null ||
            !runState.HasActiveContract ||
            playerArmy == null)
        {
            return;
        }

        // CM owns persistent squad progression; BattleGameModeController stays
        // unaware of campaign systems. Deployment order matches the persistent
        // deployable army order, so bind Equipment + Veterancy immediately at
        // battle start and perform one shared runtime-stat refresh before combat.
        int persistentArmyIndex = 0;

        for (int runtimeIndex = 0; runtimeIndex < playerArmy.Count; runtimeIndex++)
        {
            SquadController runtimeSquad = playerArmy[runtimeIndex];

            while (persistentArmyIndex < runState.Army.Count)
            {
                ContractMercenarySquadState persistentSquad =
                    runState.Army[persistentArmyIndex++];

                if (persistentSquad == null ||
                    persistentSquad.squadData == null ||
                    persistentSquad.currentSoldierCount <= 0)
                {
                    continue;
                }

                if (runtimeSquad == null)
                    break;

                if (runtimeSquad.Data != persistentSquad.squadData)
                {
                    Debug.LogWarning(
                        $"{name}: CM equipment binding order mismatch. " +
                        $"Runtime squad '{runtimeSquad.Data?.name ?? "null"}' did not match " +
                        $"persistent squad '{persistentSquad.squadData.name}'.",
                        this);
                }

                VeterancyData veterancyData =
                    persistentSquad.VeterancyData;

                persistentSquad.RefreshVeterancyRank();

                runtimeSquad.SetEquipmentLoadout(
                    persistentSquad.Equipment,
                    refreshRuntimeStats: false);

                runtimeSquad.SetVeterancy(
                    veterancyData,
                    persistentSquad.veterancyRank,
                    refreshRuntimeStats: false);

                runtimeSquad.RefreshRuntimeStats();
                break;
            }
        }
    }

    void HandleBattleResolved(BattleResult battleResult)
    {
        ContractMercenaryRunState runState = RunState;

        if (runState == null ||
            runState.CurrentContract == null ||
            battleResult == null)
        {
            return;
        }

        if (battleResult.PlayerWon)
        {
            CommitContractVictory(runState, battleResult);
            return;
        }

        // MVP defeat policy: record what happened for presentation, but do not
        // commit casualties. Retrying starts again from pre-battle company manpower.
        currentContractVictoryCommitted = false;

        ContractMercenaryContractResult defeatResult =
            ContractMercenaryContractResult.Create(
                runState.CurrentContract,
                battleResult,
                companyChangesCommitted: false);

        runState.SetLastContractResult(defeatResult);

        OnContractResultReady?.Invoke(defeatResult);
        OnContractResolved?.Invoke(runState.CurrentContract, false);
        OnRunStateChanged?.Invoke(runState);
    }

    void CommitContractVictory(
        ContractMercenaryRunState runState,
        BattleResult battleResult)
    {
        if (runState == null ||
            runState.CurrentContract == null ||
            currentContractVictoryCommitted)
        {
            return;
        }

        ContractData completedContract = runState.CurrentContract;

        ContractMercenaryContractResult victoryResult =
            ContractMercenaryContractResult.Create(
                completedContract,
                battleResult,
                companyChangesCommitted: true);

        runState.ApplyBattleResult(battleResult);

        runState.AwardBattleVeterancyExperience(battleResult);

        if (!runState.CompleteCurrentContractVictory())
            return;

        runState.SetLastContractResult(victoryResult);

        currentContractVictoryCommitted = true;
        battleController?.ClearPlayerArmyDeployments();

        OnContractResultReady?.Invoke(victoryResult);
        OnContractResolved?.Invoke(completedContract, true);
        OnRunStateChanged?.Invoke(runState);

        Debug.Log(
            $"{name}: Completed contract '{completedContract.contractName}'. " +
            $"Gold={runState.GetResource(ContractMercenaryResourceType.Gold)}, " +
            $"Iron={runState.GetResource(ContractMercenaryResourceType.Iron)}, " +
            $"Prestige={runState.Prestige}.",
            this);
    }

    #endregion
}
