using System.Collections.Generic;
using UnityEngine;

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
    public static List<UnitController> SortByID(List<UnitController> units)
    {
        List<UnitController> sorted = new List<UnitController>(units);
        sorted.Sort((a, b) => a.gameObject.GetInstanceID().CompareTo(b.gameObject.GetInstanceID()));
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
