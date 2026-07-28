using System.Collections.Generic;

/// -----------------------------------------------------------------------------
/// UpgradeTargetMatcher
/// -----------------------------------------------------------------------------
///
/// Evaluates whether one SquadData unit type matches an upgrade target filter.
/// Upgrade eligibility is always squad-based. Soldier-stat effects apply to every
/// soldier belonging to a matching squad.
///
public static class UpgradeTargetMatcher
{
    public static bool MatchesSquad(
        UpgradeTargetFilter filter,
        SquadData squadData)
    {
        if (squadData == null)
            return false;

        // Exact exclusion has the highest priority.
        if (Contains(filter.excludedSquads, squadData))
            return false;

        // Explicit inclusion bypasses normal classification filters.
        if (Contains(filter.additionallyIncludedSquads, squadData))
            return true;

        if (HasEntries(filter.nations) &&
            !Contains(filter.nations, squadData.nation))
        {
            return false;
        }

        if (HasEntries(filter.combatCategories) &&
            !Contains(filter.combatCategories, squadData.category))
        {
            return false;
        }

        if (HasEntries(filter.combatSubcategories) &&
            !Contains(filter.combatSubcategories, squadData.combatSubcategory))
        {
            return false;
        }

        if (HasEntries(filter.unitFamilies) &&
            !HasAnyMatchingFamily(filter.unitFamilies, squadData.unitFamilies))
        {
            return false;
        }

        if (filter.requiredTraits != UnitTrait.None &&
            (squadData.unitTraits & filter.requiredTraits) != filter.requiredTraits)
        {
            return false;
        }

        if (filter.excludedTraits != UnitTrait.None &&
            (squadData.unitTraits & filter.excludedTraits) != UnitTrait.None)
        {
            return false;
        }

        return true;
    }

    static bool HasAnyMatchingFamily(
        IReadOnlyList<UnitFamilyData> filterFamilies,
        IReadOnlyList<UnitFamilyData> squadFamilies)
    {
        if (!HasEntries(filterFamilies) || !HasEntries(squadFamilies))
            return false;

        for (int filterIndex = 0; filterIndex < filterFamilies.Count; filterIndex++)
        {
            UnitFamilyData filterFamily = filterFamilies[filterIndex];

            if (filterFamily == null)
                continue;

            for (int squadIndex = 0; squadIndex < squadFamilies.Count; squadIndex++)
            {
                if (squadFamilies[squadIndex] == filterFamily)
                    return true;
            }
        }

        return false;
    }

    static bool Contains<T>(IReadOnlyList<T> list, T value)
    {
        if (!HasEntries(list))
            return false;

        EqualityComparer<T> comparer = EqualityComparer<T>.Default;

        for (int index = 0; index < list.Count; index++)
        {
            if (comparer.Equals(list[index], value))
                return true;
        }

        return false;
    }

    static bool HasEntries<T>(IReadOnlyList<T> list)
    {
        return list != null && list.Count > 0;
    }
}
