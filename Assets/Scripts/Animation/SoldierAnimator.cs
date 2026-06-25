using UnityEngine;

/// -----------------------------------------------------------------------------
/// SoldierAnimator
/// -----------------------------------------------------------------------------
///
/// Basic animation bridge for a soldier.
///
/// ProjectRTS 2.0 rule:
/// Keep this simple.
///
/// Drives:
/// - IsMoving
/// - IsMovingBackwards
/// - InCombat
/// - Attack
/// - HitReact
/// - Death
///
/// Does NOT drive:
/// - MoveX
/// - MoveZ
/// - MoveSpeed
/// - 2D blendspaces
///
/// SoldierController owns committed action state.
/// SoldierMotor owns movement execution.
/// SoldierAnimator only visualizes current state and forwards animation events.
///
/// Important:
/// Combat movement can be tiny/frequent NavMeshAgent.SetDestination movement.
/// NavMeshAgent velocity/path state can flicker during those micro-moves, so this
/// script also measures actual world-position delta to prevent visual sliding.
///
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class SoldierAnimator : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private SoldierController soldierController;
    [SerializeField] private SoldierMotor soldierMotor;

    #endregion

    #region Movement Parameters

    [Header("Movement Parameters")]
    [SerializeField] private float movementVelocityDeadZone = 0.025f;

    [Tooltip("How long movement animation stays on after movement briefly stops. Helps prevent combat micro-movement flicker.")]
    [SerializeField] private float movementReleaseDelay = 0.04f;

    [Tooltip("If velocity points this far opposite the soldier's forward direction, IsMovingBackwards becomes true.")]
    [SerializeField] private float backwardsDotThreshold = -0.25f;

    [Tooltip("Set false if your Animator Controller does not have IsMovingBackwards yet.")]
    [SerializeField] private bool useMovingBackwardsParameter = true;

    [Tooltip("During clean squad movement states, this can force IsMoving even if one frame of agent velocity is missing.")]
    [SerializeField] private bool trustSquadMovementState = true;

    private bool isMovingVisual = false;
    private float movementReleaseTimer = 0f;

    private Vector3 lastWorldPosition = Vector3.zero;
    private Vector3 measuredWorldVelocity = Vector3.zero;
    private bool hasLastWorldPosition = false;

    #endregion

    #region Combat State

    [Header("Combat State")]
    [SerializeField] private bool useCombatStateFromSquad = true;

    [Tooltip("ApproachingCombat can use combat-ready idle/walk visuals.")]
    [SerializeField] private bool approachCountsAsCombat = true;

    [Tooltip("AttackMoving can use combat-ready idle/walk visuals.")]
    [SerializeField] private bool attackMoveCountsAsCombat = false;

    #endregion

    #region Animator Layers

    [Header("Animator Layers")]
    [SerializeField] private string upperBodyLayerName = "UpperBody";
    [SerializeField] private bool controlUpperBodyLayer = true;
    [SerializeField] private float upperBodyLayerDefaultWeight = 1f;
    [SerializeField] private float upperBodyLayerDisabledWeight = 0f;

    private int upperBodyLayerIndex = -1;

    #endregion

    #region Animator Hashes

    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int IsMovingBackwards = Animator.StringToHash("IsMovingBackwards");
    private static readonly int InCombat = Animator.StringToHash("InCombat");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int HitReact = Animator.StringToHash("HitReact");
    private static readonly int Death = Animator.StringToHash("Death");

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        ResolveReferences();
        InitializeUpperBodyLayer();
        InitializeMeasuredVelocity();
    }

    void OnValidate()
    {
        movementVelocityDeadZone = Mathf.Max(0f, movementVelocityDeadZone);
        movementReleaseDelay = Mathf.Max(0f, movementReleaseDelay);
        backwardsDotThreshold = Mathf.Clamp(backwardsDotThreshold, -1f, 0f);

        upperBodyLayerDefaultWeight = Mathf.Clamp01(upperBodyLayerDefaultWeight);
        upperBodyLayerDisabledWeight = Mathf.Clamp01(upperBodyLayerDisabledWeight);
    }

    void Update()
    {
        UpdateMeasuredWorldVelocity();
        UpdateMovementParameters();
        UpdateCombatParameter();
    }

    #endregion

    #region Setup

    void ResolveReferences()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (soldierController == null)
            soldierController = GetComponentInParent<SoldierController>();

        if (soldierMotor == null)
            soldierMotor = GetComponentInParent<SoldierMotor>();

        if (animator == null)
            Debug.LogError($"{name}: SoldierAnimator could not find Animator.", this);

        if (soldierController == null)
            Debug.LogError($"{name}: SoldierAnimator could not find SoldierController in parent.", this);

        if (soldierMotor == null)
            Debug.LogError($"{name}: SoldierAnimator could not find SoldierMotor in parent.", this);
    }

    void InitializeUpperBodyLayer()
    {
        if (animator == null || !controlUpperBodyLayer)
            return;

        upperBodyLayerIndex = animator.GetLayerIndex(upperBodyLayerName);

        if (upperBodyLayerIndex >= 0)
        {
            animator.SetLayerWeight(
                upperBodyLayerIndex,
                upperBodyLayerDefaultWeight);
        }
    }

    void InitializeMeasuredVelocity()
    {
        lastWorldPosition = GetMovementRootPosition();
        measuredWorldVelocity = Vector3.zero;
        hasLastWorldPosition = true;
    }

    #endregion

    #region Movement

    void UpdateMeasuredWorldVelocity()
    {
        Vector3 currentWorldPosition = GetMovementRootPosition();

        if (!hasLastWorldPosition || Time.deltaTime <= 0f)
        {
            lastWorldPosition = currentWorldPosition;
            measuredWorldVelocity = Vector3.zero;
            hasLastWorldPosition = true;
            return;
        }

        measuredWorldVelocity =
            (currentWorldPosition - lastWorldPosition) / Time.deltaTime;

        measuredWorldVelocity.y = 0f;
        lastWorldPosition = currentWorldPosition;
    }

    Vector3 GetMovementRootPosition()
    {
        if (soldierController != null)
            return soldierController.transform.position;

        return transform.position;
    }

    void UpdateMovementParameters()
    {
        if (animator == null)
            return;

        bool shouldMoveNow = ShouldUseMovingState();

        if (shouldMoveNow)
        {
            isMovingVisual = true;
            movementReleaseTimer = movementReleaseDelay;
        }
        else if (movementReleaseTimer > 0f)
        {
            movementReleaseTimer -= Time.deltaTime;
            isMovingVisual = true;
        }
        else
        {
            isMovingVisual = false;
        }

        bool isMovingBackwards =
            isMovingVisual &&
            IsClearlyMovingBackwards();

        animator.SetBool(IsMoving, isMovingVisual);

        if (useMovingBackwardsParameter)
            animator.SetBool(IsMovingBackwards, isMovingBackwards);
    }

    // bool ShouldUseMovingState()
    // {
    //     if (soldierController == null)
    //         return false;
    //
    //     if (!soldierController.IsAlive)
    //         return false;
    //
    //     // Full-body committed actions own the visual state.
    //     // No walking during Attack / HitReact / Death.
    //     if (soldierController.IsMovementLocked)
    //         return false;
    //
    //     SquadController squad = soldierController.Squad;
    //
    //     if (squad == null)
    //         return HasAnyMovement();
    //
    //     switch (squad.State)
    //     {
    //         case SquadState.Moving:
    //         case SquadState.ApproachingCombat:
    //         case SquadState.AttackMoving:
    //         case SquadState.Charging:
    //         case SquadState.Withdrawing:
    //         case SquadState.Reforming:
    //         case SquadState.Routing:
    //             return trustSquadMovementState || HasAnyMovement();
    //
    //         case SquadState.InCombat:
    //             // Combat movement is local and intermittent.
    //             // Do not force movement from squad state alone here.
    //             return HasAnyMovement();
    //
    //         case SquadState.Idle:
    //         default:
    //             return HasAnyMovement();
    //     }
    // }
    
    bool ShouldUseMovingState()
    {
        if (soldierController == null)
            return false;

        if (!soldierController.IsAlive)
            return false;

        if (soldierController.IsMovementLocked)
            return false;

        return HasMotorMovement();
    }
    
    bool HasMotorMovement()
    {
        if (soldierMotor == null)
            return false;

        Vector3 velocity = soldierMotor.Velocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude >
            movementVelocityDeadZone * movementVelocityDeadZone)
        {
            return true;
        }

        return HasMeaningfulAgentPath();
    }
    
    bool HasMeaningfulAgentPath()
    {
        if (soldierMotor == null || soldierMotor.Agent == null)
            return false;

        var agent = soldierMotor.Agent;

        if (!agent.enabled || !agent.isOnNavMesh)
            return false;

        if (agent.pathPending)
            return true;

        if (!agent.hasPath)
            return false;

        float remainingDistance = agent.remainingDistance;

        if (float.IsInfinity(remainingDistance))
            return true;

        float stopDistance = Mathf.Max(
            agent.stoppingDistance,
            0.05f);

        return remainingDistance > stopDistance + 0.05f;
    }

    // bool HasAnyMovement()
    // {
    //     float deadZoneSqr =
    //         movementVelocityDeadZone *
    //         movementVelocityDeadZone;
    //
    //     if (soldierMotor != null)
    //     {
    //         if (soldierMotor.HasPath)
    //             return true;
    //
    //         Vector3 motorVelocity = soldierMotor.Velocity;
    //         motorVelocity.y = 0f;
    //
    //         if (motorVelocity.sqrMagnitude > deadZoneSqr)
    //             return true;
    //     }
    //
    //     return measuredWorldVelocity.sqrMagnitude > deadZoneSqr;
    // }
    
    bool HasAnyMovement()
    {
        float deadZoneSqr =
            movementVelocityDeadZone *
            movementVelocityDeadZone;

        if (soldierMotor != null)
        {
            Vector3 motorVelocity = soldierMotor.Velocity;
            motorVelocity.y = 0f;

            if (motorVelocity.sqrMagnitude > deadZoneSqr)
                return true;

            if (HasMeaningfulAgentPath())
                return true;
        }

        return measuredWorldVelocity.sqrMagnitude > deadZoneSqr;
    }
    
    // bool HasMeaningfulAgentPath() // new
    // {
    //     if (soldierMotor == null || soldierMotor.Agent == null)
    //         return false;
    //
    //     UnityEngine.AI.NavMeshAgent agent = soldierMotor.Agent;
    //
    //     if (!agent.enabled || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
    //         return false;
    //
    //     if (agent.pathPending)
    //         return true;
    //
    //     if (!agent.hasPath)
    //         return false;
    //
    //     if (agent.remainingDistance == Mathf.Infinity)
    //         return true;
    //
    //     float stopDistance =
    //         Mathf.Max(agent.stoppingDistance, movementVelocityDeadZone);
    //
    //     return agent.remainingDistance > stopDistance + 0.05f;
    // }

    bool IsClearlyMovingBackwards()
    {
        Vector3 movementVelocity = GetBestMovementVelocity();

        movementVelocity.y = 0f;

        if (movementVelocity.sqrMagnitude <=
            movementVelocityDeadZone * movementVelocityDeadZone)
        {
            return false;
        }

        Vector3 forward = GetMovementForward();
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            return false;

        float dot = Vector3.Dot(
            forward.normalized,
            movementVelocity.normalized);

        return dot <= backwardsDotThreshold;
    }

    Vector3 GetBestMovementVelocity()
    {
        float deadZoneSqr =
            movementVelocityDeadZone *
            movementVelocityDeadZone;

        if (soldierMotor != null)
        {
            Vector3 motorVelocity = soldierMotor.Velocity;
            motorVelocity.y = 0f;

            if (motorVelocity.sqrMagnitude > deadZoneSqr)
                return motorVelocity;
        }

        return measuredWorldVelocity;
    }

    Vector3 GetMovementForward()
    {
        if (soldierController != null)
            return soldierController.transform.forward;

        return transform.forward;
    }

    #endregion

    #region Combat Parameter

    void UpdateCombatParameter()
    {
        if (animator == null)
            return;

        animator.SetBool(
            InCombat,
            IsInCombatAnimationState());
    }

    bool IsInCombatAnimationState()
    {
        if (!useCombatStateFromSquad)
            return false;

        if (soldierController == null || soldierController.Squad == null)
            return false;

        switch (soldierController.Squad.State)
        {
            case SquadState.InCombat:
            case SquadState.Charging:
                return true;

            case SquadState.ApproachingCombat:
                return approachCountsAsCombat;

            case SquadState.AttackMoving:
                return attackMoveCountsAsCombat;

            default:
                return false;
        }
    }

    #endregion

    #region Action Playback

    public void PlayAction(SoldierActionState actionState)
    {
        if (animator == null)
            return;

        ForceMovementParametersOff();

        switch (actionState)
        {
            case SoldierActionState.Attack:
                DisableUpperBodyLayer();
                animator.ResetTrigger(HitReact);
                animator.SetTrigger(Attack);
                break;

            case SoldierActionState.HitReact:
                DisableUpperBodyLayer();
                animator.ResetTrigger(Attack);
                animator.SetTrigger(HitReact);
                break;

            case SoldierActionState.Death:
                DisableUpperBodyLayer();
                animator.ResetTrigger(Attack);
                animator.ResetTrigger(HitReact);
                animator.SetTrigger(Death);
                break;
        }
    }

    public void HandleActionCompleted(SoldierActionState actionState)
    {
        switch (actionState)
        {
            case SoldierActionState.Attack:
            case SoldierActionState.HitReact:
                EnableUpperBodyLayer();
                break;

            case SoldierActionState.Death:
                DisableUpperBodyLayer();
                break;
        }
    }

    public void HandleActionCancelled(SoldierActionState actionState)
    {
        if (animator == null)
            return;

        switch (actionState)
        {
            case SoldierActionState.Attack:
                animator.ResetTrigger(Attack);
                EnableUpperBodyLayer();
                break;

            case SoldierActionState.HitReact:
                animator.ResetTrigger(HitReact);
                EnableUpperBodyLayer();
                break;

            case SoldierActionState.Death:
                DisableUpperBodyLayer();
                break;
        }
    }

    void ForceMovementParametersOff()
    {
        isMovingVisual = false;
        movementReleaseTimer = 0f;

        if (animator == null)
            return;

        animator.SetBool(IsMoving, false);

        if (useMovingBackwardsParameter)
            animator.SetBool(IsMovingBackwards, false);
    }

    #endregion

    #region Upper Body Layer

    public void SetUpperBodyLayerEnabled(bool enabled)
    {
        if (!controlUpperBodyLayer)
            return;

        if (animator == null)
            return;

        if (upperBodyLayerIndex < 0)
            return;

        animator.SetLayerWeight(
            upperBodyLayerIndex,
            enabled
                ? upperBodyLayerDefaultWeight
                : upperBodyLayerDisabledWeight);
    }

    public void DisableUpperBodyLayer()
    {
        SetUpperBodyLayerEnabled(false);
    }

    public void EnableUpperBodyLayer()
    {
        SetUpperBodyLayerEnabled(true);
    }

    #endregion

    #region Backward-Compatible Wrappers

    public void TriggerAttack()
    {
        PlayAction(SoldierActionState.Attack);
    }

    public void TriggerHit()
    {
        PlayAction(SoldierActionState.HitReact);
    }

    public void TriggerDeath()
    {
        PlayAction(SoldierActionState.Death);
    }

    #endregion

    #region Animation Events

    // Animation Event: melee impact frame.
    public void OnAttackImpact()
    {
        soldierController?.OnAttackImpact();
    }

    // Animation Event alias: ranged projectile release frame.
    public void OnProjectileRelease()
    {
        soldierController?.OnAttackImpact();
    }

    // Animation Event alias: generic attack execution frame.
    public void OnAttackExecute()
    {
        soldierController?.OnAttackImpact();
    }

    // Animation Event: attack clip finished / unlock Attack action.
    public void OnAttackEnd()
    {
        soldierController?.OnAttackEnd();
    }

    // Animation Event: hit reaction clip finished / unlock HitReact action.
    public void OnHitEnd()
    {
        soldierController?.OnHitEnd();
    }

    // Optional alias if a clip uses this clearer event name.
    public void OnHitReactEnd()
    {
        soldierController?.OnHitEnd();
    }

    #endregion
}


// using UnityEngine;
//
// /// -----------------------------------------------------------------------------
// /// SoldierAnimator
// /// -----------------------------------------------------------------------------
// ///
// /// Adapter between soldier gameplay state and the Unity Animator.
// /// Updates locomotion parameters, combat animation state, plays committed action
// /// triggers, manages upper-body layer weight for full-body actions, and forwards
// /// animation events such as AttackImpact, AttackEnd, and HitEnd back to
// /// SoldierController.
// ///
// /// This class should not decide combat results, target selection, damage, or
// /// movement rules. It visualizes gameplay state and reports animation timing.
// ///
// /// Design role:
// /// Soldier animation presentation and animation-event bridge.
// ///
// [DisallowMultipleComponent]
// [RequireComponent(typeof(Animator))]
// public class SoldierAnimator : MonoBehaviour
// {
//     [Header("References")]
//     [SerializeField] private Animator animator;
//     [SerializeField] private SoldierController soldierController;
//     [SerializeField] private SoldierMotor soldierMotor;
//
//     [Header("Movement Parameters")]
//     private float movementDeadZone = 0.08f;
//     private float moveBlendDampTime = 0.12f;
//
//     [Header("Combat State")]
//     private bool useCombatStateFromSquad = true;
//
//     [Header("Animator Layers")]
//     private string upperBodyLayerName = "UpperBody";
//     private bool controlUpperBodyLayer = true;
//     private float upperBodyLayerDefaultWeight = 1f;
//     private float upperBodyLayerDisabledWeight = 0f;
//
//     private int upperBodyLayerIndex = -1;
//
//     private static readonly int IsMoving = Animator.StringToHash("IsMoving");
//     private static readonly int MoveX = Animator.StringToHash("MoveX");
//     private static readonly int MoveZ = Animator.StringToHash("MoveZ");
//     private static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
//     private static readonly int InCombat = Animator.StringToHash("InCombat");
//     private static readonly int Attack = Animator.StringToHash("Attack");
//     private static readonly int HitReact = Animator.StringToHash("HitReact");
//     private static readonly int Death = Animator.StringToHash("Death");
//
//     void Awake()
//     {
//         if (animator == null)
//             animator = GetComponent<Animator>();
//
//         if (soldierController == null)
//             soldierController = GetComponentInParent<SoldierController>();
//
//         if (soldierMotor == null)
//             soldierMotor = GetComponentInParent<SoldierMotor>();
//
//         if (animator == null)
//             Debug.LogError($"{name}: SoldierAnimator could not find Animator.");
//
//         if (soldierController == null)
//             Debug.LogError($"{name}: SoldierAnimator could not find SoldierController in parent.");
//
//         if (soldierMotor == null)
//             Debug.LogError($"{name}: SoldierAnimator could not find SoldierMotor in parent.");
//
//         InitializeUpperBodyLayer();
//     }
//
//     void Update()
//     {
//         UpdateMovementParameters();
//         UpdateCombatParameter();
//     }
//
//     void InitializeUpperBodyLayer()
//     {
//         if (animator == null || !controlUpperBodyLayer)
//             return;
//
//         upperBodyLayerIndex = animator.GetLayerIndex(upperBodyLayerName);
//
//         if (upperBodyLayerIndex >= 0)
//             animator.SetLayerWeight(upperBodyLayerIndex, upperBodyLayerDefaultWeight);
//         else
//             Debug.LogWarning($"{name}: Upper body layer '{upperBodyLayerName}' was not found.");
//     }
//
//     void UpdateMovementParameters()
//     {
//         if (animator == null)
//             return;
//
//         // TODO
//         bool isMoving = false;
//         // float moveX = 0f;
//         // float moveZ = 0f;
//         // float normalizedMoveSpeed = 0f;
//
//         if (!ShouldLockLocomotionParameters() && soldierMotor != null)
//         {
//             isMoving = soldierController.Squad.State == SquadState.Moving; // TODO CHECK - DEBUG
//         }
//
//         
//         animator.SetBool(IsMoving, isMoving);
//
//         // animator.SetFloat(
//         //     MoveSpeed,
//         //     normalizedMoveSpeed,
//         //     moveBlendDampTime,
//         //     Time.deltaTime);
//         //
//         // animator.SetFloat(
//         //     MoveX,
//         //     moveX,
//         //     moveBlendDampTime,
//         //     Time.deltaTime);
//         //
//         // animator.SetFloat(
//         //     MoveZ,
//         //     moveZ,
//         //     moveBlendDampTime,
//         //     Time.deltaTime);
//     }
//
//     void UpdateCombatParameter()
//     {
//         if (animator == null)
//             return;
//
//         animator.SetBool(InCombat, IsInCombatAnimationState());
//     }
//
//     bool ShouldLockLocomotionParameters()
//     {
//         return soldierController != null && soldierController.IsMovementLocked;
//     }
//
//     bool IsInCombatAnimationState()
//     {
//         if (!useCombatStateFromSquad)
//             return false;
//
//         if (soldierController == null || soldierController.Squad == null)
//             return false;
//
//         return soldierController.Squad.State == SquadState.InCombat ||
//                soldierController.Squad.State == SquadState.Charging;
//     }
//
//     public void PlayAction(SoldierActionState actionState)
//     {
//         if (animator == null)
//             return;
//
//         switch (actionState)
//         {
//             case SoldierActionState.Attack:
//                 DisableUpperBodyLayer();
//                 animator.ResetTrigger(HitReact);
//                 animator.SetTrigger(Attack);
//                 break;
//
//             case SoldierActionState.HitReact:
//                 DisableUpperBodyLayer();
//                 animator.ResetTrigger(Attack);
//                 animator.SetTrigger(HitReact);
//                 break;
//
//             case SoldierActionState.Death:
//                 DisableUpperBodyLayer();
//                 animator.ResetTrigger(Attack);
//                 animator.ResetTrigger(HitReact);
//                 animator.SetTrigger(Death);
//                 break;
//         }
//     }
//
//     public void HandleActionCompleted(SoldierActionState actionState)
//     {
//         switch (actionState)
//         {
//             case SoldierActionState.Attack:
//             case SoldierActionState.HitReact:
//                 EnableUpperBodyLayer();
//                 break;
//
//             case SoldierActionState.Death:
//                 DisableUpperBodyLayer();
//                 break;
//         }
//     }
//
//     public void HandleActionCancelled(SoldierActionState actionState)
//     {
//         if (animator == null)
//             return;
//
//         switch (actionState)
//         {
//             case SoldierActionState.Attack:
//                 animator.ResetTrigger(Attack);
//                 EnableUpperBodyLayer();
//                 break;
//
//             case SoldierActionState.HitReact:
//                 animator.ResetTrigger(HitReact);
//                 EnableUpperBodyLayer();
//                 break;
//         }
//     }
//
//     public void SetUpperBodyLayerEnabled(bool enabled)
//     {
//         if (!controlUpperBodyLayer)
//             return;
//
//         if (animator == null)
//             return;
//
//         if (upperBodyLayerIndex < 0)
//             return;
//
//         animator.SetLayerWeight(
//             upperBodyLayerIndex,
//             enabled ? upperBodyLayerDefaultWeight : upperBodyLayerDisabledWeight);
//     }
//
//     public void DisableUpperBodyLayer()
//     {
//         SetUpperBodyLayerEnabled(false);
//     }
//
//     public void EnableUpperBodyLayer()
//     {
//         SetUpperBodyLayerEnabled(true);
//     }
//
//     // Backward-compatible wrappers while other code is migrated.
//     public void TriggerAttack()
//     {
//         PlayAction(SoldierActionState.Attack);
//     }
//
//     public void TriggerHit()
//     {
//         PlayAction(SoldierActionState.HitReact);
//     }
//
//     public void TriggerDeath()
//     {
//         PlayAction(SoldierActionState.Death);
//     }
//
//     // Animation Event.
//     public void OnAttackImpact()
//     {
//         soldierController?.OnAttackImpact();
//     }
//
//     // Animation Event alias. Useful for ranged clips where the event is placed
//     // on the bow/crossbow release frame instead of a melee impact frame.
//     public void OnProjectileRelease()
//     {
//         soldierController?.OnAttackImpact();
//     }
//
//     // Animation Event alias. Useful if a clip wants a weapon-agnostic event name.
//     public void OnAttackExecute()
//     {
//         soldierController?.OnAttackImpact();
//     }
//
//     // Animation Event.
//     public void OnAttackEnd()
//     {
//         soldierController?.OnAttackEnd();
//     }
//
//     // Animation Event.
//     public void OnHitEnd()
//     {
//         soldierController?.OnHitEnd();
//     }
// }
