using UnityEngine;

/// -----------------------------------------------------------------------------
/// SquadMorale
/// -----------------------------------------------------------------------------
///
/// First routing foundation for squad morale.
///
/// Current responsibilities:
/// - owns current squad morale as the future morale-system entry point
/// - exposes ApplyMoraleLoss for later casualty/charge/flank inputs
/// - begins an involuntary route when morale reaches the authored routing threshold
/// - chooses the nearest Rout Zone on the squad's original army side
/// - marks the squad off-field after it enters that Rout Zone
///
/// Full morale pressure/recovery/rally behavior is intentionally deferred until
/// the physical routing loop has been validated.
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
public class SquadMorale : MonoBehaviour
{
    #region Tuning / Debug

    [Header("Routing Debug")]
    [Tooltip("When enabled, pressing the debug key routes this squad if it is currently selected.")]
    [SerializeField] private bool moraleDebugRouteSelectedSquad = true;

    [SerializeField] private KeyCode moraleDebugRouteKey = KeyCode.M;

    #endregion

    #region Runtime

    private SquadController squad;
    private SquadRoster roster;
    private SquadMovement movement;

    private float currentMorale;
    private bool hasRoutedOffField;
    private BattleRoutZone currentRoutZone;

    public float CurrentMorale => currentMorale;
    public float MaximumMorale => squad != null && squad.Stats != null
        ? Mathf.Max(0f, squad.Stats.morale.maxMorale)
        : 0f;

    public bool IsRouting =>
        squad != null &&
        squad.State == SquadState.Routing &&
        !hasRoutedOffField;

    public bool HasRoutedOffField => hasRoutedOffField;
    public bool IsRoutingOrRouted => IsRouting || hasRoutedOffField;
    public BattleRoutZone CurrentRoutZone => currentRoutZone;

    #endregion

    public void Initialize(
        SquadController owner,
        SquadRoster squadRoster,
        SquadMovement squadMovement)
    {
        squad = owner;
        roster = squadRoster;
        movement = squadMovement;
        hasRoutedOffField = false;
        currentRoutZone = default;
        currentMorale = MaximumMorale;
    }

    void Update()
    {
        if (!moraleDebugRouteSelectedSquad || squad == null)
            return;

        if (Input.GetKeyDown(moraleDebugRouteKey) && squad.IsSelected)
            BeginRouting();
    }

    public void RefreshRuntimeStats()
    {
        currentMorale = Mathf.Clamp(
            currentMorale,
            0f,
            MaximumMorale);
    }

    public void ApplyMoraleLoss(float amount)
    {
        if (amount <= 0f || IsRoutingOrRouted)
            return;

        currentMorale = Mathf.Max(0f, currentMorale - amount);

        float routingThreshold = squad != null && squad.Stats != null
            ? Mathf.Max(0f, squad.Stats.morale.routingThreshold)
            : 0f;

        if (currentMorale <= routingThreshold)
            BeginRouting();
    }

    public void BeginRouting()
    {
        if (squad == null || movement == null || roster == null)
            return;

        if (!roster.HasLivingSoldiers || IsRoutingOrRouted)
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
    }

    bool ResolvePlayerArmySide()
    {
        if (GameManager.Instance != null)
        {
            if (squad.Faction == GameManager.Instance.PlayerFaction)
                return true;

            if (squad.Faction == GameManager.Instance.EnemyFaction)
                return false;
        }

        // Battle Mode currently has two opposing deployment sides. If a future
        // faction is not one of the primary battle factions, use the nearer
        // deployment side as a safe fallback.
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

    float FlatSqrDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return (a - b).sqrMagnitude;
    }
}
