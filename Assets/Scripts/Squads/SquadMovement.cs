using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SquadMovement : MonoBehaviour
{
    [Header("Slot Following")]
    [SerializeField] private float slotUpdateThreshold = 0.25f;
    [SerializeField] private float memberStoppingDistance = 0.1f;

    [Header("Catchup")]
    [SerializeField] private float catchupStartDistance = 2f;
    [SerializeField] private float catchupMaxDistance = 10f;
    [SerializeField] private float catchupMaxMultiplier = 1.45f;

    [Header("Reform")]
    [SerializeField] private float reformCheckInterval = 0.25f;
    [SerializeField] private float reformMemberDistance = 1.25f;
    [SerializeField] private float reformRatioRequired = 0.75f;
    
    [Header("Slot Reassignment")]
    [SerializeField] private bool reassignSlotsOnLargeFacingChange = true;
    [SerializeField] private float reassignFacingAngle = 100f;

    private SquadController squad;
    private SquadRoster roster;
    private SquadFormationController formation;
    private SquadData data;

    private NavMeshAgent squadAgent;

    private Vector3 finalDestination;
    private Vector3 desiredFacing = Vector3.forward;

    private float reformTimer = 0f;
    private float baseMoveSpeed = 4f;

    public Vector3 FinalDestination => finalDestination;
    public Vector3 DesiredFacing => desiredFacing;

    void Awake()
    {
        squadAgent = GetComponent<NavMeshAgent>();

        squadAgent.updateRotation = false;
        squadAgent.angularSpeed = 99999f;
        squadAgent.acceleration = 99999f;
        squadAgent.autoBraking = false;
    }

    public void Initialize(
        SquadController owner,
        SquadRoster squadRoster,
        SquadFormationController squadFormation,
        SquadData squadData)
    {
        squad = owner;
        roster = squadRoster;
        formation = squadFormation;
        data = squadData;

        baseMoveSpeed = data.movement.moveSpeed > 0f
            ? data.movement.moveSpeed
            : 4f;

        squadAgent.speed = baseMoveSpeed;
        squadAgent.acceleration = data.movement.acceleration > 0f
            ? data.movement.acceleration
            : 99999f;

        finalDestination = transform.position;
        desiredFacing = NormalizeFacing(transform.forward);

        PlaceSoldiersInCurrentFormation();
    }

    /// Orders the squad root to move while soldiers follow formation slots.
    /// Large facing changes reassign soldiers to nearest slots so the formation
    /// does not flip/cross through itself when ordered behind its current facing.
    public void OrderMove(
        Vector3 destination,
        Vector3 facing,
        float requestedFormationWidth = -1f)
    {
        finalDestination = destination;

        Vector3 newFacing = NormalizeFacing(facing);
        
        bool shouldReassignSlots = ShouldReassignSlotsForFacing(newFacing);

        desiredFacing = newFacing;

        if (requestedFormationWidth > 0f)
            formation.SetFormationWidth(requestedFormationWidth);

        formation.SetFacing(desiredFacing);
        formation.UpdateSlots(transform.position, desiredFacing);

        if (shouldReassignSlots)
        {
            formation.ReassignLivingSoldiersToNearestSlots(
                transform.position,
                desiredFacing);
        }

        squadAgent.isStopped = false;
        squadAgent.speed = baseMoveSpeed;
        squadAgent.stoppingDistance = 0.2f;
        squadAgent.SetDestination(finalDestination);

        // Only show on Drag Orders from now on.
        // FormationVisualizer.Instance?.ShowSlots(
        //     formation.GetWorldSlots(finalDestination, desiredFacing));
    }

    public void OrderStop()
    {
        if (squadAgent != null && squadAgent.enabled && squadAgent.isOnNavMesh)
            squadAgent.ResetPath();

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null)
                continue;

            soldier.Stop();
        }
    }

    public void TickIdle()
    {
        UpdateSoldiersToCurrentSlots();
    }

    /// Updates squad movement while traveling to an ordered destination.
    /// We do not overwrite desiredFacing from agent velocity because drag-facing
    /// and explicit formation facing should remain authoritative.
    public void TickMoving()
    {
        // Do NOT call UpdateFacingFromAgent() here.
        // The player/order system already decided the desired formation facing.
        TickFormationFollow();

        if (!squadAgent.pathPending &&
            squadAgent.remainingDistance <= squadAgent.stoppingDistance)
        {
            BeginReform(false);
            squad.SetState(SquadState.Reforming);
        }
    }

    public void TickReforming()
    {
        formation.UpdateSlots(transform.position, desiredFacing);
        UpdateSoldiersToCurrentSlots();

        reformTimer -= Time.deltaTime;

        if (reformTimer > 0f)
            return;

        reformTimer = reformCheckInterval;

        if (EnoughSoldiersNearCurrentSlots())
            squad.SetState(SquadState.Idle);
    }

    public void TickRouting()
    {
        // Later: morale routing.
    }
    
    /// Updates soldiers toward the squad's current formation slots.
    /// This is used by non-standard movement states like ApproachingCombat,
    /// where the squad root is moving but the state is not SquadState.Moving.
    public void TickFormationFollow()
    {
        UpdateSoldiersToCurrentSlots();
    }
    
    /// Gently moves the squad root during combat.
    /// This lets the formation body follow the pressure of the fight without
    /// teleporting or fully collapsing into the frontline.
    public void DriftCombatAnchor(
        Vector3 desiredAnchor,
        float maxDistanceThisFrame)
    {
        desiredAnchor.y = transform.position.y;

        Vector3 nextPosition = Vector3.MoveTowards(
            transform.position,
            desiredAnchor,
            Mathf.Max(0f, maxDistanceThisFrame));

        if (squadAgent != null &&
            squadAgent.enabled &&
            squadAgent.isOnNavMesh)
        {
            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                    nextPosition,
                    out hit,
                    1f,
                    NavMesh.AllAreas))
            {
                nextPosition = hit.position;
            }

            squadAgent.Warp(nextPosition);
        }
        else
        {
            transform.position = nextPosition;
        }
    }

    /// Starts reforming the squad into its current formation.
    /// Normal movement should not recenter from soldiers, because that can cause
    /// a visible snap backward near the destination.
    /// Combat can pass true if the squad became scattered and should reform around survivors.
    public void BeginReform(bool recenterFromSoldiers = false)
    {
        if (squadAgent != null && squadAgent.enabled && squadAgent.isOnNavMesh)
            squadAgent.ResetPath();

        if (recenterFromSoldiers)
            UpdateSquadCenterFromSoldiers();

        formation.UpdateSlots(transform.position, desiredFacing);
        reformTimer = 0f;
    }

    public Vector3 ResolveFacing(Vector3 destination)
    {
        Vector3 dir = destination - transform.position;
        dir.y = 0f;

        if (dir == Vector3.zero)
            return desiredFacing;

        return dir.normalized;
    }

    void PlaceSoldiersInCurrentFormation()
    {
        formation.UpdateSlots(transform.position, desiredFacing);

        IReadOnlyList<Vector3> slots = formation.CurrentSlots;

        for (int i = 0; i < roster.Soldiers.Count && i < slots.Count; i++)
        {
            SoldierController soldier = roster.Soldiers[i];

            if (soldier == null)
                continue;

            soldier.Motor.Warp(slots[i]);
            soldier.SetLastSlotPosition(slots[i]);
        }
    }

    /// Moves each living soldier toward its assigned formation slot.
    /// Important: this uses SoldierController.SlotIndex, not roster list index.
    /// That lets large facing changes instantly reassign the formation front
    /// without soldiers crossing through the squad.
    void UpdateSoldiersToCurrentSlots()
    {
        if (roster == null || formation == null)
            return;

        formation.UpdateSlots(transform.position, desiredFacing);

        IReadOnlyList<Vector3> slots = formation.CurrentSlots;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            int slotIndex = soldier.SlotIndex;

            if (slotIndex < 0 || slotIndex >= slots.Count)
                continue;

            Vector3 slot = slots[slotIndex];

            float distance = Vector3.Distance(
                soldier.transform.position,
                slot);

            float speedMultiplier = GetCatchupMultiplier(distance);

            soldier.MoveToSlot(
                slot,
                slotUpdateThreshold,
                memberStoppingDistance,
                speedMultiplier);
        }
    }
    
    
    void UpdateFacingFromAgent()
    {
        if (squadAgent == null)
            return;

        Vector3 velocity = squadAgent.velocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude < 0.1f)
            return;

        desiredFacing = velocity.normalized;
        formation.SetFacing(desiredFacing);
    }

    void UpdateSquadCenterFromSoldiers()
    {
        Vector3 center = GetAverageLivingSoldierPosition();

        if (center == Vector3.zero)
            return;

        if (squadAgent != null && squadAgent.enabled && squadAgent.isOnNavMesh)
            squadAgent.Warp(center);
        else
            transform.position = center;
    }

    bool EnoughSoldiersNearCurrentSlots()
    {
        IReadOnlyList<Vector3> slots = formation.CurrentSlots;

        if (slots.Count == 0)
            return true;

        int living = 0;
        int near = 0;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            int slotIndex = soldier.SlotIndex;

            if (slotIndex < 0 || slotIndex >= slots.Count)
                continue;

            living++;

            if (Calc.WithinRange(
                    soldier.transform.position,
                    slots[slotIndex],
                    reformMemberDistance))
            {
                near++;
            }
        }

        if (living == 0)
            return true;

        float ratio = (float)near / living;
        return ratio >= reformRatioRequired;
    }
    
    Vector3 GetAverageLivingSoldierPosition()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            sum += soldier.transform.position;
            count++;
        }

        if (count == 0)
            return Vector3.zero;

        return sum / count;
    }

    float GetCatchupMultiplier(float distance)
    {
        if (distance <= catchupStartDistance)
            return 1f;

        float t = Mathf.InverseLerp(
            catchupStartDistance,
            catchupMaxDistance,
            distance);

        return Mathf.Lerp(
            1f,
            catchupMaxMultiplier,
            t);
    }

    Vector3 NormalizeFacing(Vector3 value)
    {
        value.y = 0f;

        if (value == Vector3.zero)
            return Vector3.forward;

        return value.normalized;
    }
    
    /// Returns true when the new facing is different enough that soldiers should
    /// swap to nearest slots instead of preserving old slot identity.
    bool ShouldReassignSlotsForFacing(Vector3 newFacing)
    {
        if (!reassignSlotsOnLargeFacingChange)
            return false;

        Vector3 oldFacing = desiredFacing;
        oldFacing.y = 0f;
        newFacing.y = 0f;

        if (oldFacing == Vector3.zero || newFacing == Vector3.zero)
            return false;

        float angle = Vector3.Angle(
            oldFacing.normalized,
            newFacing.normalized);

        return angle >= reassignFacingAngle;
    }
    
    /// Holds non-excluded soldiers in their assigned formation slots.
    /// Excluded soldiers are usually autonomous combat soldiers.
    public void HoldFormationSlotsExcept(
        HashSet<SoldierController> excludedSoldiers)
    {
        if (roster == null || formation == null)
            return;

        formation.UpdateSlots(transform.position, desiredFacing);

        IReadOnlyList<Vector3> slots = formation.CurrentSlots;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            if (excludedSoldiers != null && excludedSoldiers.Contains(soldier))
                continue;

            int slotIndex = soldier.SlotIndex;

            if (slotIndex < 0 || slotIndex >= slots.Count)
                continue;

            float distance = Vector3.Distance(
                soldier.transform.position,
                slots[slotIndex]);

            float speedMultiplier = GetCatchupMultiplier(distance);

            soldier.MoveToSlot(
                slots[slotIndex],
                slotUpdateThreshold,
                memberStoppingDistance,
                speedMultiplier);
        }
    }
    
}