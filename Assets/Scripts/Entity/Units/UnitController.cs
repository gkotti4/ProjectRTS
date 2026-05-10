using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(EntityStats))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
public class UnitController : MonoBehaviour, ISelectable, IDamageable // CHECK, MERGED VIL AND SOLDIER LATE NIGHT
{
    // ISelectable
    public bool IsBoxSelectable => true;
    public GameObject GetGameObject() => gameObject;

    public virtual void OnSelect()
    {
        isSelected = true;
        if (stats.selectionDecal != null)
            stats.selectionDecal.enabled = true;
    }

    public virtual void OnDeselect()
    {
        isSelected = false;
        if (stats.selectionDecal != null)
            stats.selectionDecal.enabled = false;
    }

    // IDamageable
    public void TakeDamage(int damage) { health.TakeDamage(damage); }

    // Components
    protected EntityStats stats;
    public EntityStats Stats => stats;
    protected Health health;
    protected NavMeshAgent agent;
    protected Camera mainCamera;

    // State
    protected UnitState state = UnitState.Idle;
    protected bool isSelected = false;

    // Combat
    protected GameObject attackTarget;
    protected float attackTimer;

    // Gathering
    protected ResourceNode targetNode;
    protected float gatherTimer;

    protected virtual void Awake()
    {
        stats = GetComponent<EntityStats>();
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
    }

    protected virtual void Start()
    {
        // EntityStats.Start() initializes stats before this runs
        health.Initialize(stats.maxHealth);

        agent.speed = stats.moveSpeed;
        agent.baseOffset = 1f;

        attackTimer = stats.attackInterval;
        gatherTimer = stats.gatherInterval;

        mainCamera = Camera.main;

        if (stats.selectionDecal != null)
            stats.selectionDecal.enabled = false;

        SelectionManager.Instance.Register(this);
    }

    protected virtual void Update()
    {
        HandleState();
    }

    protected virtual void OnDestroy()
    {
        SelectionManager.Instance.Unregister(this);
    }

    // Routes to correct state handler each frame
    protected virtual void HandleState()
    {
        switch (state)
        {
            case UnitState.Idle:
                break;
            case UnitState.Moving:
                HandleMovingState();
                break;
            case UnitState.Attacking:
                HandleAttackingState();
                break;
            case UnitState.Gathering:
                HandleGatheringState();
                break;
        }
    }

    // Checks NavMesh arrival and transitions to next state
    protected virtual void HandleMovingState()
    {
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

    // Handles combat — chases, attacks, cleans up dead targets
    protected virtual void HandleAttackingState()
    {
        if (attackTarget == null)
        {
            state = UnitState.Idle;
            return;
        }

        if (!attackTarget.TryGetComponent(out IDamageable damageable))
        {
            attackTarget = null;
            state = UnitState.Idle;
            return;
        }

        float distance = Vector3.Distance(transform.position, attackTarget.transform.position);
        if (distance > stats.attackRange)
        {
            agent.SetDestination(attackTarget.transform.position);
            state = UnitState.Moving;
            return;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            damageable.TakeDamage(stats.attackDamage);
            attackTimer = stats.attackInterval;
        }
    }

    // Ticks gather timer and pulls resources from node
    protected virtual void HandleGatheringState()
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
            int harvested = targetNode.Harvest(stats.gatherAmount);
            GameManager.Instance.AddResources(targetNode.resourceNodeData.resourceType, harvested);
            gatherTimer = stats.gatherInterval;
        }
    }

    // Routes right click command based on what was clicked
    public virtual void SetMoveTarget(RaycastHit hit)
    {
        if (hit.collider == null) return;

        if (hit.collider.TryGetComponent(out ResourceNode node) && stats.gatherAmount > 0)
        {
            // Clicked resource node and this unit can gather
            targetNode = node;
            attackTarget = null;
            agent.stoppingDistance = stats.gatherRange;
            agent.SetDestination(targetNode.transform.position);
            state = UnitState.Moving;
        }
        else if (hit.collider.CompareTag("Enemy"))
        {
            // Clicked enemy
            attackTarget = hit.collider.gameObject;
            targetNode = null;
            agent.stoppingDistance = stats.attackRange;
            agent.SetDestination(attackTarget.transform.position);
            state = UnitState.Moving;
        }
        else
        {
            // Clicked ground — normal move
            targetNode = null;
            attackTarget = null;
            agent.stoppingDistance = 0.1f;
            agent.SetDestination(hit.point);
            state = UnitState.Moving;
        }
    }

    // Public move command for group movement from PlayerInputHandler
    public void MoveTo(Vector3 destination)
    {
        attackTarget = null;
        targetNode = null;
        agent.stoppingDistance = 0.1f;
        agent.SetDestination(destination);
        state = UnitState.Moving;
    }
}