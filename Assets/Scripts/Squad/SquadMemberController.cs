// SESSION: Squad Control Refactor
// SESSION: Squad Control Refactor - Loosen Up

using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(MilitaryController))]
[RequireComponent(typeof(NavMeshAgent))]

public class SquadMemberController : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 900f;

    public SquadController Squad { get; private set; }
    public MilitaryController Unit { get; private set; }
    public NavMeshAgent Agent { get; private set; }

    public EntityStats Stats => Unit != null ? Unit.Stats : null;

    public int SlotIndex { get; private set; } = -1;
    public Vector3 LastSlotPosition { get; private set; }

    public bool IsInSquad => Squad != null;
    public bool IsAlive => Stats == null || Stats.IsAlive;

    private float baseMoveSpeed = 0f;
    
    
    // SESSION: Squad Combat
    // Section: Combat
    private EntityController attackTarget;
    private float attackTimer = 0f;
    


    #region Unity Lifecycle

    void Awake()
    {
        Unit = GetComponent<MilitaryController>();
        Agent = GetComponent<NavMeshAgent>();

        if (Agent != null)
            baseMoveSpeed = Agent.speed;
    }
    
    void OnDestroy()
    {
        if (Squad != null)
        {
            SquadController oldSquad = Squad;
            Squad = null;
            oldSquad.RemoveMember(this); // Needed with current death logic?
        }
    }

    void Update()
    {
        if (!IsInSquad) return;
        if (!IsAlive) return;
        
        TickCombat();
        RotateTowardVelocity();
    }
    
    #endregion

    #region Squad Membership

    public void JoinSquad(SquadController squad, int slotIndex)
    {
        if (squad == null)
        {
            Debug.LogError("JoinSquad failed: squad is null.");
            return;
        }

        Squad = squad;
        SlotIndex = slotIndex;
        LastSlotPosition = transform.position;

        transform.SetParent(null, true);

        if (Agent != null)
        {
            baseMoveSpeed = Stats != null ? Stats.moveSpeed : Agent.speed;

            Agent.isStopped = false;
            Agent.speed = baseMoveSpeed;
        }

        if (Unit != null)
        {
            Unit.SetSquadMember(this);
            Unit.enabled = false;
        }
    }

    public void LeaveSquad()
    {
        Squad = null;
        SlotIndex = -1;

        Stop();

        if (Unit != null)
        {
            Unit.ClearSquadMember(this);
            Unit.enabled = true;
        }
    }

    public void SetSlotIndex(int slotIndex)
    {
        SlotIndex = slotIndex;
    }

    public void SetLastSlotPosition(Vector3 position)
    {
        LastSlotPosition = position;
    }

    #region Squad Death

    public void NotifyDeath()
    {
        if (Squad != null)
        {
            SquadController oldSquad = Squad;
            Squad = null;
            oldSquad.HandleMemberDeath(this);
        }

        Stop(); // maybe remove when we add ragdoll?
    }

    #endregion
    #endregion


    #region Movement

    public void MoveToSlot(
        Vector3 slotPosition,
        float updateThreshold,
        float stoppingDistance = 0.1f,
        float speedMultiplier = 1f)
    {
        if (!CanMove()) return;

        ApplySpeedMultiplier(speedMultiplier);

        if (!Calc.OutOfRange(LastSlotPosition, slotPosition, updateThreshold))
            return;

        MoveToPoint(slotPosition, stoppingDistance, speedMultiplier);
        LastSlotPosition = slotPosition;
    }

    public void MoveToPoint(
        Vector3 position,
        float stoppingDistance = 0.1f,
        float speedMultiplier = 1f)
    {
        if (!CanMove()) return;

        ApplySpeedMultiplier(speedMultiplier);

        Agent.isStopped = false;
        Agent.stoppingDistance = stoppingDistance;
        Agent.SetDestination(position);
    }

    public void Stop()
    {
        if (Agent == null) return;
        if (!Agent.enabled) return;
        if (!Agent.isActiveAndEnabled) return;
        if (!Agent.isOnNavMesh) return;

        ResetMoveSpeed();

        Agent.ResetPath();
        Agent.isStopped = false;
    }

    public void ResetMoveSpeed()
    {
        if (Agent == null) return;

        float speed = Stats != null ? Stats.moveSpeed : baseMoveSpeed;

        if (speed > 0f)
            Agent.speed = speed;
    }

    void ApplySpeedMultiplier(float multiplier)
    {
        if (Agent == null) return;

        float speed = Stats != null ? Stats.moveSpeed : baseMoveSpeed;

        if (speed <= 0f)
            speed = Agent.speed;

        Agent.speed = speed * Mathf.Max(0.1f, multiplier);
    }

    public bool IsNear(Vector3 position, float range)
    {
        return Calc.WithinRange(transform.position, position, range);
    }

    bool CanMove()
    {
        if (!IsAlive) return false;
        if (Agent == null) return false;
        if (!Agent.enabled) return false;
        if (!Agent.isOnNavMesh) return false;

        return true;
    }

    #endregion

    #region Selection Visuals

    public void ShowSelectionVisual()
    {
        if (Unit == null) return;
        Unit.OnSelect();
    }

    public void HideSelectionVisual()
    {
        if (Unit == null) return;
        Unit.OnDeselect();
    }
    
    #region Hover

    public void ShowHoverVisual()
    {
        if (Unit == null) return;

        Unit.OnHoverEnter();
    }

    public void HideHoverVisual()
    {
        if (Unit == null) return;

        Unit.OnHoverExit();
    }

    #endregion
    #endregion

    // SESSION: Squad Combat
    #region Combat

    public void AssignAttackTarget(EntityController target)
    {
        attackTarget = target;
    }

    public void ClearAttackTarget()
    {
        attackTarget = null;
    }

    void TickCombat()
    {
        if (attackTimer > 0f)
            attackTimer -= Time.deltaTime;

        if (attackTarget == null)
            return;

        if (attackTarget.Stats == null || !attackTarget.Stats.IsAlive)
        {
            ClearAttackTarget();
            return;
        }

        if (Stats == null)
            return;

        float range = Stats.attackRange;

        if (!Calc.WithinRange(transform.position, attackTarget.transform.position, range))
        {
            SquadStance stance = Squad != null
                ? Squad.Stance
                : SquadStance.Aggressive;

            if (stance == SquadStance.NoAttack)
            {
                ClearAttackTarget();
                Stop();
                return;
            }

            if (stance == SquadStance.StandGround)
            {
                ClearAttackTarget();
                Stop();
                return;
            }

            if (stance == SquadStance.Defensive &&
                Squad != null &&
                !Squad.CanMemberChaseTarget(this, attackTarget))
            {
                ClearAttackTarget();
                Stop();
                return;
            }

            MoveToPoint(attackTarget.transform.position, range * 0.85f);
            return;
        }

        Stop();
        RotateTowardTarget(attackTarget.transform.position);

        if (attackTimer <= 0f)
            PerformAttack();
    }

    void PerformAttack()
    {
        attackTimer = Stats.attackInterval;

        if (Unit != null && Unit.UnitAnimator != null)
            Unit.UnitAnimator.TriggerAttack();

        // First pass: apply damage immediately.
        // Later we can sync this to animation/projectile impact.
        if (attackTarget != null)
            attackTarget.TakeDamage(Stats.attackDamage);
    }

    #endregion

    #region Rotation

    void RotateTowardVelocity()
    {
        if (Agent == null) return;
        if (Agent.velocity.sqrMagnitude < 0.1f) return;

        Vector3 dir = Agent.velocity;
        dir.y = 0f;

        if (dir == Vector3.zero)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            dir.normalized,
            Vector3.up);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
    }

    void RotateTowardTarget(Vector3 targetPosition)
    {
        Vector3 dir = targetPosition - transform.position;
        dir.y = 0f;

        if (dir == Vector3.zero)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            dir.normalized,
            Vector3.up);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
    }
    #endregion
    
    
}

