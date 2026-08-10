using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SquadRoster))]
public class SquadHealth : MonoBehaviour
{
    public event Action<SquadHealth> OnSquadHealthChanged;

    private SquadRoster roster;
    private SquadData data;
    private bool isInitialized = false;
    private bool isEliminatingSquad = false;
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
        // SquadHealth may be enabled before SquadController.Initialize() runs.
        // Do not listen to roster changes until Initialize() has established the
        // stable MaxHealth / TotalSoldiers denominators.
        if (isInitialized)
            SubscribeToRoster();
    }

    void OnDisable()
    {
        UnsubscribeFromRoster();

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
        // Prevent duplicate subscriptions if this component was previously bound.
        UnsubscribeFromRoster();

        roster = sourceRoster;
        data = GetComponent<SquadController>()?.Data;

        if (roster == null)
        {
            Debug.LogError($"{name}: SquadHealth.Initialize received a null roster.", this);
            return;
        }

        TotalSoldiers = roster.Soldiers.Count;
        MaxHealth = 0;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || soldier.Health == null)
                continue;

            MaxHealth += soldier.Health.MaxHealth;
        }

        // From this point onward threshold evaluation is safe.
        isInitialized = true;

        SubscribeToRoster();
        RefreshSubscriptions();
        Recalculate();
    }

    public void RefreshMaximumHealthFromRoster()
    {
        if (!isInitialized || roster == null)
            return;

        MaxHealth = 0;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || soldier.Health == null)
                continue;

            MaxHealth += soldier.Health.MaxHealth;
        }

        Recalculate();
    }

    void SubscribeToRoster()
    {
        if (roster == null)
            return;

        // Remove first so repeated lifecycle/initialize calls cannot duplicate it.
        roster.OnRosterChanged -= HandleRosterChanged;
        roster.OnRosterChanged += HandleRosterChanged;
    }

    void UnsubscribeFromRoster()
    {
        if (roster != null)
            roster.OnRosterChanged -= HandleRosterChanged;
    }

    void HandleRosterChanged(SquadRoster changedRoster)
    {
        if (!isInitialized)
            return;

        RefreshSubscriptions();
        Recalculate();
    }

    void HandleSoldierHealthChanged(SoldierHealth health)
    {
        if (!isInitialized)
            return;

        Recalculate();
    }

    void HandleSoldierDied(SoldierHealth health)
    {
        if (!isInitialized)
            return;

        Recalculate();
    }

    void RefreshSubscriptions()
    {
        if (!isInitialized || roster == null)
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
        if (!isInitialized || isEliminatingSquad)
            return;

        CurrentHealth = 0;
        LivingSoldiers = 0;

        if (roster == null)
        {
            OnSquadHealthChanged?.Invoke(this);
            return;
        }

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || soldier.Health == null)
                continue;

            CurrentHealth += soldier.Health.CurrentHealth;

            if (soldier.Health.IsAlive)
                LivingSoldiers++;
        }

        if (ShouldEliminateSquad())
        {
            EliminateRemainingSoldiers();
            return;
        }

        OnSquadHealthChanged?.Invoke(this);
    }

    bool ShouldEliminateSquad()
    {
        // MaxHealth and TotalSoldiers are stable starting-roster denominators.
        // Never evaluate early-elimination rules until they are valid.
        if (!isInitialized ||
            data == null ||
            LivingSoldiers <= 0 ||
            MaxHealth <= 0 ||
            TotalSoldiers <= 0)
        {
            return false;
        }

        float healthThreshold = Mathf.Clamp(
            data.squadDeathHealthPercentageThreshold,
            0f,
            100f) / 100f;

        bool healthThresholdReached =
            healthThreshold > 0f &&
            HealthPercent <= healthThreshold;

        bool manpowerThresholdReached =
            data.squadDeathLivingSoldierThreshold > 0 &&
            TotalSoldiers > data.squadDeathLivingSoldierThreshold &&
            LivingSoldiers <= data.squadDeathLivingSoldierThreshold;

        return healthThresholdReached || manpowerThresholdReached;
    }

    void EliminateRemainingSoldiers()
    {
        if (roster == null || isEliminatingSquad)
            return;

        isEliminatingSquad = true;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || soldier.Health == null || !soldier.Health.IsAlive)
                continue;

            soldier.Health.Kill();
        }

        isEliminatingSquad = false;
        Recalculate();
    }
}
