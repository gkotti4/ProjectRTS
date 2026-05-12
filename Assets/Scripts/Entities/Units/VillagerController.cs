using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]

public class VillagerController : UnitController
{
    private int gatherAmount;
    private float gatherRange;
    private float gatherInterval;
    private float gatherTimer;
    private ResourceNode targetNode;


    protected override void Awake()
    {
        base.Awake();
        gatherAmount = stats.gatherAmount;
        gatherRange = stats.gatherRange;
        gatherInterval = stats.gatherInterval;
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
        switch (state)
        {
            case UnitState.Idle:
                break;
            case UnitState.Moving:
                HandleMovingState();
                break;
            case UnitState.Gathering:
                HandleGatheringState();
                break;
            case UnitState.Attacking:
                HandleAttackingState();
                break;
        }
    }

    protected override void HandleMovingState()
    {
        /*if (targetNode != null) // Handle Resource Node Targeting 
        {
            float distance = Vector3.Distance(transform.position, targetNode.transform.position);
            if (distance <= gatherRange)
            {
                state = UnitState.Gathering;
            }
        }
        else if (attackTarget is not null)
        {
            float distance = Vector3.Distance(transform.position, attackTarget.transform.position);
            if (distance <= attackRange)
                state = UnitState.Attacking;
        }
        else if (!agent.pathPending && agent.remainingDistance < 0.1f) // Reached target destination
        {
            state = UnitState.Idle;
        }*/
        
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (targetNode != null)
                state = UnitState.Gathering;
            else if (attackTarget != null)
                state = UnitState.Attacking;
            else
                state = UnitState.Idle;
        }
    }

    protected override void HandleAttackingState()
    {
        base.HandleAttackingState();
    }
    
    
    protected void HandleGatheringState()
    {
        if (targetNode == null || !targetNode.HasResources())
        {
            state = UnitState.Idle;
            targetNode = null;
            return;
        }

        gatherTimer -= Time.deltaTime;
        if (gatherTimer <= 0f)
        {
            int harvested = targetNode.Harvest(gatherAmount);
            GameManager.Instance.AddResources(targetNode.resourceNodeData.resourceType, harvested);
            gatherTimer = gatherInterval;
        }
    }
    
    // public override void SetMoveTarget()
    // {
    //     if (!isSelected) return;
    //     
    //     Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
    //     Physics.Raycast(ray, out RaycastHit hit);
    //     
    //     if (hit.collider != null && hit.collider.TryGetComponent(out ResourceNode node))
    //     {
    //         // Clicked a resource node — assign and move toward it
    //         targetNode = node;
    //         agent.stoppingDistance = gatherRange;
    //         agent.SetDestination(targetNode.transform.position);
    //         state = UnitState.Moving;
    //
    //         attackTarget = null;
    //     }
    //     else if (hit.collider != null && hit.collider.CompareTag("Enemy"))
    //     {
    //         attackTarget = hit.collider.gameObject;
    //         agent.stoppingDistance = stats.attackRange;
    //         agent.SetDestination(attackTarget.transform.position);
    //         state = UnitState.Moving;
    //
    //         targetNode = null;
    //     }
    //     else
    //     {
    //         // Clicked ground — normal move, clear harvest target
    //         targetNode = null;
    //         attackTarget = null;
    //         agent.stoppingDistance = 0.1f;
    //         agent.SetDestination(hit.point);
    //         state = UnitState.Moving;
    //     }
    // }

}
