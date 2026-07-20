using UnityEngine;

/// -----------------------------------------------------------------------------
/// SoldierCombat
/// -----------------------------------------------------------------------------
///
/// Minimal soldier combat event bridge for FormationCombat.
///
/// The old old melee local brain has been removed. SquadCombat now
/// owns the current formation combat loop. This component remains because
/// SoldierController and SoldierAnimator route animation events through it.
///
/// Design role:
/// Keep animation-event plumbing local to the soldier while delegating actual
/// formation attack resolution back to the owning SquadCombat.
///
[DisallowMultipleComponent]
public class SoldierCombat : MonoBehaviour
{
    private SoldierController soldier;

    public SoldierController CurrentTarget => soldier != null ? soldier.CombatTarget : null;
    public bool HasTarget => CurrentTarget != null && CurrentTarget.IsAlive;
    public bool IsAttacking => soldier != null && soldier.ActionState == SoldierActionState.Attack;
    public bool IsHitReacting => soldier != null && soldier.ActionState == SoldierActionState.HitReact;

    void Awake()
    {
        soldier = GetComponent<SoldierController>();
    }

    /// Initializes this event bridge from SoldierController.
    public void Initialize(SoldierController owner)
    {
        soldier = owner;
    }

    // /// Kept as a compatibility no-op so existing SoldierController.SetSquad code
    // /// does not need profile wiring.
    // public void ApplyProfileFromSquad()
    // {
    //     // FormationCombat no longer uses SoldierCombatProfile.
    // }

    /// Clears local event-bridge state.
    public void ClearCombat()
    {
        if (soldier == null)
            return;

        soldier.ClearCombatTarget();
    }

    /// Animation event hook for melee impact.
    public void ResolveAttackImpact()
    {
        if (soldier == null || soldier.Squad == null || soldier.Squad.Combat == null)
            return;

        soldier.Squad.Combat.ResolveSoldierAttackImpact(soldier);
    }

    /// Animation event hook for ranged projectile release.
    public void ResolveProjectileRelease()
    {
        if (soldier == null || soldier.Squad == null || soldier.Squad.Combat == null)
            return;

        soldier.Squad.Combat.ResolveSoldierProjectileRelease(soldier);
    }

    /// Called by SoldierController when a committed action completes.
    public void HandleActionCompleted(SoldierActionState completedAction)
    {
        if (soldier == null || soldier.Squad == null || soldier.Squad.Combat == null)
            return;

        soldier.Squad.Combat.HandleSoldierActionCompleted(
            soldier,
            completedAction);
    }

    /// Called by SoldierController when a committed action is interrupted.
    public void HandleActionInterrupted(
        SoldierActionState interruptedAction,
        SoldierActionState newAction)
    {
        if (soldier == null || soldier.Squad == null || soldier.Squad.Combat == null)
            return;

        soldier.Squad.Combat.HandleSoldierActionInterrupted(
            soldier,
            interruptedAction,
            newAction);
    }

    // /// Backward-compatible hook for older animation-event wiring.
    // public void NotifyAttackAnimationEnded()
    // {
    //     soldier?.CompleteAction(SoldierActionState.Attack);
    // }
    //
    // /// Compatibility no-op. Old local combat randomized an internal attack timer.
    // public void RandomizeInitialAttackTimer(float maxDelay)
    // {
    //     // FormationCombat attack timers live in SquadCombat.
    // }
}
