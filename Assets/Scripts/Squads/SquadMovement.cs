using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// -----------------------------------------------------------------------------
/// SquadMovement
/// -----------------------------------------------------------------------------
///
/// Handles squad formation movement through a virtual formation anchor.
///
/// The old movement model used a physical squad-root NavMeshAgent, then made
/// soldiers chase slots around that moving root. This version removes the root
/// agent from formed movement. The squad root is now a virtual formation pose:
/// position + facing. Soldiers remain normal NavMeshAgents, but during clean
/// formed movement they receive shared slot deltas plus small corrections instead
/// of independent SetDestination calls.
///
/// Movement modes:
/// IdleFormed  - squad is stationary and soldiers hold slots.
/// FormedMove  - virtual formation body follows a path and soldiers move with it.
/// LooseMove   - formation cannot fit / cohesion broke, soldiers path individually.
/// Reforming   - soldiers return to legal slots after movement, combat, or loose move.
///
/// Design role:
/// Squad marching, virtual anchor pathing, formation slot following, loose fallback,
/// facing, and reforming.
///
public class SquadMovement : MonoBehaviour
{
    #region Fields

    // -----------------------------------------------------------------------------
    // Profile Reference
    // -----------------------------------------------------------------------------
    // SquadMovementProfile is the single source of truth for designer-tunable
    // movement values. Runtime fields below are only for actual movement state.
    private SquadMovementProfile movementProfile;
    private bool hasLoggedMissingMovementProfile = false;

    // -----------------------------------------------------------------------------
    // Component References
    // -----------------------------------------------------------------------------
    private SquadController squad;
    private SquadRoster roster;
    private SquadFormationController formation;
    private SquadData data;
    private NavMeshAgent squadAgent;

    // -----------------------------------------------------------------------------
    // Runtime Movement State
    // -----------------------------------------------------------------------------
    private SquadMoveMode moveMode = SquadMoveMode.IdleFormed;
    private Vector3 finalDestination;
    private Vector3 desiredFacing = Vector3.forward;
    private Vector3 finalFacing = Vector3.forward;

    // -----------------------------------------------------------------------------
    // Runtime Path State
    // -----------------------------------------------------------------------------
    private NavMeshPath anchorPath;
    private readonly List<Vector3> pathCorners = new List<Vector3>();
    private int pathCornerIndex = 0;

    // -----------------------------------------------------------------------------
    // Runtime Timers
    // -----------------------------------------------------------------------------
    private float reformTimer = 0f;

    // -----------------------------------------------------------------------------
    // Runtime Movement Values
    // -----------------------------------------------------------------------------
    private float baseAnchorMoveSpeed = 4f;
    private float effectiveAnchorMoveSpeed = 4f;
    private float memberBaseMoveSpeed = 4f;

    // -----------------------------------------------------------------------------
    // Public Read-Only Access
    // -----------------------------------------------------------------------------
    public Vector3 FinalDestination => finalDestination;
    public Vector3 DesiredFacing => desiredFacing;
    public Vector3 FinalFacing => finalFacing;
    public SquadMoveMode MoveMode => moveMode;

    #endregion

    void Awake()
    {
        anchorPath = new NavMeshPath();

        squadAgent = GetComponent<NavMeshAgent>();

        // The squad root is now a virtual formation anchor, not a physical moving
        // unit. Disabling the root agent prevents the center slot from fighting an
        // invisible NavMeshAgent and prevents the anchor from avoiding soldiers.
        if (squadAgent != null)
        {
            if (squadAgent.enabled && squadAgent.isOnNavMesh)
                squadAgent.ResetPath();

            squadAgent.updateRotation = false;
            squadAgent.angularSpeed = 99999f;
            squadAgent.acceleration = 99999f;
            squadAgent.autoBraking = false;
            squadAgent.enabled = false;
        }
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
        movementProfile = data != null ? data.movementProfile : null;

        if (!HasMovementProfile())
        {
            enabled = false;
            return;
        }

        RefreshProfileAndMovementSpeeds();

        finalDestination = transform.position;
        desiredFacing = NormalizeFacing(transform.forward);
        finalFacing = desiredFacing;
        moveMode = SquadMoveMode.IdleFormed;

        PlaceSoldiersInCurrentFormation();
    }

    bool HasMovementProfile()
    {
        if (movementProfile != null)
            return true;

        if (!hasLoggedMissingMovementProfile)
        {
            Debug.LogError(
                $"{name}: SquadMovement requires SquadData.movementProfile. Assign a SquadMovementProfile asset before using squad movement.",
                this);

            hasLoggedMissingMovementProfile = true;
        }

        return false;
    }

    void RefreshProfileAndMovementSpeeds()
    {
        if (!HasMovementProfile())
            return;

        baseAnchorMoveSpeed = data != null && data.movement.moveSpeed > 0f
            ? data.movement.moveSpeed
            : 4f;

        memberBaseMoveSpeed = ResolveMemberBaseMoveSpeed();
        effectiveAnchorMoveSpeed = ResolveEffectiveAnchorMoveSpeed();
    }

    /// Orders the squad to move as a virtual formation body while soldiers follow
    /// shared slot deltas. If the formation cannot fit or breaks cohesion, movement
    /// falls back to LooseMove and then Reforming.
    public void OrderMove(
        Vector3 destination,
        Vector3 facing,
        float requestedFormationWidth = -1f)
    {
        if (!HasMovementProfile())
            return;

        finalDestination = ProjectPointToNavMesh(destination, transform.position);
        finalFacing = NormalizeFacing(facing);

        Vector3 startingFacing = desiredFacing;
        if (startingFacing == Vector3.zero)
            startingFacing = ResolveFacing(finalDestination);

        if (startingFacing == Vector3.zero)
            startingFacing = finalFacing;

        if (requestedFormationWidth > 0f)
            formation.SetFormationWidth(requestedFormationWidth);

        // Important:
        // Do NOT reassign slots immediately for large facing changes during a move
        // order. The virtual formation motor already wheels the formation toward
        // the new facing over time. Reassigning slot indices at order time means
        // a 90/180-degree order can make a soldier suddenly inherit a slot on the
        // opposite side of the formation, which creates huge first-frame slot
        // errors and can look like a slingshot.
        //
        // Slot reassignment is still useful for explicit reform/facing tools later,
        // but normal formed movement should preserve slot identity and rotate the
        // formation body instead.

        // Re-resolve profile and movement speeds at order time so play-mode
        // tuning changes are reflected without requiring a new scene load.
        RefreshProfileAndMovementSpeeds();

        if (!movementProfile.useVirtualFormationMovement)
        {
            BeginProfileLooseMove(finalDestination, finalFacing);
            return;
        }

        Vector3 travelFacing = ResolveFacing(finalDestination);
        bool shouldReassignForSharpTurn =
            IsSharpTurnFromCurrentFacing(travelFacing) ||
            IsSharpTurnFromCurrentFacing(finalFacing);

        Vector3 sharpTurnFacing = IsSharpTurnFromCurrentFacing(travelFacing)
            ? travelFacing
            : finalFacing;

        if (!BuildAnchorPath(finalDestination))
        {
            BeginLooseMove();
            return;
        }

        moveMode = SquadMoveMode.FormedMove;

        if (shouldReassignForSharpTurn)
        {
            // For 120-180 degree orders, do not physically rotate every slot
            // through the whole formation. That causes the classic crossover/flip.
            // Instead, declare the new travel-facing immediately and remap each
            // living soldier to the nearest slot in that new frame. Since this
            // happens before the first formed-move tick, there is no giant slot
            // delta or launch.
            desiredFacing = NormalizeFacing(sharpTurnFacing);
            formation.SetFacing(desiredFacing);
            formation.ReassignLivingSoldiersToNearestSlots(transform.position, desiredFacing);
            formation.UpdateSlots(transform.position, desiredFacing);
        }
        else
        {
            // Keep the current formation facing at order start.
            // TickFormedMove gradually wheels toward path/final facing.
            desiredFacing = NormalizeFacing(startingFacing);
            formation.SetFacing(desiredFacing);
            formation.UpdateSlots(transform.position, desiredFacing);
        }
    }

    public void OrderStop()
    {
        pathCorners.Clear();
        pathCornerIndex = 0;
        moveMode = SquadMoveMode.IdleFormed;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null)
                continue;

            soldier.Stop();
        }
    }

    public void TickIdle()
    {
        if (!HasMovementProfile())
            return;

        moveMode = SquadMoveMode.IdleFormed;
        UpdateSoldiersToCurrentSlots();
    }

    /// Updates squad movement while traveling to an ordered destination.
    public void TickMoving()
    {
        TickMovementCore(allowArrivalStateChange: true);
    }

    public void TickReforming()
    {
        TickReformingCore(allowStateChange: true);
    }

    public void TickRouting()
    {
        // Later: morale routing.
    }
    
    /// Ticks movement without forcing normal move-order state transitions.
    /// This is used by approach/charge states where combat owns the high-level state.
    public void TickFormationFollow()
    {
        TickMovementCore(allowArrivalStateChange: false);
    }
    
    /// Gently moves the virtual squad anchor during combat.
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

        MoveRootToProjectedPoint(nextPosition);
        formation.UpdateSlots(transform.position, desiredFacing);
    }

    /// Starts reforming the squad into its current formation.
    /// Normal movement should not recenter from soldiers, because that can cause
    /// a visible snap backward near the destination.
    /// Combat can pass true if the squad became scattered and should reform around survivors.
    public void BeginReform(bool recenterFromSoldiers = false)
    {
        pathCorners.Clear();
        pathCornerIndex = 0;
        moveMode = SquadMoveMode.Reforming;

        if (recenterFromSoldiers)
            UpdateSquadCenterFromSoldiers();

        formation.UpdateSlots(transform.position, desiredFacing);

        ForceRefreshSlotDestinations();

        reformTimer = 0f;
    }
    
    void ForceRefreshSlotDestinations() // NEW
    {
        if (roster == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            soldier.SetLastSlotPosition(Vector3.positiveInfinity);
        }
    }

    public Vector3 ResolveFacing(Vector3 destination)
    {
        Vector3 dir = destination - transform.position;
        dir.y = 0f;

        if (dir == Vector3.zero)
            return desiredFacing;

        return dir.normalized;
    }

    void TickMovementCore(bool allowArrivalStateChange)
    {
        if (!HasMovementProfile())
            return;

        switch (moveMode)
        {
            case SquadMoveMode.FormedMove:
                TickFormedMove(allowArrivalStateChange);
                return;

            case SquadMoveMode.LooseMove:
                TickLooseMove(allowArrivalStateChange);
                return;

            case SquadMoveMode.Reforming:
                TickReformingCore(allowStateChange: allowArrivalStateChange);
                return;

            case SquadMoveMode.IdleFormed:
            default:
                UpdateSoldiersToCurrentSlots();
                return;
        }
    }


    void TickReformingCore(bool allowStateChange)
    {
        if (!HasMovementProfile())
            return;

        moveMode = SquadMoveMode.Reforming;

        formation.UpdateSlots(transform.position, desiredFacing);
        UpdateSoldiersToCurrentSlots();

        reformTimer -= Time.deltaTime;

        if (reformTimer > 0f)
            return;

        reformTimer = movementProfile.reformCheckInterval;

        if (!EnoughSoldiersNearCurrentSlots())
            return;

        moveMode = SquadMoveMode.IdleFormed;

        if (allowStateChange)
            squad.SetState(SquadState.Idle);
    }

    void TickFormedMove(bool allowArrivalStateChange)
    {
        if (roster == null || formation == null)
            return;

        if (pathCorners.Count == 0)
        {
            CompleteMovementOrReform(allowArrivalStateChange);
            return;
        }

        List<Vector3> oldSlots = formation.GetWorldSlots(
            transform.position,
            desiredFacing);

        // Do not slow the entire squad for normal slot error. That made the unit
        // feel painfully slow. Members now receive catchup speed based on their
        // own error; the anchor only pauses for extreme separation.
        bool shouldEmergencyPauseAnchor = ShouldEmergencyPauseAnchor(oldSlots);

        Vector3 oldAnchor = transform.position;
        Vector3 oldFacing = desiredFacing;

        Vector3 nextAnchor = oldAnchor;
        Vector3 pathDirection = GetCurrentPathDirection();
        bool anchorAtDestination = HasAnchorReachedDestination();

        if (!shouldEmergencyPauseAnchor && !anchorAtDestination)
        {
            float moveDistance = effectiveAnchorMoveSpeed * Time.deltaTime;
            nextAnchor = AdvanceAnchorAlongPath(moveDistance);
            pathDirection = GetCurrentPathDirectionFrom(nextAnchor);
        }

        Vector3 targetFacing = ResolveMovementFacing(
            nextAnchor,
            pathDirection,
            HasAnchorReachedDestinationFrom(nextAnchor));

        float turnSpeedMultiplier = shouldEmergencyPauseAnchor ? 0.25f : 1f;

        Vector3 nextFacing = RotateFacingTowards(
            oldFacing,
            targetFacing,
            movementProfile.formedTurnSpeedDegrees * turnSpeedMultiplier * Time.deltaTime);

        Vector3 footprintProbeAnchor = nextAnchor;

        if (movementProfile.footprintLookAheadDistance > 0f && pathDirection != Vector3.zero)
            footprintProbeAnchor += pathDirection * movementProfile.footprintLookAheadDistance;

        if (!CanFormationFitAt(footprintProbeAnchor, nextFacing))
        {
            BeginLooseMove();
            return;
        }

        MoveRootToProjectedPoint(nextAnchor);
        desiredFacing = nextFacing;

        formation.SetFacing(desiredFacing);
        formation.UpdateSlots(transform.position, desiredFacing);

        List<Vector3> newSlots = CopyCurrentSlots();

        MoveSoldiersByFormationSlots(
            oldSlots,
            newSlots,
            desiredFacing);

        bool reachedPosition = HasAnchorReachedDestination();
        bool reachedFacing = Vector3.Angle(desiredFacing, finalFacing) <= movementProfile.formedFinalFacingAngle;

        if (reachedPosition && reachedFacing)
            CompleteMovementOrReform(allowArrivalStateChange);
    }

    void TickLooseMove(bool allowArrivalStateChange)
    {
        if (roster == null || formation == null)
            return;

        List<Vector3> finalSlots = formation.GetWorldSlots(
            finalDestination,
            finalFacing);

        MoveSoldiersIndividuallyToSlots(
            finalSlots,
            movementProfile.memberStoppingDistance);

        Vector3 center = GetAverageLivingSoldierPosition();
        if (center != Vector3.zero)
        {
            float maxRootFollowDistance = Mathf.Max(0.1f, memberBaseMoveSpeed) * Time.deltaTime;
            MoveRootTowardProjectedPoint(center, maxRootFollowDistance);
        }

        if (!EnoughSoldiersNearSlots(
                finalSlots,
                movementProfile.reformMemberDistance,
                movementProfile.reformRatioRequired))
        {
            return;
        }

        // At the end of loose fallback, avoid a visible root snap.
        // Estimate the anchor from current member positions, then blend toward the
        // commanded destination. Since the required near-ratio is high, this should
        // already be close to the target, but it prevents the old "jump away then
        // walk back" artifact when the root had been following the average body.
        Vector3 estimatedAnchor = EstimateAnchorFromSoldiers(finalFacing);
        if (estimatedAnchor != Vector3.zero)
            MoveRootTowardProjectedPoint(estimatedAnchor, memberBaseMoveSpeed * Time.deltaTime);

        if (Vector3.Distance(Flatten(transform.position), Flatten(finalDestination)) <= movementProfile.reformMemberDistance)
            MoveRootToProjectedPoint(finalDestination);

        desiredFacing = finalFacing;
        formation.SetFacing(desiredFacing);
        formation.UpdateSlots(transform.position, desiredFacing);

        CompleteMovementOrReform(allowArrivalStateChange);
    }

    void CompleteMovementOrReform(bool allowArrivalStateChange)
    {
        BeginReform(false);

        if (allowArrivalStateChange)
            squad.SetState(SquadState.Reforming);
    }

    void BeginLooseMove()
    {
        pathCorners.Clear();
        pathCornerIndex = 0;
        moveMode = SquadMoveMode.LooseMove;
    }

    void BeginProfileLooseMove(
        Vector3 destination,
        Vector3 facing)
    {
        finalDestination = destination;
        finalFacing = facing;
        BeginLooseMove();
    }

    bool BuildAnchorPath(Vector3 destination)
    {
        if (anchorPath == null)
            anchorPath = new NavMeshPath();

        pathCorners.Clear();
        pathCornerIndex = 0;

        Vector3 start = ProjectPointToNavMesh(
            transform.position,
            transform.position);

        Vector3 end = ProjectPointToNavMesh(
            destination,
            destination);

        bool pathFound = NavMesh.CalculatePath(
            start,
            end,
            NavMesh.AllAreas,
            anchorPath);

        if (!pathFound || anchorPath.status != NavMeshPathStatus.PathComplete)
            return false;

        if (anchorPath.corners == null || anchorPath.corners.Length == 0)
            return false;

        foreach (Vector3 corner in anchorPath.corners)
            pathCorners.Add(corner);

        if (pathCorners.Count == 1)
            pathCorners.Add(end);

        pathCornerIndex = pathCorners.Count > 1 ? 1 : 0;
        return true;
    }

    Vector3 AdvanceAnchorAlongPath(float distance)
    {
        Vector3 current = transform.position;
        distance = Mathf.Max(0f, distance);

        while (distance > 0f && pathCornerIndex < pathCorners.Count)
        {
            Vector3 target = pathCorners[pathCornerIndex];
            target.y = current.y;

            Vector3 toTarget = target - current;
            toTarget.y = 0f;

            float remaining = toTarget.magnitude;

            if (remaining <= 0.01f)
            {
                pathCornerIndex++;
                continue;
            }

            float step = Mathf.Min(distance, remaining);
            current += toTarget.normalized * step;
            distance -= step;

            if (remaining - step <= 0.01f)
                pathCornerIndex++;
        }

        return current;
    }

    Vector3 GetCurrentPathDirection()
    {
        return GetCurrentPathDirectionFrom(transform.position);
    }

    Vector3 GetCurrentPathDirectionFrom(Vector3 position)
    {
        Vector3 target = finalDestination;

        if (pathCornerIndex >= 0 && pathCornerIndex < pathCorners.Count)
            target = pathCorners[pathCornerIndex];

        Vector3 direction = target - position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = finalDestination - position;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.0001f)
            return desiredFacing;

        return direction.normalized;
    }

    Vector3 ResolveMovementFacing(
        Vector3 anchorPosition,
        Vector3 pathDirection,
        bool anchorAtDestination)
    {
        if (anchorAtDestination)
            return finalFacing;

        float distanceToDestination = Vector3.Distance(
            Flatten(anchorPosition),
            Flatten(finalDestination));

        if (distanceToDestination <= movementProfile.formedFinalFacingDistance)
            return finalFacing;

        if (pathDirection != Vector3.zero)
            return pathDirection;

        return desiredFacing;
    }

    Vector3 RotateFacingTowards(
        Vector3 current,
        Vector3 target,
        float maxDegrees)
    {
        current = NormalizeFacing(current);
        target = NormalizeFacing(target);

        if (current == Vector3.zero)
            return target;

        if (target == Vector3.zero)
            return current;

        return Vector3.RotateTowards(
            current,
            target,
            Mathf.Deg2Rad * Mathf.Max(0f, maxDegrees),
            0f).normalized;
    }

    bool HasAnchorReachedDestination()
    {
        return HasAnchorReachedDestinationFrom(transform.position);
    }

    bool HasAnchorReachedDestinationFrom(Vector3 anchorPosition)
    {
        bool pathComplete = pathCornerIndex >= pathCorners.Count;

        float distanceToDestination = Vector3.Distance(
            Flatten(anchorPosition),
            Flatten(finalDestination));

        return pathComplete || distanceToDestination <= movementProfile.formedAnchorArrivalDistance;
    }

    bool CanFormationFitAt(
        Vector3 anchorPosition,
        Vector3 anchorFacing)
    {
        if (!movementProfile.useFootprintValidation)
            return true;

        List<Vector3> probes = BuildFootprintProbes(anchorPosition, anchorFacing);

        foreach (Vector3 probe in probes)
        {
            if (!NavMesh.SamplePosition(
                    probe,
                    out NavMeshHit navHit,
                    movementProfile.footprintProbeRadius,
                    NavMesh.AllAreas))
            {
                return false;
            }

            if (Vector3.Distance(Flatten(probe), Flatten(navHit.position)) > movementProfile.footprintMaxNavMeshProjection)
                return false;

            if (movementProfile.footprintObstacleLayers.value != 0 &&
                Physics.CheckSphere(
                    probe,
                    movementProfile.footprintObstacleProbeRadius,
                    movementProfile.footprintObstacleLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }
        }

        return true;
    }

    List<Vector3> BuildFootprintProbes(
        Vector3 anchorPosition,
        Vector3 anchorFacing)
    {
        List<Vector3> probes = new List<Vector3>();

        probes.Add(anchorPosition);

        List<Vector3> slots = formation.GetWorldSlots(anchorPosition, anchorFacing);

        for (int i = 0; i < slots.Count; i++)
            probes.Add(slots[i]);

        AddFootprintCornerProbes(
            probes,
            anchorPosition,
            anchorFacing);

        return probes;
    }

    void AddFootprintCornerProbes(
        List<Vector3> probes,
        Vector3 anchorPosition,
        Vector3 anchorFacing)
    {
        IReadOnlyList<Vector2> localOffsets = formation.LocalOffsets;

        if (localOffsets == null || localOffsets.Count == 0)
            return;

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        foreach (Vector2 offset in localOffsets)
        {
            if (offset.x < minX) minX = offset.x;
            if (offset.x > maxX) maxX = offset.x;
            if (offset.y < minY) minY = offset.y;
            if (offset.y > maxY) maxY = offset.y;
        }

        float padding = Mathf.Max(0.1f, movementProfile.footprintProbeRadius);
        minX -= padding;
        maxX += padding;
        minY -= padding;
        maxY += padding;

        probes.Add(LocalOffsetToWorld(anchorPosition, anchorFacing, new Vector2(minX, minY)));
        probes.Add(LocalOffsetToWorld(anchorPosition, anchorFacing, new Vector2(minX, maxY)));
        probes.Add(LocalOffsetToWorld(anchorPosition, anchorFacing, new Vector2(maxX, minY)));
        probes.Add(LocalOffsetToWorld(anchorPosition, anchorFacing, new Vector2(maxX, maxY)));

        probes.Add(LocalOffsetToWorld(anchorPosition, anchorFacing, new Vector2(minX, 0f)));
        probes.Add(LocalOffsetToWorld(anchorPosition, anchorFacing, new Vector2(maxX, 0f)));
        probes.Add(LocalOffsetToWorld(anchorPosition, anchorFacing, new Vector2(0f, minY)));
        probes.Add(LocalOffsetToWorld(anchorPosition, anchorFacing, new Vector2(0f, maxY)));
    }

    Vector3 LocalOffsetToWorld(
        Vector3 anchorPosition,
        Vector3 anchorFacing,
        Vector2 localOffset)
    {
        anchorFacing = NormalizeFacing(anchorFacing);
        Vector3 right = new Vector3(anchorFacing.z, 0f, -anchorFacing.x).normalized;

        return anchorPosition +
               right * localOffset.x +
               anchorFacing * localOffset.y;
    }

    bool ShouldEmergencyPauseAnchor(IReadOnlyList<Vector3> slots)
    {
        GetSlotErrorCounts(
            slots,
            movementProfile.formedAnchorPauseDistance,
            out int living,
            out int overDistance);

        if (living <= 0)
            return false;

        float ratio = (float)overDistance / living;
        return ratio >= movementProfile.formedAnchorPauseRatio;
    }

    void GetSlotErrorCounts(
        IReadOnlyList<Vector3> slots,
        float distanceThreshold,
        out int living,
        out int overDistance)
    {
        living = 0;
        overDistance = 0;

        if (roster == null || slots == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            int slotIndex = soldier.SlotIndex;

            if (slotIndex < 0 || slotIndex >= slots.Count)
                continue;

            living++;

            if (Vector3.Distance(soldier.transform.position, slots[slotIndex]) > distanceThreshold)
                overDistance++;
        }
    }


    float GetFormedCatchupSpeedMultiplier(float slotError)
    {
        return GetCatchupMultiplier(slotError);
    }

    Vector3 EstimateAnchorFromSoldiers(Vector3 anchorFacing)
    {
        if (roster == null || formation == null)
            return Vector3.zero;

        IReadOnlyList<Vector2> localOffsets = formation.LocalOffsets;

        if (localOffsets == null || localOffsets.Count == 0)
            return Vector3.zero;

        anchorFacing = NormalizeFacing(anchorFacing);
        Vector3 right = new Vector3(anchorFacing.z, 0f, -anchorFacing.x).normalized;

        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            int slotIndex = soldier.SlotIndex;

            if (slotIndex < 0 || slotIndex >= localOffsets.Count)
                continue;

            Vector2 offset = localOffsets[slotIndex];
            Vector3 estimatedAnchor =
                soldier.transform.position -
                right * offset.x -
                anchorFacing * offset.y;

            sum += estimatedAnchor;
            count++;
        }

        if (count == 0)
            return Vector3.zero;

        return sum / count;
    }

    void MoveSoldiersByFormationSlots(
        IReadOnlyList<Vector3> oldSlots,
        IReadOnlyList<Vector3> newSlots,
        Vector3 facing)
    {
        if (roster == null || oldSlots == null || newSlots == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive || soldier.IsMovementLocked)
                continue;

            int slotIndex = soldier.SlotIndex;

            if (slotIndex < 0 || slotIndex >= oldSlots.Count || slotIndex >= newSlots.Count)
                continue;

            Vector3 slotDelta = newSlots[slotIndex] - oldSlots[slotIndex];
            slotDelta.y = 0f;

            Vector3 slotError = newSlots[slotIndex] - soldier.transform.position;
            slotError.y = 0f;

            float slotErrorDistance = slotError.magnitude;
            float catchupMultiplier = GetFormedCatchupSpeedMultiplier(slotErrorDistance);
            float speedLimit = Mathf.Max(0.1f, memberBaseMoveSpeed * catchupMultiplier);

            // Correction is the steering force back into the assigned slot.
            // Catchup raises the soldier speed budget; correction itself remains
            // a predictable profile value.
            float maxCorrectionThisFrame = movementProfile.formedSlotCorrectionSpeed * Time.deltaTime;

            Vector3 correction = Vector3.ClampMagnitude(
                slotError,
                maxCorrectionThisFrame);

            Vector3 movementDelta = slotDelta + correction;

            soldier.Motor.MoveByFormationDelta(
                movementDelta,
                facing,
                speedLimit);

            soldier.SetLastSlotPosition(newSlots[slotIndex]);
        }
    }

    void MoveSoldiersIndividuallyToSlots(
        IReadOnlyList<Vector3> slots,
        float stoppingDistance)
    {
        if (roster == null || slots == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null || !soldier.IsAlive)
                continue;

            int slotIndex = soldier.SlotIndex;

            if (slotIndex < 0 || slotIndex >= slots.Count)
                continue;

            float distance = Vector3.Distance(
                soldier.transform.position,
                slots[slotIndex]);

            float speedMultiplier = GetCatchupMultiplier(distance);

            soldier.MoveToPoint(
                slots[slotIndex],
                stoppingDistance,
                speedMultiplier);

            soldier.SetLastSlotPosition(slots[slotIndex]);
        }
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

    /// Moves each living soldier toward its assigned formation slot using normal
    /// individual NavMeshAgent destinations. Used by idle/reforming/hold behavior.
    void UpdateSoldiersToCurrentSlots()
    {
        if (!HasMovementProfile())
            return;

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
                movementProfile.slotUpdateThreshold,
                movementProfile.memberStoppingDistance,
                speedMultiplier);
        }
    }

    void UpdateSquadCenterFromSoldiers()
    {
        Vector3 center = GetAverageLivingSoldierPosition();

        if (center == Vector3.zero)
            return;

        MoveRootToProjectedPoint(center);
    }

    bool EnoughSoldiersNearCurrentSlots()
    {
        IReadOnlyList<Vector3> slots = formation.CurrentSlots;
        return EnoughSoldiersNearSlots(
            slots,
            movementProfile.reformMemberDistance,
            movementProfile.reformRatioRequired);
    }

    bool EnoughSoldiersNearSlots(
        IReadOnlyList<Vector3> slots,
        float distance,
        float requiredRatio)
    {
        if (slots == null || slots.Count == 0)
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

            if (Vector3.Distance(
                    soldier.transform.position,
                    slots[slotIndex]) <= distance)
            {
                near++;
            }
        }

        if (living == 0)
            return true;

        float ratio = (float)near / living;
        return ratio >= requiredRatio;
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
        if (distance <= movementProfile.catchupStartDistance)
            return 1f;

        float t = Mathf.InverseLerp(
            movementProfile.catchupStartDistance,
            movementProfile.catchupMaxDistance,
            distance);

        return Mathf.Lerp(
            1f,
            movementProfile.catchupMaxMultiplier,
            t);
    }

    float ResolveMemberBaseMoveSpeed()
    {
        if (data != null &&
            data.soldierData != null &&
            data.soldierData.movement.moveSpeed > 0f)
        {
            return data.soldierData.movement.moveSpeed;
        }

        return baseAnchorMoveSpeed > 0f ? baseAnchorMoveSpeed : 4f;
    }

    float ResolveEffectiveAnchorMoveSpeed()
    {
        float memberLimitedSpeed = memberBaseMoveSpeed * movementProfile.maxAnchorToMemberSpeedRatio;
        return Mathf.Min(
            Mathf.Max(0.1f, baseAnchorMoveSpeed),
            Mathf.Max(0.1f, memberLimitedSpeed));
    }

    void MoveRootToProjectedPoint(Vector3 point)
    {
        transform.position = ProjectPointToNavMesh(
            point,
            transform.position);
    }

    void MoveRootTowardProjectedPoint(
        Vector3 point,
        float maxDistance)
    {
        Vector3 projectedPoint = ProjectPointToNavMesh(
            point,
            transform.position);

        projectedPoint.y = transform.position.y;

        Vector3 nextPosition = Vector3.MoveTowards(
            transform.position,
            projectedPoint,
            Mathf.Max(0f, maxDistance));

        transform.position = ProjectPointToNavMesh(
            nextPosition,
            transform.position);
    }

    Vector3 ProjectPointToNavMesh(
        Vector3 point,
        Vector3 fallback)
    {
        if (NavMesh.SamplePosition(
                point,
                out NavMeshHit hit,
                2f,
                NavMesh.AllAreas))
        {
            return hit.position;
        }

        return fallback;
    }

    Vector3 NormalizeFacing(Vector3 value)
    {
        value.y = 0f;

        if (value == Vector3.zero)
            return Vector3.forward;

        return value.normalized;
    }

    Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    List<Vector3> CopyCurrentSlots()
    {
        List<Vector3> copy = new List<Vector3>();

        foreach (Vector3 slot in formation.CurrentSlots)
            copy.Add(slot);

        return copy;
    }
    
    /// Returns true when a new travel/final facing is so far behind the current
    /// frame that wheeling the whole slot grid would make soldiers cross through
    /// each other. In that case we remap nearest slots before movement starts.
    bool IsSharpTurnFromCurrentFacing(Vector3 newFacing)
    {
        if (!movementProfile.reassignSlotsOnLargeFacingChange)
            return false;

        Vector3 oldFacing = desiredFacing;
        oldFacing.y = 0f;
        newFacing.y = 0f;

        if (oldFacing == Vector3.zero || newFacing == Vector3.zero)
            return false;

        float threshold = movementProfile.reassignFacingAngle;

        float angle = Vector3.Angle(
            oldFacing.normalized,
            newFacing.normalized);

        return angle >= threshold;
    }
    
    /// Holds non-excluded soldiers in their assigned formation slots.
    /// Excluded soldiers are usually autonomous combat soldiers.
    public void HoldFormationSlotsExcept(
        HashSet<SoldierController> excludedSoldiers)
    {
        if (!HasMovementProfile())
            return;

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
                movementProfile.slotUpdateThreshold,
                movementProfile.memberStoppingDistance,
                speedMultiplier);
        }
    }
}
