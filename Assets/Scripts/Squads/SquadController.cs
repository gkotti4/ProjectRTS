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
    private readonly Dictionary<UpgradeData, int> appliedUpgradeStacks =
        new Dictionary<UpgradeData, int>();

    private enum QueuedSquadCommandType
    {
        Move,
        Attack
    }

    private struct QueuedSquadCommand
    {
        public QueuedSquadCommandType commandType;
        public Vector3 destination;
        public Vector3 facing;
        public float requestedFormationWidth;
        public SquadController targetSquad;
    }

    private readonly Queue<QueuedSquadCommand> queuedCommands =
        new Queue<QueuedSquadCommand>();

    private bool isExecutingQueuedCommand = false;

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
    public SquadRuntimeStats Stats { get; private set; }
    public IReadOnlyDictionary<UpgradeData, int> AppliedUpgradeStacks =>
        appliedUpgradeStacks;

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

    public int GetUpgradeStackCount(UpgradeData upgrade)
    {
        if (upgrade == null)
            return 0;

        return appliedUpgradeStacks.TryGetValue(upgrade, out int stackCount)
            ? Mathf.Max(0, stackCount)
            : 0;
    }

    public bool IsUpgradeApplied(UpgradeData upgrade)
    {
        return GetUpgradeStackCount(upgrade) > 0;
    }

    public bool CanApplyUpgrade(UpgradeData upgrade)
    {
        if (upgrade == null || upgrade.scope != UpgradeScope.Squad)
            return false;

        int maximumStacks = upgrade.repeatable
            ? Mathf.Max(1, upgrade.maximumStacks)
            : 1;

        if (GetUpgradeStackCount(upgrade) >= maximumStacks)
            return false;

        if (upgrade.requiredUpgrades != null)
        {
            for (int index = 0; index < upgrade.requiredUpgrades.Count; index++)
            {
                UpgradeData requiredUpgrade = upgrade.requiredUpgrades[index];

                if (requiredUpgrade == null)
                    continue;

                bool requirementMet = requiredUpgrade.scope == UpgradeScope.Faction
                    ? Faction != null && Faction.IsUpgradeApplied(requiredUpgrade)
                    : IsUpgradeApplied(requiredUpgrade);

                if (!requirementMet)
                    return false;
            }
        }

        if (upgrade.blockedByUpgrades != null)
        {
            for (int index = 0; index < upgrade.blockedByUpgrades.Count; index++)
            {
                UpgradeData blockedUpgrade = upgrade.blockedByUpgrades[index];

                if (blockedUpgrade == null)
                    continue;

                bool isBlocked = blockedUpgrade.scope == UpgradeScope.Faction
                    ? Faction != null && Faction.IsUpgradeApplied(blockedUpgrade)
                    : IsUpgradeApplied(blockedUpgrade);

                if (isBlocked)
                    return false;
            }
        }

        return true;
    }

    public bool TryApplyUpgrade(
        UpgradeData upgrade,
        UpgradeGrantSource grantSource = UpgradeGrantSource.Debug)
    {
        if (!CanApplyUpgrade(upgrade))
            return false;

        appliedUpgradeStacks[upgrade] = GetUpgradeStackCount(upgrade) + 1;
        RefreshRuntimeStats();
        return true;
    }

    // Compatibility wrapper for older callers.
    public void ApplyUpgrade(UpgradeData upgrade)
    {
        TryApplyUpgrade(upgrade);
    }

    public void RefreshRuntimeStats()
    {
        Stats = RuntimeStatResolver.ResolveSquad(
            squadData,
            Faction,
            appliedUpgradeStacks);

        Formation?.ApplyStats(Stats.formation);
        Movement?.RefreshRuntimeStats();

        if (Roster == null)
            return;

        foreach (SoldierController soldier in Roster.Soldiers)
            soldier?.RefreshRuntimeStats();

        // Ranged ammunition is squad-owned. Recalculate capacity after soldier
        // weapon/stat upgrades resolve, while preserving ammunition already spent.
        Combat?.RefreshRangedAmmunitionCapacity(refill: false);

        Health?.RefreshMaximumHealthFromRoster();
    }

    void HandleFactionUpgradeApplied(
        FactionInstance faction,
        UpgradeData upgrade,
        UpgradeGrantSource source,
        int stackCount)
    {
        if (!isInitialized || faction != Faction)
            return;

        RefreshRuntimeStats();
    }

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
        if (Faction != null)
            Faction.OnUpgradeApplied -= HandleFactionUpgradeApplied;

        SelectionManager.Instance?.UnregisterSelectable(this);
        SquadManager.Instance?.UnregisterSquad(this);

        Roster.DestroyAllSoldiers(); // Hotfix - squad destroy cleanup after game mode restart (was only destroying/unregistering squad GO, leaving the remaining solider GOs)
    }
    
    public void DestroySquad() => Destroy(gameObject);

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
        Faction.OnUpgradeApplied += HandleFactionUpgradeApplied;

        Stance = squadData.defaultStance;
        State = SquadState.Idle;
        Stats = RuntimeStatResolver.ResolveSquad(
            squadData,
            Faction,
            appliedUpgradeStacks);

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

                // Ranged avoidance can chain another retreat just before arrival
                // without briefly dropping back into combat between retreats.
                if (!Combat.TryChainFormationRangedAvoidanceWithdrawal())
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
        SquadState previousState = State;
        State = newState;

        if (newState == SquadState.Idle && previousState != SquadState.Idle)
            TryExecuteNextQueuedCommand();
    }

    #endregion

    #region Orders

    public void OrderMove(Vector3 destination)
    {
        Vector3 facing = Movement.ResolveFacing(destination);
        OrderMove(destination, facing, -1f, queueCommand: false);
    }

    public void OrderMove(
        Vector3 destination,
        Vector3 facing,
        float requestedFormationWidth = -1f,
        bool queueCommand = false)
    {
        if (queueCommand && !isExecutingQueuedCommand)
        {
            queuedCommands.Enqueue(new QueuedSquadCommand
            {
                commandType = QueuedSquadCommandType.Move,
                destination = destination,
                facing = facing,
                requestedFormationWidth = requestedFormationWidth,
                targetSquad = null
            });

            TryExecuteNextQueuedCommand();
            return;
        }

        if (!isExecutingQueuedCommand)
            queuedCommands.Clear();

        ExecuteMoveCommand(
            destination,
            facing,
            requestedFormationWidth);
    }

    void ExecuteMoveCommand(
        Vector3 destination,
        Vector3 facing,
        float requestedFormationWidth)
    {
        // Active combat can leave the virtual squad root behind while soldiers move
        // independently toward enemies. Before creating a normal movement path, snap
        // the anchor to the actual living squad center so the new order starts from
        // the squad rather than pulling everyone back toward an old banner position.
        if (State == SquadState.InCombat)
            Movement.SyncRootToLivingSoldierCenter();

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
        queuedCommands.Clear();

        Combat.ClearTargets();
        Movement.OrderStop();

        State = SquadState.Idle;
    }

    /// Orders this squad to attack another squad.
    /// SquadCombat decides whether to approach first or enter melee immediately.
    public void OrderAttack(
        SquadController target,
        bool queueCommand = false)
    {
        if (target == null)
            return;

        if (queueCommand && !isExecutingQueuedCommand)
        {
            queuedCommands.Enqueue(new QueuedSquadCommand
            {
                commandType = QueuedSquadCommandType.Attack,
                targetSquad = target,
                destination = Vector3.zero,
                facing = Vector3.forward,
                requestedFormationWidth = -1f
            });

            TryExecuteNextQueuedCommand();
            return;
        }

        if (!isExecutingQueuedCommand)
            queuedCommands.Clear();

        Combat.OrderAttack(target);
    }

    void TryExecuteNextQueuedCommand()
    {
        if (isExecutingQueuedCommand || State != SquadState.Idle)
            return;

        while (queuedCommands.Count > 0)
        {
            QueuedSquadCommand queuedCommand = queuedCommands.Dequeue();

            if (queuedCommand.commandType == QueuedSquadCommandType.Attack &&
                !IsValidQueuedAttackTarget(queuedCommand.targetSquad))
            {
                continue;
            }

            isExecutingQueuedCommand = true;

            if (queuedCommand.commandType == QueuedSquadCommandType.Move)
            {
                ExecuteMoveCommand(
                    queuedCommand.destination,
                    queuedCommand.facing,
                    queuedCommand.requestedFormationWidth);
            }
            else
            {
                Combat.OrderAttack(queuedCommand.targetSquad);
            }

            isExecutingQueuedCommand = false;

            if (State == SquadState.Idle)
                continue;

            return;
        }
    }

    bool IsValidQueuedAttackTarget(SquadController target)
    {
        return target != null &&
               target.IsInitialized &&
               target.Roster != null &&
               target.Roster.HasLivingSoldiers;
    }

    public void ClearQueuedCommands()
    {
        queuedCommands.Clear();
    }

    public int QueuedCommandCount => queuedCommands.Count;

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
