// SESSION: Squad Control Refactor

using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(MilitaryController))]
[RequireComponent(typeof(NavMeshAgent))]
public class SquadMemberController : MonoBehaviour
{
    #region Fields

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

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        Unit = GetComponent<MilitaryController>();
        Agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (!IsInSquad) return;
        if (!IsAlive) return;

        RotateTowardVelocity();
    }

    void OnDestroy()
    {
        if (Squad != null)
        {
            SquadController oldSquad = Squad;
            Squad = null;
            oldSquad.RemoveMember(this);
        }
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

        // Members are logically owned by the squad but must stay unparented in world space.
        // This prevents parent movement + member NavMeshAgent movement from double-applying.
        transform.SetParent(null, true);

        if (Agent != null)
        {
            Agent.isStopped = false;
            Agent.speed = Stats != null ? Stats.moveSpeed : Agent.speed;
        }

        // MilitaryController is the old independent military brain.
        // SquadController now owns high-level military behavior.
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

    #endregion

    #region Movement

    public void MoveToSlot(
        Vector3 slotPosition,
        float updateThreshold,
        float stoppingDistance = 0.1f)
    {
        if (!CanMove()) return;

        if (!Calc.OutOfRange(LastSlotPosition, slotPosition, updateThreshold))
            return;

        MoveToPoint(slotPosition, stoppingDistance);
        LastSlotPosition = slotPosition;
    }

    public void MoveToPoint(Vector3 position, float stoppingDistance = 0.1f)
    {
        if (!CanMove()) return;

        Agent.isStopped = false;
        Agent.speed = Stats != null ? Stats.moveSpeed : Agent.speed;
        Agent.stoppingDistance = stoppingDistance;
        Agent.SetDestination(position);
    }

    public void Stop()
    {
        if (Agent == null) return;
        if (!Agent.enabled) return;
        if (!Agent.isActiveAndEnabled) return;
        if (!Agent.isOnNavMesh) return;

        Agent.ResetPath();
        Agent.isStopped = false;
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

    #endregion

    #region Combat Hooks

    // Future:
    // SquadController will assign targets.
    // SquadMemberController will execute low-level attack behavior.
    //
    // public void AssignAttackTarget(EntityController target) {}
    // public void ClearAttackTarget() {}
    // public bool IsInAttackRange(EntityController target) {}

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

    #endregion
}


// // SESSION: Squad Control
//
// using UnityEngine;
// using UnityEngine.AI;
//
// [RequireComponent(typeof(MilitaryController))]
// [RequireComponent(typeof(NavMeshAgent))]
//
//
// public class SquadMemberController : MonoBehaviour
// {
//     public SquadController Squad { get; private set; }
//     public MilitaryController Unit { get; private set; }
//     public EntityStats Stats => Unit != null ? Unit.Stats : null;
//     public NavMeshAgent Agent { get; private set; }
//
//     public int SlotIndex { get; private set; } = -1;
//     public Vector3 LastSlotPosition { get; private set; }
//
//     public bool IsInSquad => Squad != null;
//
//     [SerializeField] private float rotationSpeed = 900f;
//
//     
//     void Awake()
//     {
//         Unit = GetComponent<MilitaryController>();
//         Agent = GetComponent<NavMeshAgent>();
//     }
//     
//
//     void OnDestroy()
//     {
//         if (Squad != null)
//             Squad.RemoveMember(this);
//     }
//     
//     
//     void Update()
//     {
//         if (!IsInSquad) return;
//         RotateTowardVelocity();
//     }
//     
//
//     public void JoinSquad(SquadController squad, int slotIndex)
//     {
//         Squad = squad;
//         SlotIndex = slotIndex;
//         LastSlotPosition = transform.position;
//
//         // SESSION: Squad Control
//         // Members are logically owned by the squad but must stay unparented in world space.
//         // This prevents parent movement + member NavMeshAgent movement from double-applying.
//         transform.SetParent(null, true);
//
//         if (Unit != null)
//         {
//             Unit.SetSquadMember(this);
//             Unit.enabled = false;
//         }
//     }
//     
//
//     public void LeaveSquad()
//     {
//         Squad = null;
//         SlotIndex = -1;
//
//         if (Unit != null)
//         {
//             Unit.ClearSquadMember(this);
//             Unit.enabled = true;
//         }
//     }
//
//     
//     public void SetSlotIndex(int slotIndex)
//     {
//         SlotIndex = slotIndex;
//     }
//
//     
//     public void MoveToSlot(Vector3 slotPosition, float updateThreshold, float stoppingDistance = 0.1f)
//     {
//         if (Agent == null || !Agent.enabled) return;
//
//         if (!Calc.OutOfRange(LastSlotPosition, slotPosition, updateThreshold))
//             return;
//
//         MoveToPoint(slotPosition, stoppingDistance);
//         LastSlotPosition = slotPosition;
//     }
//
//     
//     public void MoveToPoint(Vector3 position, float stoppingDistance = 0.1f)
//     {
//         if (Agent == null || !Agent.enabled) return;
//
//         Agent.speed = Stats != null ? Stats.moveSpeed : Agent.speed;
//         Agent.stoppingDistance = stoppingDistance;
//         Agent.SetDestination(position);
//     }
//
//     
//     public void Stop()
//     {
//         if (Agent == null || !Agent.enabled) return;
//         Agent.ResetPath();
//     }
//
//     
//     public bool IsNear(Vector3 position, float range)
//     {
//         return Calc.WithinRange(transform.position, position, range);
//     }
//     
//     
//     void RotateTowardVelocity()
//     {
//         if (Agent == null) return;
//         if (Agent.velocity.sqrMagnitude < 0.1f) return;
//
//         Vector3 dir = Agent.velocity;
//         dir.y = 0f;
//         if (dir == Vector3.zero) return;
//
//         Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
//
//         transform.rotation = Quaternion.RotateTowards(
//             transform.rotation,
//             targetRot,
//             rotationSpeed * Time.deltaTime);
//     }
//
//     
//
//     
//     
// }