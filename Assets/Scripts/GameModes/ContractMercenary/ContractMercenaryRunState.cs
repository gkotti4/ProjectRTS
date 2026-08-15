using System;
using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// ContractMercenaryResourceType
/// -----------------------------------------------------------------------------
///
/// Meta/run resources used by Contract Mercenary between battles.
/// These are intentionally separate from the AoE-style in-match ResourceType enum.
/// Contract Mercenary currently treats Gold as the common currency and Iron as the
/// first specialized equipment/forge material.
/// -----------------------------------------------------------------------------
public enum ContractMercenaryResourceType
{
    Gold,
    Iron
}

[Serializable]
public sealed class ContractMercenaryResourceAmount
{
    public ContractMercenaryResourceType resourceType =
        ContractMercenaryResourceType.Gold;

    [Min(0)]
    public int amount = 0;
}

[Serializable]
public sealed class ContractMercenaryStartingSquad
{
    public SquadData squadData;

    [Min(1)]
    public int squadCount = 1;
}


[Serializable]
public sealed class ContractMercenaryRecruitOption
{
    public SquadData squadData;

    [Min(0)]
    public int goldCost = 250;

    [Min(0)]
    public int ironCost = 0;
}

/// <summary>
/// Persistent-within-session record for one owned mercenary squad.
/// companySquadId identifies this specific squad even when the company owns
/// multiple squads using the same SquadData asset.
/// </summary>
[Serializable]
public sealed class ContractMercenarySquadState
{
    public string companySquadId;
    public SquadData squadData;

    [Min(0)]
    public int currentSoldierCount = 0;

    public int MaximumSoldierCount =>
        squadData != null
            ? Mathf.Max(1, squadData.maxSoldierCount)
            : 0;

    public bool HasLivingSoldiers => currentSoldierCount > 0;

    public ContractMercenarySquadState(
        SquadData data,
        int startingSoldierCount)
    {
        companySquadId = Guid.NewGuid().ToString("N");
        squadData = data;

        int maximumCount = data != null
            ? Mathf.Max(1, data.maxSoldierCount)
            : 0;

        currentSoldierCount = Mathf.Clamp(
            startingSoldierCount,
            0,
            maximumCount);
    }
}

/// -----------------------------------------------------------------------------
/// ContractMercenaryRunState
/// -----------------------------------------------------------------------------
///
/// Mutable company/run state that survives scene changes through GameSession.
///
/// Owns the strategic truth for Contract Mercenary:
/// - company resources
/// - owned army and current manpower
/// - prestige / completed contracts
/// - currently accepted contract
///
/// This is NOT live battle state. Battle scenes instantiate runtime squads from
/// this information and later return explicit battle results to update it.
/// -----------------------------------------------------------------------------
[Serializable]
public sealed class ContractMercenaryRunState
{
    private readonly List<ContractMercenaryResourceAmount> resources =
        new List<ContractMercenaryResourceAmount>();

    private readonly List<ContractMercenarySquadState> army =
        new List<ContractMercenarySquadState>();

    private readonly List<string> completedContractIds =
        new List<string>();

    private ContractData currentContract;
    private int prestige = 0;
    private int completedContractCount = 0;

    public IReadOnlyList<ContractMercenaryResourceAmount> Resources => resources;
    public IReadOnlyList<ContractMercenarySquadState> Army => army;
    public IReadOnlyList<string> CompletedContractIds => completedContractIds;

    public ContractData CurrentContract => currentContract;
    public int Prestige => prestige;
    public int CompletedContractCount => completedContractCount;
    public bool HasActiveContract => currentContract != null;

    public void Initialize(
        IReadOnlyList<ContractMercenaryResourceAmount> startingResources,
        IReadOnlyList<ContractMercenaryStartingSquad> startingArmy,
        int startingPrestige = 0)
    {
        resources.Clear();
        army.Clear();
        completedContractIds.Clear();

        currentContract = null;
        prestige = Mathf.Max(0, startingPrestige);
        completedContractCount = 0;

        if (startingResources != null)
        {
            for (int index = 0; index < startingResources.Count; index++)
            {
                ContractMercenaryResourceAmount resource =
                    startingResources[index];

                if (resource == null || resource.amount <= 0)
                    continue;

                AddResource(resource.resourceType, resource.amount);
            }
        }

        EnsureResourceEntry(ContractMercenaryResourceType.Gold);
        EnsureResourceEntry(ContractMercenaryResourceType.Iron);

        if (startingArmy == null)
            return;

        for (int entryIndex = 0; entryIndex < startingArmy.Count; entryIndex++)
        {
            ContractMercenaryStartingSquad entry = startingArmy[entryIndex];

            if (entry == null || entry.squadData == null)
                continue;

            int squadCount = Mathf.Max(1, entry.squadCount);

            for (int squadIndex = 0; squadIndex < squadCount; squadIndex++)
            {
                AddSquad(
                    entry.squadData,
                    entry.squadData.ResolvedStartingSoldierCount);
            }
        }
    }

    #region Resources

    public int GetResource(ContractMercenaryResourceType resourceType)
    {
        ContractMercenaryResourceAmount resource =
            FindResource(resourceType);

        return resource != null
            ? Mathf.Max(0, resource.amount)
            : 0;
    }

    public void AddResource(
        ContractMercenaryResourceType resourceType,
        int amount)
    {
        if (amount <= 0)
            return;

        ContractMercenaryResourceAmount resource =
            EnsureResourceEntry(resourceType);

        resource.amount += amount;
    }

    public bool CanAfford(
        ContractMercenaryResourceType resourceType,
        int amount)
    {
        return amount >= 0 && GetResource(resourceType) >= amount;
    }

    public bool TrySpendResource(
        ContractMercenaryResourceType resourceType,
        int amount)
    {
        amount = Mathf.Max(0, amount);

        if (!CanAfford(resourceType, amount))
            return false;

        ContractMercenaryResourceAmount resource =
            EnsureResourceEntry(resourceType);

        resource.amount = Mathf.Max(0, resource.amount - amount);
        return true;
    }

    ContractMercenaryResourceAmount FindResource(
        ContractMercenaryResourceType resourceType)
    {
        for (int index = 0; index < resources.Count; index++)
        {
            ContractMercenaryResourceAmount resource = resources[index];

            if (resource != null && resource.resourceType == resourceType)
                return resource;
        }

        return null;
    }

    ContractMercenaryResourceAmount EnsureResourceEntry(
        ContractMercenaryResourceType resourceType)
    {
        ContractMercenaryResourceAmount existing = FindResource(resourceType);

        if (existing != null)
            return existing;

        ContractMercenaryResourceAmount created =
            new ContractMercenaryResourceAmount
            {
                resourceType = resourceType,
                amount = 0
            };

        resources.Add(created);
        return created;
    }

    #endregion

    #region Army

    public ContractMercenarySquadState AddSquad(
        SquadData squadData,
        int soldierCount)
    {
        if (squadData == null)
            return null;

        ContractMercenarySquadState squadState =
            new ContractMercenarySquadState(
                squadData,
                soldierCount);

        army.Add(squadState);
        return squadState;
    }



    public int GetMissingSoldierCount(ContractMercenarySquadState squadState)
    {
        if (squadState == null)
            return 0;

        return Mathf.Max(
            0,
            squadState.MaximumSoldierCount - squadState.currentSoldierCount);
    }

    public int ReplenishSquadToFull(ContractMercenarySquadState squadState)
    {
        if (squadState == null)
            return 0;

        int missingCount = GetMissingSoldierCount(squadState);

        if (missingCount <= 0)
            return 0;

        squadState.currentSoldierCount = squadState.MaximumSoldierCount;
        return missingCount;
    }
    public bool RemoveSquad(string companySquadId)
    {
        if (string.IsNullOrWhiteSpace(companySquadId))
            return false;

        for (int index = 0; index < army.Count; index++)
        {
            ContractMercenarySquadState squadState = army[index];

            if (squadState == null ||
                squadState.companySquadId != companySquadId)
            {
                continue;
            }

            army.RemoveAt(index);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Commits one successful battle's player manpower back into the company.
    /// Defeat/retry intentionally does not call this method, so a failed attempt
    /// restores the pre-battle company state for the current MVP.
    /// </summary>
    public void ApplyBattleResult(BattleResult battleResult)
    {
        if (battleResult == null || battleResult.playerSquads == null)
            return;

        for (int resultIndex = 0;
             resultIndex < battleResult.playerSquads.Count;
             resultIndex++)
        {
            BattleSquadResult squadResult =
                battleResult.playerSquads[resultIndex];

            if (squadResult == null ||
                string.IsNullOrWhiteSpace(squadResult.externalSquadId))
            {
                continue;
            }

            ContractMercenarySquadState squadState =
                FindSquad(squadResult.externalSquadId);

            if (squadState == null)
                continue;

            squadState.currentSoldierCount = Mathf.Clamp(
                squadResult.survivingSoldierCount,
                0,
                squadState.MaximumSoldierCount);
        }

        // A fully destroyed squad is removed from the owned company roster.
        for (int armyIndex = army.Count - 1; armyIndex >= 0; armyIndex--)
        {
            ContractMercenarySquadState squadState = army[armyIndex];

            if (squadState == null || !squadState.HasLivingSoldiers)
                army.RemoveAt(armyIndex);
        }
    }

    public ContractMercenarySquadState FindSquad(string companySquadId)
    {
        if (string.IsNullOrWhiteSpace(companySquadId))
            return null;

        for (int index = 0; index < army.Count; index++)
        {
            ContractMercenarySquadState squadState = army[index];

            if (squadState != null &&
                squadState.companySquadId == companySquadId)
            {
                return squadState;
            }
        }

        return null;
    }

    #endregion

    #region Contracts / Progression

    public bool BeginContract(ContractData contract)
    {
        if (contract == null || currentContract != null)
            return false;

        if (!contract.repeatable && IsContractCompleted(contract))
            return false;

        currentContract = contract;
        return true;
    }

    public bool CompleteCurrentContractVictory()
    {
        if (currentContract == null)
            return false;

        ContractData completedContract = currentContract;

        if (completedContract.rewards != null)
        {
            for (int index = 0; index < completedContract.rewards.Count; index++)
            {
                ContractMercenaryResourceAmount reward =
                    completedContract.rewards[index];

                if (reward == null || reward.amount <= 0)
                    continue;

                AddResource(reward.resourceType, reward.amount);
            }
        }

        prestige += Mathf.Max(0, completedContract.prestigeReward);
        completedContractCount++;

        if (!string.IsNullOrWhiteSpace(completedContract.contractId) &&
            !completedContractIds.Contains(completedContract.contractId))
        {
            completedContractIds.Add(completedContract.contractId);
        }

        currentContract = null;
        return true;
    }

    public void AbandonCurrentContract()
    {
        currentContract = null;
    }

    public bool IsContractCompleted(ContractData contract)
    {
        if (contract == null || string.IsNullOrWhiteSpace(contract.contractId))
            return false;

        return completedContractIds.Contains(contract.contractId);
    }

    #endregion
}
