using System;
using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// SquadRoster
/// -----------------------------------------------------------------------------
///
/// Owns the runtime soldier list for a squad.
/// Handles spawning starting soldiers from SquadData/SoldierData, assigning slot
/// indices, tracking living/existing soldiers, and notifying the squad when
/// members die or are removed.
///
/// This class is the authority for squad membership. Other systems may read the
/// roster, but should not directly own or rebuild the soldier list.
///
/// Design role:
/// The squad's "membership/body" system.
///
public class SquadRoster : MonoBehaviour
{
    public event Action<SquadRoster> OnRosterChanged;

    private Transform soldierParent; // WAS serialized

    private readonly List<SoldierController> soldiers = new List<SoldierController>();

    private SquadController squad;
    private SquadData squadData;
    private FactionInstance _faction;

    public IReadOnlyList<SoldierController> Soldiers => soldiers;
    public int Count => GetExistingCount();
    public int LivingCount => GetLivingCount();
    public bool HasLivingSoldiers => LivingCount > 0;
    public FactionInstance Faction => _faction;

    public void Initialize(
        SquadController owner,
        SquadData data,
        FactionInstance ownerFactionInstance)
    {
        squad = owner;
        squadData = data;
        _faction = ownerFactionInstance;

        if (soldierParent == null)
            soldierParent = transform;

        ClearExistingRuntimeSoldiers();
        SpawnStartingSoldiers();

        OnRosterChanged?.Invoke(this);
    }

    public void AddExistingSoldier(SoldierController soldier)
    {
        if (soldier == null)
            return;

        if (soldiers.Contains(soldier))
            return;

        soldiers.Add(soldier);
        soldier.SetSquad(squad, this);
        soldier.SetSlotIndex(soldiers.Count - 1);

        OnRosterChanged?.Invoke(this);
    }

    public void RemoveSoldier(SoldierController soldier)
    {
        if (soldier == null)
            return;

        if (!soldiers.Remove(soldier))
            return;

        ReassignLivingSlotIndices();
        OnRosterChanged?.Invoke(this);

        if (!HasLivingSoldiers)
            HandleEmptySquad();
    }

    public void NotifySoldierDied(SoldierController soldier)
    {
        if (soldier != null)
            soldier.SetSlotIndex(-1);

        OnRosterChanged?.Invoke(this);

        // Do not compact slots during active melee.
        // Mid-combat compaction is what causes flipping/crossing.
        if (squad != null &&
            squad.State != SquadState.InCombat &&
            squad.State != SquadState.ApproachingCombat &&
            squad.State != SquadState.Charging)
        {
            ReassignLivingSlotIndices();

            if (squad.Formation != null)
                squad.Formation.Rebuild();

            if (squad.Movement != null)
                squad.Movement.BeginReform(false);
        }

        if (!HasLivingSoldiers)
            HandleEmptySquad();
    }

    void SpawnStartingSoldiers()
    {
        if (squadData == null)
        {
            Debug.LogError($"{name}: SquadRoster has no SquadData.");
            return;
        }

        SoldierData soldierData = squadData.soldierData;

        if (soldierData == null)
        {
            Debug.LogError($"{name}: SquadData has no SoldierData.");
            return;
        }

        if (soldierData.prefab == null)
        {
            Debug.LogError($"{name}: SoldierData has no Soldier prefab.");
            return;
        }

        int count = Mathf.Max(1, squadData.ResolvedStartingSoldierCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition = GetInitialSpawnPosition(i, count);

            SoldierController soldier = Instantiate(
                soldierData.prefab,
                spawnPosition,
                transform.rotation,
                soldierParent);

            soldier.Initialize(
                soldierData,
                squad,
                this,
                _faction);

            soldier.SetSlotIndex(i);
            soldier.SetLastSlotPosition(spawnPosition);

            soldiers.Add(soldier);
        }
    }

    Vector3 GetInitialSpawnPosition(int index, int count)
    {
        int unitsPerRow = Mathf.Max(1, squadData.defaultUnitsPerRow);
        float spacing = Mathf.Max(0.1f, squadData.defaultSpacing);

        int row = index / unitsPerRow;
        int col = index % unitsPerRow;

        int unitsInRow = Mathf.Min(unitsPerRow, count - row * unitsPerRow);
        float rowWidth = (unitsInRow - 1) * spacing;

        Vector3 right = transform.right;
        Vector3 back = -transform.forward;

        return transform.position +
               right * (col * spacing - rowWidth / 2f) +
               back * (row * spacing);
    }

    int GetLivingCount()
    {
        int living = 0;

        foreach (SoldierController soldier in soldiers)
        {
            if (soldier != null && soldier.IsAlive)
                living++;
        }

        return living;
    }

    void ReassignLivingSlotIndices()
    {
        int nextSlotIndex = 0;

        foreach (SoldierController soldier in soldiers)
        {
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

    void ClearExistingRuntimeSoldiers()
    {
        for (int i = soldiers.Count - 1; i >= 0; i--)
        {
            if (soldiers[i] != null)
                Destroy(soldiers[i].gameObject);
        }

        soldiers.Clear();
    }

    void HandleEmptySquad()
    {
        // Final behavior can move to SquadController later.
        Destroy(gameObject);
    }
    
    int GetExistingCount()
    {
        int count = 0;

        foreach (SoldierController soldier in soldiers)
        {
            if (soldier != null)
                count++;
        }

        return count;
    }
}