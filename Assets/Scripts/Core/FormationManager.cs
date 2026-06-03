using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FormationAnchor
{
    public Vector3 position;
    public Vector3 facing;
    public Vector3 destination;
    public float speed;
    public bool isMoving;

    public FormationAnchor(Vector3 startPos, Vector3 startFacing, float speed)
    {
        position = startPos;
        facing = startFacing;
        facing.y = 0f;
        if (facing != Vector3.zero)
            facing.Normalize();

        destination = startPos;
        this.speed = speed;
        isMoving = false;
    }

    /// Moves anchor toward destination each frame.
    /// Facing is set by MoveTo / reform logic, not by travel direction.
    public void Tick()
    {
        if (!isMoving) return;

        position = Vector3.MoveTowards(position, destination, speed * Time.deltaTime);

        if (Calc.WithinRange(position, destination, 0.1f))
        {
            position = destination;
            isMoving = false;
        }
    }

    /// Sets a new destination for the anchor.
    public void MoveTo(Vector3 dest, Vector3 facingDir = default)
    {
        destination = dest;

        if (facingDir != default)
        {
            facingDir.y = 0f;
            if (facingDir != Vector3.zero)
                facing = facingDir.normalized;
        }

        isMoving = true;
    }

    /// Returns world position for a given formation offset.
    public Vector3 GetSlotPosition(Vector2 offset, FormationManager fm)
    {
        return fm.OffsetsToWorldPositions(
            new List<Vector2> { offset }, position, facing)[0];
    }
}

public class FormationManager : MonoBehaviour
{
    public static FormationManager Instance { get; private set; }

    [SerializeField] private float defaultSpacing = 2f;
    public float DefaultSpacing => defaultSpacing;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        TickFormationAnchors();
    }

    #region Anchor Ticking
    void TickFormationAnchors()
    {
        foreach (ControlGroup cg in ControlGroupManager.Instance.GetControlGroups())
        {
            if (cg.anchor == null || !cg.anchor.isMoving) continue;
            cg.anchor.Tick();
            UpdateGroupSlots(cg);
        }
    }

    void UpdateGroupSlots(ControlGroup cg)
    {
        if (cg.formationOffsets.Count == 0) return;

        List<MilitaryController> military = ControlGroupManager.Instance.GetMilitaryFromGroup(cg);

        foreach (MilitaryController mc in military)
        {
            if (mc.offsetIndex < 0 || mc.offsetIndex >= cg.formationOffsets.Count) continue;

            Vector3 newSlotPos = cg.anchor.GetSlotPosition(
                cg.formationOffsets[mc.offsetIndex], this);

            mc.formationSlot = newSlotPos;

            // Only update NavMesh destination if slot moved significantly
            if (Calc.OutOfRange(mc.lastSlotPos, newSlotPos, 0.5f))
            {
                // Only drive agent if formation mode is active
                if (cg.formationMode)
                    mc.Agent.SetDestination(newSlotPos);
                mc.lastSlotPos = newSlotPos;
            }
        }
    }
    
    /// Estimates the best current anchor position for a group using the units'
    /// current world positions, their saved offsets, and the new facing direction.
    public Vector3 EstimateAnchorPosition(
        List<MilitaryController> military,
        List<Vector2> offsets,
        Vector3 facingDirection)
    {
        if (military == null || military.Count == 0)
            return Vector3.zero;

        Vector3 facing = facingDirection;
        facing.y = 0f;

        if (facing == Vector3.zero)
            facing = military[0].transform.forward;

        facing.Normalize();

        Vector3 right = Calc.Perpendicular(facing);

        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (MilitaryController mc in military)
        {
            if (mc.offsetIndex < 0 || mc.offsetIndex >= offsets.Count)
                continue;

            Vector2 offset = offsets[mc.offsetIndex];

            Vector3 rotatedOffset =
                right * offset.x +
                facing * offset.y;

            Vector3 estimatedAnchor = mc.transform.position - rotatedOffset;

            sum += estimatedAnchor;
            count++;
        }

        if (count == 0)
            return GetAveragePosition(military.Select(mc => mc as UnitController).ToList());

        return sum / count;
    }
    #endregion

    #region Reform In Place
    /// Reforms a control group in place at current positions using saved offsets
    public void ReformInPlace(ControlGroup cg)
    {
        List<MilitaryController> military = ControlGroupManager.Instance.GetMilitaryFromGroup(cg);
        if (military.Count == 0) return;

        Vector3 center = GetAveragePosition(military.Select(mc => mc as UnitController).ToList());
        Vector3 facing = military[0].transform.forward;

        // Update anchor to current position so slots are correct
        if (cg.anchor != null)
        {
            cg.anchor.position = center;
            cg.anchor.facing = facing;
            cg.anchor.destination = center;
            cg.anchor.isMoving = false;
        }

        List<Vector3> worldPositions = OffsetsToWorldPositions(cg.formationOffsets, center, facing);
        var slots = AssignNearestSlots(
            military.Select(mc => mc as UnitController).ToList(), worldPositions);

        foreach (var slot in slots)
            slot.Key.OrderMove(slot.Value);

        FormationVisualizer.Instance.ShowSlots(new List<Vector3>(slots.Values));
    }

    /// Reforms selected units in place — no control group, no save
    public void ReformInPlaceTemporary(List<ISelectable> selected, List<Vector2> offsets)
    {
        List<MilitaryController> military = selected
            .Select(s => s.GetGameObject().GetComponent<MilitaryController>())
            .Where(mc => mc != null)
            .ToList();

        if (military.Count == 0) return;

        Vector3 center = GetAveragePosition(military.Select(mc => mc as UnitController).ToList());
        Vector3 facing = military[0].transform.forward;

        List<Vector3> worldPositions = OffsetsToWorldPositions(offsets, center, facing);

        var slots = AssignNearestSlots(
            military.Select(mc => mc as UnitController).ToList(), worldPositions);

        foreach (var slot in slots)
            slot.Key.OrderMove(slot.Value);

        FormationVisualizer.Instance.ShowSlots(new List<Vector3>(slots.Values));
    }
    
    
    /// Reforms selected units in place based off passed formation - no control group needed, no save
    public void ReformTemporaryWithFormation(List<UnitController> military, UnitFormation formation) // Check against regular InPlace function and see if we need both.
    {
        float width = Mathf.Min(military.Count, 10) * FormationManager.Instance.DefaultSpacing;
        List<Vector2> offsets = FormationManager.Instance.CalculateOffsets(
            military.Count, width, formation);
        FormationManager.Instance.ReformInPlaceTemporary(
            SelectionManager.Instance.GetSelectedObjects(), offsets);
    }
    #endregion

    #region Offset Calculations
    /// Calculates formation offsets in local formation space (X = right, Y = forward).
    /// Width drives units per row. Formation type drives arrangement.
    public List<Vector2> CalculateOffsets(int unitCount, float width, UnitFormation formation = UnitFormation.Line)
    {
        int unitsPerRow = Mathf.Max(1, Mathf.FloorToInt(width / defaultSpacing));

        return formation switch
        {
            UnitFormation.Line   => CalculateLineOffsets(unitCount, unitsPerRow),
            UnitFormation.Spread => CalculateSpreadOffsets(unitCount, unitsPerRow, width),
            UnitFormation.Box    => CalculateBoxOffsets(unitCount, unitsPerRow),
            UnitFormation.Circle => CalculateCircleOffsets(unitCount, width / 2f),
            UnitFormation.Wedge  => CalculateWedgeOffsets(unitCount, unitsPerRow),
            _ => CalculateLineOffsets(unitCount, unitsPerRow)
        };
    }

    /// Converts formation offsets to world positions given destination and facing
    public List<Vector3> OffsetsToWorldPositions(
        List<Vector2> offsets, Vector3 destination, Vector3 facingDirection)
    {
        Vector3 facing = facingDirection.normalized;
        facing.y = 0f;
        Vector3 perpendicular = Calc.Perpendicular(facing);

        List<Vector3> positions = new List<Vector3>();
        foreach (Vector2 offset in offsets)
            positions.Add(destination + perpendicular * offset.x + facing * offset.y);

        return positions;
    }

    /// Full pipeline — calculates offsets and converts to world positions
    public Dictionary<UnitController, Vector3> CalculateFormationPositions(
        List<UnitController> units, Vector3 destination, Vector3 facingDirection,
        List<Vector2> savedOffsets = null)
    {
        List<Vector2> offsets = savedOffsets != null && savedOffsets.Count == units.Count
            ? savedOffsets
            : CalculateOffsets(units.Count, units.Count * defaultSpacing, UnitFormation.Line); // Default width = 10 or units.Count * defaultSpacing

        List<Vector3> worldPositions = OffsetsToWorldPositions(offsets, destination, facingDirection);
        return AssignNearestSlots(units, worldPositions);
    }

    /// Line — rows of unitsPerRow, centered
    List<Vector2> CalculateLineOffsets(int unitCount, int unitsPerRow = 10)
    {
        List<Vector2> offsets = new List<Vector2>();

        for (int i = 0; i < unitCount; i++)
        {
            int row = i / unitsPerRow;
            int col = i % unitsPerRow;
            int unitsInRow = Mathf.Min(unitsPerRow, unitCount - row * unitsPerRow);
            float rowWidth = (unitsInRow - 1) * defaultSpacing;

            float x = col * defaultSpacing - rowWidth / 2f;
            float y = -row * defaultSpacing;
            offsets.Add(new Vector2(x, y));
        }

        return offsets;
    }

    /// Spread — fills rectangle with maximum spacing
    List<Vector2> CalculateSpreadOffsets(int unitCount, int unitsPerRow, float width)
    {
        List<Vector2> offsets = new List<Vector2>();

        float aspect = 1.5f;
        int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(unitCount * aspect)));

        float colSpacing = cols > 1 ? width / (cols - 1) : 0f;
        float rowSpacing = colSpacing * 0.8f;

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

    /// Box — units around perimeter of rectangle, interior fill if needed
    List<Vector2> CalculateBoxOffsets(int unitCount, int unitsPerRow)
    {
        List<Vector2> offsets = new List<Vector2>();

        int cols = Mathf.Max(2, unitsPerRow);
        float width = (cols - 1) * defaultSpacing;

        int frontBack = cols;
        int remaining = unitCount - frontBack * 2;
        int sideRows = remaining > 0 ? Mathf.CeilToInt(remaining / 2f) + 1 : 1;
        float depth = sideRows * defaultSpacing;

        List<Vector2> perimeter = new List<Vector2>();

        for (int i = 0; i < cols; i++)
            perimeter.Add(new Vector2(i * defaultSpacing - width / 2f, 0));

        for (int i = 0; i < cols; i++)
            perimeter.Add(new Vector2(i * defaultSpacing - width / 2f, -depth));

        for (int i = 1; i < sideRows; i++)
            perimeter.Add(new Vector2(-width / 2f, -i * defaultSpacing));

        for (int i = 1; i < sideRows; i++)
            perimeter.Add(new Vector2(width / 2f, -i * defaultSpacing));

        int perimeterCount = Mathf.Min(unitCount, perimeter.Count);
        for (int i = 0; i < perimeterCount; i++)
            offsets.Add(perimeter[i]);

        if (unitCount > perimeter.Count)
        {
            int interior = unitCount - perimeter.Count;
            int intCols = Mathf.Max(1, cols - 2);
            for (int i = 0; i < interior; i++)
            {
                int row = i / intCols + 1;
                int col = i % intCols;
                float intWidth = (intCols - 1) * defaultSpacing;
                float x = col * defaultSpacing - intWidth / 2f;
                float y = -row * defaultSpacing;
                offsets.Add(new Vector2(x, y));
            }
        }

        return offsets;
    }

    /// Circle — units evenly around circumference
    List<Vector2> CalculateCircleOffsets(int unitCount, float radius)
    {
        List<Vector2> offsets = new List<Vector2>();
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

    /// Wedge — V shape, tip at front
    List<Vector2> CalculateWedgeOffsets(int unitCount, int unitsPerRow = 5)
    {
        List<Vector2> offsets = new List<Vector2>();
        offsets.Add(new Vector2(0, 0));

        int side = 1;
        int row = 1;
        float spread = defaultSpacing * (unitsPerRow / 5f);

        for (int i = 1; i < unitCount; i++)
        {
            float x = side * row * spread * 0.5f;
            float y = -row * spread * 0.5f;
            offsets.Add(new Vector2(x, y));
            side = -side;
            if (side == 1) row++;
        }

        return offsets;
    }
    #endregion

    #region Slot Assignment
    /// Assigns each unit to nearest available slot — minimizes crossing paths
    public Dictionary<UnitController, Vector3> AssignNearestSlots(
        List<UnitController> units, List<Vector3> slots)
    {
        Dictionary<UnitController, Vector3> result = new Dictionary<UnitController, Vector3>();
        List<Vector3> available = new List<Vector3>(slots);
        List<UnitController> sorted = Calc.SortByID(units);

        foreach (UnitController unit in sorted)
        {
            if (available.Count == 0) break;

            Vector3 nearest = available[0];
            float nearestDist = Calc.SqrDistance(unit.transform.position, nearest);

            foreach (Vector3 slot in available)
            {
                float dist = Calc.SqrDistance(unit.transform.position, slot);
                if (dist < nearestDist) { nearestDist = dist; nearest = slot; }
            }

            result[unit] = nearest;
            available.Remove(nearest);
        }

        return result;
    }
    #endregion

    #region Helpers
    Vector3 GetAveragePosition(List<UnitController> units)
    {
        Vector3 avg = Vector3.zero;
        foreach (UnitController u in units) avg += u.transform.position;
        return avg / units.Count;
    }
    #endregion
}