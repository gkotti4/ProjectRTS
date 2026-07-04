using UnityEngine;
using UnityEngine.AI;

/// -----------------------------------------------------------------------------
/// SoldierController
/// -----------------------------------------------------------------------------
///
/// Root gameplay component for an individual soldier inside a squad.
/// Owns soldier identity, owning squad/roster/faction references, slot assignment,
/// current combat target, component references, and committed SoldierActionState.
///
/// This class acts as the soldier-level coordinator and action gate. SoldierCombat
/// requests actions, SoldierController decides whether the action can start,
/// SoldierAnimator plays it, and animation events return here before notifying
/// combat.
///
/// Design role:
/// Soldier identity, action locking, death handling, selection redirection, and
/// coordination between combat, movement, animation, and health.
///
/// SoldierController is the 'OWNER' of a soldier.
/// It is the main 'COORDINATOR' for related scripts

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(SoldierHealth))]
[RequireComponent(typeof(SoldierMotor))]
[RequireComponent(typeof(SoldierCombat))]
[RequireComponent(typeof(SoldierContactSensor))] // SESSION: PROTOTYPE COMBAT

public class SoldierController : MonoBehaviour
{
    public SoldierData Data { get; private set; }
    public SquadController Squad { get; private set; }
    public SquadRoster Roster { get; private set; }
    public FactionInstance Faction { get; private set; }
    
    public SoldierRole Role { get; private set; } = SoldierRole.None;
    public SoldierController CombatTarget { get; private set; }
    public SoldierCombat Combat { get; private set; }
    public SoldierContactSensor ContactSensor { get; private set; }

    public SoldierHealth Health { get; private set; }
    public SoldierMotor Motor { get; private set; }
    public SoldierAnimator SoldierAnimator { get; private set; }
    public SoldierSelectionVisualUI SelectionVisual { get; private set; }
    
    // -----------------------------------------------------------------------------
    // Prefab / Socket References
    // -----------------------------------------------------------------------------
    [Header("Combat Sockets")]
    [SerializeField] private Transform attackOrigin;

    // -----------------------------------------------------------------------------
    // Runtime Action Debug Constants
    // -----------------------------------------------------------------------------
    // These are intentionally not serialized.
    // They are watchdog values for catching missing animation events.
    private const bool actionDebugWarningsEnabled = true;
    private const float attackActionWarningSeconds = 3f;
    private const float hitReactActionWarningSeconds = 2f;

    // -----------------------------------------------------------------------------
    // Runtime Action Debug State
    // -----------------------------------------------------------------------------
    private float currentActionStartedAt = 0f;
    private bool hasLoggedActionStuckWarning = false;

    public Transform AttackOrigin => attackOrigin != null ? attackOrigin : transform; // CHECK IF IMPLEMENTED

    public int SlotIndex { get; private set; } = -1;
    public Vector3 LastSlotPosition { get; private set; }

    public bool IsAlive => Health != null && Health.IsAlive;

    public SoldierActionState ActionState { get; private set; } = SoldierActionState.None;

    public bool IsActionLocked =>
        ActionState != SoldierActionState.None;

    public bool IsMovementLocked =>
        ActionState == SoldierActionState.Attack ||
        ActionState == SoldierActionState.HitReact ||
        ActionState == SoldierActionState.Death;

    #region Unity Lifecycle

    void Awake()
    {
        Health = GetComponent<SoldierHealth>();
        Motor = GetComponent<SoldierMotor>();
        SoldierAnimator = GetComponentInChildren<SoldierAnimator>();
        Combat = GetComponent<SoldierCombat>();
        ContactSensor = GetComponent<SoldierContactSensor>();
        SelectionVisual = GetComponentInChildren<SoldierSelectionVisualUI>();

        SetSelectionVisual(false);
        SetHoverVisual(false);

        // -------------------------------------------------------------------------
        // Validation
        // -------------------------------------------------------------------------
        if (Health == null)
            Debug.LogError($"{name}: SoldierController missing SoldierHealth.", this);

        if (Motor == null)
            Debug.LogError($"{name}: SoldierController missing SoldierMotor.", this);

        if (SoldierAnimator == null)
            Debug.LogWarning($"{name}: SoldierController has no SoldierAnimator in children. Animations/events will not fire.", this);

        if (Combat == null)
            Debug.LogError($"{name}: SoldierController missing SoldierCombat.", this);

        if (SelectionVisual == null)
            Debug.LogWarning($"{name}: SoldierController has no SoldierSelectionVisualUI in children. Selection/hover visuals will not show.", this);
    }

    void Update()
    {
        TickActionDebugWatchdog();
    }

    void OnDestroy()
    {
        if (Health != null)
            Health.OnDied -= HandleDeath;
    }

    #endregion

    #region Initialization

    public void Initialize(
        SoldierData data,
        SquadController squad,
        SquadRoster roster,
        FactionInstance faction)
    {
        Data = data;
        Squad = squad;
        Roster = roster;
        Faction = faction;

        if (Data != null)
        {
            Health.Initialize(Data.health);
            Motor.Initialize(Data.movement);
        }
        
        Combat.Initialize(this);

        Health.OnDied += HandleDeath;

        EnsureSelectableTarget();

        // -------------------------------------------------------------------------
        // Validation
        // -------------------------------------------------------------------------
        if (Data == null)
            Debug.LogError($"{name}: Soldier Initialize validation failed. Data is null.", this);

        if (Squad == null)
            Debug.LogError($"{name}: Soldier Initialize validation failed. Squad is null.", this);

        if (Roster == null)
            Debug.LogError($"{name}: Soldier Initialize validation failed. Roster is null.", this);

        if (Faction == null)
            Debug.LogError($"{name}: Soldier Initialize validation failed. Faction is null.", this);

        if (Health == null)
            Debug.LogError($"{name}: Soldier Initialize validation failed. Health is null.", this);

        if (Motor == null)
            Debug.LogError($"{name}: Soldier Initialize validation failed. Motor is null.", this);

        if (Combat == null)
            Debug.LogError($"{name}: Soldier Initialize validation failed. Combat is null.", this);

        if (SoldierAnimator == null)
            Debug.LogWarning($"{name}: Soldier Initialize validation warning. SoldierAnimator is null.", this);

        if (SelectionVisual == null)
            Debug.LogWarning($"{name}: Soldier Initialize validation warning. SelectionVisual is null.", this);

        if (Data != null && Data.weaponProfile == null)
            Debug.LogWarning($"{name}: SoldierData has no WeaponProfile. WeaponProfile is required because it is the combat attack-stat source of truth.", this);

        if (Data != null && Data.prefab == null)
            Debug.LogWarning($"{name}: SoldierData has no prefab assigned. This is okay only if this SoldierData is not used for spawning.", this);

        if (Data != null &&
            Data.weaponProfile != null &&
            Data.weaponProfile.weaponKind == WeaponKind.Ranged &&
            attackOrigin == null)
        {
            Debug.LogWarning(
                $"{name}: Ranged soldier has no attackOrigin assigned. Projectile will spawn from soldier transform fallback.",
                this);
        }
    }

    public void SetSquad(
        SquadController squad,
        SquadRoster roster)
    {
        Squad = squad;
        Roster = roster;

        EnsureSelectableTarget();
        Combat?.ApplyProfileFromSquad();

        // -------------------------------------------------------------------------
        // Validation
        // -------------------------------------------------------------------------
        if (Squad == null)
            Debug.LogError($"{name}: SetSquad validation failed. Squad is null.", this);

        if (Roster == null)
            Debug.LogError($"{name}: SetSquad validation failed. Roster is null.", this);

        if (Combat == null)
            Debug.LogError($"{name}: SetSquad validation failed. Combat is null.", this);
    }
    
    /// Ensures this soldier redirects selection input to its owning squad.
    void EnsureSelectableTarget()
    {
        SelectionTarget target = GetComponentInChildren<SelectionTarget>();

        if (target == null)
            target = gameObject.AddComponent<SelectionTarget>();

        target.SetTarget(Squad);
    }

    #endregion

    #region Slot / Formation

    public void SetSlotIndex(int slotIndex)
    {
        SlotIndex = slotIndex;
    }

    public void SetLastSlotPosition(Vector3 position)
    {
        LastSlotPosition = position;
    }

    public void MoveToSlot(
        Vector3 slotPosition,
        float updateThreshold,
        float stoppingDistance = 0.1f,
        float speedMultiplier = 1f)
    {
        if (!IsAlive || IsMovementLocked)
            return;

        if (!Calc.OutOfRange(LastSlotPosition, slotPosition, updateThreshold))
            return;

        Motor.MoveTo(
            slotPosition,
            stoppingDistance,
            speedMultiplier);

        LastSlotPosition = slotPosition;
    }

    public void MoveToPoint(
        Vector3 position,
        float stoppingDistance = 0.1f,
        float speedMultiplier = 1f)
    {
        if (!IsAlive || IsMovementLocked)
            return;

        Motor.MoveTo(
            position,
            stoppingDistance,
            speedMultiplier);
    }

    public void Stop()
    {
        if (Motor == null)
            return;

        Motor.Stop();
    }

    #endregion

    #region Health / Death

    void HandleDeath(SoldierHealth health)
    {
        Stop();

        Role = SoldierRole.None;
        CombatTarget = null;

        Combat?.ClearCombat();

        TryBeginAction(SoldierActionState.Death);

        Roster?.NotifySoldierDied(this);

        Collider[] colliders = GetComponentsInChildren<Collider>();

        foreach (Collider col in colliders)
            col.enabled = false;

        Destroy(gameObject, 2f);
    }

    #endregion

    #region Selection Visuals

    public void SetSelectionVisual(bool visible)
    {
        // if (selectionVisual != null)
        //     selectionVisual.SetActive(visible);
        if (!SelectionVisual) return;
        SelectionVisual.SetSelected(visible);
        
    }

    public void SetHoverVisual(bool visible)
    {
        // if (hoverVisual != null)
        //     hoverVisual.SetActive(visible);
        if (!SelectionVisual) return;
        SelectionVisual.SetHovered(visible);
    }

    #endregion
    
    #region Team Colors
    public void ApplyTeamColors(
        Color selectionColor,
        Color hoverColor)
    {
        // SESSION: Team Visuals
        if (SelectionVisual == null)
            return;
        
        SelectionVisual.ApplyColors(selectionColor, hoverColor);
    }
    
    #endregion
    
    #region Action State

    public bool TryBeginAction(SoldierActionState newAction)
    {
        if (!CanBeginAction(newAction))
            return false;

        SoldierActionState previousAction = ActionState;

        if (previousAction != SoldierActionState.None &&
            previousAction != newAction)
        {
            SoldierAnimator?.HandleActionCancelled(previousAction);
            Combat?.HandleActionInterrupted(previousAction, newAction);
        }

        ActionState = newAction;
        currentActionStartedAt = Time.time;
        hasLoggedActionStuckWarning = false;

        Stop();

        SoldierAnimator?.PlayAction(newAction);

        return true;
    }

    public void CompleteAction(SoldierActionState completedAction)
    {
        if (ActionState != completedAction)
            return;

        ActionState = SoldierActionState.None;
        currentActionStartedAt = 0f;
        hasLoggedActionStuckWarning = false;

        SoldierAnimator?.HandleActionCompleted(completedAction);
        Combat?.HandleActionCompleted(completedAction);
    }

    public void CancelCurrentAction(bool notifyCombat = true)
    {
        if (ActionState == SoldierActionState.None)
            return;

        if (ActionState == SoldierActionState.Death)
            return;

        SoldierActionState cancelledAction = ActionState;

        ActionState = SoldierActionState.None;
        currentActionStartedAt = 0f;
        hasLoggedActionStuckWarning = false;

        SoldierAnimator?.HandleActionCancelled(cancelledAction);

        if (notifyCombat)
            Combat?.HandleActionInterrupted(cancelledAction, SoldierActionState.None);
    }

    bool CanBeginAction(SoldierActionState newAction)
    {
        if (newAction == SoldierActionState.None)
            return false;

        if (ActionState == SoldierActionState.Death)
            return false;

        if (newAction != SoldierActionState.Death && !IsAlive)
            return false;

        if (ActionState == SoldierActionState.None)
            return true;

        if (ActionState == newAction)
            return false;

        return GetActionPriority(newAction) > GetActionPriority(ActionState);
    }

    int GetActionPriority(SoldierActionState actionState)
    {
        switch (actionState)
        {
            case SoldierActionState.Death:
                return 100;

            case SoldierActionState.HitReact:
                return 50;

            case SoldierActionState.Attack:
                return 25;

            case SoldierActionState.None:
            default:
                return 0;
        }
    }

    void TickActionDebugWatchdog()
    {
        if (!actionDebugWarningsEnabled)
            return;

        if (ActionState == SoldierActionState.None ||
            ActionState == SoldierActionState.Death)
        {
            return;
        }

        if (hasLoggedActionStuckWarning)
            return;

        float warningSeconds = ActionState == SoldierActionState.Attack
            ? attackActionWarningSeconds
            : hitReactActionWarningSeconds;

        if (warningSeconds <= 0f)
            return;

        if (Time.time - currentActionStartedAt < warningSeconds)
            return;

        hasLoggedActionStuckWarning = true;

        Debug.LogWarning(
            $"{name}: ActionState '{ActionState}' has lasted longer than {warningSeconds:0.00}s. " +
            "Check the animation transition and required animation end event.",
            this);
    }
    
    #endregion

    #region Combat Visuals / Animation Hooks

    public void OnAttackImpact()
    {
        Combat?.ResolveAttackImpact();
    }

    public void OnProjectileRelease()
    {
        Combat?.ResolveProjectileRelease();
    }

    public void OnAttackEnd()
    {
        CompleteAction(SoldierActionState.Attack);
    }

    public void OnHitEnd()
    {
        CompleteAction(SoldierActionState.HitReact);
    }

    #endregion

    #region Helpers
    
    public void SetCombatRole(SoldierRole role)
    {
        Role = role;
    }

    public void SetCombatTarget(SoldierController target)
    {
        CombatTarget = target;
    }

    public void ClearCombatTarget()
    {
        CombatTarget = null;
    }

    public void MoveToCombatPoint(
        Vector3 position,
        float stoppingDistance,
        float speedMultiplier = 1f)
    {
        if (!IsAlive || IsMovementLocked)
            return;

        Motor.MoveTo(position, stoppingDistance, speedMultiplier);
    }

    public void FaceToward(Vector3 position, float turnSpeed = 900f)
    {
        Vector3 dir = position - transform.position;
        dir.y = 0f;

        if (dir == Vector3.zero)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            dir.normalized,
            Vector3.up);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime);

        Motor?.SuppressVelocityRotation();
    }
    #endregion

}


