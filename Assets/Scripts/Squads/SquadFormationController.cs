using System.Collections.Generic;
using UnityEngine;


/// -----------------------------------------------------------------------------
/// SquadFormationController
/// -----------------------------------------------------------------------------
///
/// Owns the squad's current formation definition and slot layout.
/// Stores formation type, facing, spacing, local offsets, world slots, and soldier
/// slot assignments.
///
/// This class calculates where soldiers should be in formation, but does not move
/// them. Movement execution belongs to SquadMovement/SoldierMotor.
///
/// Design role:
/// Formation geometry and slot ownership.
///
public class SquadFormationController : MonoBehaviour
{
    private SquadController squad;
    private SquadRoster roster;
    private SquadData data;

    private List<Vector2> localOffsets = new List<Vector2>();
    private List<Vector3> currentSlots = new List<Vector3>();

    private SquadFormation currentFormation = SquadFormation.Line;

    private float formationWidth = -1f;
    private float spacing = 2f;
    private int defaultUnitsPerRow = 10;

    private Vector3 facing = Vector3.forward;

    public SquadFormation CurrentFormation => currentFormation;
    public Vector3 Facing => facing;
    public float Width => formationWidth;
    public float Spacing => spacing;
    public IReadOnlyList<Vector2> LocalOffsets => localOffsets;
    public IReadOnlyList<Vector3> CurrentSlots => currentSlots;

    public void Initialize(
        SquadController owner,
        SquadRoster squadRoster,
        SquadData squadData)
    {
        squad = owner;
        roster = squadRoster;
        data = squadData;

        currentFormation = data.defaultFormation;
        spacing = Mathf.Max(0.1f, data.defaultSpacing);
        defaultUnitsPerRow = Mathf.Max(1, data.defaultUnitsPerRow);
        facing = NormalizeFacing(transform.forward);

        Rebuild();
        UpdateSlots(transform.position, facing);
    }

    public FormationBounds GetCurrentFormationBounds()
    {
        return GetFormationBounds();
    }

    /// Returns the axis-aligned local footprint of either the current formation or
    /// a proposed formation width. Bounds are always calculated from the same local
    /// offsets used by live slots and drag previews, so every formation shape owns
    /// a consistent rectangular group-movement footprint.
    public FormationBounds GetFormationBounds(
        float requestedFormationWidth = -1f)
    {
        List<Vector2> offsets = BuildFormationOffsets(
            requestedFormationWidth);

        if (FormationCalculator.Instance != null)
            return FormationCalculator.Instance.CalculateBounds(offsets);

        return CalculateFallbackBounds(offsets);
    }
    

    public void SetFormation(SquadFormation formation)
    {
        currentFormation = formation;
        Rebuild();
        UpdateSlots(transform.position, facing);
    }

    public void SetFacing(Vector3 newFacing)
    {
        facing = NormalizeFacing(newFacing);
        UpdateSlots(transform.position, facing);
    }

    public void SetFormationWidth(float width)
    {
        if (width > 0f)
            formationWidth = width;

        Rebuild();
        UpdateSlots(transform.position, facing);
    }

    public void Rebuild()
    {
        localOffsets = BuildFormationOffsets();
        AssignSlotIndices();
    }

    public void UpdateSlots(Vector3 center, Vector3 slotFacing)
    {
        facing = NormalizeFacing(slotFacing);

        if (FormationCalculator.Instance != null)
        {
            currentSlots = FormationCalculator.Instance.ConvertOffsetsToWorldPositions(
                localOffsets,
                center,
                facing);
        }
        else
        {
            currentSlots = ConvertOffsetsFallback(center, facing);
        }
    }

    public List<Vector3> GetWorldSlots(Vector3 center, Vector3 slotFacing)
    {
        slotFacing = NormalizeFacing(slotFacing);

        if (FormationCalculator.Instance != null)
        {
            return FormationCalculator.Instance.ConvertOffsetsToWorldPositions(
                localOffsets,
                center,
                slotFacing);
        }

        return ConvertOffsetsFallback(center, slotFacing);
    }

    /// Calculates preview slots without mutating the live formation.
    ///
    /// Important:
    /// This must not call Rebuild(), SetFacing(), UpdateSlots(), or AssignSlotIndices().
    /// Drag preview runs every frame while the player is holding right click, so it
    /// must be a pure read-only calculation.
    public List<Vector3> GetPreviewSlots(
        Vector3 center,
        Vector3 slotFacing,
        float requestedFormationWidth = -1f)
    {
        List<Vector2> previewOffsets = BuildFormationOffsets(
            requestedFormationWidth);

        if (FormationCalculator.Instance != null)
        {
            return FormationCalculator.Instance.ConvertOffsetsToWorldPositions(
                previewOffsets,
                center,
                slotFacing);
        }

        return ConvertPreviewOffsetsFallback(
            previewOffsets,
            center,
            slotFacing);
    }

    /// Converts temporary preview offsets to world positions without touching the
    /// live localOffsets/currentSlots/facing fields.
    List<Vector3> ConvertPreviewOffsetsFallback(
        List<Vector2> offsets,
        Vector3 center,
        Vector3 slotFacing)
    {
        List<Vector3> result = new List<Vector3>();

        if (offsets == null || offsets.Count == 0)
            return result;

        slotFacing = NormalizeFacing(slotFacing);
        Vector3 right = new Vector3(slotFacing.z, 0f, -slotFacing.x).normalized;

        foreach (Vector2 offset in offsets)
            result.Add(center + right * offset.x + slotFacing * offset.y);

        return result;
    }
    

    List<Vector2> BuildFormationOffsets(
        float requestedFormationWidth = -1f)
    {
        int count = roster != null ? roster.Count : 0;

        if (count <= 0)
            return new List<Vector2>();

        float resolvedWidth = requestedFormationWidth > 0f
            ? requestedFormationWidth
            : formationWidth > 0f
                ? formationWidth
                : GetDefaultWidth(count);

        if (FormationCalculator.Instance != null)
        {
            return FormationCalculator.Instance.CalculateOffsets(
                count,
                resolvedWidth,
                currentFormation,
                spacing);
        }

        return BuildFallbackLineOffsets(count);
    }

    float GetDefaultWidth(int count)
    {
        int unitsPerRow = Mathf.Clamp(
            defaultUnitsPerRow,
            1,
            Mathf.Max(1, count));

        return Mathf.Max(0f, (unitsPerRow - 1) * spacing);
    }

    void AssignSlotIndices()
    {
        if (roster == null)
            return;

        int nextSlotIndex = 0;

        for (int i = 0; i < roster.Soldiers.Count; i++)
        {
            SoldierController soldier = roster.Soldiers[i];

            if (soldier == null)
                continue;

            if (!soldier.IsAlive)
            {
                soldier.SetSlotIndex(-1);
                continue;
            }

            soldier.SetSlotIndex(nextSlotIndex);
            nextSlotIndex++;
        }
    }

    List<Vector2> BuildFallbackLineOffsets(int count)
    {
        List<Vector2> offsets = new List<Vector2>();

        if (count <= 0)
            return offsets;

        float width = (count - 1) * spacing;

        for (int i = 0; i < count; i++)
        {
            float x = i * spacing - width / 2f;
            offsets.Add(new Vector2(x, 0f));
        }

        return offsets;
    }

    List<Vector3> ConvertOffsetsFallback(Vector3 center, Vector3 slotFacing)
    {
        List<Vector3> result = new List<Vector3>();

        slotFacing = NormalizeFacing(slotFacing);
        Vector3 right = new Vector3(slotFacing.z, 0f, -slotFacing.x).normalized;

        foreach (Vector2 offset in localOffsets)
            result.Add(center + right * offset.x + slotFacing * offset.y);

        return result;
    }

    Vector3 NormalizeFacing(Vector3 value)
    {
        value.y = 0f;

        if (value == Vector3.zero)
            return Vector3.forward;

        return value.normalized;
    }
    
    public bool TryGetSlotForSoldier(
        SoldierController soldier,
        out Vector3 slot)
    {
        slot = transform.position;

        if (soldier == null)
            return false;

        int index = soldier.SlotIndex;

        if (index < 0 || index >= currentSlots.Count)
            return false;

        slot = currentSlots[index];
        return true;
    }
    
    /// Reassigns living soldiers to the nearest available slot for a new facing.
    /// This is used when the squad receives a large turn-around order.
    /// Instead of physically rotating the old front through the back, the squad
    /// instantly declares the new facing and assigns soldiers to the closest slots.
    public void ReassignLivingSoldiersToNearestSlots(
        Vector3 center,
        Vector3 slotFacing)
    {
        if (roster == null)
            return;

        UpdateSlots(center, slotFacing);

        if (currentSlots == null || currentSlots.Count == 0)
            return;

        List<int> availableSlotIndices = new List<int>();

        for (int i = 0; i < currentSlots.Count; i++)
            availableSlotIndices.Add(i);

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            if (availableSlotIndices.Count == 0)
                return;

            int bestAvailableListIndex = -1;
            int bestSlotIndex = -1;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < availableSlotIndices.Count; i++)
            {
                int slotIndex = availableSlotIndices[i];

                float distance = Vector3.SqrMagnitude(
                    soldier.transform.position - currentSlots[slotIndex]);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestAvailableListIndex = i;
                    bestSlotIndex = slotIndex;
                }
            }

            if (bestSlotIndex < 0)
                continue;

            soldier.SetSlotIndex(bestSlotIndex);

            // Force the next MoveToSlot call to issue a new destination.
            soldier.SetLastSlotPosition(Vector3.positiveInfinity);

            availableSlotIndices.RemoveAt(bestAvailableListIndex);
        }
    }
    
    
    /// Helper: visualizes this squad's current formation slots.
    public void VisualizeCurrentSlots(bool autoHide = true)
    {
        if (!FormationVisualizer.Instance)
            return;

        UpdateSlots(transform.position, facing);

        FormationVisualizer.Instance.ShowSlots(
            new List<Vector3>(currentSlots),
            facing,
            autoHide);
    }
    
    
    
    
    FormationBounds CalculateFallbackBounds(
        IReadOnlyList<Vector2> offsets)
    {
        if (offsets == null || offsets.Count == 0)
            return FormationBounds.Empty;

        float minX = offsets[0].x;
        float maxX = offsets[0].x;
        float minY = offsets[0].y;
        float maxY = offsets[0].y;

        for (int i = 1; i < offsets.Count; i++)
        {
            minX = Mathf.Min(minX, offsets[i].x);
            maxX = Mathf.Max(maxX, offsets[i].x);
            minY = Mathf.Min(minY, offsets[i].y);
            maxY = Mathf.Max(maxY, offsets[i].y);
        }

        return new FormationBounds
        {
            columnCount = offsets.Count,
            rowCount = 1,
            width = Mathf.Max(0f, maxX - minX),
            depth = Mathf.Max(0f, maxY - minY)
        };
    }
    
    
}