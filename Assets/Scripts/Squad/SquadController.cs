// SESSION: Squad Control Refactor

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SquadController : MonoBehaviour, ISelectable
{
    #region Fields

    // ============================================================
    // Base Squad Data
    // ============================================================
    [SerializeField] private SquadData squadData;


    // ============================================================
    // Identity / Definition
    // ============================================================

    [Header("Identity")] 
    public SquadCategory Category { get; private set; }

    [SerializeField] private int maxMembers = 50; // TODO: max members to be based off squad category rather than initial squad


    // ============================================================
    // Runtime State
    // ============================================================

    [Header("Behavior")]
    public SquadState State { get; private set; } = SquadState.Idle;
    public SquadStance Stance { get; private set; } = SquadStance.Aggressive;

    [Header("Movement State")]
    public SquadMoveMode MoveMode { get; private set; } = SquadMoveMode.IdleFormed;


    // ============================================================
    // Members
    // ============================================================

    [Header("Members")]
    private List<SquadMemberController> members = new List<SquadMemberController>();


    // ============================================================
    // NavMesh Agent / Squad Anchor
    // ============================================================
    //
    // The SquadController NavMeshAgent is the squad anchor/body mover.
    // It does NOT directly move the visible soldiers.
    // Members have their own NavMeshAgents and follow slots generated from this anchor.
    //
    // baseSquadAgentSpeed = the speed of the squad root/anchor.
    // Usually this should be derived from member speed, usually the slowest living member.

    [Header("Nav Mesh Agent")]
    private NavMeshAgent squadAgent;
    private float baseSquadAgentSpeed = 0f;


    // ============================================================
    // Formation Definition
    // ============================================================

    [Header("Formation")] 
    public SquadFormation Formation { get; private set; } = SquadFormation.Line;

    [SerializeField] private float formationWidth = -1f;
    private float defaultSpacing = 2f;
    private int defaultUnitsPerRow = 5;

    [SerializeField] private float slotUpdateThreshold = 0.25f;


    // ============================================================
    // Formation Runtime
    // ============================================================

    [Header("Formation and Slots")]
    private List<Vector2> formationOffsets = new List<Vector2>();
    private List<Vector3> finalSlots = new List<Vector3>();

    private Vector3 facing = Vector3.forward;
    private Vector3 desiredFacing = Vector3.forward;
    private Vector3 finalDestination;


    // ============================================================
    // Movement Tuning
    // ============================================================

    [Header("Movement")] 
    [SerializeField] private float turnSpeed = 540f;


    // ============================================================
    // Slot Reassignment
    // ============================================================

    [Header("Slot Reassignment")]
    [SerializeField] private bool reassignSlotsOnLargeFacingChange = true;
    [SerializeField] private float reassignFacingAngle = 100f;


    // ============================================================
    // Cohesion / Catch Up
    // ============================================================

    [Header("Cohesion / Catch Up")]
    private bool useCatchupSpeed = true;

    [SerializeField] private float catchupStartDistance = 2f;
    [SerializeField] private float catchupMaxDistance = 10f;
    [SerializeField] private float catchupMaxMultiplier = 1.45f;

    private bool slowSquadAnchorForCohesion = true;

    [SerializeField] private float anchorSlowDistance = 4f;
    [SerializeField] private float anchorHeavySlowDistance = 8f;
    [SerializeField] private float anchorMinSpeedMultiplier = 0.65f;


    // ============================================================
    // Slot Validity / Formation Breaking
    // ============================================================

    [Header("Slot Validity")]
    [SerializeField] private float slotCheckRadius = 0.45f;
    [SerializeField] private float navMeshSampleRadius = 0.75f;
    [SerializeField] private float slotValidationInterval = 0.15f;
    [SerializeField] private int badSlotCountToBreak = 2;
    [SerializeField] private float badSlotRatioToBreak = 0.25f;

    private float slotValidationTimer = 0f;


    // ============================================================
    // Loose Movement / Reform
    // ============================================================

    [Header("Loose / Reform")]
    [SerializeField] private float reformCheckInterval = 0.25f;
    [SerializeField] private float reformMemberDistance = 1.25f;
    [SerializeField] private float reformRatioRequired = 0.75f;

    private float reformCheckTimer = 0f;


    // ============================================================
    // Combat Targeting
    // ============================================================

    [Header("Combat")] 
    [SerializeField] private float combatTargetRefreshInterval = 0.25f;
    [SerializeField] private float combatDefensiveLeashRange = 8f;

    private SquadController attackSquadTarget;
    private EntityController attackEntityTarget;
    private float combatTargetRefreshTimer = 0f;
    private Vector3 combatHomePosition;


    // ============================================================
    // Combat Auto Scan
    // ============================================================

    [Header("Combat Auto Scan")]
    [SerializeField] private bool enableAutoCombatScan = true; // DEBUG 
    // [SerializeField] private bool autoScanWhileMoving = true; // DEBUG

    private float combatAutoScanInterval = 0.35f;
    private float combatAutoScanTimer = 0f;

    private float combatAutoScanAggressiveRange = 14f;
    private float combatAutoScanDefensiveRange = 8f;
    private float combatAutoScanStandGroundPadding = 0.5f;
    
    #endregion
    
    
    #region Public Properties
    
    public SquadData SquadData => squadData;
    public bool IsSelected { get; private set; } = false;

    public IReadOnlyList<SquadMemberController> Members => members;
    public bool HasMembers => members.Count > 0;
    public int MemberCount => members.Count;
    public int MaxMembers => maxMembers;
    public bool HasRoom => members.Count < maxMembers;

    public bool IsDragSelectable => true;

    public FactionInstance Faction
    {
        get
        {
            foreach (SquadMemberController member in members)
            {
                if (member && member.Stats)
                    return member.Stats.faction;
            }

            return null;
        }
    }
    
    #endregion

    
    #region Unity Lifecycle

    void Awake()
    {
        squadAgent = GetComponent<NavMeshAgent>();

        // Squad Data Assignment
        if (Verify.IsNull(squadData, "SquadData", this))
            return;
        
        // Squad Data Values
        Category = squadData.category;
        Formation = squadData.defaultFormation;
        Stance = squadData.defaultStance;
        defaultSpacing = squadData.defaultSpacing; 
        defaultUnitsPerRow = squadData.defaultUnitsPerRow; 
        combatAutoScanAggressiveRange = squadData.aggressiveAutoScanRange;
        combatAutoScanDefensiveRange = squadData.defensiveAutoScanRange;
        combatAutoScanStandGroundPadding = squadData.standGroundScanPadding;
        combatDefensiveLeashRange = squadData.combatDefensiveLeashRange;
        
        State = SquadState.Idle;
        MoveMode = SquadMoveMode.IdleFormed;
        
        baseSquadAgentSpeed = squadAgent.speed;
        squadAgent.updateRotation = false;
        squadAgent.angularSpeed = 99999f;
        squadAgent.acceleration = 99999f;
        squadAgent.autoBraking = false;
    }

    void Start()
    {
        SelectionManager.Instance.RegisterSelectable(this);
        SquadManager.Instance?.RegisterSquad(this);
        
    }

    void Update()
    {
        if (!HasMembers) // Safety Check DEBUG?
        {
            Debug.Log("HANDLE EMPTY SQUAD SAFETY CHECK CALLED FROM UPDATE");
            HandleEmptySquad();
            return;
        }

        switch (State)
        {
            case SquadState.Idle:
                TickIdle();
                break;
            case SquadState.Moving:
                TickMoving();
                break;
            case SquadState.InCombat:
                TickCombat();
                break;
            case SquadState.AttackMoving: 
                TickAttackMove();
                break;
            case SquadState.Routing:
                TickRouting();
                break;
        }
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.UnregisterSelectable(this);

        if (SquadManager.Instance != null)
            SquadManager.Instance.UnregisterSquad(this);
    }

    #endregion

    #region Initialization

    public void InitializeSquad(
        List<SquadMemberController> startingMembers,
        SquadFormation startingFormation = SquadFormation.Line,
        SquadStance startingStance = SquadStance.Aggressive)
    {
        if (startingMembers == null)
        {
            Debug.LogError("InitializeSquad failed: startingMembers is null.");
            return;
        }

        if (!MembersHaveSameFaction(startingMembers))
        {
            Debug.LogError("InitializeSquad failed: squad members have mixed factions.");
            return;
        }

        members.Clear();

        Formation = startingFormation;
        Stance = startingStance;

        facing = NormalizeFacing(transform.forward);
        desiredFacing = facing;
        finalDestination = transform.position;

        foreach (SquadMemberController member in startingMembers)
        {
            if (member == null) continue;

            member.transform.SetParent(null, true);

            members.Add(member);
            member.JoinSquad(this, members.Count - 1);
        }

        
        // SESSION: LoosenUp
        RebuildFormation();
        RefreshSquadAgentSpeed();

        finalSlots = GetWorldSlots(transform.position, facing);
        PlaceMembersInInitialSlots();

        MoveMode = SquadMoveMode.IdleFormed;
    }

    void PlaceMembersInInitialSlots()
    {
        List<Vector3> slots = GetWorldSlots(transform.position, facing);

        for (int i = 0; i < members.Count && i < slots.Count; i++)
        {
            SquadMemberController member = members[i];
            if (member == null) continue;

            Vector3 validSlot = GetNearestValidPointForSlot(slots[i]);

            if (member.Agent != null && member.Agent.enabled)
                member.Agent.Warp(validSlot);
            else
                member.transform.position = validSlot;

            member.SetLastSlotPosition(validSlot);
        }
    }

    #endregion

    #region Selection

    public void OnSelect()
    {
        IsSelected = true;

        foreach (SquadMemberController member in members)
        {
            if (member == null || member.Unit == null) continue;
            member.ShowSelectionVisual();
        }
    }

    public void OnDeselect()
    {
        IsSelected = false;

        foreach (SquadMemberController member in members)
        {
            if (member == null || member.Unit == null) continue;
            member.HideSelectionVisual();
        }
    }

    public GameObject GetGameObject()
    {
        return gameObject;
    }


    #region Hover

    public void OnHoverEnter()
    {
        foreach (SquadMemberController member in members)
        {
            if (member == null) continue;
            member.ShowHoverVisual();
        }
    }

    public void OnHoverExit()
    {
        // If the squad is actually selected, leave the selected visuals alone.
        if (IsSelected)
            return;

        foreach (SquadMemberController member in members)
        {
            if (member == null) continue;
            member.HideHoverVisual();
        }
    }

    #endregion
    #endregion

    
    #region State Ticks

    void TickIdle()
    {
        // Move Mode
        switch (MoveMode)
        {
            case SquadMoveMode.IdleFormed:
                TickIdleFormed();
                break;

            case SquadMoveMode.Reforming:
                TickReforming();
                break;

            case SquadMoveMode.FormedMove:
            case SquadMoveMode.LooseMove:
                TickMoving();
                break;
        }

        // Combat Scan
        TickAutoCombatScan();
    }

    void TickMoving()
    {
        // Move Mode
        switch (MoveMode)
        {
            case SquadMoveMode.IdleFormed:
                TickIdleFormed();
                break;

            case SquadMoveMode.FormedMove:
                TickFormedMove();
                break;

            case SquadMoveMode.LooseMove:
                TickLooseMove();
                break;

            case SquadMoveMode.Reforming:
                TickReforming();
                break;
        }
        
        // Combat Scan
        TickAutoCombatScan();
    }

    void TickCombat()
    {
        TickCombatBrain();
    }

    void TickAttackMove()
    {
        // TODO later.
        // For now, fall back to normal movement behavior.
        TickMoving();
    }

    void TickRouting()
    {
        // TODO later.
    }

    #endregion
    
    
    #region State Transitions

    void EnterIdle()
    {
        State = SquadState.Idle;
        MoveMode = SquadMoveMode.IdleFormed;
    }

    void EnterMoving()
    {
        State = SquadState.Moving;
        MoveMode = SquadMoveMode.FormedMove;
    }

    void EnterCombat()
    {
        State = SquadState.InCombat;
        MoveMode = SquadMoveMode.IdleFormed;

        combatTargetRefreshTimer = 0f;

        if (squadAgent != null && squadAgent.enabled && squadAgent.isOnNavMesh)
        {
            squadAgent.speed = baseSquadAgentSpeed;
            squadAgent.ResetPath();
        }
    }

    void EnterReforming(SquadState nextStateAfterReform = SquadState.Idle)
    {
        State = nextStateAfterReform;
        MoveMode = SquadMoveMode.Reforming;
        reformCheckTimer = 0f;
    }

    #endregion
    
    
    
    
    #region Orders

    public void OrderMove(Vector3 destination)
    {
        Vector3 resolvedFacing = ResolveFacing(destination);
        OrderMove(destination, resolvedFacing);
    }

    public void OrderMove(
        Vector3 destination,
        Vector3 orderedFacing,
        float requestedFormationWidth = -1f)
    {
        if (!HasMembers) return;

        ClearCombatTargets();

        Vector3 previousFacing = facing;

        if (requestedFormationWidth > 0f)
            formationWidth = requestedFormationWidth;

        finalDestination = destination;
        desiredFacing = NormalizeFacing(orderedFacing);

        RebuildFormation();
        RefreshSquadAgentSpeed();

        finalSlots = GetWorldSlots(finalDestination, desiredFacing);

        bool reassignedForBigTurn = ShouldReassignSlotsForNewOrder(previousFacing, desiredFacing);

        if (reassignedForBigTurn)
        {
            ReassignMembersToNearestSlots(finalSlots);
            facing = desiredFacing;
        }

        squadAgent.isStopped = false;
        squadAgent.stoppingDistance = 0.2f;
        squadAgent.SetDestination(finalDestination);

        EnterMoving();

        slotValidationTimer = 0f;

        FormationVisualizer.Instance?.ShowSlots(finalSlots);
    }

    public void OrderStop()
    {
        ClearCombatTargets();

        if (squadAgent && squadAgent.enabled)
        {
            squadAgent.speed = baseSquadAgentSpeed;
            squadAgent.ResetPath();
        }

        foreach (SquadMemberController member in members)
        {
            if (member == null) continue;
            member.Stop();
        }

        EnterIdle();
    }

    public void OrderAttackSquad(SquadController enemySquad)
    {
        BeginCombatWithSquad(enemySquad);
    }

    public void OrderAttackEntity(EntityController target)
    {
        if (!CanAttackEntity(target))
            return;

        ClearCombatTargets();

        attackEntityTarget = target;
        combatHomePosition = transform.position;

        EnterCombat();
        TickCombatBrain();
    }

    public void SetFormation(SquadFormation newFormation)
    {
        Formation = newFormation;
        RebuildFormation();

        // In combat, store the new preference but do not yank members away
        // from their active targets.
        if (State == SquadState.InCombat)
            return;

        List<Vector3> currentSlots = GetWorldSlots(transform.position, facing);
        FormationVisualizer.Instance?.ShowSlots(currentSlots);

        if (State == SquadState.Idle)
            MoveMode = SquadMoveMode.Reforming;
    }

    public void SetStance(SquadStance newStance)
    {
        Stance = newStance;

        if (Stance == SquadStance.NoAttack) // Do we want this here or in state logic?
        {
            ClearCombatTargets();

            if (State == SquadState.InCombat)
                EnterReforming(SquadState.Idle);
        }
    }
    
    public List<Vector3> GetPreviewSlots(
        Vector3 center,
        Vector3 slotFacing,
        float requestedFormationWidth = -1f)
    {
        float oldWidth = formationWidth;

        if (requestedFormationWidth > 0f)
            formationWidth = requestedFormationWidth;

        RebuildFormation();
        List<Vector3> previewSlots = GetWorldSlots(center, NormalizeFacing(slotFacing));

        formationWidth = oldWidth;
        RebuildFormation();

        return previewSlots;
    }

    #endregion

    
    #region Combat Brain
    void TickCombatBrain()
    {
        // if !HasMembers HandleEmtpySquad -> should be handled through HandleMemberDeath
        
        if (Stance == SquadStance.NoAttack)
        {
            ClearCombatTargets();
            EnterReforming(SquadState.Idle);
            return;
        }

        combatTargetRefreshTimer -= Time.deltaTime;

        if (combatTargetRefreshTimer > 0f)
            return;

        combatTargetRefreshTimer = combatTargetRefreshInterval;

        CleanNullMembers();


        if (attackSquadTarget != null)
        {
            if (!CanAttackSquad(attackSquadTarget))
            {
                ExitCombatAndTryReacquire();
                return;
            }

            AssignMemberTargetsFromEnemySquad(attackSquadTarget);
            return;
        }

        if (attackEntityTarget != null)
        {
            if (!CanAttackEntity(attackEntityTarget))
            {
                ExitCombatAndTryReacquire();
                return;
            }

            AssignAllMembersToEntity(attackEntityTarget);
            return;
        }

        ExitCombatAndTryReacquire();
    }

    bool CanAttackSquad(SquadController enemySquad)
    {
        if (enemySquad == null) return false;
        if (enemySquad == this) return false;
        if (Stance == SquadStance.NoAttack) return false;
        if (Faction == null || enemySquad.Faction == null) return false;
        if (Faction.teamId == enemySquad.Faction.teamId) return false;
        if (!enemySquad.HasLivingMembers()) return false;

        return true;
    }

    bool CanAttackEntity(EntityController target)
    {
        if (target == null) return false;
        if (target.Stats == null) return false;
        if (!target.Stats.IsAlive) return false;
        if (Stance == SquadStance.NoAttack) return false;
        if (Faction == null || target.Stats.faction == null) return false;
        if (Faction.teamId == target.Stats.faction.teamId) return false;

        return true;
    }

    public bool HasLivingMembers()
    {
        foreach (SquadMemberController member in members)
        {
            if (member == null) continue;
            if (member.IsAlive) return true;
        }

        return false;
    }
    
    
    void AssignMemberTargetsFromEnemySquad(SquadController enemySquad)
    {
        if (enemySquad == null) return;

        IReadOnlyList<SquadMemberController> enemyMembers = enemySquad.Members;

        foreach (SquadMemberController member in members)
        {
            if (member == null) continue;
            if (!member.IsAlive) continue;

            EntityController bestTarget = FindBestTargetForMember(member, enemyMembers);

            if (bestTarget != null)
                member.AssignAttackTarget(bestTarget);
            else
                member.ClearAttackTarget();
        }
    }

    EntityController FindBestTargetForMember(
        SquadMemberController member,
        IReadOnlyList<SquadMemberController> enemyMembers)
    {
        if (member == null) return null;
        if (member.Stats == null) return null;
        if (enemyMembers == null) return null;

        EntityController best = null;
        float bestDistance = float.MaxValue;

        foreach (SquadMemberController enemyMember in enemyMembers)
        {
            if (enemyMember == null) continue;
            if (!enemyMember.IsAlive) continue;
            if (enemyMember.Unit == null) continue;
            if (enemyMember.Stats == null) continue;
            if (enemyMember.Stats.faction == member.Stats.faction) continue;

            float distance = Vector3.SqrMagnitude(
                member.transform.position - enemyMember.transform.position);

            if (Stance == SquadStance.StandGround)
            {
                float range = member.Stats.attackRange;
                if (distance > range * range)
                    continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = enemyMember.Unit;
            }
        }

        return best;
    }

    void AssignAllMembersToEntity(EntityController target)
    {
        if (target == null) return;

        foreach (SquadMemberController member in members)
        {
            if (member == null) continue;
            if (!member.IsAlive) continue;

            if (Stance == SquadStance.StandGround &&
                member.Stats != null &&
                !Calc.WithinRange(
                    member.transform.position,
                    target.transform.position,
                    member.Stats.attackRange))
            {
                member.ClearAttackTarget();
                continue;
            }

            member.AssignAttackTarget(target);
        }
    }

    void ClearCombatTargets()
    {
        attackSquadTarget = null;
        attackEntityTarget = null;

        foreach (SquadMemberController member in members)
        {
            if (member == null) continue;
            member.ClearAttackTarget();
        }
    }

    public bool CanMemberChaseTarget(
        SquadMemberController member,
        EntityController target)
    {
        if (member == null || target == null)
            return false;

        switch (Stance)
        {
            case SquadStance.Aggressive:
                return true;

            case SquadStance.Defensive:
                return Calc.WithinRange(
                    combatHomePosition,
                    target.transform.position,
                    combatDefensiveLeashRange);

            case SquadStance.StandGround:
                if (member.Stats == null) return false;

                return Calc.WithinRange(
                    member.transform.position,
                    target.transform.position,
                    member.Stats.attackRange);

            case SquadStance.NoAttack:
                return false;
        }

        return false;
    }
    
    void ExitCombatAndTryReacquire()
    {
        ClearCombatTargets();

        if (TryFindAutoCombatTarget(out SquadController nextEnemySquad))
        {
            BeginCombatWithSquad(nextEnemySquad);
            return;
        }

        EnterReforming(SquadState.Idle);
    }

    #region Auto Scanning

    void TickAutoCombatScan()
    {
        if (!ShouldAutoCombatScan()) return;
        
        combatAutoScanTimer -= Time.deltaTime;

        if (combatAutoScanTimer > 0f) return;

        if (TryFindAutoCombatTarget(out SquadController enemySquad))
            BeginCombatWithSquad(enemySquad);
    }

    bool ShouldAutoCombatScan()
    {
        if (!enableAutoCombatScan) return false;

        if (!HasMembers) return false;

        if (Stance == SquadStance.NoAttack) return false;

        if (State == SquadState.InCombat) return false;

        if (State == SquadState.Routing) return false;
        
        if (State == SquadState.Moving) return false; // TODO: decide on a foundation for tww type combat when moving (
        
        return true;
    }

    bool TryFindAutoCombatTarget(out SquadController enemySquad)
    {
        enemySquad = null;
        if (SquadManager.Instance == null) return false;

        float scanRange = GetAutoCombatScanRange();
        
        if (scanRange <= 0f) return false;
        
        float bestDistanceSqr = scanRange * scanRange;
        
        foreach (SquadController candidate in SquadManager.Instance.Squads)
        {
            if (!candidate) continue;
            if (!CanAttackSquad(candidate)) continue;

            float distanceSqr = Calc.SqrDistance(transform.position, candidate.transform.position);

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                enemySquad = candidate;
            }
        }

        return enemySquad != null;
    }

    float GetAutoCombatScanRange()
    {
        switch (Stance)
        {
            case SquadStance.Aggressive:
                return combatAutoScanAggressiveRange;

            case SquadStance.Defensive:
                return combatAutoScanDefensiveRange;

            case SquadStance.StandGround:
                float maxRange = 0f;

                foreach (SquadMemberController member in members)
                {
                    if (member == null) continue;
                    if (!member.IsAlive) continue;
                    if (member.Stats == null) continue;

                    maxRange = Mathf.Max(maxRange, member.Stats.attackRange);
                }
                return maxRange + combatAutoScanStandGroundPadding; // TODO we need to make this per unit instead of entire squad if we use mixed squads

            case SquadStance.NoAttack:
                return 0f;
        }

        return 0f;
    }
    
    
    void BeginCombatWithSquad(SquadController enemySquad)
    {
        if (!CanAttackSquad(enemySquad))
            return;

        ClearCombatTargets();

        attackSquadTarget = enemySquad;
        combatHomePosition = transform.position;

        EnterCombat();
        TickCombatBrain();
    }

    #endregion
    #endregion
    
    
    
    
    #region Movement Modes

    void TickIdleFormed()
    {
        SmoothFacingToward(desiredFacing);
        UpdateMembersToCurrentSlots();
    }

    void TickFormedMove()
    {
        UpdateFacingFromAgent();
        UpdateMembersToCurrentSlots();

        slotValidationTimer -= Time.deltaTime;

        if (slotValidationTimer <= 0f)
        {
            slotValidationTimer = slotValidationInterval;

            int badSlots = CountBadCurrentSlots();

            if (ShouldBreakFormation(badSlots))
            {
                SwitchToLooseMove();
                return;
            }
        }

        if (!squadAgent.pathPending &&
            squadAgent.remainingDistance <= squadAgent.stoppingDistance)
        {
            MoveMode = SquadMoveMode.Reforming;
            reformCheckTimer = 0f;
        }
    }

    void TickLooseMove()
    {
        UpdateSquadCenterFromMembers();

        reformCheckTimer -= Time.deltaTime;

        if (reformCheckTimer <= 0f)
        {
            reformCheckTimer = reformCheckInterval;

            if (EnoughMembersNearFinalSlots())
                BeginReforming();
        }
    }

    void TickReforming()
    {
        SmoothFacingToward(desiredFacing);
        UpdateMembersToCurrentSlots();

        reformCheckTimer -= Time.deltaTime;

        if (reformCheckTimer <= 0f)
        {
            reformCheckTimer = reformCheckInterval;

            if (EnoughMembersNearCurrentSlots())
            {
                MoveMode = SquadMoveMode.IdleFormed;

                if (State == SquadState.Moving)
                    State = SquadState.Idle;
            }
        }
    }

    #endregion

    #region Formed Movement

    void UpdateMembersToCurrentSlots()
    {
        if (!HasMembers) return;

        if (formationOffsets.Count != members.Count)
            RebuildFormation();

        List<Vector3> currentSlots = GetWorldSlots(transform.position, facing);

        UpdateSquadAnchorSpeedForCohesion(currentSlots);

        for (int i = 0; i < members.Count && i < currentSlots.Count; i++)
        {
            SquadMemberController member = members[i];
            if (member == null) continue;

            Vector3 slot = GetNearestValidPointForSlot(currentSlots[i]);

            float distanceToSlot = Vector3.Distance(
                member.transform.position,
                slot);

            float speedMultiplier = GetCatchupSpeedMultiplier(distanceToSlot);

            member.MoveToSlot(
                slot,
                slotUpdateThreshold,
                0.1f,
                speedMultiplier);
        }
    }

    int CountBadCurrentSlots()
    {
        List<Vector3> currentSlots = GetWorldSlots(transform.position, facing);
        int badCount = 0;

        for (int i = 0; i < members.Count && i < currentSlots.Count; i++)
        {
            SquadMemberController member = members[i];
            if (member == null) continue;

            if (IsCurrentSlotBad(member, currentSlots[i]))
                badCount++;
        }

        return badCount;
    }

    bool IsCurrentSlotBad(SquadMemberController member, Vector3 slot)
    {
        if (!IsSlotNearNavMesh(slot))
            return true;

        if (IsSlotBlockedByObstacle(slot))
            return true;

        if (!CanMemberPathToCurrentSlot(member, slot))
            return true;

        return false;
    }

    bool ShouldBreakFormation(int badSlotCount)
    {
        if (!HasMembers) return false;

        float badRatio = (float)badSlotCount / members.Count;

        return badSlotCount >= badSlotCountToBreak ||
               badRatio >= badSlotRatioToBreak;
    }

    #endregion

    #region Loose Movement / Reform

    void SwitchToLooseMove()
    {
        if (squadAgent && squadAgent.enabled)
            squadAgent.ResetPath();

        MoveMode = SquadMoveMode.LooseMove;
        finalSlots = GetWorldSlots(finalDestination, desiredFacing);

        for (int i = 0; i < members.Count && i < finalSlots.Count; i++)
        {
            SquadMemberController member = members[i];
            if (member == null) continue;

            Vector3 target = GetNearestValidPointForSlot(finalSlots[i]);
            
            float distanceToTarget = Vector3.Distance(member.transform.position, target);
            float speedMultiplier = GetCatchupSpeedMultiplier(distanceToTarget);

            member.MoveToPoint(target, 0.1f, speedMultiplier);
        }

        FormationVisualizer.Instance?.ShowSlots(finalSlots);
    }

    void BeginReforming()
    {
        UpdateSquadCenterFromMembers();

        facing = desiredFacing;
        finalDestination = transform.position;
        finalSlots = GetWorldSlots(transform.position, facing);

        MoveMode = SquadMoveMode.Reforming;
        reformCheckTimer = 0f;
    }

    bool EnoughMembersNearFinalSlots()
    {
        if (!HasMembers || finalSlots.Count == 0)
            return false;

        int nearCount = 0;

        for (int i = 0; i < members.Count && i < finalSlots.Count; i++)
        {
            SquadMemberController member = members[i];
            if (member == null) continue;

            Vector3 slot = GetNearestValidPointForSlot(finalSlots[i]);

            if (member.IsNear(slot, reformMemberDistance))
                nearCount++;
        }

        float ratio = (float)nearCount / members.Count;
        return ratio >= reformRatioRequired;
    }

    bool EnoughMembersNearCurrentSlots()
    {
        List<Vector3> currentSlots = GetWorldSlots(transform.position, facing);

        if (!HasMembers || currentSlots.Count == 0)
            return false;

        int nearCount = 0;

        for (int i = 0; i < members.Count && i < currentSlots.Count; i++)
        {
            SquadMemberController member = members[i];
            if (member == null) continue;

            if (member.IsNear(currentSlots[i], reformMemberDistance))
                nearCount++;
        }

        float ratio = (float)nearCount / members.Count;
        return ratio >= reformRatioRequired;
    }

    void UpdateSquadCenterFromMembers()
    {
        if (!HasMembers) return;

        Vector3 center = GetAverageMemberPosition();

        if (squadAgent != null && squadAgent.enabled)
            squadAgent.Warp(center);
        else
            transform.position = center;
    }

    #endregion

    #region Slot Validity

    bool IsSlotNearNavMesh(Vector3 slot)
    {
        return NavMesh.SamplePosition(
            slot,
            out _,
            navMeshSampleRadius,
            NavMesh.AllAreas);
    }

    bool IsSlotBlockedByObstacle(Vector3 slot)
    {
        return Physics.CheckSphere(
            slot + Vector3.up * 0.3f,
            slotCheckRadius,
            GameLayers.Instance.ObstacleLayers,
            QueryTriggerInteraction.Ignore);
    }

    bool CanMemberPathToCurrentSlot(SquadMemberController member, Vector3 slot)
    {
        if (!member || !member.Agent || !member.Agent.enabled)
            return false;

        Vector3 validSlot = GetNearestValidPointForSlot(slot);

        NavMeshPath path = new NavMeshPath();

        bool hasPath = NavMesh.CalculatePath(
            member.transform.position,
            validSlot,
            NavMesh.AllAreas,
            path);

        return hasPath && path.status == NavMeshPathStatus.PathComplete;
    }

    Vector3 GetNearestValidPointForSlot(Vector3 slot)
    {
        if (NavMesh.SamplePosition(
                slot,
                out NavMeshHit hit,
                navMeshSampleRadius,
                NavMesh.AllAreas))
        {
            return hit.position;
        }

        return slot;
    }

    #endregion

    #region Formation

    void RebuildFormation()
    {
        CleanNullMembers();

        float width = formationWidth > 0f
            ? formationWidth
            : GetDefaultFormationWidth();

        if (FormationCalculator.Instance != null)
        {
            formationOffsets = FormationCalculator.Instance.CalculateOffsets(
                members.Count,
                width,
                Formation);
        }
        else
        {
            formationOffsets = BuildFallbackLineOffsets(members.Count);
        }

        ReassignSlotIndices();
    }

    List<Vector3> GetWorldSlots(Vector3 center, Vector3 slotFacing)
    {
        slotFacing = NormalizeFacing(slotFacing);

        if (FormationCalculator.Instance != null)
        {
            return FormationCalculator.Instance.ConvertOffsetsToWorldPositions(
                formationOffsets,
                center,
                slotFacing);
        }

        List<Vector3> result = new List<Vector3>();
        Vector3 right = new Vector3(slotFacing.z, 0f, -slotFacing.x).normalized;

        foreach (Vector2 offset in formationOffsets)
            result.Add(center + right * offset.x + slotFacing * offset.y);

        return result;
    }

    float GetDefaultFormationWidth()
    {
        float spacing = FormationCalculator.Instance
            ? FormationCalculator.Instance.DefaultSpacing
            : defaultSpacing;

        int unitsPerRow = Mathf.Clamp(
            defaultUnitsPerRow,
            1,
            Mathf.Max(1, members.Count));

        return unitsPerRow * spacing;
    }
    List<Vector2> BuildFallbackLineOffsets(int count)
    {
        List<Vector2> offsets = new List<Vector2>();
        if (count <= 0) return offsets;

        float rowWidth = (count - 1) * defaultSpacing;

        for (int i = 0; i < count; i++)
        {
            float x = i * defaultSpacing - rowWidth / 2f;
            offsets.Add(new Vector2(x, 0f));
        }

        return offsets;
    }

    #endregion

    #region Members

    public bool CanAcceptMember(SquadMemberController member)
    {
        if (member == null) return false;
        if (!HasRoom) return false;

        if (member.Stats == null || member.Stats.faction == null)
            return false;

        return member.Stats.faction == Faction;
    }

    public bool AddMember(SquadMemberController member)
    {
        if (!CanAcceptMember(member)) return false;

        if (member.Squad != null && member.Squad != this)
            member.Squad.RemoveMember(member);

        member.transform.SetParent(null, true);

        members.Add(member);
        member.JoinSquad(this, members.Count - 1);

        RebuildFormation();

        if (MoveMode == SquadMoveMode.IdleFormed)
            MoveMode = SquadMoveMode.Reforming;

        return true;
    }

    public void RemoveMember(SquadMemberController member)
    {
        if (member == null) return;
        if (!members.Contains(member)) return;

        members.Remove(member);
        member.LeaveSquad();

        ReassignSlotIndices();
        RebuildFormation();

        if (!HasMembers)
            HandleEmptySquad();
    }

    void ReassignSlotIndices()
    {
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] == null) continue;
            members[i].SetSlotIndex(i);
        }
    }

    void CleanNullMembers()
    {
        members = members.Where(m => m != null).ToList();
    }

    bool MembersHaveSameFaction(List<SquadMemberController> startingMembers)
    {
        FactionInstance faction = null;

        foreach (SquadMemberController member in startingMembers)
        {
            if (member == null || member.Stats == null)
                continue;

            if (faction == null)
            {
                faction = member.Stats.faction;
                continue;
            }

            if (member.Stats.faction != faction)
                return false;
        }

        return true;
    }

    Vector3 GetAverageMemberPosition()
    {
        Vector3 avg = Vector3.zero;
        int count = 0;

        foreach (SquadMemberController member in members)
        {
            if (member == null) continue;

            avg += member.transform.position;
            count++;
        }

        return count > 0 ? avg / count : transform.position;
    }

    #endregion

    #region Merge

    public bool CanMergeWith(SquadController other)
    {
        if (other == null) return false;
        if (other == this) return false;
        if (other.Category != Category) return false;
        if (members.Count + other.MemberCount > maxMembers) return false;
        if (Faction != null && other.Faction != null && Faction != other.Faction) return false;

        return true;
    }

    public bool AbsorbSquad(SquadController other)
    {
        if (!CanMergeWith(other)) return false;

        List<SquadMemberController> incoming = other.members.ToList();

        foreach (SquadMemberController member in incoming)
            AddMember(member);

        other.members.Clear();
        Destroy(other.gameObject);

        RebuildFormation();
        MoveMode = SquadMoveMode.Reforming;

        return true;
    }

    #endregion

    #region Facing

    Vector3 ResolveFacing(Vector3 destination)
    {
        Vector3 dir = destination - transform.position;
        dir.y = 0f;

        if (dir == Vector3.zero)
            return facing == Vector3.zero ? Vector3.forward : facing;

        return dir.normalized;
    }

    void UpdateFacingFromAgent()
    {
        // Optional:
        // Do not continuously derive formation facing from the squad agent velocity.
        // That causes the slot layout to rotate during movement, which makes members twirl/cross.
        // Facing is chosen at order time from click/drag input.
        //SmoothFacingToward(desiredFacing);
        
        if (!squadAgent) return;
        
        if (squadAgent.velocity.sqrMagnitude > 0.1f)
        {
            Vector3 dir = squadAgent.velocity;
            dir.y = 0f;
        
            if (dir != Vector3.zero)
                desiredFacing = dir.normalized;
        }
        
        SmoothFacingToward(desiredFacing);
    }

    void SmoothFacingToward(Vector3 targetFacing)
    {
        targetFacing = NormalizeFacing(targetFacing);

        facing = Vector3.RotateTowards(
            facing,
            targetFacing,
            turnSpeed * Mathf.Deg2Rad * Time.deltaTime,
            0f);
    }

    Vector3 NormalizeFacing(Vector3 dir)
    {
        dir.y = 0f;

        if (dir == Vector3.zero)
            return Vector3.forward;

        return dir.normalized;
    }

    #endregion
    
    #region Member Death

    public void HandleMemberDeath(SquadMemberController member)
    {
        if (!member) return;
        if (!members.Remove(member)) return;
        
        if (!HasMembers)
            HandleEmptySquad();
        
        ReassignSlotIndices();
        RebuildFormation();
        RefreshSquadAgentSpeed();

        if (MoveMode == SquadMoveMode.IdleFormed)
            MoveMode = SquadMoveMode.Reforming;
    }

    #endregion
    
    #region Squad Death / Empty

    void HandleEmptySquad()
    {
        if (HasMembers) return;
        
        // Later (if needed):
        // - notify SquadManager
        // - play squad destroyed feedback
        // - clear selection/control groups
        // - drop banner/equipment
        // - trigger morale effects nearby
        // - notify AI/director
        // - pool instead of destroy
        
        Destroy(gameObject);
    }
    #endregion
    
    #region Slot Reassignment / Cohesion

    bool ShouldReassignSlotsForNewOrder(Vector3 oldFacing, Vector3 newFacing)
    {
        if (!reassignSlotsOnLargeFacingChange)
            return false;

        oldFacing = NormalizeFacing(oldFacing);
        newFacing = NormalizeFacing(newFacing);

        float angle = Vector3.Angle(oldFacing, newFacing);

        return angle >= reassignFacingAngle;
    }

    void ReassignMembersToNearestSlots(List<Vector3> targetSlots)
    {
        if (members.Count <= 1) return;
        if (targetSlots == null || targetSlots.Count == 0) return;
    
        List<SquadMemberController> unassignedMembers =
            members.Where(m => m != null).ToList();
    
        List<SquadMemberController> reorderedMembers =
            new List<SquadMemberController>();
    
        int slotCount = Mathf.Min(targetSlots.Count, unassignedMembers.Count);
    
        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            Vector3 slot = targetSlots[slotIndex];
    
            SquadMemberController nearest = null;
            float nearestDistance = float.MaxValue;
    
            foreach (SquadMemberController member in unassignedMembers)
            {
                float distance = Vector3.SqrMagnitude(
                    member.transform.position - slot);
    
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = member;
                }
            }
    
            if (!nearest)
                continue;
    
            reorderedMembers.Add(nearest);
            unassignedMembers.Remove(nearest);
        }
    
        foreach (SquadMemberController leftover in unassignedMembers)
            reorderedMembers.Add(leftover);
    
        members = reorderedMembers;
        ReassignSlotIndices();
    }
    

    float GetCatchupSpeedMultiplier(float distanceToSlot)
    {
        if (!useCatchupSpeed)
            return 1f;

        if (distanceToSlot <= catchupStartDistance)
            return 1f;

        float t = Mathf.InverseLerp(
            catchupStartDistance,
            catchupMaxDistance,
            distanceToSlot);

        // SmoothStep makes the speedup less twitchy than raw linear.
        t = t * t * (3f - 2f * t);

        return Mathf.Lerp(
            1f,
            catchupMaxMultiplier,
            t);
    }

    void RefreshSquadAgentSpeed()
    {
        if (squadAgent == null)
            return;

        float speed = 0f; // NEW: was float.maxvalue

        foreach (SquadMemberController member in members)
        {
            if (member == null || member.Stats == null)
                continue;

            speed = Mathf.Max(speed, member.Stats.moveSpeed);
        }

        if (speed == 0f)
            speed = baseSquadAgentSpeed;

        baseSquadAgentSpeed = speed;
        squadAgent.speed = baseSquadAgentSpeed;
    }

    void UpdateSquadAnchorSpeedForCohesion(List<Vector3> currentSlots)
    {
        if (!slowSquadAnchorForCohesion)
            return;

        if (squadAgent == null)
            return;

        if (MoveMode != SquadMoveMode.FormedMove)
        {
            squadAgent.speed = baseSquadAgentSpeed;
            return;
        }

        if (!HasMembers || currentSlots == null || currentSlots.Count == 0)
        {
            squadAgent.speed = baseSquadAgentSpeed;
            return;
        }

        float worstDistance = 0f;

        for (int i = 0; i < members.Count && i < currentSlots.Count; i++)
        {
            SquadMemberController member = members[i];
            if (member == null) continue;

            float distance = Vector3.Distance(
                member.transform.position,
                currentSlots[i]);

            if (distance > worstDistance)
                worstDistance = distance;
        }

        if (worstDistance <= anchorSlowDistance)
        {
            squadAgent.speed = baseSquadAgentSpeed;
            return;
        }

        float t = Mathf.InverseLerp(
            anchorSlowDistance,
            anchorHeavySlowDistance,
            worstDistance);

        float multiplier = Mathf.Lerp(
            1f,
            anchorMinSpeedMultiplier,
            t);

        squadAgent.speed = baseSquadAgentSpeed * multiplier;
    }

    #endregion
}
