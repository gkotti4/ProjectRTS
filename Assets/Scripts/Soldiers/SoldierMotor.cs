using UnityEngine;
using UnityEngine.AI;


/// -----------------------------------------------------------------------------
/// SoldierMotor
/// -----------------------------------------------------------------------------
///
/// Movement wrapper around the soldier NavMeshAgent.
/// Provides MoveTo, Stop, Warp, speed setup, formation-delta movement, and
/// velocity/facing-based rotation.
/// Respects SoldierController movement locks so committed actions like Attack,
/// HitReact, Death, and temporary combat-lock withdrawal delays cannot be
/// overridden by normal movement requests.
///
/// This class should not decide why the soldier moves. It only executes movement
/// requests when movement is allowed.
///
/// Design role:
/// Low-level soldier movement execution.
///
[RequireComponent(typeof(NavMeshAgent))]
public class SoldierMotor : MonoBehaviour
{
    [Header("Avoidance")]
    [SerializeField] private float agentRadius = 0.4f;
    [SerializeField] private float agentHeight = 2f;
    [SerializeField] private ObstacleAvoidanceType obstacleAvoidanceType =
        ObstacleAvoidanceType.HighQualityObstacleAvoidance;

    [SerializeField] private float defaultStoppingDistance = 0.1f;

    [SerializeField] private int avoidancePriorityMin = 40;
    [SerializeField] private int avoidancePriorityMax = 60;

    [Header("Custom Body")]
    [Tooltip("Relative body mass used for collision correction and received impulses. One impulse unit applied to one mass unit produces one meter-per-second of initial push velocity.")]
    [Min(0.01f)]
    [SerializeField] private float bodyMass = 1f;

    // -----------------------------------------------------------------------------
    // Body Collision Master Tuning
    // -----------------------------------------------------------------------------
    // NavMeshAgent remains locomotion authority. The custom body layer is split into
    // three searchable systems: HardBodyConstraint, SoftBodyPressure, and
    // DirectionalBodyBlocking.
    private const bool bodyCollisionEnabled = true;
    private const int bodyCollisionMaximumNeighbors = 16;

    // -----------------------------------------------------------------------------
    // Hard Body Constraint Tuning
    // -----------------------------------------------------------------------------
    // Enforces a near-nonpenetrating minimum body distance through positional
    // projection. This runs even while soldiers are idle or action-locked because
    // action locks stop voluntary movement, not physical body separation.
    private const bool hardBodyConstraintEnabled = true; // Master toggle for positional overlap correction between soldier bodies.
    private const int hardBodyConstraintSolverIterations = 2; // Number of overlap-resolution passes performed each frame; higher values settle dense crowds more firmly but cost more and may increase jitter.
    private const float hardBodyConstraintFriendlyMinimumDistance = 0.70f; // Minimum allowed center-to-center distance between friendly soldiers before positional separation is applied.
    private const float hardBodyConstraintEnemyMinimumDistance = 0.80f; // Minimum allowed center-to-center distance between enemy soldiers; slightly larger to create a firmer opposing frontline.
    private const float hardBodyConstraintProjectionPercent = 0.90f; // Fraction of detected penetration removed during each solver pass; 1.0 removes the full overlap immediately.
    private const float hardBodyConstraintMaximumProjectionPerIteration = 0.20f; // Maximum distance one soldier may be moved by a single solver pass, preventing large visible pops from deep overlaps.
    private const float hardBodyConstraintMinimumMassShare = 0.15f; // Minimum portion of the separation assigned to either body, preventing extreme mass differences from making one soldier absorb nearly all correction.


    // -----------------------------------------------------------------------------
    // Soft Body Pressure Tuning
    // -----------------------------------------------------------------------------
    // Adds compressible crowd feel outside the hard minimum distance. Unlike the
    // hard constraint, this remains a velocity-like cosmetic correction.

    private const bool softBodyPressureEnabled = true; // Master toggle for the softer spacing force outside the hard body radius.
    private const float softBodyPressurePreferredDistance = 0.88f; // Preferred center-to-center spacing; soldiers inside this distance receive gradual outward pressure until they reach the hard minimum.
    private const float softBodyPressureStrength = 1.35f; // Scales how strongly soft overlap is converted into outward correction speed.
    private const float softBodyPressureMaximumCorrectionSpeed = 1.15f; // Maximum soft-pressure movement speed, preventing compressed groups from rapidly exploding apart.
    private const float softBodyPressureFriendlyMultiplier = 0.70f; // Multiplier applied to soft pressure between friendly soldiers; lower values allow friendlies to compress and flow more easily.
    private const float softBodyPressureEnemyMultiplier = 1.20f; // Multiplier applied to soft pressure between enemies; higher values make opposing bodies resist interpenetration more strongly.
    private const float softBodyPressureFrontlineMultiplier = 0.30f; // Additional multiplier for active frontline soldiers during combat, reducing sideways drifting while they are fighting.


    // -----------------------------------------------------------------------------
    // Directional Body Blocking Tuning
    // -----------------------------------------------------------------------------
    // Clips only the inward portion of requested movement while preserving tangent
    // movement, allowing soldiers to slide around shoulders and exposed edges.
    private const bool directionalBodyBlockingEnabled = true; // Master toggle for removing movement that would push directly through another soldier.
    private const float directionalBodyBlockingFriendlyStrength = 0.45f; // Fraction of inward movement blocked against friendly soldiers; lower values allow more shoulder sliding and formation flow.
    private const float directionalBodyBlockingEnemyStrength = 1.00f; // Fraction of inward movement blocked against enemies; 1.0 prevents voluntary movement from advancing through an opposing body.

    // -----------------------------------------------------------------------------
    // Prototype External Impulse MVP Tuning
    // -----------------------------------------------------------------------------
    // Impulse is authored as mass * velocity. The receiver converts it to initial
    // push velocity with receivedPushSpeed = impulseMagnitude / BodyMass, then the
    // stored velocity decays over the requested duration. All push movement still
    // passes through the same directional body blocking used by normal locomotion.
    private const float prototypeImpulseDefaultDuration = 0.15f;               // Default time over which received push velocity decays to zero.
    private const float prototypeImpulseMaximumPushSpeed = 20.0f;                // Safety cap for accumulated push velocity. // WAS: 8.0f
    private const float prototypeImpulseMinimumPushSpeed = 0.03f;               // Velocities below this are cleared to avoid tiny endless movement.

    // -----------------------------------------------------------------------------
    // Prototype Diminishing Impulse Transfer MVP Tuning
    // -----------------------------------------------------------------------------
    // A pushed soldier may pass momentum through a short body chain. Each hop only
    // receives a fraction of the previous body's remaining push, and the chain has a
    // strict depth cap. This creates visible formation compression without allowing
    // one hit to recursively move an unlimited number of soldiers.
    private const bool prototypeImpulseTransferEnabled = true;                  // Enables diminishing push transfer through directly contacted living soldiers.
    private const int prototypeImpulseTransferMaximumHops = 3;                  // Maximum number of secondary bodies reached after the original pushed soldier.
    private const float prototypeImpulseTransferRatio = 0.50f;                  // Each hop receives half of the current body's push momentum.
    private const float prototypeImpulseTransferMinimumSpeed = 0.50f;            // Stops weak tail-end pushes before they create invisible micro chains.
    private const float prototypeImpulseTransferContactDistance = 0.92f;         // Maximum center distance for considering another soldier a direct push contact.
    private const float prototypeImpulseTransferMinimumForwardDot = 0.55f;       // Requires the contacted soldier to be meaningfully ahead of the push direction.
    private const float prototypeImpulseTransferDuration = 0.10f;                // Short secondary decay keeps transferred movement subtle.
    private const float prototypeImpulseTransferCooldown = 0.08f;                // Prevents repeated transfer checks from behaving like a continuous force hose.

    private NavMeshAgent agent;
    private SoldierController soldierController;
    
    private int bodyQueryLayerMask = ~0; // Unit Layer Mask

    private float baseMoveSpeed = 4f;
    private float turnSpeed = 900f;
    private float velocityRotationSuppressedUntil = 0f;

    private Vector3 prototypeExternalPushVelocity = Vector3.zero;
    private float prototypeExternalPushTimeRemaining = 0f;
    private int prototypeExternalImpulseTransferHopsRemaining = 0;
    private bool prototypeExternalImpulseHasTransferred = false;
    private float prototypeImpulseTransferCooldownRemaining = 0f;

    private readonly Collider[] prototypeBodyOverlapBuffer =
        new Collider[bodyCollisionMaximumNeighbors];

    // Formation movement uses NavMeshAgent.Move rather than SetDestination.
    // NavMeshAgent.Move does not create a path, so we cache a short-lived manual
    // velocity for animation and movement-state queries.
    private Vector3 manualMovementVelocity = Vector3.zero;
    private float manualMovementVelocityValidUntil = 0f;

    private bool HasManualMovementVelocity =>
        Time.time <= manualMovementVelocityValidUntil &&
        manualMovementVelocity.sqrMagnitude > 0.0001f;

    public NavMeshAgent Agent => agent;
    public bool HasPath => agent != null && (agent.hasPath || HasManualMovementVelocity);
    public float BodyMass => Mathf.Max(0.01f, bodyMass);
    public Vector3 ExternalPushVelocity => prototypeExternalPushVelocity;
    public bool IsBeingPushed => prototypeExternalPushTimeRemaining > 0f &&
                                 prototypeExternalPushVelocity.sqrMagnitude > 0.0001f;

    public Vector3 Velocity
    {
        get
        {
            if (HasManualMovementVelocity)
                return manualMovementVelocity;

            return agent != null ? agent.velocity : Vector3.zero;
        }
    }
    
    public float CurrentMoveSpeedLimit =>
        agent != null
            ? Mathf.Max(0.001f, agent.speed)
            : Mathf.Max(0.001f, baseMoveSpeed); // used in SoldierAnimator for calculating speed

    void OnValidate()
    {
        bodyMass = Mathf.Max(0.01f, bodyMass);
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        soldierController = GetComponent<SoldierController>();

        bodyQueryLayerMask =
            GameLayers.Instance != null
                ? GameLayers.Instance.UnitLayer.value
                : ~0;

        agent.radius = agentRadius;
        agent.height = agentHeight;
        agent.obstacleAvoidanceType = obstacleAvoidanceType;
        agent.avoidancePriority = Random.Range(
            avoidancePriorityMin,
            avoidancePriorityMax + 1);

        agent.angularSpeed = 99999f;
        agent.acceleration = 99999f;
        agent.autoBraking = false;
        agent.updateRotation = false;
    }

    void Update()
    {
        TickPrototypeExternalImpulse();
        TickDirectionalBodyBlocking();
        TickBodyCollision();
        RotateTowardVelocity();
    }


    #region Custom Body Collision

    void TickBodyCollision()
    {
        if (!bodyCollisionEnabled)
            return;

        if (!CanMove())
            return;

        if (soldierController == null || !soldierController.IsAlive)
            return;

        if (hardBodyConstraintEnabled)
        {
            int iterationCount = Mathf.Max(1, hardBodyConstraintSolverIterations);

            for (int iteration = 0; iteration < iterationCount; iteration++)
                TickHardBodyConstraints();
        }

        if (softBodyPressureEnabled)
            TickSoftBodyPressure(); 
    }

    /// Enforces the minimum body radius through positional projection. This does
    /// not check voluntary movement locks, so idle, attacking, and hit-reacting
    /// soldiers still behave as physical bodies.
    void TickHardBodyConstraints()
    {
        float queryRadius = Mathf.Max(
            hardBodyConstraintFriendlyMinimumDistance,
            hardBodyConstraintEnemyMinimumDistance);

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            queryRadius,
            prototypeBodyOverlapBuffer,
            bodyQueryLayerMask,
            QueryTriggerInteraction.Collide);

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hit = prototypeBodyOverlapBuffer[hitIndex];

            if (hit == null)
                continue;

            SoldierController otherSoldier =
                hit.GetComponentInParent<SoldierController>(); // PERFORMANCE

            if (!IsValidBodyNeighbor(otherSoldier))
                continue;

            if (WasBodyNeighborAlreadyCounted(hitIndex, otherSoldier))
                continue;

            if (!OwnsBodyPair(otherSoldier))
                continue;

            ResolveHardBodyConstraintPair(otherSoldier);
        }
    }

    void ResolveHardBodyConstraintPair(SoldierController otherSoldier)
    {
        if (otherSoldier == null || otherSoldier.Motor == null)
            return;

        Vector3 awayFromOther =
            transform.position - otherSoldier.transform.position;

        awayFromOther.y = 0f;

        float distance = awayFromOther.magnitude;
        bool isFriendly = IsBodyFriendly(otherSoldier);
        float minimumDistance = isFriendly
            ? hardBodyConstraintFriendlyMinimumDistance
            : hardBodyConstraintEnemyMinimumDistance;

        if (distance >= minimumDistance)
            return;

        if (distance <= 0.0001f)
        {
            awayFromOther = GetBodyFallbackDirection(otherSoldier);
            distance = 0f;
        }
        else
        {
            awayFromOther /= distance;
        }

        float penetration = minimumDistance - distance;
        float projectedDistance = Mathf.Min(
            penetration * Mathf.Clamp01(hardBodyConstraintProjectionPercent),
            hardBodyConstraintMaximumProjectionPerIteration);

        if (projectedDistance <= 0f)
            return;

        ResolveBodyMassShares(
            otherSoldier.Motor,
            out float ownShare,
            out float otherShare);

        Vector3 pairProjection = awayFromOther * projectedDistance;

        ApplyHardBodyConstraintProjection(pairProjection * ownShare);
        otherSoldier.Motor.ApplyHardBodyConstraintProjection(
            -pairProjection * otherShare);
    }

    void ApplyHardBodyConstraintProjection(Vector3 projectionDelta)
    {
        if (!bodyCollisionEnabled || !hardBodyConstraintEnabled)
            return;

        if (!CanMove())
            return;

        if (soldierController == null || !soldierController.IsAlive)
            return;

        projectionDelta.y = 0f;

        if (projectionDelta.sqrMagnitude <= 0.000001f)
            return;

        agent.Move(projectionDelta);
        CacheManualMovementVelocity(projectionDelta);
    }

    void TickSoftBodyPressure()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            softBodyPressurePreferredDistance,
            prototypeBodyOverlapBuffer,
            bodyQueryLayerMask,
            QueryTriggerInteraction.Collide); // needed performance

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hit = prototypeBodyOverlapBuffer[hitIndex];

            if (hit == null)
                continue;

            SoldierController otherSoldier =
                hit.GetComponentInParent<SoldierController>(); // PERFORMANCE

            if (!IsValidBodyNeighbor(otherSoldier)) // slight performance
                continue;

            if (WasBodyNeighborAlreadyCounted(hitIndex, otherSoldier))
                continue;

            if (!OwnsBodyPair(otherSoldier))
                continue;

            ResolveSoftBodyPressurePair(otherSoldier);
        }
    }

    void ResolveSoftBodyPressurePair(SoldierController otherSoldier)
    {
        if (otherSoldier == null || otherSoldier.Motor == null)
            return;

        Vector3 awayFromOther =
            transform.position - otherSoldier.transform.position;

        awayFromOther.y = 0f;

        float distance = awayFromOther.magnitude;

        if (distance >= softBodyPressurePreferredDistance)
            return;

        if (distance <= 0.0001f)
            awayFromOther = GetBodyFallbackDirection(otherSoldier);
        else
            awayFromOther /= distance;

        bool isFriendly = IsBodyFriendly(otherSoldier);
        float minimumDistance = isFriendly
            ? hardBodyConstraintFriendlyMinimumDistance
            : hardBodyConstraintEnemyMinimumDistance;

        // The hard solver owns penetration. Soft pressure only owns the compressible
        // band between minimum distance and preferred distance.
        if (distance < minimumDistance)
            return;

        float overlap = softBodyPressurePreferredDistance - distance;
        float correctionSpeed = overlap * softBodyPressureStrength;

        correctionSpeed *= isFriendly
            ? softBodyPressureFriendlyMultiplier
            : softBodyPressureEnemyMultiplier;

        correctionSpeed = Mathf.Min(
            correctionSpeed,
            softBodyPressureMaximumCorrectionSpeed);

        if (correctionSpeed <= 0f)
            return;

        ResolveBodyMassShares(
            otherSoldier.Motor,
            out float ownShare,
            out float otherShare);

        Vector3 pairCorrectionDelta =
            awayFromOther * correctionSpeed * Time.deltaTime;

        ApplySoftBodyPressureCorrection(pairCorrectionDelta * ownShare);
        otherSoldier.Motor.ApplySoftBodyPressureCorrection(
            -pairCorrectionDelta * otherShare);
    }

    void ApplySoftBodyPressureCorrection(Vector3 correctionDelta)
    {
        if (!bodyCollisionEnabled || !softBodyPressureEnabled)
            return;

        if (!CanMove())
            return;

        if (soldierController == null || !soldierController.IsAlive)
            return;

        correctionDelta.y = 0f;

        if (correctionDelta.sqrMagnitude <= 0.000001f)
            return;

        if (soldierController.Role == SoldierRole.Frontline &&
            soldierController.Squad != null &&
            soldierController.Squad.State == SquadState.InCombat)
        {
            correctionDelta *= softBodyPressureFrontlineMultiplier;
        }

        agent.Move(correctionDelta);
        CacheManualMovementVelocity(correctionDelta);
    }

    /// Applies directional body resistance to normal SetDestination movement.
    /// NavMesh avoidance still chooses the desired velocity, but any portion that
    /// would move directly through a nearby soldier is removed while tangential
    /// movement is preserved. Formed movement uses the same resolver before its
    /// direct NavMeshAgent.Move call.
    void TickDirectionalBodyBlocking()
    {
        if (!bodyCollisionEnabled || !directionalBodyBlockingEnabled)
            return;

        if (!CanMove() || IsMovementRequestLocked())
            return;

        if (soldierController == null || !soldierController.IsAlive)
            return;

        if (!agent.hasPath || agent.pathPending || Time.deltaTime <= 0f)
            return;

        Vector3 requestedDelta = agent.desiredVelocity * Time.deltaTime;
        requestedDelta.y = 0f;

        if (requestedDelta.sqrMagnitude <= 0.000001f)
            return;

        Vector3 resolvedDelta = ResolveBodyBlockedMovementDelta(requestedDelta);
        Vector3 blockingAdjustment = resolvedDelta - requestedDelta;
        blockingAdjustment.y = 0f;

        if (blockingAdjustment.sqrMagnitude <= 0.000001f)
            return;

        agent.Move(blockingAdjustment);
    }

    Vector3 ResolveBodyBlockedMovementDelta(Vector3 requestedDelta)
    {
        if (!bodyCollisionEnabled || !directionalBodyBlockingEnabled)
            return requestedDelta;

        if (soldierController == null || !soldierController.IsAlive)
            return requestedDelta;

        requestedDelta.y = 0f;

        if (requestedDelta.sqrMagnitude <= 0.000001f)
            return requestedDelta;

        float queryRadius =
            softBodyPressurePreferredDistance + requestedDelta.magnitude;

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            queryRadius,
            prototypeBodyOverlapBuffer, // slight perf
            bodyQueryLayerMask, 
            QueryTriggerInteraction.Collide);

        Vector3 resolvedDelta = requestedDelta;

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hit = prototypeBodyOverlapBuffer[hitIndex];

            if (hit == null)
                continue;

            SoldierController otherSoldier =
                hit.GetComponentInParent<SoldierController>(); // PERFORMANCE

            if (!IsValidBodyNeighbor(otherSoldier)) // slight perf
                continue;

            if (WasBodyNeighborAlreadyCounted(hitIndex, otherSoldier))
                continue;

            Vector3 toOther =
                otherSoldier.transform.position - transform.position;

            toOther.y = 0f;

            float currentDistance = toOther.magnitude;

            if (currentDistance <= 0.0001f)
                continue;

            Vector3 contactNormal = toOther / currentDistance;
            float inwardDistance = Vector3.Dot(resolvedDelta, contactNormal);

            if (inwardDistance <= 0f)
                continue;

            bool isFriendly = IsBodyFriendly(otherSoldier);
            float minimumDistance = isFriendly
                ? hardBodyConstraintFriendlyMinimumDistance
                : hardBodyConstraintEnemyMinimumDistance;

            float allowedInwardDistance = Mathf.Max(
                0f,
                currentDistance - minimumDistance);

            if (inwardDistance <= allowedInwardDistance)
                continue;

            float blockedInwardDistance =
                inwardDistance - allowedInwardDistance;

            float blockingStrength = isFriendly
                ? directionalBodyBlockingFriendlyStrength
                : directionalBodyBlockingEnemyStrength;

            resolvedDelta -=
                contactNormal * (blockedInwardDistance * blockingStrength);
        }

        return resolvedDelta;
    }

    void ResolveBodyMassShares(
        SoldierMotor otherMotor,
        out float ownShare,
        out float otherShare)
    {
        float ownInverseMass = 1f / BodyMass;
        float otherInverseMass = otherMotor != null
            ? 1f / otherMotor.BodyMass
            : ownInverseMass;

        float totalInverseMass = ownInverseMass + otherInverseMass;

        ownShare = totalInverseMass > 0f
            ? ownInverseMass / totalInverseMass
            : 0.5f;

        ownShare = Mathf.Clamp(
            ownShare,
            hardBodyConstraintMinimumMassShare,
            1f - hardBodyConstraintMinimumMassShare);

        otherShare = 1f - ownShare;
    }

    void CacheManualMovementVelocity(Vector3 movementDelta)
    {
        if (agent.hasPath || Time.deltaTime <= 0f)
            return;

        manualMovementVelocity = movementDelta / Time.deltaTime;
        manualMovementVelocityValidUntil = Time.time + 0.12f;
    }
    

    bool IsValidBodyNeighbor(SoldierController otherSoldier) // slight perf due to calls
    {
        if (otherSoldier == null || otherSoldier == soldierController)
            return false;

        return otherSoldier.IsAlive;
    }

    bool WasBodyNeighborAlreadyCounted(
        int currentHitIndex,
        SoldierController candidateSoldier)
    {
        for (int previousHitIndex = 0;
             previousHitIndex < currentHitIndex;
             previousHitIndex++)
        {
            Collider previousHit =
                prototypeBodyOverlapBuffer[previousHitIndex];

            if (previousHit == null)
                continue;

            SoldierController previousSoldier =
                previousHit.GetComponentInParent<SoldierController>();

            if (previousSoldier == candidateSoldier)
                return true;
        }

        return false;
    }

    bool OwnsBodyPair(SoldierController otherSoldier)
    {
        if (otherSoldier == null)
            return false;

        return gameObject.GetInstanceID() <
               otherSoldier.gameObject.GetInstanceID();
    }

    bool IsBodyFriendly(SoldierController otherSoldier)
    {
        if (soldierController == null || otherSoldier == null)
            return false;

        if (soldierController.Faction == null || otherSoldier.Faction == null)
            return soldierController.Squad == otherSoldier.Squad;

        return soldierController.Faction.teamId ==
               otherSoldier.Faction.teamId;
    }

    Vector3 GetBodyFallbackDirection(SoldierController otherSoldier)
    {
        int ownId = gameObject.GetInstanceID();
        int otherId = otherSoldier != null
            ? otherSoldier.gameObject.GetInstanceID()
            : 0;

        return ownId <= otherId
            ? Vector3.right
            : Vector3.left;
    }

    #endregion

    #region Prototype External Impulse

    /// Applies an authored impulse that does not depend on another unit's movement.
    /// One impulse unit applied to one body-mass unit produces one meter-per-second
    /// of initial push velocity before duration-based decay and safety clamping.
    public void ApplyExternalImpulse(
        Vector3 direction,
        float impulseMagnitude,
        float duration = prototypeImpulseDefaultDuration,
        bool allowTransfer = true)
    {
        ApplyResolvedImpulse(
            direction,
            impulseMagnitude,
            duration,
            allowTransfer ? prototypeImpulseTransferMaximumHops : 0);
    }

    /// Calculates a body-driven impulse from the source motor's mass and closing
    /// speed, then feeds the exact same receiver pipeline as an external impulse.
    /// A sideways or stationary source contributes little or no body impulse.
    public void ApplyBodyImpulse(
        SoldierMotor sourceMotor,
        Vector3 direction,
        float impulseMultiplier = 1f,
        float duration = prototypeImpulseDefaultDuration)
    {
        if (sourceMotor == null)
            return;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Vector3 normalizedDirection = direction.normalized;
        Vector3 sourceVelocity = sourceMotor.Velocity;
        sourceVelocity.y = 0f;

        float closingSpeed = Mathf.Max(
            0f,
            Vector3.Dot(sourceVelocity, normalizedDirection));

        float resolvedImpulseMagnitude =
            sourceMotor.BodyMass *
            closingSpeed *
            Mathf.Max(0f, impulseMultiplier);

        ApplyResolvedImpulse(
            normalizedDirection,
            resolvedImpulseMagnitude,
            duration,
            prototypeImpulseTransferMaximumHops);
    }

    /// Clears all currently stored custom push velocity. Normal path/formation
    /// movement is not affected.
    public void ClearExternalImpulse()
    {
        prototypeExternalPushVelocity = Vector3.zero;
        prototypeExternalPushTimeRemaining = 0f;
        prototypeExternalImpulseTransferHopsRemaining = 0;
        prototypeExternalImpulseHasTransferred = false;
        prototypeImpulseTransferCooldownRemaining = 0f;
    }

    void ApplyResolvedImpulse(
        Vector3 direction,
        float impulseMagnitude,
        float duration,
        int transferHopsRemaining)
    {
        if (soldierController == null || !soldierController.IsAlive)
            return;

        direction.y = 0f;
        impulseMagnitude = Mathf.Max(0f, impulseMagnitude);
        duration = Mathf.Max(0.01f, duration);

        if (direction.sqrMagnitude <= 0.0001f || impulseMagnitude <= 0f)
            return;

        float receivedPushSpeed = impulseMagnitude / BodyMass;

        prototypeExternalPushVelocity +=
            direction.normalized * receivedPushSpeed;

        prototypeExternalPushVelocity = Vector3.ClampMagnitude(
            prototypeExternalPushVelocity,
            prototypeImpulseMaximumPushSpeed);

        prototypeExternalPushTimeRemaining = Mathf.Max(
            prototypeExternalPushTimeRemaining,
            duration);

        if (transferHopsRemaining > 0)
        {
            prototypeExternalImpulseTransferHopsRemaining = Mathf.Max(
                prototypeExternalImpulseTransferHopsRemaining,
                transferHopsRemaining);

            prototypeExternalImpulseHasTransferred = false;
        }
    }

    void TickPrototypeExternalImpulse()
    {
        if (!CanMove()) // check if this applies when attacking in combat and if we want this.
            return;

        if (soldierController == null || !soldierController.IsAlive)
        {
            ClearExternalImpulse();
            return;
        }

        if (prototypeExternalPushTimeRemaining <= 0f ||
            prototypeExternalPushVelocity.sqrMagnitude <=
            prototypeImpulseMinimumPushSpeed * prototypeImpulseMinimumPushSpeed)
        {
            ClearExternalImpulse();
            return;
        }

        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        Vector3 requestedPushDelta =
            prototypeExternalPushVelocity * deltaTime;

        requestedPushDelta.y = 0f;

        Vector3 resolvedPushDelta = ResolveBodyBlockedMovementDelta(
            requestedPushDelta);

        TickPrototypeImpulseTransferCooldown(deltaTime);
        TryTransferPrototypeImpulse(
            requestedPushDelta,
            resolvedPushDelta);

        if (resolvedPushDelta.sqrMagnitude > 0.000001f)
            agent.Move(resolvedPushDelta);

        manualMovementVelocity = resolvedPushDelta / deltaTime;
        manualMovementVelocityValidUntil = Time.time + 0.12f;

        float previousTimeRemaining = prototypeExternalPushTimeRemaining;
        prototypeExternalPushTimeRemaining = Mathf.Max(
            0f,
            prototypeExternalPushTimeRemaining - deltaTime);

        float decaySpeed = previousTimeRemaining > 0f
            ? prototypeExternalPushVelocity.magnitude / previousTimeRemaining
            : prototypeExternalPushVelocity.magnitude;

        prototypeExternalPushVelocity = Vector3.MoveTowards(
            prototypeExternalPushVelocity,
            Vector3.zero,
            decaySpeed * deltaTime);

        if (prototypeExternalPushTimeRemaining <= 0f ||
            prototypeExternalPushVelocity.sqrMagnitude <=
            prototypeImpulseMinimumPushSpeed * prototypeImpulseMinimumPushSpeed)
        {
            ClearExternalImpulse();
        }
    }


    void TickPrototypeImpulseTransferCooldown(float deltaTime)
    {
        if (prototypeImpulseTransferCooldownRemaining <= 0f)
            return;

        prototypeImpulseTransferCooldownRemaining = Mathf.Max(
            0f,
            prototypeImpulseTransferCooldownRemaining - deltaTime);
    }

    void TryTransferPrototypeImpulse(
        Vector3 requestedPushDelta,
        Vector3 resolvedPushDelta)
    {
        if (!prototypeImpulseTransferEnabled)
            return;

        if (prototypeExternalImpulseTransferHopsRemaining <= 0 ||
            prototypeExternalImpulseHasTransferred ||
            prototypeImpulseTransferCooldownRemaining > 0f)
        {
            return;
        }

        float currentPushSpeed = prototypeExternalPushVelocity.magnitude;

        if (currentPushSpeed < prototypeImpulseTransferMinimumSpeed)
            return;

        Vector3 blockedDelta = requestedPushDelta - resolvedPushDelta;
        blockedDelta.y = 0f;

        if (blockedDelta.sqrMagnitude <= 0.000001f)
            return;

        Vector3 pushDirection = prototypeExternalPushVelocity;
        pushDirection.y = 0f;

        if (pushDirection.sqrMagnitude <= 0.0001f)
            return;

        pushDirection.Normalize();

        if (!TryFindPrototypeImpulseTransferTarget(
                pushDirection,
                out SoldierController transferTarget,
                out float forwardContactFactor))
        {
            return;
        }

        float resolvedTransferRatio = Mathf.Clamp01(
            prototypeImpulseTransferRatio * forwardContactFactor);

        if (resolvedTransferRatio <= 0f)
            return;

        float transferredImpulseMagnitude =
            BodyMass *
            currentPushSpeed *
            resolvedTransferRatio;

        transferTarget.Motor.ApplyResolvedImpulse(
            pushDirection,
            transferredImpulseMagnitude,
            prototypeImpulseTransferDuration,
            prototypeExternalImpulseTransferHopsRemaining - 1);

        prototypeExternalPushVelocity *=
            Mathf.Max(0f, 1f - resolvedTransferRatio);

        prototypeExternalImpulseHasTransferred = true;
        prototypeExternalImpulseTransferHopsRemaining = 0;
        prototypeImpulseTransferCooldownRemaining =
            prototypeImpulseTransferCooldown;
    }

    bool TryFindPrototypeImpulseTransferTarget(
        Vector3 pushDirection,
        out SoldierController transferTarget,
        out float forwardContactFactor)
    {
        transferTarget = null;
        forwardContactFactor = 0f;

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            prototypeImpulseTransferContactDistance,
            prototypeBodyOverlapBuffer,
            bodyQueryLayerMask,
            QueryTriggerInteraction.Collide);

        float bestScore = float.NegativeInfinity;

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hit = prototypeBodyOverlapBuffer[hitIndex];

            if (hit == null)
                continue;

            SoldierController candidate =
                hit.GetComponentInParent<SoldierController>();

            if (!IsValidBodyNeighbor(candidate) ||
                candidate.Motor == null)
            {
                continue;
            }

            if (WasBodyNeighborAlreadyCounted(
                    hitIndex,
                    candidate))
            {
                continue;
            }

            Vector3 toCandidate =
                candidate.transform.position - transform.position;

            toCandidate.y = 0f;

            float distance = toCandidate.magnitude;

            if (distance <= 0.0001f ||
                distance > prototypeImpulseTransferContactDistance)
            {
                continue;
            }

            float forwardDot = Vector3.Dot(
                pushDirection,
                toCandidate / distance);

            if (forwardDot < prototypeImpulseTransferMinimumForwardDot)
                continue;

            float distanceScore = 1f - Mathf.Clamp01(
                distance / prototypeImpulseTransferContactDistance);

            float score = forwardDot + distanceScore * 0.35f;

            if (score <= bestScore)
                continue;

            bestScore = score;
            transferTarget = candidate;
            forwardContactFactor = Mathf.InverseLerp(
                prototypeImpulseTransferMinimumForwardDot,
                1f,
                forwardDot);
        }

        return transferTarget != null;
    }

    #endregion

    public void Initialize(MovementStats movement)
    {
        baseMoveSpeed = movement.moveSpeed > 0f ? movement.moveSpeed : baseMoveSpeed;
        turnSpeed = movement.turnSpeed > 0f ? movement.turnSpeed : turnSpeed;

        if (agent == null)
            return;

        agent.speed = baseMoveSpeed;
        agent.acceleration = movement.acceleration > 0f
            ? movement.acceleration
            : 99999f;
    }

    public void MoveTo(
        Vector3 position,
        float stoppingDistance = -1f,
        float speedMultiplier = 1f)
    {
        if (!CanMove())
            return;

        if (IsMovementRequestLocked())
            return;

        agent.isStopped = false;
        agent.speed = baseMoveSpeed * Mathf.Max(0.1f, speedMultiplier);
        agent.stoppingDistance = stoppingDistance >= 0f
            ? stoppingDistance
            : defaultStoppingDistance;

        agent.SetDestination(position);
    }

    /// Moves the soldier by a formation-controlled world delta instead of giving
    /// the soldier an independent path destination.
    ///
    /// This is used by SquadMovement.FormedMove so the squad can move as one
    /// shared formation body while each soldier remains a normal NavMeshAgent.
    ///
    /// Important:
    /// Formation facing and soldier visual facing are not always the same.
    /// During movement, the soldier should usually face its own movement delta.
    /// When not moving, callers can explicitly rotate the soldier toward final
    /// formation facing through FaceDirection().
    public void MoveByFormationDelta( // PERFORMANCE
        Vector3 movementDelta,
        Vector3 fallbackFacingDirection,
        float speedLimit = -1f,
        bool faceMovementDirection = true)
    {
        if (!CanMove()) // slight perf
            return;

        if (IsMovementRequestLocked()) // perf
            return;

        if (agent.hasPath)
            agent.ResetPath();

        float resolvedSpeedLimit = speedLimit > 0f
            ? speedLimit
            : baseMoveSpeed;

        agent.isStopped = false;
        agent.stoppingDistance = 0f;
        agent.speed = resolvedSpeedLimit;

        movementDelta.y = 0f;

        float maxDistanceThisFrame =
            Mathf.Max(0f, resolvedSpeedLimit) * Time.deltaTime;

        if (maxDistanceThisFrame > 0f &&
            movementDelta.magnitude > maxDistanceThisFrame)
        {
            movementDelta = movementDelta.normalized * maxDistanceThisFrame;
        }

        movementDelta = ResolveBodyBlockedMovementDelta( // PERFORMANCE
            movementDelta); 

        manualMovementVelocity = Time.deltaTime > 0f
            ? movementDelta / Time.deltaTime
            : Vector3.zero;

        manualMovementVelocityValidUntil = Time.time + 0.12f;

        if (movementDelta.sqrMagnitude > 0.000001f)
            agent.Move(movementDelta);

        Vector3 visualFacingDirection = fallbackFacingDirection;

        if (faceMovementDirection &&
            movementDelta.sqrMagnitude > 0.000001f)
        {
            visualFacingDirection = movementDelta;
        }

        RotateTowardDirection(visualFacingDirection); // PERFORMANCE 
    }
    
    /// Rotates the soldier toward a requested direction without issuing movement.
    /// Used when a soldier has reached its slot and should settle into the squad's
    /// final facing.
    public void FaceDirection(Vector3 direction)
    {
        if (!CanMove())
            return;

        if (IsMovementRequestLocked())
            return;

        RotateTowardDirection(direction);
    }

    public void Stop()
    {
        if (!CanMove())
            return;

        agent.ResetPath();
        agent.isStopped = false;
        agent.speed = baseMoveSpeed;

        manualMovementVelocity = Vector3.zero;
        manualMovementVelocityValidUntil = 0f;
    }

    public void Warp(Vector3 position)
    {
        ClearExternalImpulse();

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.Warp(position);
        else
            transform.position = position;
    }

    public void SetBaseSpeed(float speed)
    {
        baseMoveSpeed = Mathf.Max(0f, speed);

        if (agent != null)
            agent.speed = baseMoveSpeed;
    }

    public void SuppressVelocityRotation(float duration = 0.12f)
    {
        velocityRotationSuppressedUntil = Mathf.Max(
            velocityRotationSuppressedUntil,
            Time.time + Mathf.Max(0f, duration));
    }

    bool CanMove() // ~ PERFORMANCE
    {
        return agent != null &&
               agent.enabled && // perf  1/3
               agent.isActiveAndEnabled && // perf 1/3
               agent.isOnNavMesh; // perf 1/3
    }

    bool IsMovementRequestLocked()
    {
        return soldierController != null &&
               (soldierController.IsMovementLocked ||
                soldierController.IsCombatMoveLocked); // PERFORMANCE
    }

    void RotateTowardVelocity()
    {
        if (Time.time < velocityRotationSuppressedUntil)
            return;

        if (IsMovementRequestLocked()) // see performance
            return;

        if (agent == null)
            return;

        Vector3 velocity = Velocity; // slight performance
        velocity.y = 0f;

        if (velocity.sqrMagnitude < 0.01f)
            return;

        RotateTowardDirection(velocity.normalized);
    }

    void RotateTowardDirection(Vector3 direction) // Performance
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            direction.normalized,
            Vector3.up);

        transform.rotation = Quaternion.RotateTowards( // slight performance
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime);
    }
}

