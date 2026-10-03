using UnityEngine;

/// -----------------------------------------------------------------------------
/// MountAnimator
/// -----------------------------------------------------------------------------
///
/// Presentation-only animation driver for a mount used by a SoldierController.
/// The mounted soldier remains the gameplay entity; this script only mirrors the
/// owning soldier's movement/action state onto the mount Animator.
///
/// Expected Animator parameters on AC_Mounts:
/// - bool    isWalking
/// - bool    isRunning
/// - bool    IsMoving
/// - float   MoveSpeed
/// - trigger Hit
/// - trigger Die
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class MountAnimator : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private SoldierController soldierController;
    [SerializeField] private SoldierMotor soldierMotor;

    #endregion

    #region Locomotion

    [Header("Locomotion")]
    [Tooltip("Horizontal velocity required before the mount is treated as moving.")]
    [Min(0f)]
    [SerializeField] private float locomotionVelocityDeadZone = 0.025f;

    [Tooltip("Keeps locomotion active briefly when movement momentarily drops to zero.")]
    [Min(0f)]
    [SerializeField] private float locomotionReleaseDelay = 0.04f;

    private bool locomotionMovingVisual = false;
    private float locomotionReleaseTimer = 0f;

    #endregion

    #region Runtime State

    private SoldierActionState previousActionState = SoldierActionState.None;

    #endregion

    #region Animator Hashes

    private static readonly int IsWalking = Animator.StringToHash("isWalking");
    private static readonly int IsRunning = Animator.StringToHash("isRunning");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int Hit = Animator.StringToHash("Hit");
    private static readonly int Die = Animator.StringToHash("Die");

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        ResolveReferences();

        if (soldierController != null)
            previousActionState = soldierController.ActionState;
    }

    void OnValidate()
    {
        locomotionVelocityDeadZone = Mathf.Max(0f, locomotionVelocityDeadZone);
        locomotionReleaseDelay = Mathf.Max(0f, locomotionReleaseDelay);
    }

    void Update()
    {
        UpdateLocomotion();
        UpdateActions();
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
            Debug.LogError($"{name}: MountAnimator could not find Animator.", this);

        if (soldierController == null)
            Debug.LogError($"{name}: MountAnimator could not find SoldierController in parent.", this);

        if (soldierMotor == null)
            Debug.LogError($"{name}: MountAnimator could not find SoldierMotor in parent.", this);
    }

    #endregion

    #region Locomotion

    void UpdateLocomotion()
    {
        if (animator == null)
            return;

        bool shouldMoveNow = ShouldUseLocomotion();

        if (shouldMoveNow)
        {
            locomotionMovingVisual = true;
            locomotionReleaseTimer = locomotionReleaseDelay;
        }
        else if (locomotionReleaseTimer > 0f)
        {
            locomotionReleaseTimer -= Time.deltaTime;
            locomotionMovingVisual = true;
        }
        else
        {
            locomotionMovingVisual = false;
        }

        bool shouldRun = locomotionMovingVisual && ShouldUseRunLocomotion();
        bool shouldWalk = locomotionMovingVisual && !shouldRun;

        // TODO: Comment out for BlendSpace Only
        // animator.SetBool(IsWalking, shouldWalk);
        // animator.SetBool(IsRunning, shouldRun);

        // Optional blend-tree parameters. Existing walking/running parameters remain
        // unchanged so current AC_Mounts setups continue to work as before.
        float moveSpeed01 = 0f;

        if (soldierMotor != null)
        {
            float currentSpeed = new Vector3(
                soldierMotor.Velocity.x,
                0f,
                soldierMotor.Velocity.z
            ).magnitude;

            float maxSpeed = Mathf.Max(0.01f, soldierMotor.BaseMoveSpeed);

            moveSpeed01 = Mathf.Clamp01(currentSpeed / maxSpeed);
        }

        animator.SetFloat(MoveSpeed, moveSpeed01);

        animator.SetBool(IsMoving, locomotionMovingVisual);
        
        //Debug.Log(moveSpeed01);
    }

    bool ShouldUseLocomotion()
    {
        if (soldierController == null || soldierMotor == null)
            return false;

        if (!soldierController.IsAlive || soldierController.IsMovementLocked)
            return false;

        Vector3 velocity = soldierMotor.Velocity;
        velocity.y = 0f;

        return velocity.sqrMagnitude >
               locomotionVelocityDeadZone * locomotionVelocityDeadZone;
    }

    bool ShouldUseRunLocomotion()
    {
        if (soldierController == null || soldierController.Squad == null)
            return false;

        switch (soldierController.Squad.State)
        {
            case SquadState.Charging:
            case SquadState.Routing:
                return true;

            default:
                return false;
        }
    }

    #endregion

    #region Actions

    void UpdateActions()
    {
        if (animator == null || soldierController == null)
            return;

        SoldierActionState currentActionState = soldierController.ActionState;

        if (currentActionState == previousActionState)
            return;

        if (currentActionState == SoldierActionState.HitReact)
            animator.SetTrigger(Hit);
        else if (currentActionState == SoldierActionState.Death)
        {
            animator.SetBool(IsWalking, false);
            animator.SetBool(IsRunning, false);
            animator.SetBool(IsMoving, false);
            animator.SetFloat(MoveSpeed, 0f);
            animator.SetTrigger(Die);
        }

        previousActionState = currentActionState;
    }

    #endregion
}
