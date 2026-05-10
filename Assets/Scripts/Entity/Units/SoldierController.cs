using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]

public class SoldierController : UnitController
{
    
    protected override void Awake()
    {
        base.Awake();
    }
    
    protected override void Start()
    {
        base.Start();
    }

    protected override void Update()
    {
        base.Update();
    }

    protected override void HandleState()
    {
        base.HandleState();
    }
    

    protected override void HandleMovingState()
    {
        base.HandleMovingState();
    }

    protected override void HandleAttackingState()
    {
        base.HandleAttackingState();
    }
    
    
    public override void SetMoveTarget()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Physics.Raycast(ray, out RaycastHit hit);

        if (hit.collider == null) return;

        if (hit.collider.CompareTag("Enemy"))
        {
            attackTarget = hit.collider.gameObject;
            agent.stoppingDistance = stats.attackDamage;
            agent.SetDestination(attackTarget.transform.position);
            state = UnitState.Moving;
        }
        else
        {
            attackTarget = null;
            agent.stoppingDistance = 0.1f;
            agent.SetDestination(hit.point); 
            state = UnitState.Moving;
        }
    }
}

