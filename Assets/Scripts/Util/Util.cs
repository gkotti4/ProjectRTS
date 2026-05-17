using UnityEngine;

public static class Calc
{
    public static Vector3 Dir(Vector3 from, Vector3 to)
    {
        return (to - from);
    }
    
    public static float SqrDistance(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude;
    }
    
    public static bool WithinRange(Vector3 a, Vector3 b, float range)
    {
        return (a - b).sqrMagnitude <= range * range;
    }

    public static bool WithinRange(float sqrMagnitude, float range)
    {
        return sqrMagnitude <= range * range;
    }

    public static bool WithinRange(Vector3 dir, float range)
    {
        return dir.sqrMagnitude <= range * range;
    }

    public static bool OutOfRange(Vector3 a, Vector3 b, float range)
    {
        return (a - b).sqrMagnitude > range * range;
    }

    public static bool OutOfRange(float sqrMagnitude, float range)
    {
        return sqrMagnitude > range * range;
    }
    
    
    public static float RealDistance(Vector3 a, Vector3 b)
    {
        return Vector3.Distance(a, b);
    }
    
}
