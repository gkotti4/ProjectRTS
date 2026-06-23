using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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
[RequireComponent(typeof(NavMeshAgent))]
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
    public SquadStance Stance { get; private set; } = SquadStance.Aggressive;

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
    }

    void Start()
    {
        if (initializeOnStart && !isInitialized)
            Initialize(squadData, ResolveSceneFaction());

        SelectionManager.Instance?.RegisterSelectable(this);
        SquadManager.Instance?.RegisterSquad(this);
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
            Debug.LogWarning($"{name}: Squad Initialize called more than once.");
            return;
        }

        if (data == null)
        {
            Debug.LogError($"{name}: Squad Initialize failed. SquadData is null.");
            return;
        }

        if (faction == null)
        {
            Debug.LogError($"{name}: Squad Initialize failed. Faction is null.");
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
    }

    FactionInstance ResolveSceneFaction()
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
                Movement.TickMoving();
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
                Movement.TickMoving();
                Combat.TickCombat();
                break;

            case SquadState.Withdrawing:
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

    public void OrderAttackMove(Vector3 destination)
    {
        Vector3 facing = Movement.ResolveFacing(destination);

        Movement.OrderMove(destination, facing);
        State = SquadState.AttackMoving;
    }

    public void OrderWithdraw(Vector3 destination)
    {
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
    }

    public void SetStance(SquadStance stance)
    {
        Stance = stance;

        if (Stance == SquadStance.NoAttack)
        {
            Combat.ClearTargets();

            if (State == SquadState.InCombat)
            {
                Movement.BeginReform();
                State = SquadState.Reforming;
            }
        }
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


