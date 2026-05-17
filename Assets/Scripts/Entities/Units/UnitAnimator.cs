using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(UnitController))]
[RequireComponent(typeof(NavMeshAgent))]

public class UnitAnimator : MonoBehaviour
{
    private Animator animator;
    private UnitController unitController;
    private NavMeshAgent agent;
    
    // Animator parameter hashes - faster than string lookup
    private static readonly int IsMoving = Animator.StringToHash("isMoving");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Death = Animator.StringToHash("Death");
    
    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        unitController = GetComponent<UnitController>();
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) Debug.LogError("No animator attached to " + gameObject.name);
    }
    
    void Update()
    {
        UpdateMovement();
    }

    void UpdateMovement()
    {
        bool isMoving = agent.velocity.magnitude > 0.1f;
        animator.SetBool(IsMoving, isMoving);
    }
    
    public void TriggerAttack()
    {
        animator.SetTrigger(Attack);
    }

    public void TriggerDeath()
    {
        animator.SetTrigger(Death);
    }
    
    
    // Animator events - relayed from AnimationEventRelay script
    public void OnAttackImpact()
    {
        unitController.DealAttackDamage();
    }

    public void OnAttackEnd()
    {
        unitController.OnAttackEnd();
    }
    
}
