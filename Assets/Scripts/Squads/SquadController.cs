
using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// SquadController
/// -----------------------------------------------------------------------------
///
/// Root gameplay component for a controllable squad.
/// Owns the squad's high-level state, selected command interface, faction identity,
/// and references to the squad subsystems: roster, health, formation, movement,
/// selection, and combat.
///
/// This class should coordinate orders and state transitions, but should not
/// calculate formation geometry, move individual soldiers, resolve melee attacks,
/// or play soldier animations directly.
///
/// Design role:
/// Player/AI orders enter here, then get routed to SquadMovement, SquadCombat,
/// SquadFormationController, or other squad-level systems.
/// 
[RequireComponent(typeof(FactionOwner))]
[RequireComponent(typeof(SquadRoster))]
[RequireComponent(typeof(SquadHealth))]
[RequireComponent(typeof(SquadFormationController))]
[RequireComponent(typeof(SquadMovement))]
[RequireComponent(typeof(SquadSelection))]
[RequireComponent(typeof(SquadCombat))]
public class SquadController : MonoBehaviour,
    ISelectable,
    IHoverable,
    ISelectionComparable,
    ICommandable,
    IFactionOwned
{
    #region Fields

    [Header("Data")]
    [SerializeField] private SquadData squadData;

    [Header("Debug / Scene Setup")]
    [SerializeField] private bool initializeOnStart = true;

    private bool isInitialized = false;
    private bool isSelected = false;

    #endregion

    #region Components

    public SquadRoster Roster { get; private set; }
    public SquadHealth Health { get; private set; }
    public SquadFormationController Formation { get; private set; }
    public SquadMovement Movement { get; private set; }
    public SquadSelection Selection { get; private set; }
    public SquadCombat Combat { get; private set; }

    #endregion

    #region Public Properties

    public SquadData Data => squadData;

    public SquadCategory Category =>
        squadData != null ? squadData.category : SquadCategory.Infantry;

    public SquadState State { get; private set; } = SquadState.Idle;
    public SquadStance Stance { get; private set; } = SquadStance.Engage;

    public FactionInstance Faction { get; private set; }

    public bool IsSelected => isSelected;
    public bool IsInitialized => isInitialized;

    public SelectableKind SelectionKind => SelectableKind.Squad;
    public bool IsDragSelectable => true;

    public SelectableKind CommandKind => SelectableKind.Squad;

    public float DoubleClickSelectRange => 45f;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        Roster = GetComponent<SquadRoster>();
        Health = GetComponent<SquadHealth>();
        Formation = GetComponent<SquadFormationController>();        
        Movement = GetComponent<SquadMovement>();
        Selection = GetComponent<SquadSelection>();
        Combat = GetComponent<SquadCombat>();

        // -------------------------------------------------------------------------
        // Validation
        // -------------------------------------------------------------------------
        if (Roster == null)
            Debug.LogError($"{name}: SquadController missing SquadRoster.", this);

        if (Health == null)
            Debug.LogError($"{name}: SquadController missing SquadHealth.", this);

        if (Formation == null)
            Debug.LogError($"{name}: SquadController missing SquadFormationController.", this);

        if (Movement == null)
            Debug.LogError($"{name}: SquadController missing SquadMovement.", this);

        if (Selection == null)
            Debug.LogError($"{name}: SquadController missing SquadSelection.", this);

        if (Combat == null)
            Debug.LogError($"{name}: SquadController missing SquadCombat.", this);
    }

    void Start()
    {
        if (initializeOnStart && !isInitialized)
            Initialize(squadData, ResolveSceneFaction());

        SelectionManager.Instance?.RegisterSelectable(this);
        SquadManager.Instance?.RegisterSquad(this);

        // -------------------------------------------------------------------------
        // Validation
        // -------------------------------------------------------------------------
        if (initializeOnStart && !isInitialized)
        {
            Debug.LogError(
                $"{name}: SquadController Start finished but squad is not initialized.",
                this);
        }

        if (SelectionManager.Instance == null)
        {
            Debug.LogWarning(
                $"{name}: SelectionManager.Instance is null. This squad will not be selectable through the selection registry.",
                this);
        }

        if (SquadManager.Instance == null)
        {
            Debug.LogWarning(
                $"{name}: SquadManager.Instance is null. This squad will not be available for squad combat scanning.",
                this);
        }
    }

    void Update()
    {
        if (!isInitialized)
            return;

        TickState();
    }

    void OnDestroy()
    {
        SelectionManager.Instance?.UnregisterSelectable(this);
        SquadManager.Instance?.UnregisterSquad(this);
    }

    #endregion

    #region Initialization

    public void Initialize(SquadData data, FactionInstance faction)
    {
        if (isInitialized)
        {
            Debug.LogWarning($"{name}: Squad Initialize called more than once.", this);
            return;
        }

        if (data == null)
        {
            Debug.LogError($"{name}: Squad Initialize failed. SquadData is null.", this);
            return;
        }

        if (faction == null)
        {
            Debug.LogError($"{name}: Squad Initialize failed. Faction is null.", this);
            return;
        }

        squadData = data;
        Faction = faction;

        Stance = squadData.defaultStance;
        State = SquadState.Idle;

        // 1. Build physical/gameplay body.
        Roster.Initialize(this, squadData, Faction);

        // 2. Bind squad-level state systems that depend on roster/soldiers.
        Health.Initialize(Roster);
        Formation.Initialize(this, Roster, squadData);
        Movement.Initialize(this, Roster, Formation, squadData);
        Combat.Initialize(this, Roster, Formation, Movement, squadData);

        // 3. Bind visuals last. Visuals can safely read Data/Faction/Health/Roster now.
        Selection.Initialize(this, Roster);

        // 4. The squad is now safe for external systems and Update ticks.
        isInitialized = true;

        // -------------------------------------------------------------------------
        // Validation
        // -------------------------------------------------------------------------
        if (squadData == null)
            Debug.LogError($"{name}: Squad Initialize validation failed. squadData is null.", this);

        if (Faction == null)
            Debug.LogError($"{name}: Squad Initialize validation failed. Faction is null.", this);

        if (Roster == null)
            Debug.LogError($"{name}: Squad Initialize validation failed. Roster is null.", this);

        if (Health == null)
            Debug.LogError($"{name}: Squad Initialize validation failed. Health is null.", this);

        if (Formation == null)
            Debug.LogError($"{name}: Squad Initialize validation failed. Formation is null.", this);

        if (Movement == null)
            Debug.LogError($"{name}: Squad Initialize validation failed. Movement is null.", this);

        if (Selection == null)
            Debug.LogError($"{name}: Squad Initialize validation failed. Selection is null.", this);

        if (Combat == null)
            Debug.LogError($"{name}: Squad Initialize validation failed. Combat is null.", this);

        if (squadData.soldierData == null)
            Debug.LogError($"{name}: SquadData validation failed. soldierData is null.", this);

        if (squadData.squadCombatProfile == null)
            Debug.LogError($"{name}: SquadData is missing required SquadCombatProfile. SquadCombat will not run without it.", this);

        if (squadData.movementProfile == null)
            Debug.LogError($"{name}: SquadData is missing required SquadMovementProfile. SquadMovement will not run without it.", this);

        if (Roster != null && Roster.Count <= 0)
            Debug.LogError($"{name}: Squad initialized with no soldiers in roster.", this);
    }

    FactionInstance ResolveSceneFaction() // CHECK Naming, Convention, Design Choice for future faction/team initialization
    {
        if (GameManager.Instance == null)
            return null;

        if (CompareTag("Enemy"))
            return GameManager.Instance.EnemyFaction;

        return GameManager.Instance.PlayerFaction;
    }

    #endregion

    #region State

    void TickState()
    {
        switch (State)
        {
            case SquadState.Idle:
                Movement.TickIdle();
                Combat.TickIdleScan();
                break;

            case SquadState.Moving:
                Combat.TickCombatLocks();
                Movement.TickMoving(); // PERFORMANCE
                break;

            case SquadState.ApproachingCombat:
                Combat.TickApproachingCombat();
                break;

            case SquadState.InCombat:
                Combat.TickCombat();
                break;

            case SquadState.AttackMoving:
                Movement.TickMoving();
                Combat.TickAttackMoveScan();
                break;

            case SquadState.Charging:
                Combat.TickCharging();
                break;

            case SquadState.Withdrawing:
                Combat.TickCombatLocks();
                Movement.TickMoving();
                break;

            case SquadState.Reforming:
                Movement.TickReforming();
                Combat.TickIdleScan();
                break;

            case SquadState.Routing:
                Movement.TickRouting();
                break;
        }
    }

    public void SetState(SquadState newState)
    {
        State = newState;
    }

    #endregion

    #region Orders

    public void OrderMove(Vector3 destination)
    {
        Vector3 facing = Movement.ResolveFacing(destination);
        OrderMove(destination, facing);
    }

    public void OrderMove(
        Vector3 destination,
        Vector3 facing,
        float requestedFormationWidth = -1f)
    {
        if (State == SquadState.InCombat)
            Combat.BeginCombatLockedMoveOrder();
        else
            Combat.ClearTargets();

        Movement.OrderMove(
            destination,
            facing,
            requestedFormationWidth);

        State = SquadState.Moving;
    }

    public void OrderStop()
    {
        Combat.ClearTargets();
        Movement.OrderStop();

        State = SquadState.Idle;
    }

    /// Orders this squad to attack another squad.
    /// SquadCombat decides whether to approach first or enter melee immediately.
    public void OrderAttack(SquadController target)
    {
        if (target == null)
            return;

        Combat.OrderAttack(target);
    }

    public void OrderAttackMove(Vector3 destination) // UNUSED
    {
        Vector3 facing = Movement.ResolveFacing(destination);

        Movement.OrderMove(destination, facing);
        State = SquadState.AttackMoving;
    }

    public void OrderWithdraw(Vector3 destination) // UNUSED 
    {
        if (State == SquadState.InCombat)
            Combat.BeginCombatLockedMoveOrder();
        else
            Combat.ClearTargets();

        Vector3 facing = Movement.ResolveFacing(destination);
        Movement.OrderMove(destination, facing);

        State = SquadState.Withdrawing;
    }

    public void SetFormation(SquadFormation formation)
    {
        if (State == SquadState.InCombat ||
            State == SquadState.ApproachingCombat ||
            State == SquadState.Charging)
        {
            return;
        }

        Formation.SetFormation(formation);

        Movement.BeginReform();
        State = SquadState.Reforming;
        
        Formation.VisualizeCurrentSlots();
    }

    public void SetStance(SquadStance stance)
    {
        Stance = stance;
    }

    #endregion

    #region Preview

    public List<Vector3> GetPreviewSlots(
        Vector3 center,
        Vector3 facing,
        float requestedFormationWidth = -1f)
    {
        return Formation.GetPreviewSlots(
            center,
            facing,
            requestedFormationWidth);
    }

    public FormationBounds GetFormationBounds(
        float requestedFormationWidth = -1f)
    {
        return Formation != null
            ? Formation.GetFormationBounds(requestedFormationWidth)
            : FormationBounds.Empty;
    }

    #endregion

    #region Selection

    public void OnSelect()
    {
        isSelected = true;
        Selection.OnSelected();
    }

    public void OnDeselect()
    {
        isSelected = false;
        Selection.OnDeselected();
    }

    public void OnHoverEnter()
    {
        Selection.OnHoverEnter();
    }

    public void OnHoverExit()
    {
        if (isSelected)
            return;

        Selection.OnHoverExit();
    }

    public GameObject GetGameObject()
    {
        return gameObject;
    }

    public bool IsSameSelectionType(ISelectable other)
    {
        if (other is not SquadController otherSquad)
            return false;

        return otherSquad.Category == Category;
    }

    #endregion

    #region Commands

    public List<CommandData> GetCommands()
    {
        if (squadData == null || squadData.commandSet == null)
            return new List<CommandData>();

        return squadData.commandSet.GetAllCommands();
    }

    #endregion
}
