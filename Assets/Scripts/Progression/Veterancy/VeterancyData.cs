using System;
using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// VeterancyRankDefinition
/// -----------------------------------------------------------------------------
/// One cumulative squad-veterancy rank. totalExperienceRequired is the total XP
/// threshold for reaching this rank; modifiers are gained when the rank is reached
/// and remain active at every higher rank.
/// -----------------------------------------------------------------------------
[Serializable]
public sealed class VeterancyRankDefinition
{
    public string rankName = "Veteran";

    [Min(0)]
    public int totalExperienceRequired = 0;

    [Header("Rank Stat Bonuses")]
    public SoldierStatModifiers soldierModifiers;
    public SquadStatModifiers squadModifiers;
}

/// -----------------------------------------------------------------------------
/// VeterancyData
/// -----------------------------------------------------------------------------
/// Generic authored squad-veterancy progression definition.
///
/// V1 rules:
/// - rank is derived from total persistent squad XP
/// - rank bonuses are automatic and cumulative
/// - no skill points, branching, subclasses, or manual rank choices
/// - modifiers feed the same RuntimeStatResolver used by upgrades/equipment
/// -----------------------------------------------------------------------------
[CreateAssetMenu(
    fileName = "VeterancyData_",
    menuName = "Scriptable Objects/Veterancy/VeterancyData")]
public class VeterancyData : ScriptableObject
{
    [Header("Ranks")]
    [Tooltip("List index is the veterancy rank. Rank 0 should require 0 XP. Rank bonuses are cumulative through the current rank.")]
    public List<VeterancyRankDefinition> ranks =
        new List<VeterancyRankDefinition>
        {
            new VeterancyRankDefinition
            {
                rankName = "Recruit",
                totalExperienceRequired = 0
            },
            new VeterancyRankDefinition
            {
                rankName = "Rank 1",
                totalExperienceRequired = 100,
                soldierModifiers = new SoldierStatModifiers
                {
                    maxHealth = 5
                }
            },
            new VeterancyRankDefinition
            {
                rankName = "Rank 2",
                totalExperienceRequired = 250,
                soldierModifiers = new SoldierStatModifiers
                {
                    meleeDefense = 1,
                    missileDefense = 1
                }
            },
            new VeterancyRankDefinition
            {
                rankName = "Rank 3",
                totalExperienceRequired = 450,
                squadModifiers = new SquadStatModifiers
                {
                    leadership = 2f
                }
            },
            new VeterancyRankDefinition
            {
                rankName = "Rank 4",
                totalExperienceRequired = 700,
                soldierModifiers = new SoldierStatModifiers
                {
                    meleeAttack = 1,
                    rangedAccuracy = 1
                }
            },
            new VeterancyRankDefinition
            {
                rankName = "Rank 5",
                totalExperienceRequired = 1000,
                soldierModifiers = new SoldierStatModifiers
                {
                    maxHealth = 5
                }
            },
            new VeterancyRankDefinition
            {
                rankName = "Rank 6",
                totalExperienceRequired = 1400,
                soldierModifiers = new SoldierStatModifiers
                {
                    meleeDefense = 1,
                    missileDefense = 1
                }
            },
            new VeterancyRankDefinition
            {
                rankName = "Rank 7",
                totalExperienceRequired = 1850,
                squadModifiers = new SquadStatModifiers
                {
                    leadership = 2f
                }
            },
            new VeterancyRankDefinition
            {
                rankName = "Rank 8",
                totalExperienceRequired = 2350,
                soldierModifiers = new SoldierStatModifiers
                {
                    meleeAttack = 1,
                    rangedAccuracy = 1
                }
            },
            new VeterancyRankDefinition
            {
                rankName = "Elite",
                totalExperienceRequired = 3000,
                soldierModifiers = new SoldierStatModifiers
                {
                    maxHealth = 5,
                    meleeAttack = 1,
                    rangedAccuracy = 1
                }
            }
        };

    public int MaximumRank =>
        ranks != null && ranks.Count > 0
            ? ranks.Count - 1
            : 0;

    public int GetRankForExperience(int totalExperience)
    {
        totalExperience = Mathf.Max(0, totalExperience);

        if (ranks == null || ranks.Count == 0)
            return 0;

        int resolvedRank = 0;

        for (int rankIndex = 1; rankIndex < ranks.Count; rankIndex++)
        {
            VeterancyRankDefinition rank = ranks[rankIndex];

            if (rank == null)
                continue;

            if (totalExperience < Mathf.Max(0, rank.totalExperienceRequired))
                break;

            resolvedRank = rankIndex;
        }

        return Mathf.Clamp(resolvedRank, 0, MaximumRank);
    }

    public VeterancyRankDefinition GetRankDefinition(int rank)
    {
        if (ranks == null || ranks.Count == 0)
            return null;

        rank = Mathf.Clamp(rank, 0, MaximumRank);
        return ranks[rank];
    }

    public string GetRankDisplayName(int rank)
    {
        VeterancyRankDefinition definition = GetRankDefinition(rank);

        if (definition == null || string.IsNullOrWhiteSpace(definition.rankName))
            return $"Rank {Mathf.Max(0, rank)}";

        return definition.rankName;
    }

    public int GetTotalExperienceRequiredForRank(int rank)
    {
        VeterancyRankDefinition definition = GetRankDefinition(rank);
        return definition != null
            ? Mathf.Max(0, definition.totalExperienceRequired)
            : 0;
    }

    public int GetTotalExperienceRequiredForNextRank(int currentRank)
    {
        if (currentRank >= MaximumRank)
            return GetTotalExperienceRequiredForRank(MaximumRank);

        return GetTotalExperienceRequiredForRank(currentRank + 1);
    }

    public bool IsMaximumRank(int rank)
    {
        return rank >= MaximumRank;
    }

    void OnValidate()
    {
        if (ranks == null || ranks.Count == 0)
            return;

        int previousThreshold = 0;

        for (int rankIndex = 0; rankIndex < ranks.Count; rankIndex++)
        {
            VeterancyRankDefinition rank = ranks[rankIndex];

            if (rank == null)
                continue;

            if (rankIndex == 0)
                rank.totalExperienceRequired = 0;
            else
                rank.totalExperienceRequired = Mathf.Max(
                    previousThreshold + 1,
                    rank.totalExperienceRequired);

            previousThreshold = rank.totalExperienceRequired;

            if (HasPersistentCapacityModifiers(rank.squadModifiers))
            {
                Debug.LogWarning(
                    $"{name}: Veterancy rank {rankIndex} modifies persistent squad-capacity/replenishment values. " +
                    "CM Veterancy V1 only guarantees battle/runtime stat modifiers.",
                    this);
            }
        }
    }

    static bool HasPersistentCapacityModifiers(SquadStatModifiers modifiers)
    {
        return modifiers.startingSoldierCount != 0 ||
               modifiers.maximumSoldierCount != 0 ||
               modifiers.reinforcementAmount != 0 ||
               !Mathf.Approximately(modifiers.reinforcementCostMultiplierDelta, 0f) ||
               modifiers.officerSlots != 0 ||
               modifiers.specialistSlots != 0;
    }
}
