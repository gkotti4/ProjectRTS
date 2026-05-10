using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(EntityStats))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]

public class UnitController : MonoBehaviour, ISelectable, IDamageable
{
    public virtual void OnSelect() { isSelected = true; if (stats.selectionDecal != null) stats.selectionDecal.enabled = true; }
    public virtual void OnDeselect() { isSelected = false; if (stats.selectionDecal != null) stats.selectionDecal.enabled = false; }
    public GameObject GetGameObject() => gameObject;
    public bool IsBoxSelectable => true;
    public void TakeDamage(int damage) { health.TakeDamage(damage); }

    protected EntityStats stats;
    public EntityStats Stats => stats;
    protected Health health;
    protected UnitState state = UnitState.Idle;

    protected Camera mainCamera;
    protected NavMeshAgent agent;
    protected bool isSelected = false;
    protected GameObject attackTarget;
    protected float attackTimer;

    protected virtual void Awake()
    {
        stats = GetComponent<EntityStats>();
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
    }

    protected virtual void Start()
    {
        // EntityStats.Start() runs before this and initializes stats
        health.Initialize(stats.maxHealth);

        agent.speed = stats.moveSpeed;
        agent.baseOffset = 1f;

        attackTimer = stats.attackInterval;

        if (stats.selectionDecal != null)
            stats.selectionDecal.enabled = false;

        mainCamera = Camera.main;
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
        }
    }

    protected virtual void HandleMovingState()
    {
        if (attackTarget != null)
        {
            float distance = Vector3.Distance(transform.position, attackTarget.transform.position);
            if (distance <= stats.attackRange)
                state = UnitState.Attacking;
        }
        else if (!agent.pathPending && agent.remainingDistance < 0.1f)
        {
            state = UnitState.Idle;
        }
    }

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

    public virtual void SetMoveTarget() { }
}