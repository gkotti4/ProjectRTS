using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SquadRoster))]


/// -----------------------------------------------------------------------------
/// SquadHealth
/// -----------------------------------------------------------------------------
///
/// Aggregates individual SoldierHealth values into squad-level health and manpower.
/// Subscribes to soldier health/death events, recalculates current squad health,
/// living soldier count, health percentage, and manpower percentage.
///
/// The squad's maximum health and total soldier count are initialized from the
/// starting roster and remain stable so deaths reduce manpower instead of shrinking
/// the denominator.
///
/// Design role:
/// Converts individual soldier health into squad UI/gameplay health state.
///
public class SquadHealth : MonoBehaviour
{
    public event Action<SquadHealth> OnSquadHealthChanged;

    private SquadRoster roster;
    private readonly HashSet<SoldierHealth> subscribedHealth = new HashSet<SoldierHealth>();

    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }

    public int LivingSoldiers { get; private set; }
    public int TotalSoldiers { get; private set; }

    public float HealthPercent =>
        MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0f;

    public float ManpowerPercent =>
        TotalSoldiers > 0 ? (float)LivingSoldiers / TotalSoldiers : 0f;

    void Awake()
    {
        roster = GetComponent<SquadRoster>();
    }

    void OnEnable()
    {
        if (roster != null)
            roster.OnRosterChanged += HandleRosterChanged;
    }

    void OnDisable()
    {
        if (roster != null)
            roster.OnRosterChanged -= HandleRosterChanged;

        foreach (SoldierHealth health in subscribedHealth)
        {
            if (health == null)
                continue;

            health.OnHealthChanged -= HandleSoldierHealthChanged;
            health.OnDied -= HandleSoldierDied;
        }

        subscribedHealth.Clear();
    }

    public void Initialize(SquadRoster sourceRoster)
    {
        roster = sourceRoster;

        if (roster != null)
            roster.OnRosterChanged += HandleRosterChanged;
        
        TotalSoldiers = roster.Soldiers.Count;
        MaxHealth = 0;
        foreach (SoldierController soldier in roster.Soldiers)
            MaxHealth += soldier.Health.MaxHealth;

        RefreshSubscriptions();
        Recalculate();
    }

    void HandleRosterChanged(SquadRoster changedRoster)
    {
        RefreshSubscriptions();
        Recalculate();
    }

    void HandleSoldierHealthChanged(SoldierHealth health)
    {
        Recalculate();
    }

    void HandleSoldierDied(SoldierHealth health)
    {
        Recalculate();
    }

    void RefreshSubscriptions()
    {
        if (roster == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || soldier.Health == null)
                continue;

            SoldierHealth health = soldier.Health;

            if (subscribedHealth.Contains(health))
                continue;

            health.OnHealthChanged += HandleSoldierHealthChanged;
            health.OnDied += HandleSoldierDied;

            subscribedHealth.Add(health);
        }
    }

    void Recalculate()
    {
        CurrentHealth = 0;
        LivingSoldiers = 0;
        // MaxHealth = 0;
        // TotalSoldiers = 0;

        if (roster == null)
        {
            OnSquadHealthChanged?.Invoke(this);
            return;
        }

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || soldier.Health == null)
                continue;

            // TotalSoldiers++;
            // MaxHealth += soldier.Health.MaxHealth;
            
            CurrentHealth += soldier.Health.CurrentHealth;

            if (soldier.Health.IsAlive)
                LivingSoldiers++;
        }

        OnSquadHealthChanged?.Invoke(this);
    }
}