using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Health), typeof(NavMeshAgent))]

public class UnitController : MonoBehaviour, ISelectable, IDamageable
{
    public virtual void OnSelect() { isSelected = true; if (selectionDecal) selectionDecal.enabled = true; }
    public virtual void OnDeselect() { isSelected = false; if (selectionDecal) selectionDecal.enabled = false; }
    public GameObject GetGameObject() => gameObject;
    public bool IsBoxSelectable => true;
    
    public void TakeDamage(int damage){ health.TakeDamage(damage); }


    public UnitSO unitData;
    [SerializeField] protected DecalProjector selectionDecal;

    protected Health health;
    protected UnitState state = UnitState.Idle;
    protected float attackRange;
    protected float attackInterval;
    protected int attackDamage;

    protected Camera mainCamera;
    protected NavMeshAgent agent;
    protected bool isSelected;
    protected GameObject attackTarget;
    protected float attackTimer;
    
    protected virtual void Start() // Must be called in child
    {
        if (unitData == null)
        {
            Debug.LogError($"No UnitSO assigned on {gameObject.name}.");
            return;
        }
        if (selectionDecal == null)
        {
            Debug.LogError($"No Decal projector assigned on {gameObject.name}.");
            return;
        }

        mainCamera = Camera.main;
        health = GetComponent<Health>();
        health.Initialize(unitData.maxHealth);
        isSelected = false;
        
        agent = GetComponent<NavMeshAgent>();
        agent.speed = unitData.moveSpeed;
        agent.baseOffset = 1f; // MIGHT CHANGE IN THE FUTURE DOUBLE CHECK
        
        attackRange = unitData.attackRange;
        attackDamage = unitData.attackDamage;
        attackInterval = unitData.attackInterval;
        attackTimer = attackInterval;
        
        SelectionManager.Instance.Register(this);
        selectionDecal.enabled = false;
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
        if (attackTarget is not null)
        {
            float distance = Vector3.Distance(transform.position, attackTarget.transform.position);
            if (distance <= attackRange)
                state = UnitState.Attacking;
        }
        
        else if (!agent.pathPending && agent.remainingDistance < 0.1f) // Reached target destination
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

        // Check if target died mid combat
        if (!attackTarget.TryGetComponent(out IDamageable damageable))
        {
            attackTarget = null;
            state = UnitState.Idle;
            return;
        }

        // Chase target if it moves out of range
        float distance = Vector3.Distance(transform.position, attackTarget.transform.position);
        if (distance > attackRange)
        {
            agent.SetDestination(attackTarget.transform.position);
            state = UnitState.Moving;
            return;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            damageable.TakeDamage(attackDamage);
            attackTimer = attackInterval;
        }
    }
    
    public virtual void SetMoveTarget() { } // subclasses override
    
    
    
}
