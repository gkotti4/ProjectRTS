using System;
using UnityEngine;

public enum SquadMoraleState
{
    Stable,
    Wavering,
    Routing,
    Shattered
}

/// -----------------------------------------------------------------------------
/// SquadMorale
/// -----------------------------------------------------------------------------
///
/// Squad-level morale and routing coordinator.
///
/// MVP responsibilities:
/// - owns current morale and readable Stable / Wavering / Routing / Shattered state
/// - converts soldier casualties into morale loss
/// - applies authored casualty resistance
/// - recovers morale after a safe delay when enemies are not nearby
/// - begins routing at the authored routing threshold
/// - rallies a routing squad if morale recovers sufficiently before escape
/// - permanently prevents rally once the authored shattered threshold is reached
/// - chooses and completes escape through the squad's home-side Rout Zone
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
public class SquadMorale : MonoBehaviour
{
    public event Action<SquadMorale, int> OnRoutedOffField;

    #region Tuning

    [Header("Morale - Casualties")]
    [Tooltip("Base morale lost for each soldier casualty before casualtyMoraleResistance is applied.")]
    [Min(0f)]
    [SerializeField] private float moraleCasualtyLossPerSoldier = 12f;

    [Header("Morale - Wavering")]
    [Tooltip("Squads at or below this fraction of maximum morale are Wavering, unless already Routing/Shattered.")]
    [Range(0f, 1f)]
    [SerializeField] private float moraleWaveringThresholdNormalized = 0.50f;

    [Header("Morale - Recovery")]
    [Tooltip("Seconds after the most recent morale loss before safe morale recovery may begin.")]
    [Min(0f)]
    [SerializeField] private float moraleRecoveryDelayAfterLoss = 3f;

    [Tooltip("Recovery is blocked while a living enemy squad is within this distance of the squad's living-body center.")]
    [Min(0f)]
    [SerializeField] private float moraleRecoveryEnemyDistance = 12f;

    [Header("Morale - Rally")]
    [Tooltip("A routing squad rallies after recovering this much morale above its routing threshold. Prevents threshold flicker.")]
    [Min(0f)]
    [SerializeField] private float moraleRallyThresholdMargin = 10f;

    [Header("Routing Debug")]
    [Tooltip("When enabled, pressing the debug key routes this squad if it is currently selected.")]
    [SerializeField] private bool moraleDebugRouteSelectedSquad = true;

    [SerializeField] private KeyCode moraleDebugRouteKey = KeyCode.M;

    #endregion

    #region Runtime

    private SquadController squad;
    private SquadRoster roster;
    private SquadMovement movement;
    private SquadHealth health;

    private float currentMorale;
    private float moraleRecoveryDelayTimer;
    private int previousLivingSoldierCount;

    private SquadMoraleState state = SquadMoraleState.Stable;
    private bool hasRoutedOffField;
    private BattleRoutZone currentRoutZone;

    public float CurrentMorale => currentMorale;
    public float MaximumMorale => squad != null && squad.Stats != null
        ? Mathf.Max(0f, squad.Stats.morale.maxMorale)
        : 0f;

    public float MoralePercent => MaximumMorale > 0f
        ? Mathf.Clamp01(currentMorale / MaximumMorale)
        : 0f;

    public SquadMoraleState State => state;
    public bool IsStable => state == SquadMoraleState.Stable;
    public bool IsWavering => state == SquadMoraleState.Wavering;
    public bool IsShattered => state == SquadMoraleState.Shattered;

    public bool IsRouting =>
        squad != null &&
        squad.State == SquadState.Routing &&
        !hasRoutedOffField;

    public bool HasRoutedOffField => hasRoutedOffField;
    public bool IsRoutingOrRouted => IsRouting || hasRoutedOffField;
    public BattleRoutZone CurrentRoutZone => currentRoutZone;

    #endregion

    #region Unity Lifecycle

    void OnValidate()
    {
        moraleCasualtyLossPerSoldier = Mathf.Max(0f, moraleCasualtyLossPerSoldier);
        moraleWaveringThresholdNormalized = Mathf.Clamp01(moraleWaveringThresholdNormalized);
        moraleRecoveryDelayAfterLoss = Mathf.Max(0f, moraleRecoveryDelayAfterLoss);
        moraleRecoveryEnemyDistance = Mathf.Max(0f, moraleRecoveryEnemyDistance);
        moraleRallyThresholdMargin = Mathf.Max(0f, moraleRallyThresholdMargin);
    }

    void Update()
    {
        if (squad == null || roster == null || !roster.HasLivingSoldiers)
            return;

        HandleDebugRouting();
        TickMoraleRecovery();
    }

    void OnDisable()
    {
        UnsubscribeFromHealth();
    }

    #endregion

    #region Initialization

    public void Initialize(
        SquadController owner,
        SquadRoster squadRoster,
        SquadMovement squadMovement)
    {
        UnsubscribeFromHealth();

        squad = owner;
        roster = squadRoster;
        movement = squadMovement;
        health = owner != null ? owner.Health : null;

        hasRoutedOffField = false;
        currentRoutZone = default;
        currentMorale = MaximumMorale;
        moraleRecoveryDelayTimer = 0f;
        previousLivingSoldierCount = health != null
            ? Mathf.Max(0, health.LivingSoldiers)
            : CountLivingSoldiers();

        state = SquadMoraleState.Stable;
        SubscribeToHealth();
        RefreshMoraleState();
    }

    public void RefreshRuntimeStats()
    {
        currentMorale = Mathf.Clamp(
            currentMorale,
            0f,
            MaximumMorale);

        RefreshMoraleState();
    }

    #endregion

    #region Morale Loss

    public void ApplyMoraleLoss(float amount)
    {
        if (amount <= 0f || hasRoutedOffField || IsShattered)
            return;

        currentMorale = Mathf.Max(0f, currentMorale - amount);
        moraleRecoveryDelayTimer = moraleRecoveryDelayAfterLoss;

        EvaluateBreakState();
    }

    void ApplyCasualtyMoraleLoss(int casualtyCount)
    {
        if (casualtyCount <= 0 || squad == null || squad.Stats == null)
            return;

        float resistance = Mathf.Clamp01(
            squad.Stats.morale.casualtyMoraleResistance);

        float moraleLoss =
            casualtyCount *
            moraleCasualtyLossPerSoldier *
            (1f - resistance);

        ApplyMoraleLoss(moraleLoss);
    }

    void EvaluateBreakState()
    {
        float shatteredThreshold = GetShatteredThreshold();
        float routingThreshold = GetRoutingThreshold();

        if (currentMorale <= shatteredThreshold)
        {
            state = SquadMoraleState.Shattered;

            if (!IsRouting && !hasRoutedOffField)
                BeginRouting();

            return;
        }

        if (currentMorale <= routingThreshold)
        {
            if (!IsRouting)
                BeginRouting();
            else
                state = SquadMoraleState.Routing;

            return;
        }

        RefreshMoraleState();
    }

    #endregion

    #region Casualty Tracking

    void SubscribeToHealth()
    {
        if (health == null)
            return;

        health.OnSquadHealthChanged -= HandleSquadHealthChanged;
        health.OnSquadHealthChanged += HandleSquadHealthChanged;
    }

    void UnsubscribeFromHealth()
    {
        if (health != null)
            health.OnSquadHealthChanged -= HandleSquadHealthChanged;
    }

    void HandleSquadHealthChanged(SquadHealth changedHealth)
    {
        if (changedHealth == null)
            return;

        int livingSoldiers = Mathf.Max(0, changedHealth.LivingSoldiers);
        int casualties = Mathf.Max(0, previousLivingSoldierCount - livingSoldiers);

        previousLivingSoldierCount = livingSoldiers;

        if (casualties > 0)
            ApplyCasualtyMoraleLoss(casualties);
    }

    int CountLivingSoldiers()
    {
        if (roster == null)
            return 0;

        int count = 0;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier != null && soldier.IsAlive)
                count++;
        }

        return count;
    }

    #endregion

    #region Recovery / Rally

    void TickMoraleRecovery()
    {
        if (hasRoutedOffField || IsShattered || squad == null || squad.Stats == null)
            return;

        if (moraleRecoveryDelayTimer > 0f)
        {
            moraleRecoveryDelayTimer -= Time.deltaTime;
            return;
        }

        if (!CanRecoverMorale())
            return;

        float recoveryRate = Mathf.Max(
            0f,
            squad.Stats.morale.moraleRecoveryRate);

        if (recoveryRate <= 0f || currentMorale >= MaximumMorale)
            return;

        currentMorale = Mathf.Min(
            MaximumMorale,
            currentMorale + recoveryRate * Time.deltaTime);

        if (IsRouting && currentMorale >= GetRallyThreshold())
        {
            Rally();
            return;
        }

        RefreshMoraleState();
    }

    bool CanRecoverMorale()
    {
        if (squad == null || roster == null || !roster.HasLivingSoldiers)
            return false;

        // Active fighting/approach is never considered safe enough to recover.
        switch (squad.State)
        {
            case SquadState.InCombat:
            case SquadState.Charging:
            case SquadState.ApproachingCombat:
                return false;
        }

        if (moraleRecoveryEnemyDistance <= 0f)
            return true;

        return !HasLivingEnemyWithinRecoveryDistance();
    }

    bool HasLivingEnemyWithinRecoveryDistance()
    {
        if (SquadManager.Instance == null || squad == null || squad.Faction == null)
            return false;

        Vector3 myCenter = GetLivingBodyCenter(squad);
        float rangeSqr = moraleRecoveryEnemyDistance * moraleRecoveryEnemyDistance;

        foreach (SquadController candidate in SquadManager.Instance.Squads)
        {
            if (candidate == null || candidate == squad || !candidate.IsInitialized)
                continue;

            if (candidate.Faction == null || candidate.Faction.teamId == squad.Faction.teamId)
                continue;

            if (candidate.Roster == null || !candidate.Roster.HasLivingSoldiers)
                continue;

            if (candidate.Morale != null && candidate.Morale.HasRoutedOffField)
                continue;

            Vector3 enemyCenter = GetLivingBodyCenter(candidate);

            if (FlatSqrDistance(myCenter, enemyCenter) <= rangeSqr)
                return true;
        }

        return false;
    }

    public void Rally()
    {
        if (!IsRouting || IsShattered || hasRoutedOffField || squad == null || movement == null)
            return;

        squad.Combat?.ClearTargets();
        movement.OrderStop();
        movement.BeginReform(recenterFromSoldiers: true);
        squad.SetState(SquadState.Reforming);

        currentRoutZone = default;
        RefreshMoraleState();
    }

    #endregion

    #region Routing

    public void BeginRouting()
    {
        if (squad == null || movement == null || roster == null)
            return;

        if (!roster.HasLivingSoldiers || hasRoutedOffField || IsRouting)
            return;

        BattleMap battleMap = BattleMap.Instance;

        if (battleMap == null)
        {
            Debug.LogWarning(
                $"{name}: Cannot begin routing because BattleMap.Instance is null.",
                this);
            return;
        }

        bool playerSide = ResolvePlayerArmySide();

        currentRoutZone = battleMap.GetBestRoutZone(
            playerSide,
            transform.position);

        if (!IsShattered)
            state = SquadMoraleState.Routing;

        squad.Combat?.ClearTargets();
        movement.BeginRouting(currentRoutZone);
        squad.SetState(SquadState.Routing);
    }

    public void CompleteRouting()
    {
        if (hasRoutedOffField)
            return;

        hasRoutedOffField = true;
        squad?.Combat?.ClearTargets();
        movement?.OrderStop();

        // Capture survivors before the squad is destroyed. BattleController consumes
        // this generic event so successful routers remain valid battle survivors.
        int survivingSoldierCount = roster != null
            ? Mathf.Max(0, roster.LivingCount)
            : 0;

        OnRoutedOffField?.Invoke(this, survivingSoldierCount);

        // Routed squads are physically gone from the battlefield after escape.
        squad?.DestroySquad();
    }

    #endregion

    #region State / Threshold Helpers

    void RefreshMoraleState()
    {
        if (IsShattered)
            return;

        if (IsRouting)
        {
            state = SquadMoraleState.Routing;
            return;
        }

        float waveringThreshold = MaximumMorale * moraleWaveringThresholdNormalized;

        state = currentMorale <= waveringThreshold
            ? SquadMoraleState.Wavering
            : SquadMoraleState.Stable;
    }

    float GetRoutingThreshold()
    {
        return squad != null && squad.Stats != null
            ? Mathf.Clamp(squad.Stats.morale.routingThreshold, 0f, MaximumMorale)
            : 0f;
    }

    float GetShatteredThreshold()
    {
        float routingThreshold = GetRoutingThreshold();

        return squad != null && squad.Stats != null
            ? Mathf.Clamp(squad.Stats.morale.shatteredThreshold, 0f, routingThreshold)
            : 0f;
    }

    float GetRallyThreshold()
    {
        return Mathf.Clamp(
            GetRoutingThreshold() + moraleRallyThresholdMargin,
            0f,
            MaximumMorale);
    }

    #endregion

    #region Debug

    void HandleDebugRouting()
    {
        if (!moraleDebugRouteSelectedSquad || squad == null)
            return;

        if (!Input.GetKeyDown(moraleDebugRouteKey) || !squad.IsSelected)
            return;

        if (IsRoutingOrRouted)
            return;

        // Put the squad at the routing threshold so the debug route exercises
        // normal rally behavior instead of accidentally shattering the squad.
        currentMorale = GetRoutingThreshold();
        moraleRecoveryDelayTimer = moraleRecoveryDelayAfterLoss;
        BeginRouting();
    }

    #endregion

    #region Map / Position Helpers

    bool ResolvePlayerArmySide()
    {
        if (GameManager.Instance != null)
        {
            if (squad.Faction == GameManager.Instance.PlayerFaction)
                return true;

            if (squad.Faction == GameManager.Instance.EnemyFaction)
                return false;
        }

        BattleMap battleMap = BattleMap.Instance;

        if (battleMap == null)
            return true;

        float playerDistance = FlatSqrDistance(
            transform.position,
            battleMap.GetDeploymentCenter(true));

        float enemyDistance = FlatSqrDistance(
            transform.position,
            battleMap.GetDeploymentCenter(false));

        return playerDistance <= enemyDistance;
    }

    Vector3 GetLivingBodyCenter(SquadController targetSquad)
    {
        if (targetSquad == null || targetSquad.Roster == null)
            return targetSquad != null ? targetSquad.transform.position : Vector3.zero;

        Vector3 center = Vector3.zero;
        int livingCount = 0;

        foreach (SoldierController soldier in targetSquad.Roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            center += soldier.transform.position;
            livingCount++;
        }

        return livingCount > 0
            ? center / livingCount
            : targetSquad.transform.position;
    }

    float FlatSqrDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return (a - b).sqrMagnitude;
    }

    #endregion
}
