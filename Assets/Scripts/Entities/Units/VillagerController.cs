using UnityEngine;

public class VillagerController : UnitController
{
    private ResourceNode nodeTarget;
    private float gatherTimer;

    protected override void Start()
    {
        base.Start();
        gatherTimer = stats.gatherInterval;
    }

    protected override void Update()
    {
        base.Update();
    }

    protected override void HandleState()
    {
        switch (state)
        {
            case UnitState.Idle: break;
            case UnitState.Moving: HandleMovingState(); break;
            case UnitState.Attacking: HandleAttackingState(); break;
            case UnitState.Gathering: HandleGatheringState(); break;
        }
    }

    protected override void HandleMovingState()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (nodeTarget != null) state = UnitState.Gathering;
            else if (attackTarget != null) state = UnitState.Attacking;
            else state = UnitState.Idle;
        }
    }

    // Ticks gather timer and pulls resources from node
    void HandleGatheringState()
    {
        if (nodeTarget == null || !nodeTarget.HasResources())
        {
            state = UnitState.Idle;
            nodeTarget = null;
            return;
        }

        gatherTimer -= Time.deltaTime;
        if (gatherTimer <= 0f)
        {
            int harvested = nodeTarget.Harvest(stats.gatherAmount);
            GameManager.Instance.AddResources(nodeTarget.resourceNodeData.resourceType, harvested, stats.faction);
            gatherTimer = stats.gatherInterval;
        }
    }

    public void OrderGather(ResourceNode node)
    {
        if (node == null || stats.gatherAmount <= 0) return;
        nodeTarget = node;
        attackTarget = null;
        agent.stoppingDistance = stats.gatherRange;
        agent.SetDestination(nodeTarget.transform.position);
        state = UnitState.Moving;

        Vector3 dir = Calc.Dir(transform.position, node.transform.position);
        if (Calc.WithinRange(dir, stats.gatherRange))
            transform.rotation = SnapToYAngles(dir);
    }

    public override void OrderMove(Vector3 destination)
    {
        nodeTarget = null;
        base.OrderMove(destination);
    }

    public override void OrderStop()
    {
        nodeTarget = null;
        base.OrderStop();
    }
}