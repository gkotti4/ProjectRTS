using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// FormationCalculator
/// -----------------------------------------------------------------------------
///
/// Math-only utility for calculating formation offsets and converting those offsets
/// into world-space positions.
///
/// Does not move units, tick state, own soldiers, issue commands, or know about
/// combat. It only answers formation geometry questions.
///
/// Design role:
/// Reusable formation math service.
///
/// This does NOT move units.
/// This does NOT tick anchors.
/// This does NOT know about ControlGroups.

public class FormationCalculator : MonoBehaviour
{
    public static FormationCalculator Instance { get; private set; }

    [SerializeField] private float defaultSpacing = 2f;
    public float DefaultSpacing => defaultSpacing;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #region Public API

    /// <summary>
    /// Calculates centered local-space formation offsets.
    /// Local X = right.
    /// Local Y = forward.
    /// </summary>
    public List<Vector2> CalculateOffsets(
        int unitCount,
        float width,
        SquadFormation formation = SquadFormation.Line,
        float spacingOverride = -1)
    {
        if (unitCount <= 0)
            return new List<Vector2>();

        float spacing = spacingOverride > 0f
            ? spacingOverride
            : defaultSpacing;
        
        width = Mathf.Max(width, spacing);

        int unitsPerRow = Mathf.Max(
            1,
            Mathf.FloorToInt(width / spacing));

        List<Vector2> offsets = formation switch
        {
            SquadFormation.Line => CalculateLineOffsets(unitCount, unitsPerRow, spacing),
            SquadFormation.Spread => CalculateSpreadOffsets(unitCount, width, spacing),
            SquadFormation.Box => CalculateBoxOffsets(unitCount, unitsPerRow, spacing),
            SquadFormation.Circle => CalculateCircleOffsets(unitCount, width, spacing),
            SquadFormation.Wedge => CalculateWedgeOffsets(unitCount, spacing),
            _ => CalculateLineOffsets(unitCount, unitsPerRow, spacing)
        };

        return CenterOffsets(offsets);
    }

    /// <summary>
    /// Converts local formation offsets into world-space slot positions.
    /// </summary>
    public List<Vector3> ConvertOffsetsToWorldPositions(
        List<Vector2> offsets,
        Vector3 center,
        Vector3 facingDirection)
    {
        List<Vector3> positions = new List<Vector3>();

        if (offsets == null || offsets.Count == 0)
            return positions;

        Vector3 facing = facingDirection;
        facing.y = 0f;

        if (facing == Vector3.zero)
            facing = Vector3.forward;

        facing.Normalize();

        Vector3 right = Calc.Perpendicular(facing);

        foreach (Vector2 offset in offsets)
        {
            Vector3 worldPos =
                center +
                right * offset.x +
                facing * offset.y;

            positions.Add(worldPos);
        }

        return positions;
    }

    /// <summary>
    /// Assigns each squad member to the nearest available world slot.
    /// Useful later for reforming, merging, or smart slot reassignment.
    /// </summary>
    public Dictionary<SoldierController, Vector3> AssignMembersToNearestSlots(
        List<SoldierController> members,
        List<Vector3> slots)
    {
        Dictionary<SoldierController, Vector3> result = new Dictionary<SoldierController, Vector3>();

        if (members == null || slots == null)
            return result;

        List<Vector3> availableSlots = new List<Vector3>(slots);

        foreach (SoldierController member in members)
        {
            if (member == null) continue;
            if (availableSlots.Count == 0) break;

            Vector3 nearest = availableSlots[0];
            float nearestDist = Calc.SqrDistance(member.transform.position, nearest);

            for (int i = 1; i < availableSlots.Count; i++)
            {
                float dist = Calc.SqrDistance(member.transform.position, availableSlots[i]);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = availableSlots[i];
                }
            }

            result[member] = nearest;
            availableSlots.Remove(nearest);
        }

        return result;
    }

    #endregion

    #region Offset Calculations

    /// <summary>
    /// Line formation.
    /// Units fill rows from front to back.
    /// </summary>
    List<Vector2> CalculateLineOffsets(int unitCount, int unitsPerRow, float spacingOverride = -1f)
    {
        List<Vector2> offsets = new List<Vector2>();
        
        float spacing = spacingOverride > 0f ? spacingOverride : defaultSpacing;

        for (int i = 0; i < unitCount; i++)
        {
            int row = i / unitsPerRow;
            int col = i % unitsPerRow;

            int unitsInRow = Mathf.Min(
                unitsPerRow,
                unitCount - row * unitsPerRow);

            float rowWidth = (unitsInRow - 1) * spacing;

            float x = col * spacing - rowWidth / 2f;
            float y = -row * spacing;

            offsets.Add(new Vector2(x, y));
        }

        return offsets;
    }

    /// <summary>
    /// Spread formation.
    /// Wider spacing, good for anti-AOE behavior later.
    /// </summary>
    List<Vector2> CalculateSpreadOffsets(int unitCount, float width, float spacingOverride = -1f)
    {
        List<Vector2> offsets = new List<Vector2>();

        if (unitCount <= 0)
            return offsets;
        
        float spacing = spacingOverride > 0f ? spacingOverride : defaultSpacing;

        float aspect = 1.5f;
        int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(unitCount * aspect)));
        float colSpacing = cols > 1 ? width / (cols - 1) : 0f;
        float rowSpacing = Mathf.Max(spacing, colSpacing * 0.8f);

        for (int i = 0; i < unitCount; i++)
        {
            int row = i / cols;
            int col = i % cols;

            int unitsInRow = Mathf.Min(cols, unitCount - row * cols);
            float rowWidth = (unitsInRow - 1) * colSpacing;

            float x = col * colSpacing - rowWidth / 2f;
            float y = -row * rowSpacing;

            offsets.Add(new Vector2(x, y));
        }

        return offsets;
    }

    /// <summary>
    /// Box formation.
    /// Fills a compact rectangle.
    /// Later we can make this perimeter-first again if desired.
    /// </summary>
    List<Vector2> CalculateBoxOffsets(int unitCount, int unitsPerRow, float spacingOverride = -1f)
    {
        List<Vector2> offsets = new List<Vector2>();

        float spacing = spacingOverride > 0f ? spacingOverride : defaultSpacing;
        
        int cols = Mathf.Max(1, unitsPerRow);
        int rows = Mathf.CeilToInt((float)unitCount / cols);

        for (int i = 0; i < unitCount; i++)
        {
            int row = i / cols;
            int col = i % cols;

            int unitsInRow = Mathf.Min(cols, unitCount - row * cols);
            float rowWidth = (unitsInRow - 1) * spacing;

            float x = col * spacing - rowWidth / 2f;
            float y = -row * spacing;

            offsets.Add(new Vector2(x, y));
        }

        return offsets;
    }

    /// <summary>
    /// Circle formation.
    /// One unit becomes centered instead of making a weird tiny circle.
    /// </summary>
    List<Vector2> CalculateCircleOffsets(int unitCount, float width, float spacingOverride = -1f)
    {
        List<Vector2> offsets = new List<Vector2>();

        float spacing = spacingOverride > 0f ? spacingOverride : defaultSpacing;
        
        if (unitCount <= 0)
            return offsets;

        if (unitCount == 1)
        {
            offsets.Add(Vector2.zero);
            return offsets;
        }

        float radius = Mathf.Max(spacing, width * 0.5f);
        float angleStep = 360f / unitCount;

        for (int i = 0; i < unitCount; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;

            float x = Mathf.Sin(angle) * radius;
            float y = Mathf.Cos(angle) * radius;

            offsets.Add(new Vector2(x, y));
        }

        return offsets;
    }

    /// <summary>
    /// Wedge formation.
    /// First unit at the tip, others alternate left/right behind.
    /// </summary>
    List<Vector2> CalculateWedgeOffsets(int unitCount, float spacingOverride = -1f)
    {
        List<Vector2> offsets = new List<Vector2>();

        if (unitCount <= 0)
            return offsets;
        
        float spacing = spacingOverride > 0f ? spacingOverride : defaultSpacing;

        offsets.Add(Vector2.zero);

        int side = 1;
        int row = 1;

        for (int i = 1; i < unitCount; i++)
        {
            float x = side * row * spacing * 0.5f;
            float y = -row * spacing * 0.75f;

            offsets.Add(new Vector2(x, y));

            side *= -1;

            if (side == 1)
                row++;
        }

        return offsets;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Centers offsets around their average point.
    /// This makes the squad transform represent the formation center.
    /// </summary>
    List<Vector2> CenterOffsets(List<Vector2> offsets)
    {
        if (offsets == null || offsets.Count == 0)
            return offsets;

        Vector2 center = Vector2.zero;

        foreach (Vector2 offset in offsets)
            center += offset;

        center /= offsets.Count;

        for (int i = 0; i < offsets.Count; i++)
            offsets[i] -= center;

        return offsets;
    }

    #endregion
}


