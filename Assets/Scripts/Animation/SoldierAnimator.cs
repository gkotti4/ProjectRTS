using UnityEngine;

/// -----------------------------------------------------------------------------
/// SoldierAnimator
/// -----------------------------------------------------------------------------
///
/// Adapter between soldier gameplay state and the Unity Animator.
/// Updates locomotion parameters, combat animation state, plays committed action
/// triggers, manages upper-body layer weight for full-body actions, and forwards
/// animation events such as AttackImpact, AttackEnd, and HitEnd back to
/// SoldierController.
///
/// This class should not decide combat results, target selection, damage, or
/// movement rules. It visualizes gameplay state and reports animation timing.
///
/// Design role:
/// Soldier animation presentation and animation-event bridge.
///
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class SoldierAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private SoldierController soldierController;
    [SerializeField] private SoldierMotor soldierMotor;

    [Header("Movement Parameters")]
    private float movementDeadZone = 0.08f;
    private float moveBlendDampTime = 0.12f;

    [Header("Combat State")]
    private bool useCombatStateFromSquad = true;

    [Header("Animator Layers")]
    private string upperBodyLayerName = "UpperBody";
    private bool controlUpperBodyLayer = true;
    private float upperBodyLayerDefaultWeight = 1f;
    private float upperBodyLayerDisabledWeight = 0f;

    private int upperBodyLayerIndex = -1;

    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveZ = Animator.StringToHash("MoveZ");
    private static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int InCombat = Animator.StringToHash("InCombat");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int HitReact = Animator.StringToHash("HitReact");
    private static readonly int Death = Animator.StringToHash("Death");

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (soldierController == null)
            soldierController = GetComponentInParent<SoldierController>();

        if (soldierMotor == null)
            soldierMotor = GetComponentInParent<SoldierMotor>();

        if (animator == null)
            Debug.LogError($"{name}: SoldierAnimator could not find Animator.");

        if (soldierController == null)
            Debug.LogError($"{name}: SoldierAnimator could not find SoldierController in parent.");

        if (soldierMotor == null)
            Debug.LogError($"{name}: SoldierAnimator could not find SoldierMotor in parent.");

        InitializeUpperBodyLayer();
    }

    void Update()
    {
        UpdateMovementParameters();
        UpdateCombatParameter();
    }

    void InitializeUpperBodyLayer()
    {
        if (animator == null || !controlUpperBodyLayer)
            return;

        upperBodyLayerIndex = animator.GetLayerIndex(upperBodyLayerName);

        if (upperBodyLayerIndex >= 0)
            animator.SetLayerWeight(upperBodyLayerIndex, upperBodyLayerDefaultWeight);
        else
            Debug.LogWarning($"{name}: Upper body layer '{upperBodyLayerName}' was not found.");
    }

    void UpdateMovementParameters()
    {
        if (animator == null)
            return;

        // TODO
        bool isMoving = false;
        // float moveX = 0f;
        // float moveZ = 0f;
        // float normalizedMoveSpeed = 0f;

        if (!ShouldLockLocomotionParameters() && soldierMotor != null)
        {
            isMoving = soldierController.Squad.State == SquadState.Moving; // TODO CHECK - DEBUG
        }

        
        animator.SetBool(IsMoving, isMoving);

        // animator.SetFloat(
        //     MoveSpeed,
        //     normalizedMoveSpeed,
        //     moveBlendDampTime,
        //     Time.deltaTime);
        //
        // animator.SetFloat(
        //     MoveX,
        //     moveX,
        //     moveBlendDampTime,
        //     Time.deltaTime);
        //
        // animator.SetFloat(
        //     MoveZ,
        //     moveZ,
        //     moveBlendDampTime,
        //     Time.deltaTime);
    }

    void UpdateCombatParameter()
    {
        if (animator == null)
            return;

        animator.SetBool(InCombat, IsInCombatAnimationState());
    }

    bool ShouldLockLocomotionParameters()
    {
        return soldierController != null && soldierController.IsMovementLocked;
    }

    bool IsInCombatAnimationState()
    {
        if (!useCombatStateFromSquad)
            return false;

        if (soldierController == null || soldierController.Squad == null)
            return false;

        return soldierController.Squad.State == SquadState.InCombat ||
               soldierController.Squad.State == SquadState.Charging;
    }

    public void PlayAction(SoldierActionState actionState)
    {
        if (animator == null)
            return;

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
        }
    }

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
            enabled ? upperBodyLayerDefaultWeight : upperBodyLayerDisabledWeight);
    }

    public void DisableUpperBodyLayer()
    {
        SetUpperBodyLayerEnabled(false);
    }

    public void EnableUpperBodyLayer()
    {
        SetUpperBodyLayerEnabled(true);
    }

    // Backward-compatible wrappers while other code is migrated.
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

    // Animation Event.
    public void OnAttackImpact()
    {
        soldierController?.OnAttackImpact();
    }

    // Animation Event alias. Useful for ranged clips where the event is placed
    // on the bow/crossbow release frame instead of a melee impact frame.
    public void OnProjectileRelease()
    {
        soldierController?.OnAttackImpact();
    }

    // Animation Event alias. Useful if a clip wants a weapon-agnostic event name.
    public void OnAttackExecute()
    {
        soldierController?.OnAttackImpact();
    }

    // Animation Event.
    public void OnAttackEnd()
    {
        soldierController?.OnAttackEnd();
    }

    // Animation Event.
    public void OnHitEnd()
    {
        soldierController?.OnHitEnd();
    }
}
