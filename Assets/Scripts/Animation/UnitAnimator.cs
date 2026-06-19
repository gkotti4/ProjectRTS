using UnityEngine;
using UnityEngine.AI;

public class UnitAnimator : MonoBehaviour
{
    private Animator animator;
    private NavMeshAgent agent;

    private SoldierController soldierController;

    private static readonly int IsMoving = Animator.StringToHash("isMoving");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Death = Animator.StringToHash("Death");

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponentInParent<NavMeshAgent>();

        soldierController = GetComponentInParent<SoldierController>();

        if (animator == null)
            Debug.LogError("No animator attached to " + gameObject.name);
    }

    void Update()
    {
        UpdateMovement();
    }

    void UpdateMovement()
    {
        if (animator == null || agent == null)
            return;

        animator.SetBool(IsMoving, agent.velocity.sqrMagnitude > 0.01f);
    }

    public void TriggerAttack()
    {
        animator?.SetTrigger(Attack);
    }

    public void TriggerDeath()
    {
        animator?.SetTrigger(Death);
    }

    public void OnAttackImpact()
    {
        soldierController?.OnAttackImpact();
    }

    public void OnAttackEnd()
    {
        soldierController?.OnAttackEnd();
    }
}