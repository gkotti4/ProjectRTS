using TMPro.SpriteAssetUtilities;
using UnityEngine;

public enum ControlState { PlayerControlled, AIControlled, Locked } // do we want Locked as state or bool

public class MilitaryController : UnitController
{
    // Stances
    protected UnitStance stance = UnitStance.Aggressive;

    // Scanning
    private float scanInterval = 0.5f;
    private float scanTimer = 0f;
    private Collider[] scanBuffer = new Collider[20];

    protected override void Update()
    {
        HandleState();
        UpdateRotation();
        
    }
    
    protected override void HandleState()
    {
        if (attackTimer > 0f) attackTimer -= Time.deltaTime;

        if (stance == UnitStance.NoAttack && attackTarget != null) // check
        {
            attackTarget = null;
            controlState = ControlState.AIControlled;
            SetUnitState(UnitState.Idle);
            agent.ResetPath();
            return;
        }

        // Control State
        if (controlState == ControlState.Locked) return;
        
        if (controlState == ControlState.PlayerControlled)
        {
            HandlePlayerControlled();
            return;
        }

        HandleAIControlled();
    }

    void HandlePlayerControlled()
    {
        switch (state)
        {
            case UnitState.Moving:
                if (agent.pathPending) return;
                if (agent.remainingDistance <= agent.stoppingDistance)
                {
                    // Order complete — hand back to AI
                    if (attackTarget != null)
                        SetUnitState(UnitState.Attacking);
                    else
                    {
                        SetUnitState(UnitState.Idle);
                        controlState = ControlState.AIControlled;
                    }
                }

                break;

            case UnitState.Attacking:
                if (attackTarget == null || !attackTarget.Stats.IsAlive)
                {
                    attackTarget = null;
                    SetUnitState(UnitState.Idle);
                    controlState = ControlState.AIControlled;
                    return;
                }

                ExecuteAttack();
                break;
        }
    }

    void HandleAIControlled()
    {
        switch (state)
        {
            case UnitState.Idle:
                // Scan for enemies
                scanTimer -= Time.deltaTime;
                if (scanTimer <= 0f)
                {
                    scanTimer = scanInterval;
                    EntityController target = FindTarget();
                    if (target != null)
                    {
                        attackTarget = target;
                        SetUnitState(UnitState.Moving);
                        agent.stoppingDistance = stats.attackRange;
                        agent.SetDestination(attackTarget.transform.position);
                    }
                }

                break;

            case UnitState.Moving:
                if (attackTarget == null || !attackTarget.Stats.IsAlive)
                {
                    attackTarget = null;
                    SetUnitState(UnitState.Idle);
                    return;
                }
                
                // Defensive - stop chasing if too far from home - seems to be working fine
                if (stance == UnitStance.Defensive && IsTooFarFromHome())
                {
                    attackTarget = null;
                    SetUnitState(UnitState.Returning);
                    agent.SetDestination(homePosition);
                    return;
                }

                agent.SetDestination(attackTarget.transform.position);
                if (IsInAttackRange())
                    SetUnitState(UnitState.Attacking);
                break;

            case UnitState.Attacking:
                if (attackTarget == null || !attackTarget.Stats.IsAlive)
                {
                    attackTarget = FindTarget(); // Target Switching - immediately look for next target
                    if (attackTarget == null)
                        SetUnitState(UnitState.Idle);
                    return;
                }

                if (!IsInAttackRange())
                {
                    if (stance == UnitStance.StandGround)
                    {
                        attackTarget = null;
                        SetUnitState(UnitState.Idle);
                        return;
                    }

                    SetUnitState(UnitState.Moving);
                    return;
                }

                ExecuteAttack();
                break;

            case UnitState.Returning:
                if (IsNearHome())
                {
                    SetUnitState(UnitState.Idle);
                    return;
                }

                // Aggressive can break return to attack
                if (stance == UnitStance.Aggressive)
                {
                    EntityController target = FindTarget();
                    if (target != null)
                    {
                        attackTarget = target;
                        SetUnitState(UnitState.Moving);
                        agent.SetDestination(attackTarget.transform.position);
                    }
                }

                break;
        }

    }

    void ExecuteAttack()
    {
        agent.ResetPath();
        if (attackTimer <= 0f)
            TriggerAttack();
    }
    
    EntityController FindTarget()
    {
        if (stance == UnitStance.NoAttack) return null;
    
        float scanRange = Stats.lineOfSight;
    
        // StandGround only targets enemies already in attack range
        if (stance == UnitStance.StandGround)
            scanRange = Stats.attackRange + 1f;
    
        int count = Physics.OverlapSphereNonAlloc(transform.position, scanRange, scanBuffer);
    
        EntityController nearest = null;
        float nearestDist = float.MaxValue;
    
        for (int i = 0; i < count; i++)
        {
            if (!scanBuffer[i].TryGetComponent(out EntityController entity)) continue;
            if (!entity.Stats.IsEnemy(Stats)) continue;
    
            float dist = (entity.transform.position - transform.position).sqrMagnitude;
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = entity;
            }
        }
        return nearest;
    }
    
    bool IsTooFarFromHome()
    {
        return !Calc.WithinRange(transform.position, homePosition, Stats.defensiveChaseRange);
    }
    
    bool IsInAttackRange()
    {
        if (attackTarget && Calc.WithinRange(attackTarget.transform.position, transform.position, Stats.attackRange))
            return true;
        return false;
    }

    bool IsNearHome(float range=2f)
    {
        return Calc.WithinRange(transform.position, homePosition, range);
    }
    
    
    #region Player Orders
    public override void OrderAttack(EntityController target)
    {
        if (target == null) return;
        attackTarget = target;
        controlState = ControlState.PlayerControlled;
        SetUnitState(UnitState.Moving);
        agent.stoppingDistance = stats.attackRange;
        agent.SetDestination(target.transform.position);
        homePosition = transform.position;
    }

    public override void OrderMove(Vector3 destination)
    {
        attackTarget = null;
        controlState = ControlState.PlayerControlled;
        SetUnitState(UnitState.Moving);
        homePosition = destination;
        agent.stoppingDistance = 0.1f;
        agent.SetDestination(destination);
    }

    public override void OrderStop()
    {
        attackTarget = null;
        controlState = ControlState.AIControlled;
        SetUnitState(UnitState.Idle);
        agent.ResetPath();
    }

    public void OrderSetStance(UnitStance newStance)
    {
        stance = newStance;
        homePosition = transform.position; // eh
    }
    #endregion
    
    
    
}





