using System.Collections.Generic;
using UnityEngine;


using System.Collections.Generic;

public static class UpgradeTargetMatcher
{
    /// <summary>
    /// Returns true when the supplied SquadData is eligible for this target filter.
    ///
    /// Rules:
    /// - Exact exclusions always win.
    /// - Exact additional inclusions bypass normal classification filters.
    /// - Empty filter groups impose no restriction.
    /// - Entries inside one list use OR.
    /// - Separate populated filter groups use AND.
    /// - Every required trait must be present.
    /// - Any excluded trait causes rejection.
    /// </summary>
    public static bool MatchesSquad(
        UpgradeTargetFilter filter,
        SquadData squadData)
    {
        if (squadData == null)
            return false;

        // ---------------------------------------------------------------------
        // Exact Exclusion
        // ---------------------------------------------------------------------
        // Exclusion has the highest priority, including over explicit inclusion.
        if (Contains(filter.excludedSquads, squadData))
            return false;

        // ---------------------------------------------------------------------
        // Exact Additional Inclusion
        // ---------------------------------------------------------------------
        // Explicit inclusion bypasses all normal classification filters.
        if (Contains(filter.additionallyIncludedSquads, squadData))
            return true;

        // ---------------------------------------------------------------------
        // Nation
        // ---------------------------------------------------------------------
        if (HasEntries(filter.nations) &&
            !Contains(filter.nations, squadData.nation))
        {
            return false;
        }

        // ---------------------------------------------------------------------
        // Combat Category
        // ---------------------------------------------------------------------
        if (HasEntries(filter.combatCategories) &&
            !Contains(filter.combatCategories, squadData.category))
        {
            return false;
        }

        // ---------------------------------------------------------------------
        // Combat Subcategory
        // ---------------------------------------------------------------------
        if (HasEntries(filter.combatSubcategories) &&
            !Contains(
                filter.combatSubcategories,
                squadData.combatSubcategory))
        {
            return false;
        }

        // ---------------------------------------------------------------------
        // Unit Families
        // ---------------------------------------------------------------------
        // A squad matches this group when it has at least one of the listed
        // families. Multiple selected families therefore use OR.
        if (HasEntries(filter.unitFamilies) &&
            !HasAnyMatchingFamily(
                filter.unitFamilies,
                squadData.unitFamilies))
        {
            return false;
        }

        // ---------------------------------------------------------------------
        // Required Traits
        // ---------------------------------------------------------------------
        // Every required flag must exist on the squad.
        if (filter.requiredTraits != UnitTrait.None &&
            (squadData.unitTraits & filter.requiredTraits) !=
            filter.requiredTraits)
        {
            return false;
        }

        // ---------------------------------------------------------------------
        // Excluded Traits
        // ---------------------------------------------------------------------
        // Any overlap with excluded traits rejects the squad.
        if (filter.excludedTraits != UnitTrait.None &&
            (squadData.unitTraits & filter.excludedTraits) !=
            UnitTrait.None)
        {
            return false;
        }

        return true;
    }

    static bool HasAnyMatchingFamily(
        IReadOnlyList<UnitFamilyData> filterFamilies,
        IReadOnlyList<UnitFamilyData> squadFamilies)
    {
        if (!HasEntries(filterFamilies) ||
            !HasEntries(squadFamilies))
        {
            return false;
        }

        for (int filterIndex = 0;
             filterIndex < filterFamilies.Count;
             filterIndex++)
        {
            UnitFamilyData filterFamily =
                filterFamilies[filterIndex];

            if (filterFamily == null)
                continue;

            for (int squadIndex = 0;
                 squadIndex < squadFamilies.Count;
                 squadIndex++)
            {
                if (squadFamilies[squadIndex] == filterFamily)
                    return true;
            }
        }

        return false;
    }

    static bool Contains<T>(
        IReadOnlyList<T> list,
        T value)
    {
        if (!HasEntries(list))
            return false;

        EqualityComparer<T> comparer =
            EqualityComparer<T>.Default;

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




public static class Calc
{
    /// Returns the raw direction vector from one point to another (not normalized).
    public static Vector3 Dir(Vector3 from, Vector3 to)
    {
        return (to - from);
    }

    /// Returns the squared distance between two points. Faster than RealDistance — use for comparisons.
    public static float SqrDistance(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude;
    }

    /// Returns true if the distance between two points is within range.
    /// Uses squared magnitude for performance — avoids sqrt.
    public static bool WithinRange(Vector3 a, Vector3 b, float range)
    {
        return (a - b).sqrMagnitude <= range * range;
    }

    /// Returns true if a precomputed squared magnitude is within range.
    public static bool WithinRange(float sqrMagnitude, float range)
    {
        return sqrMagnitude <= range * range;
    }

    /// Returns true if a precomputed direction vector's length is within range.
    public static bool WithinRange(Vector3 dir, float range)
    {
        return dir.sqrMagnitude <= range * range;
    }

    /// Returns true if the distance between two points exceeds range.
    /// Uses squared magnitude for performance — avoids sqrt.
    public static bool OutOfRange(Vector3 a, Vector3 b, float range)
    {
        return (a - b).sqrMagnitude > range * range;
    }

    /// Returns true if a precomputed squared magnitude exceeds range.
    /// sqrMagnitude -> (a - b).sqrMagnitude, range -> range to square
    public static bool OutOfRange(float sqrMagnitude, float range)
    {
        return sqrMagnitude > range * range;
    }

    /// Returns true if the distance between two points exceeds range.
    /// Uses real Euclidean distance between two points. 
    public static bool OutOfRangeRealDistance(Vector3 a, Vector3 b, float range)
    {
        return RealDistance(a, b) > range;
    }

    /// Returns the real Euclidean distance between two points.
    /// Use sparingly — prefer SqrDistance for comparisons.
    public static float RealDistance(Vector3 a, Vector3 b)
    {
        return Vector3.Distance(a, b);
    }

    public static float RealFlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);;
    }

    /// Returns the perpendicular direction to a given direction on the horizontal XZ plane.
    /// Useful for formation slot positioning.
    public static Vector3 Perpendicular(Vector3 direction)
    {
        direction.y = 0f;
        return Vector3.Cross(direction.normalized, Vector3.up).normalized;
    }

    /// Returns a normalized direction from one point to another on the horizontal XZ plane.
    /// Strips Y component — use for ground-level directional calculations.
    public static Vector3 DirectionFlat(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        dir.y = 0f;
        return dir.normalized;
    }

    /// Returns the median value from a list of integers.
    /// Used to find the anchor unit for formation centering.
    public static int Median(List<int> values)
    {
        if (values.Count == 0) return 0;
        List<int> sorted = new List<int>(values);
        sorted.Sort();
        return sorted[sorted.Count / 2];
    }

    /// Sorts a list of units by their GameObject instanceID.
    /// Ensures consistent and deterministic formation slot assignment.
    public static List<T> SortByID<T>(List<T> items) where T : Component
    {
        List<T> sorted = new List<T>(items);

        sorted.Sort((a, b) =>
        {
            if (a == null && b == null)
                return 0;

            if (a == null)
                return 1;

            if (b == null)
                return -1;

            return a.gameObject.GetInstanceID()
                .CompareTo(b.gameObject.GetInstanceID());
        });

        return sorted;
    }
}


public static class Verify
{
    public static bool IsNull(object obj, string label = "Object", Object context = null)
    {
        bool isNull = obj == null;

        // Unity "fake null" check for destroyed UnityEngine.Objects.
        if (!isNull && obj is Object unityObject)
            isNull = unityObject == null;

        if (isNull)
            Debug.LogWarning($"{label} is null.", context);

        return isNull;
    }

    public static bool NotNull(object obj, string label = "Object", Object context = null)
    {
        return !IsNull(obj, label, context);
    }
}
