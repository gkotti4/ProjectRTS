// SESSION: Squad Control

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Math-only formation utility.
/// 
/// This does NOT move units.
/// This does NOT tick anchors.
/// This does NOT know about ControlGroups.
/// 
/// SquadController owns movement.
/// FormationCalculator only calculates local offsets and converts them to world positions.
/// </summary>
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
            SquadFormation.Line => CalculateLineOffsets(unitCount, unitsPerRow),
            SquadFormation.Spread => CalculateSpreadOffsets(unitCount, width),
            SquadFormation.Box => CalculateBoxOffsets(unitCount, unitsPerRow),
            SquadFormation.Circle => CalculateCircleOffsets(unitCount, width),
            SquadFormation.Wedge => CalculateWedgeOffsets(unitCount),
            _ => CalculateLineOffsets(unitCount, unitsPerRow)
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
    List<Vector2> CalculateLineOffsets(int unitCount, int unitsPerRow)
    {
        List<Vector2> offsets = new List<Vector2>();

        for (int i = 0; i < unitCount; i++)
        {
            int row = i / unitsPerRow;
            int col = i % unitsPerRow;

            int unitsInRow = Mathf.Min(
                unitsPerRow,
                unitCount - row * unitsPerRow);

            float rowWidth = (unitsInRow - 1) * defaultSpacing;

            float x = col * defaultSpacing - rowWidth / 2f;
            float y = -row * defaultSpacing;

            offsets.Add(new Vector2(x, y));
        }

        return offsets;
    }

    /// <summary>
    /// Spread formation.
    /// Wider spacing, good for anti-AOE behavior later.
    /// </summary>
    List<Vector2> CalculateSpreadOffsets(int unitCount, float width)
    {
        List<Vector2> offsets = new List<Vector2>();

        if (unitCount <= 0)
            return offsets;

        float aspect = 1.5f;
        int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(unitCount * aspect)));
        float colSpacing = cols > 1 ? width / (cols - 1) : 0f;
        float rowSpacing = Mathf.Max(defaultSpacing, colSpacing * 0.8f);

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
    List<Vector2> CalculateBoxOffsets(int unitCount, int unitsPerRow)
    {
        List<Vector2> offsets = new List<Vector2>();

        int cols = Mathf.Max(1, unitsPerRow);
        int rows = Mathf.CeilToInt((float)unitCount / cols);

        for (int i = 0; i < unitCount; i++)
        {
            int row = i / cols;
            int col = i % cols;

            int unitsInRow = Mathf.Min(cols, unitCount - row * cols);
            float rowWidth = (unitsInRow - 1) * defaultSpacing;

            float x = col * defaultSpacing - rowWidth / 2f;
            float y = -row * defaultSpacing;

            offsets.Add(new Vector2(x, y));
        }

        return offsets;
    }

    /// <summary>
    /// Circle formation.
    /// One unit becomes centered instead of making a weird tiny circle.
    /// </summary>
    List<Vector2> CalculateCircleOffsets(int unitCount, float width)
    {
        List<Vector2> offsets = new List<Vector2>();

        if (unitCount <= 0)
            return offsets;

        if (unitCount == 1)
        {
            offsets.Add(Vector2.zero);
            return offsets;
        }

        float radius = Mathf.Max(defaultSpacing, width * 0.5f);
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
    List<Vector2> CalculateWedgeOffsets(int unitCount)
    {
        List<Vector2> offsets = new List<Vector2>();

        if (unitCount <= 0)
            return offsets;

        offsets.Add(Vector2.zero);

        int side = 1;
        int row = 1;

        for (int i = 1; i < unitCount; i++)
        {
            float x = side * row * defaultSpacing * 0.5f;
            float y = -row * defaultSpacing * 0.75f;

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



//
//
//
// public class FormationCalculator : MonoBehaviour
// {
//     public static FormationCalculator Instance { get; private set; }
//
//     [SerializeField] private float defaultSpacing = 2f;
//     public float DefaultSpacing => defaultSpacing;
//     [SerializeField] private float slotUpdateThreshold = 0.15f; // Double Check (formation catch up and polish)
//     
//     [Header("Formation Mode Catch Up Settings")]
//     [SerializeField] private float catchUpStartDistance = 0.5f; // (formation catch up and polish)
//     [SerializeField] private float catchUpMaxDistance = 5.0f;
//     [SerializeField] private float maxCatchUpSpeedMultiplier = 2.0f;
//
// #region Unity Lifecycle
//     
//     void Awake()
//     {
//         if (Instance != null && Instance != this) { Destroy(gameObject); return; }
//         Instance = this;
//     }
//
//     void Update()
//     {
//         TickMovingFormationAnchors();
//     }
//     
//     #endregion
//
//     
//     #region Anchor
//     
//     
//     void TickMovingFormationAnchors()
//     {
//         foreach (ControlGroup cg in ControlGroupManager.Instance.GetControlGroups())
//         {
//             if (cg.anchor == null || !cg.anchor.isMoving) continue;
//
//             cg.anchor.Tick();
//             UpdateFormationSlotsForGroup(cg);
//         }
//     }
//
//     void UpdateFormationSlotsForGroup(SquadController squad)
//     {
//         if (cg == null) return;
//         if (cg.formationOffsets == null || cg.formationOffsets.Count == 0) return;
//
//         List<MilitaryController> military = ControlGroupManager.Instance.GetMilitaryFromGroup(cg);
//
//         foreach (MilitaryController mc in military)
//         {
//             if (!mc) continue; // mc == null
//             if (mc.offsetIndex < 0 || mc.offsetIndex >= cg.formationOffsets.Count) continue;
//
//             Vector3 newSlotPos = cg.anchor.GetSlotPosition(
//                 cg.formationOffsets[mc.offsetIndex],
//                 this);
//
//             mc.formationSlot = newSlotPos;
//             
//             float distanceFromSlot = Calc.SqrDistance(mc.transform.position, newSlotPos); // (formation catch up and polish) could maybe move inside formationMode section
//
//             if (cg.formationMode)
//             {
//                 //mc.Agent.speed = mc.Stats.moveSpeed;
//                 // mc.Agent.speed = Calc.OutOfRange(distanceFromSlot, catchUpStartDistance) // distanceFromSlot >= catchUpDistanceThreshold * catchUpDistanceThreshold
//                 //     ? mc.Stats.moveSpeed * maxCatchUpSpeedMultiplier
//                 //     : mc.Stats.moveSpeed;
//
//                 mc.Agent.speed = mc.Stats.moveSpeed * GetCatchUpSpeedMultiplier(distanceFromSlot);
//                 
//                 Debug.Log(GetCatchUpSpeedMultiplier(distanceFromSlot));
//
//                 if (Calc.OutOfRange(mc.lastSlotPos, newSlotPos, slotUpdateThreshold))
//                 {
//                     mc.Agent.SetDestination(newSlotPos);
//                     mc.lastSlotPos = newSlotPos;
//                 }
//             }
//             else
//             {
//                 mc.Agent.speed = mc.Stats.moveSpeed;
//             }
//         }
//     }
//     
//
//     // /// Moves a group's virtual center anchor from the current group center.
//     // public void MoveAnchorFromCurrentCenter(
//     //     ControlGroup cg,
//     //     List<MilitaryController> military,
//     //     Vector3 destination,
//     //     Vector3 facing)
//     // {
//     //     if (cg == null || military == null || military.Count == 0)
//     //         return;
//     //
//     //     float speed = military.Min(u => u.Stats.moveSpeed);
//     //     Vector3 currentCenter = GetAveragePosition(military.Select(mc => mc as UnitController).ToList());
//     //
//     //     if (cg.anchor == null)
//     //         cg.anchor = new FormationAnchor(currentCenter, facing, speed);
//     //
//     //     cg.anchor.speed = speed;
//     //     cg.anchor.position = currentCenter;
//     //     cg.anchor.facing = facing;
//     //     cg.anchor.destination = currentCenter;
//     //     cg.anchor.isMoving = false;
//     //
//     //     cg.anchor.MoveTo(destination, facing);
//     // }
//     
//     /// Moves a group's virtual anchor.
//     /// If the anchor already exists, keep its current position as truth.
//     /// Only use unit average when creating/recovering the anchor.
//     public void MoveFormationAnchorTo( 
//         ControlGroup cg,
//         List<MilitaryController> military,
//         Vector3 destination,
//         Vector3 facing)
//     {
//         if (cg == null || military == null || military.Count == 0)
//             return;
//
//         float speed = military.Min(u => u.Stats.moveSpeed);
//
//         if (cg.anchor == null)
//         {
//             Vector3 currentCenter = GetAveragePosition(
//                 military.Select(mc => mc as UnitController).ToList());
//
//             cg.anchor = new FormationAnchor(currentCenter, facing, speed);
//         }
//
//         cg.anchor.speed = speed;
//         // (formation rotations)
//         // cg.anchor.facing = facing;
//         cg.anchor.MoveTo(destination, facing);
//     }
//     
//     
//     #endregion
//
//     
//     
//     #region Reform In Place
//     /// Reforms a control group in place at current positions using saved offsets
//     public void ReformInPlace(ControlGroup cg)
//     {
//         List<MilitaryController> military = ControlGroupManager.Instance.GetMilitaryFromGroup(cg);
//         if (military.Count == 0) return;
//
//         Vector3 center = GetAveragePosition(military.Select(mc => mc as UnitController).ToList());
//         Vector3 facing = military[0].transform.forward;
//
//         // Update anchor to current position so slots are correct
//         if (cg.anchor != null)
//         {
//             cg.anchor.position = center;
//             cg.anchor.facing = facing;
//             cg.anchor.destination = center;
//             cg.anchor.isMoving = false;
//         }
//
//         List<Vector3> worldPositions = ConvertOffsetsToWorldPositions(cg.formationOffsets, center, facing);
//         var slots = AssignUnitsToNearestSlots(
//             military.Select(mc => mc as UnitController).ToList(), worldPositions);
//
//         foreach (var slot in slots)
//             slot.Key.OrderMove(slot.Value);
//
//         FormationVisualizer.Instance.ShowSlots(new List<Vector3>(slots.Values));
//     }
//
//     /// Reforms selected units in place — no control group, no save
//     public void ReformInPlaceTemporary(List<ISelectable> selected, List<Vector2> offsets)
//     {
//         List<MilitaryController> military = selected
//             .Select(s => s.GetGameObject().GetComponent<MilitaryController>())
//             .Where(mc => mc != null)
//             .ToList();
//
//         if (military.Count == 0) return;
//
//         Vector3 center = GetAveragePosition(military.Select(mc => mc as UnitController).ToList());
//         Vector3 facing = military[0].transform.forward;
//
//         List<Vector3> worldPositions = ConvertOffsetsToWorldPositions(offsets, center, facing);
//
//         var slots = AssignUnitsToNearestSlots(
//             military.Select(mc => mc as UnitController).ToList(), worldPositions);
//
//         foreach (var slot in slots)
//             slot.Key.OrderMove(slot.Value);
//
//         FormationVisualizer.Instance.ShowSlots(new List<Vector3>(slots.Values));
//     }
//
//     /// Reforms selected units in place based off passed formation - no control group needed, no save
//     public void ReformTemporaryWithFormation(List<UnitController> military, UnitFormation formation) // Check against regular InPlace function and see if we need both.
//     {
//         float width = Mathf.Min(military.Count, 10) * FormationManager.Instance.DefaultSpacing;
//         List<Vector2> offsets = FormationManager.Instance.CalculateOffsets(
//             military.Count, width, formation);
//         FormationManager.Instance.ReformInPlaceTemporary(
//             SelectionManager.Instance.GetSelectedObjects(), offsets);
//     }
//     #endregion
//
//     #region Offset Calculations
//     /// Rebuilds centered offsets for a control group.
//     public void RebuildGroupOffsets(ControlGroup cg, int unitCount)
//     {
//         if (cg == null) return;
//
//         float width = cg.formationWidth > 0
//             ? cg.formationWidth
//             : unitCount * defaultSpacing;
//
//         cg.formationOffsets = CalculateOffsets(
//             unitCount,
//             width,
//             cg.formation);
//     }
//
//     /// Calculates formation offsets in local formation space.
//     /// X = right, Y = forward.
//     /// Offsets are centered so the FormationAnchor represents the formation center.
//     public List<Vector2> CalculateOffsets(int unitCount, float width, UnitFormation formation = UnitFormation.Line)
//     {
//         if (unitCount <= 0)
//             return new List<Vector2>();
//
//         int unitsPerRow = Mathf.Max(1, Mathf.FloorToInt(width / defaultSpacing));
//
//         List<Vector2> offsets = formation switch
//         {
//             UnitFormation.Line   => CalculateLineOffsets(unitCount, unitsPerRow),
//             UnitFormation.Spread => CalculateSpreadOffsets(unitCount, unitsPerRow, width),
//             UnitFormation.Box    => CalculateBoxOffsets(unitCount, unitsPerRow),
//             UnitFormation.Circle => CalculateCircleOffsets(unitCount, width / 2f),
//             UnitFormation.Wedge  => CalculateWedgeOffsets(unitCount, unitsPerRow),
//             _ => CalculateLineOffsets(unitCount, unitsPerRow)
//         };
//
//         return CenterOffsets(offsets);
//     }
//
//     /// Converts formation offsets to world positions given destination and facing
//     public List<Vector3> ConvertOffsetsToWorldPositions(
//         List<Vector2> offsets, Vector3 destination, Vector3 facingDirection)
//     {
//         Vector3 facing = facingDirection.normalized;
//         facing.y = 0f;
//         Vector3 perpendicular = Calc.Perpendicular(facing);
//
//         List<Vector3> positions = new List<Vector3>();
//         foreach (Vector2 offset in offsets)
//             positions.Add(destination + perpendicular * offset.x + facing * offset.y);
//
//         return positions;
//     }
//
//     /// Centers local formation offsets around their average point
//     /// This makes FormationAnchor.position represent the true center of the formation.
//     List<Vector2> CenterOffsets(List<Vector2> offsets)
//     {
//         if (offsets == null || offsets.Count == 0)
//             return offsets;
//
//         Vector2 center = Vector2.zero;
//
//         foreach (Vector2 offset in offsets)
//             center += offset;
//
//         center /= offsets.Count;
//
//         for (int i = 0; i < offsets.Count; i++)
//             offsets[i] -= center;
//
//         return offsets;
//     }
//
//     /// Line — rows of unitsPerRow, centered
//     List<Vector2> CalculateLineOffsets(int unitCount, int unitsPerRow = 10)
//     {
//         List<Vector2> offsets = new List<Vector2>();
//
//         for (int i = 0; i < unitCount; i++)
//         {
//             int row = i / unitsPerRow;
//             int col = i % unitsPerRow;
//             int unitsInRow = Mathf.Min(unitsPerRow, unitCount - row * unitsPerRow);
//             float rowWidth = (unitsInRow - 1) * defaultSpacing;
//
//             float x = col * defaultSpacing - rowWidth / 2f;
//             float y = -row * defaultSpacing;
//             offsets.Add(new Vector2(x, y));
//         }
//
//         return offsets;
//     }
//
//     /// Spread — fills rectangle with maximum spacing
//     List<Vector2> CalculateSpreadOffsets(int unitCount, int unitsPerRow, float width)
//     {
//         List<Vector2> offsets = new List<Vector2>();
//
//         float aspect = 1.5f;
//         int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(unitCount * aspect)));
//
//         float colSpacing = cols > 1 ? width / (cols - 1) : 0f;
//         float rowSpacing = colSpacing * 0.8f;
//
//         for (int i = 0; i < unitCount; i++)
//         {
//             int row = i / cols;
//             int col = i % cols;
//             int unitsInRow = Mathf.Min(cols, unitCount - row * cols);
//
//             float rowWidth = (unitsInRow - 1) * colSpacing;
//             float x = col * colSpacing - rowWidth / 2f;
//             float y = -row * rowSpacing;
//             offsets.Add(new Vector2(x, y));
//         }
//
//         return offsets;
//     }
//
//     /// Box — units around perimeter of rectangle, interior fill if needed
//     List<Vector2> CalculateBoxOffsets(int unitCount, int unitsPerRow)
//     {
//         List<Vector2> offsets = new List<Vector2>();
//
//         int cols = Mathf.Max(2, unitsPerRow);
//         float width = (cols - 1) * defaultSpacing;
//
//         int frontBack = cols;
//         int remaining = unitCount - frontBack * 2;
//         int sideRows = remaining > 0 ? Mathf.CeilToInt(remaining / 2f) + 1 : 1;
//         float depth = sideRows * defaultSpacing;
//
//         List<Vector2> perimeter = new List<Vector2>();
//
//         for (int i = 0; i < cols; i++)
//             perimeter.Add(new Vector2(i * defaultSpacing - width / 2f, 0));
//
//         for (int i = 0; i < cols; i++)
//             perimeter.Add(new Vector2(i * defaultSpacing - width / 2f, -depth));
//
//         for (int i = 1; i < sideRows; i++)
//             perimeter.Add(new Vector2(-width / 2f, -i * defaultSpacing));
//
//         for (int i = 1; i < sideRows; i++)
//             perimeter.Add(new Vector2(width / 2f, -i * defaultSpacing));
//
//         int perimeterCount = Mathf.Min(unitCount, perimeter.Count);
//         for (int i = 0; i < perimeterCount; i++)
//             offsets.Add(perimeter[i]);
//
//         if (unitCount > perimeter.Count)
//         {
//             int interior = unitCount - perimeter.Count;
//             int intCols = Mathf.Max(1, cols - 2);
//             for (int i = 0; i < interior; i++)
//             {
//                 int row = i / intCols + 1;
//                 int col = i % intCols;
//                 float intWidth = (intCols - 1) * defaultSpacing;
//                 float x = col * defaultSpacing - intWidth / 2f;
//                 float y = -row * defaultSpacing;
//                 offsets.Add(new Vector2(x, y));
//             }
//         }
//
//         return offsets;
//     }
//
//     /// Circle — units evenly around circumference
//     List<Vector2> CalculateCircleOffsets(int unitCount, float radius)
//     {
//         List<Vector2> offsets = new List<Vector2>();
//         float angleStep = 360f / unitCount;
//
//         for (int i = 0; i < unitCount; i++)
//         {
//             float angle = i * angleStep * Mathf.Deg2Rad;
//             float x = Mathf.Sin(angle) * radius;
//             float y = Mathf.Cos(angle) * radius;
//             offsets.Add(new Vector2(x, y));
//         }
//
//         return offsets;
//     }
//
//     /// Wedge — V shape, tip at front
//     List<Vector2> CalculateWedgeOffsets(int unitCount, int unitsPerRow = 5)
//     {
//         List<Vector2> offsets = new List<Vector2>();
//         offsets.Add(new Vector2(0, 0));
//
//         int side = 1;
//         int row = 1;
//         float spread = defaultSpacing * (unitsPerRow / 5f);
//
//         for (int i = 1; i < unitCount; i++)
//         {
//             float x = side * row * spread * 0.5f;
//             float y = -row * spread * 0.5f;
//             offsets.Add(new Vector2(x, y));
//             side = -side;
//             if (side == 1) row++;
//         }
//
//         return offsets;
//     }
//     #endregion
//
//     #region Slot Assignment
//     // Used for 
//     /// Assigns each unit to nearest available slot — minimizes crossing paths
//     public Dictionary<UnitController, Vector3> AssignUnitsToNearestSlots(
//         List<UnitController> units, List<Vector3> slots)
//     {
//         Dictionary<UnitController, Vector3> result = new Dictionary<UnitController, Vector3>();
//         List<Vector3> available = new List<Vector3>(slots);
//         List<UnitController> sorted = Calc.SortByID(units);
//
//         foreach (UnitController unit in sorted)
//         {
//             if (available.Count == 0) break;
//
//             Vector3 nearest = available[0];
//             float nearestDist = Calc.SqrDistance(unit.transform.position, nearest);
//
//             foreach (Vector3 slot in available)
//             {
//                 float dist = Calc.SqrDistance(unit.transform.position, slot);
//                 if (dist < nearestDist) { nearestDist = dist; nearest = slot; }
//             }
//
//             result[unit] = nearest;
//             available.Remove(nearest);
//         }
//
//         return result;
//     }
//     
//     
//     // Use when calling a new move order, reassign formation offsets based off the new facing direction.
//     /// Reassigns each military unit to the nearest offset slot for the new facing.
//     /// This prevents units from crossing through the formation when the group flips direction.
//     public void ReassignUnitsToOffsetsForFacing(ControlGroup cg, List<MilitaryController> military, Vector3 facing)
//     {
//         if (cg == null) return;
//         if (military == null || military.Count == 0) return;
//         if (cg.formationOffsets == null || cg.formationOffsets.Count != military.Count) return;
//
//         Vector3 center = GetAveragePosition(military.Select(mc => mc as UnitController).ToList());
//
//         List<Vector3> currentSlots = ConvertOffsetsToWorldPositions(
//             cg.formationOffsets,
//             center,
//             facing);
//
//         List<int> availableSlotIndices = new List<int>();
//         for (int i = 0; i < currentSlots.Count; i++)
//             availableSlotIndices.Add(i);
//
//         cg.unitToOffsetIndex.Clear();
//
//         foreach (MilitaryController mc in military)
//         {
//             int bestListIndex = -1;
//             int bestOffsetIndex = -1;
//             float bestDist = float.MaxValue;
//
//             for (int i = 0; i < availableSlotIndices.Count; i++)
//             {
//                 int offsetIndex = availableSlotIndices[i];
//                 float dist = Calc.SqrDistance(mc.transform.position, currentSlots[offsetIndex]);
//
//                 if (dist < bestDist)
//                 {
//                     bestDist = dist;
//                     bestListIndex = i;
//                     bestOffsetIndex = offsetIndex;
//                 }
//             }
//
//             if (bestOffsetIndex < 0)
//                 continue;
//
//             mc.offsetIndex = bestOffsetIndex;
//             cg.unitToOffsetIndex[mc.GetInstanceID()] = bestOffsetIndex;
//             availableSlotIndices.RemoveAt(bestListIndex);
//         }
//     }
//     
//     
//     #endregion
//
//     
//     
//     #region Helpers
//     Vector3 GetAveragePosition(List<UnitController> units)
//     {
//         Vector3 avg = Vector3.zero;
//         foreach (UnitController u in units) avg += u.transform.position;
//         return avg / units.Count;
//     }
//     
//     
//     float GetCatchUpSpeedMultiplier(float distanceFromSlot)
//     {
//         if (Calc.WithinRange(distanceFromSlot, catchUpStartDistance))
//             return 1f;
//
//         float t = Mathf.InverseLerp(
//             catchUpStartDistance,
//             catchUpMaxDistance,
//             distanceFromSlot);
//
//         return Mathf.Lerp(
//             1f,
//             maxCatchUpSpeedMultiplier,
//             t);
//     }
//     
//     
//     /// Returns true if the formation changed facing enough that units should swap slots.
//     /// Small turns preserve slot identity so the formation stays concrete.
//     public bool ShouldReassignUnitsForFacingChange(ControlGroup cg, Vector3 newFacing, float angleThreshold = 100f)
//     {
//         if (cg == null)
//             return false;
//
//         if (cg.anchor == null)
//             return true;
//
//         Vector3 oldFacing = cg.anchor.facing;
//         oldFacing.y = 0f;
//         newFacing.y = 0f;
//
//         if (oldFacing == Vector3.zero || newFacing == Vector3.zero)
//             return false;
//
//         float angle = Vector3.Angle(oldFacing.normalized, newFacing.normalized);
//
//         return angle >= angleThreshold;
//     }
//
//
//     public bool CanAnchorReachDestination(ControlGroup cg, Vector3 destination)
//     {
//         if (cg == null || cg.anchor == null) return false;
//         
//         NavMeshPath path = new NavMeshPath();
//         
//         bool hasPath = NavMesh.CalculatePath(
//             cg.anchor.position,
//             destination,
//             NavMesh.AllAreas,
//             path);
//
//         return hasPath && path.status == NavMeshPathStatus.PathComplete; // complete path
//     }
//     
//     #endregion
//
//     
//
//
//     #region GIZMOS
//
//     // DELETE AFTER TESTING
//     // void OnDrawGizmos() // Currently draws anchor, facing direction, and destination
//     // {
//     //     if (!Application.isPlaying) return;
//     //     if (ControlGroupManager.Instance == null) return;
//     //
//     //     foreach (ControlGroup cg in ControlGroupManager.Instance.GetControlGroups())
//     //     {
//     //         if (cg == null || cg.anchor == null) continue;
//     //
//     //         Vector3 pos = cg.anchor.position + Vector3.up * 0.15f;
//     //         Vector3 dest = cg.anchor.destination + Vector3.up * 0.15f;
//     //         Vector3 facingEnd = pos + cg.anchor.facing.normalized * 3f;
//     //
//     //         // Anchor position
//     //         Gizmos.color = Color.yellow;
//     //         Gizmos.DrawSphere(pos, 0.35f);
//     //
//     //         // Facing direction
//     //         Gizmos.color = Color.blue;
//     //         Gizmos.DrawLine(pos, facingEnd);
//     //         Gizmos.DrawSphere(facingEnd, 0.15f);
//     //
//     //         // Destination
//     //         Gizmos.color = Color.green;
//     //         Gizmos.DrawWireSphere(dest, 0.45f);
//     //
//     //         // Anchor to destination
//     //         Gizmos.color = Color.white;
//     //         Gizmos.DrawLine(pos, dest);
//     //     }
//     // }
//
//     #endregion
// }
//
//
//
// // /// Estimates the best current anchor position for a group using the units'
// // /// current world positions, their saved offsets, and the new facing direction.
// // public Vector3 EstimateAnchorPosition(
// //     List<MilitaryController> military,
// //     List<Vector2> offsets,
// //     Vector3 facingDirection)
// // {
// //     if (military == null || military.Count == 0)
// //         return Vector3.zero;
// //
// //     Vector3 facing = facingDirection;
// //     facing.y = 0f;
// //
// //     if (facing == Vector3.zero)
// //         facing = military[0].transform.forward;
// //
// //     facing.Normalize();
// //
// //     Vector3 right = Calc.Perpendicular(facing);
// //
// //     Vector3 sum = Vector3.zero;
// //     int count = 0;
// //
// //     foreach (MilitaryController mc in military)
// //     {
// //         if (mc.offsetIndex < 0 || mc.offsetIndex >= offsets.Count)
// //             continue;
// //
// //         Vector2 offset = offsets[mc.offsetIndex];
// //
// //         Vector3 rotatedOffset =
// //             right * offset.x +
// //             facing * offset.y;
// //
// //         Vector3 estimatedAnchor = mc.transform.position - rotatedOffset;
// //
// //         sum += estimatedAnchor;
// //         count++;
// //     }
// //
// //     if (count == 0)
// //         return GetAveragePosition(military.Select(mc => mc as UnitController).ToList());
// //
// //     return sum / count;
// // }
//
//
// // /// Full pipeline — calculates offsets and converts to world positions
// // public Dictionary<UnitController, Vector3> CalculateFormationPositions(
// //     List<UnitController> units, Vector3 destination, Vector3 facingDirection,
// //     List<Vector2> savedOffsets = null)
// // {
// //     List<Vector2> offsets = savedOffsets != null && savedOffsets.Count == units.Count
// //         ? savedOffsets
// //         : CalculateOffsets(units.Count, units.Count * defaultSpacing, UnitFormation.Line); // Default width = 10 or units.Count * defaultSpacing
// //
// //     List<Vector3> worldPositions = OffsetsToWorldPositions(offsets, destination, facingDirection);
// //     return AssignNearestSlots(units, worldPositions);
// // }
//
//
//
//
//
// // public class FormationAnchor
// // {
// //     public Vector3 position;
// //     public Vector3 facing;
// //     public Vector3 destination;
// //     public float speed;
// //     public bool isMoving;
// //
// //     // (rotating formations)
// //     public Vector3 targetFacing;
// //     public float turnSpeed = 180f; // degrees per second
// //
// //     public FormationAnchor(Vector3 startPos, Vector3 startFacing, float speed)
// //     {
// //         position = startPos;
// //         facing = NormalizeFacing(startFacing);
// //         targetFacing = facing;
// //         destination = startPos;
// //         this.speed = speed;
// //         isMoving = false;
// //     }
// //
// //     // /// Moves anchor toward destination each frame.
// //     // /// Facing is controlled by move/reform commands, not by travel direction.
// //     // public void Tick()
// //     // {
// //     //     if (!isMoving) return;
// //     //
// //     //     position = Vector3.MoveTowards(position, destination, speed * Time.deltaTime);
// //     //
// //     //     if (Calc.WithinRange(position, destination, 0.1f))
// //     //     {
// //     //         position = destination;
// //     //         isMoving = false;
// //     //     }
// //     // }
// //
// //     /// Moves anchor toward destination and rotates toward target facing.
// //     public void Tick()
// //     {
// //         if (!isMoving) return;
// //
// //         position = Vector3.MoveTowards(
// //             position,
// //             destination,
// //             speed * Time.deltaTime);
// //
// //         facing = Vector3.RotateTowards(
// //             facing,
// //             targetFacing,
// //             turnSpeed * Mathf.Deg2Rad * Time.deltaTime,
// //             0f);
// //
// //         if (Calc.WithinRange(position, destination, 0.1f))
// //         {
// //             position = destination;
// //             isMoving = false;
// //         }
// //     }
// //     
// //     
// //     /// Sets a new destination for the virtual center anchor.
// //     public void MoveTo(Vector3 dest, Vector3 facingDir = default)
// //     {
// //         destination = dest;
// //
// //         // if (facingDir != default)
// //         //     facing = NormalizeFacing(facingDir);
// //         
// //         // (rotating formations)
// //         if (facingDir != default)
// //             targetFacing = NormalizeFacing(facingDir);
// //
// //         isMoving = true;
// //     }
// //
// //     /// Returns world position for a given centered formation offset.
// //     public Vector3 GetSlotPosition(Vector2 offset, FormationManager fm)
// //     {
// //         return fm.ConvertOffsetsToWorldPositions(
// //             new List<Vector2> { offset },
// //             position,
// //             facing)[0];
// //     }
// //
// //     Vector3 NormalizeFacing(Vector3 dir)
// //     {
// //         dir.y = 0f;
// //
// //         if (dir == Vector3.zero)
// //             return Vector3.forward;
// //
// //         return dir.normalized;
// //     }
// //     
// //     
// // }