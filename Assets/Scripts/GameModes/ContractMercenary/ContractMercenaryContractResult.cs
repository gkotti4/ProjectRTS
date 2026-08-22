using System;
using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// ContractMercenaryContractResult
/// -----------------------------------------------------------------------------
///
/// Strategic presentation/history snapshot for one completed battle attempt.
/// BattleResult remains the generic battle-engine output; this class translates it
/// into Contract Mercenary language and also snapshots the authored contract rewards.
///
/// Defeat results may be recorded with companyChangesCommitted=false. That means the
/// casualties shown are what happened during the failed attempt, while the persistent
/// company army remains unchanged for the current retry-friendly MVP policy.
/// -----------------------------------------------------------------------------
[Serializable]
public sealed class ContractMercenaryContractResult
{
    public ContractData contract;
    public bool playerWon;
    public bool companyChangesCommitted;
    public float battleDuration;

    public int prestigeEarned;

    public List<ContractMercenaryResourceAmount> rewardsEarned =
        new List<ContractMercenaryResourceAmount>();

    public List<ContractMercenarySquadBattleSummary> playerSquads =
        new List<ContractMercenarySquadBattleSummary>();

    public int TotalStartingSoldiers
    {
        get
        {
            int total = 0;

            for (int index = 0; index < playerSquads.Count; index++)
                total += Mathf.Max(0, playerSquads[index]?.startingSoldierCount ?? 0);

            return total;
        }
    }

    public int TotalSurvivingSoldiers
    {
        get
        {
            int total = 0;

            for (int index = 0; index < playerSquads.Count; index++)
                total += Mathf.Max(0, playerSquads[index]?.survivingSoldierCount ?? 0);

            return total;
        }
    }

    public int TotalCasualties =>
        Mathf.Max(0, TotalStartingSoldiers - TotalSurvivingSoldiers);

    public int RoutedSquadCount
    {
        get
        {
            int total = 0;

            for (int index = 0; index < playerSquads.Count; index++)
            {
                if (playerSquads[index] != null && playerSquads[index].routedOffField)
                    total++;
            }

            return total;
        }
    }

    public int LostSquadCount
    {
        get
        {
            int total = 0;

            for (int index = 0; index < playerSquads.Count; index++)
            {
                ContractMercenarySquadBattleSummary squad = playerSquads[index];

                if (squad != null && squad.startingSoldierCount > 0 && squad.survivingSoldierCount <= 0)
                    total++;
            }

            return total;
        }
    }

    public static ContractMercenaryContractResult Create(
        ContractData contract,
        BattleResult battleResult,
        bool companyChangesCommitted)
    {
        ContractMercenaryContractResult result =
            new ContractMercenaryContractResult
            {
                contract = contract,
                playerWon = battleResult != null && battleResult.PlayerWon,
                companyChangesCommitted = companyChangesCommitted,
                battleDuration = battleResult != null
                    ? Mathf.Max(0f, battleResult.battleDuration)
                    : 0f,
                prestigeEarned = battleResult != null &&
                                  battleResult.PlayerWon &&
                                  contract != null
                    ? Mathf.Max(0, contract.prestigeReward)
                    : 0
            };

        if (battleResult != null && battleResult.PlayerWon && contract != null)
            CopyRewards(contract.rewards, result.rewardsEarned);

        if (battleResult != null && battleResult.playerSquads != null)
        {
            for (int index = 0; index < battleResult.playerSquads.Count; index++)
            {
                BattleSquadResult battleSquad = battleResult.playerSquads[index];

                if (battleSquad == null)
                    continue;

                result.playerSquads.Add(
                    new ContractMercenarySquadBattleSummary
                    {
                        companySquadId = battleSquad.externalSquadId,
                        squadData = battleSquad.squadData,
                        startingSoldierCount = Mathf.Max(0, battleSquad.startingSoldierCount),
                        survivingSoldierCount = Mathf.Max(0, battleSquad.survivingSoldierCount),
                        casualtyCount = Mathf.Max(0, battleSquad.casualtyCount),
                        routedOffField = battleSquad.routedOffField
                    });
            }
        }

        return result;
    }

    static void CopyRewards(
        IReadOnlyList<ContractMercenaryResourceAmount> source,
        List<ContractMercenaryResourceAmount> destination)
    {
        destination.Clear();

        if (source == null)
            return;

        for (int index = 0; index < source.Count; index++)
        {
            ContractMercenaryResourceAmount reward = source[index];

            if (reward == null || reward.amount <= 0)
                continue;

            destination.Add(
                new ContractMercenaryResourceAmount
                {
                    resourceType = reward.resourceType,
                    amount = reward.amount
                });
        }
    }
}

[Serializable]
public sealed class ContractMercenarySquadBattleSummary
{
    public string companySquadId;
    public SquadData squadData;
    public int startingSoldierCount;
    public int survivingSoldierCount;
    public int casualtyCount;
    public bool routedOffField;
}
