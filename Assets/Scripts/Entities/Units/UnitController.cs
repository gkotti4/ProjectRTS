using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(EntityStats))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]

public class UnitController : MonoBehaviour, ISelectable, IDamageable // CHECK, MERGED VIL AND SOLDIER LATE NIGHT
{
    [SerializeField] private DecalProjector selectionDecal;

    // Components
    protected EntityStats stats;
    public EntityStats Stats => stats;
    protected Health health;
    protected NavMeshAgent agent;
    public NavMeshAgent Agent => agent;
    protected Camera mainCamera;
    protected Rigidbody rb;
    
    protected UnitAnimator unitAnimator;

    // State
    protected UnitState state = UnitState.Idle;
    public UnitState State => state;
    protected bool isSelected = false;

    // Combat
    protected GameObject attackTarget;
    public GameObject AttackTarget => attackTarget;
    protected float attackTimer;

    // Gathering
    protected ResourceNode nodeTarget;
    protected float gatherTimer;

    
    // ISelectable
    public bool IsBoxSelectable => true;
    public GameObject GetGameObject() => gameObject;

    public virtual void OnSelect()
    {
        isSelected = true;
        if (selectionDecal != null)
            selectionDecal.enabled = true;
    }

    public virtual void OnDeselect()
    {
        isSelected = false;
        if (selectionDecal != null)
            selectionDecal.enabled = false;
    }

    // IDamageable
    public void TakeDamage(int damage) { health.TakeDamage(damage); }
    
    
    protected virtual void Awake()
    {
        stats = GetComponent<EntityStats>();
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        agent.angularSpeed = 99999f;
        agent.acceleration = 99999f; // 100f
        agent.autoBraking = false;
        agent.updateRotation = false;
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        unitAnimator = GetComponentInChildren<UnitAnimator>();
        if(unitAnimator == null) Debug.LogWarning("No unit animator attached to " + gameObject.name);
    }

    protected virtual void Start()
    {
        // EntityStats.Start() initializes stats before this runs
        health.Initialize(stats.maxHealth);

        agent.speed = stats.moveSpeed;

        attackTimer = stats.attackInterval;
        gatherTimer = stats.gatherInterval;

        mainCamera = Camera.main;

        if (selectionDecal != null)
            selectionDecal.enabled = false;

        SelectionManager.Instance.Register(this);
        
        // Randomly enable pouch/accessory on character prefab
    }

    protected virtual void Update()
    {
        HandleState();
        HandleRotation();
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
            if (nodeTarget != null)
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

        // Check if out of range (also check agent settings - stop distance is set to attack range too)
        Vector3 toTarget = attackTarget.transform.position - transform.position; 
        float sqrDistance = toTarget.sqrMagnitude; // Vector3.Distance() ray - switching saves performance (save more by locking by number of frames %)
        float sqrAttackRange = stats.attackRange * stats.attackRange;
        if (sqrDistance > sqrAttackRange)
        {
            agent.SetDestination(attackTarget.transform.position);
            state = UnitState.Moving;
            return;
        }

        // Attack the Target
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            unitAnimator.TriggerAttack(); // Damage triggered here from anim event
            attackTimer = stats.attackInterval;
        }

        // Rotate to current attack target - new check
        toTarget.y = 0f;
        if (toTarget != Vector3.zero)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, 
                Quaternion.LookRotation(toTarget), 
                360f * Time.deltaTime);
        }

    }

    // Ticks gather timer and pulls resources from node
    protected virtual void HandleGatheringState()
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
            GameManager.Instance.AddResources(nodeTarget.resourceNodeData.resourceType, harvested);
            gatherTimer = stats.gatherInterval;
        }
    }

    // Routes right click command based on what was clicked
    public virtual void MoveToTarget(RaycastHit hit)
    {
        if (hit.collider == null) return;

        if (hit.collider.TryGetComponent(out ResourceNode node) && stats.gatherAmount > 0)
        {
            // Clicked resource node and this unit can gather
            nodeTarget = node;
            attackTarget = null;
            agent.stoppingDistance = stats.gatherRange;
            agent.SetDestination(nodeTarget.transform.position);
            state = UnitState.Moving;
            
            
            Vector3 toPos = nodeTarget.transform.position;
            Vector3 pos = transform.position;
            float sqrDist = (toPos - pos).sqrMagnitude;
            if (sqrDist < stats.gatherRange * stats.gatherRange) // If inside range snap to target
            {
                Vector3 dir = (toPos - pos).normalized;
                transform.rotation = SnapToXDirections(dir);
            }
        }
        else if (hit.collider.CompareTag("Enemy"))
        {
            // Clicked enemy
            attackTarget = hit.collider.gameObject;
            nodeTarget = null;
            agent.stoppingDistance = stats.attackRange;
            agent.SetDestination(attackTarget.transform.position);
            state = UnitState.Moving;
            
            Vector3 toPos = attackTarget.transform.position;
            Vector3 pos = transform.position;
            float sqrDist = (toPos - pos).sqrMagnitude;
            if (sqrDist < stats.gatherRange * stats.gatherRange) // If inside range snap to target
            {
                Vector3 dir = (toPos - pos).normalized;
                transform.rotation = SnapToXDirections(dir);
            }
        }
        else
        {
            // Clicked ground — normal move
            // targetNode = null;
            // attackTarget = null;
            // agent.stoppingDistance = 0.1f;
            // agent.SetDestination(hit.point);
            // state = UnitState.Moving;
            MoveTo(hit.point);
        }
    }

    // Public move command for group movement from PlayerInputHandler
    public void MoveTo(Vector3 destination)
    {
        attackTarget = null;
        nodeTarget = null;
        agent.stoppingDistance = 0.1f;
        agent.SetDestination(destination);
        state = UnitState.Moving;
    }
    
    
    void HandleRotation()
    {
        switch (state)
        {
            case UnitState.Moving:
                RotateTowardVelocity();
                break;
            case UnitState.Attacking:
                RotateToward(attackTarget?.transform.position);
                break;
            case UnitState.Gathering:
                RotateTowardIfNeeded(nodeTarget?.transform.position);
                break;
            case UnitState.Idle:
                break;
        }
    }

// Smoothly rotates toward movement direction snapped to 8 dirs - note: maybe just use Snap
    void RotateTowardVelocity()
    {
        if (agent.velocity.sqrMagnitude < 0.1f) return;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            SnapToXDirections(agent.velocity),
            900f * Time.deltaTime);
    }

// Instantly snaps to face a position
    void RotateToward(Vector3? targetPos)
    {
        if (targetPos == null) return;
        Vector3 dir = targetPos.Value - transform.position;
        dir.y = 0f;
        if (dir == Vector3.zero) return;
        transform.rotation = SnapToXDirections(dir);
    }

// Only rotates if significantly misaligned — snap once, stay put
    void RotateTowardIfNeeded(Vector3? targetPos, float threshold = 22.5f)
    {
        if (targetPos == null) return;
        Vector3 dir = targetPos.Value - transform.position;
        dir.y = 0f;
        if (dir == Vector3.zero) return;
        if (Vector3.Angle(transform.forward, dir) > threshold)
            transform.rotation = SnapToXDirections(dir);
    }

// Snaps direction to nearest of X angles
    Quaternion SnapToXDirections(Vector3 direction)
    {
        float angleDiv = 22.5f;
        direction.y = 0f;
        if (direction == Vector3.zero) return transform.rotation;
        float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float snapped = Mathf.Round(angle / angleDiv) * angleDiv;
        return Quaternion.Euler(0f, snapped, 0f);
    }
    
    
}