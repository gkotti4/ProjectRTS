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
    // Prototype Infantry Body Pressure MVP Tuning
    // -----------------------------------------------------------------------------
    // NavMeshAgent remains the locomotion authority. This layer only modifies direct
    // formation deltas and applies pair-owned NavMeshAgent.Move corrections so living
    // soldiers behave like compressible bodies instead of freely passing through one
    // another. It is intentionally not Rigidbody physics and does not decide combat.
    private const bool prototypeBodyPressureEnabled = true;                    // Master toggle for the custom infantry body model.
    private const float prototypeBodyPreferredDistance = 0.88f;                // Soft pressure begins inside this center-to-center distance.
    private const float prototypeBodyFriendlyMinimumDistance = 0.64f;          // Friendlies may compress more tightly before direct inward movement is blocked.
    private const float prototypeBodyEnemyMinimumDistance = 0.78f;             // Enemies create a firmer body wall and resist direct penetration sooner.
    private const float prototypeBodySoftPressureStrength = 1.35f;              // Converts soft-zone overlap depth into separation velocity.
    private const float prototypeBodyHardCorrectionStrength = 7.0f;             // Converts minimum-distance penetration into a fast corrective velocity.
    private const float prototypeBodyMaximumCorrectionSpeed = 1.15f;            // Caps total body correction so contacts remain stable rather than snapping.
    private const float prototypeBodyFriendlyCorrectionMultiplier = 0.70f;      // Friendlies yield softly so formations can compress and flow.
    private const float prototypeBodyEnemyCorrectionMultiplier = 1.20f;         // Enemy contacts separate more firmly than friendly contacts.
    private const float prototypeBodyFrontlineSoftPressureMultiplier = 0.30f;   // Reduces cosmetic soft sliding for active melee frontlines.
    private const float prototypeBodyFriendlyInwardBlockingMultiplier = 0.45f;  // Friendlies resist direct inward movement partially so formations can still compress and pass.
    private const float prototypeBodyEnemyInwardBlockingMultiplier = 1.00f;     // Enemies remove all movement that would violate their minimum body distance.
    private const int prototypeBodyMaximumNeighbors = 16;                       // Maximum collider hits considered by one local body query.

    // -----------------------------------------------------------------------------
    // Prototype External Impulse MVP Tuning
    // -----------------------------------------------------------------------------
    // Impulse is authored as mass * velocity. The receiver converts it to initial
    // push velocity with receivedPushSpeed = impulseMagnitude / BodyMass, then the
    // stored velocity decays over the requested duration. All push movement still
    // passes through the same directional body blocking used by normal locomotion.
    private const float prototypeImpulseDefaultDuration = 0.15f;               // Default time over which received push velocity decays to zero.
    private const float prototypeImpulseMaximumPushSpeed = 8.0f;                // Safety cap for accumulated push velocity.
    private const float prototypeImpulseMinimumPushSpeed = 0.03f;               // Velocities below this are cleared to avoid tiny endless movement.

    // -----------------------------------------------------------------------------
    // Prototype Single-Hop Impulse Transfer MVP Tuning
    // -----------------------------------------------------------------------------
    // A directly pushed soldier may transfer part of that momentum into one body
    // blocking its push direction. The transferred impulse is explicitly marked as
    // non-transferable, preventing recursive chains through an entire formation.
    private const bool prototypeImpulseTransferEnabled = true;                  // Enables one-hop push transfer into a blocking living soldier.
    private const float prototypeImpulseTransferRatio = 0.25f;                  // Fraction of the current push momentum passed into the blocking body.
    private const float prototypeImpulseTransferMinimumSpeed = 0.75f;            // Minimum current push speed required before a secondary body can be affected.
    private const float prototypeImpulseTransferContactDistance = 0.92f;         // Maximum center distance for considering another soldier a direct push contact.
    private const float prototypeImpulseTransferMinimumForwardDot = 0.55f;       // Requires the contacted soldier to be meaningfully ahead of the push direction.
    private const float prototypeImpulseTransferDuration = 0.10f;                // Short secondary decay keeps transferred movement subtle.
    private const float prototypeImpulseTransferCooldown = 0.08f;                // Prevents repeated transfer checks from behaving like a continuous force hose.

    private NavMeshAgent agent;
    private SoldierController soldierController;

    private float baseMoveSpeed = 4f;
    private float turnSpeed = 900f;
    private float velocityRotationSuppressedUntil = 0f;

    private Vector3 prototypeExternalPushVelocity = Vector3.zero;
    private float prototypeExternalPushTimeRemaining = 0f;
    private bool prototypeExternalImpulseCanTransfer = false;
    private bool prototypeExternalImpulseHasTransferred = false;
    private float prototypeImpulseTransferCooldownRemaining = 0f;

    private readonly Collider[] prototypeBodyOverlapBuffer =
        new Collider[prototypeBodyMaximumNeighbors];

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
        TickPrototypePathDirectionalBlocking();
        TickPrototypeBodyPressure();
        RotateTowardVelocity();
    }


    #region Prototype Infantry Body Pressure


    /// Applies directional body resistance to normal SetDestination movement.
    /// NavMesh avoidance still chooses the desired velocity, but any portion that
    /// would move directly through a nearby soldier is removed while tangential
    /// movement is preserved. Formed movement uses the same resolver before its
    /// direct NavMeshAgent.Move call.
    void TickPrototypePathDirectionalBlocking()
    {
        if (!prototypeBodyPressureEnabled)
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

        Vector3 resolvedDelta = ResolvePrototypeBodyBlockedMovementDelta(
            requestedDelta);

        Vector3 blockingAdjustment = resolvedDelta - requestedDelta;
        blockingAdjustment.y = 0f;

        if (blockingAdjustment.sqrMagnitude <= 0.000001f)
            return;

        // The agent simulation supplies the requested path movement. This opposite
        // correction cancels only the blocked inward component, leaving lateral
        // steering intact so soldiers can slide around shoulders and exposed edges.
        agent.Move(blockingAdjustment);
    }

    void TickPrototypeBodyPressure()
    {
        if (!prototypeBodyPressureEnabled)
            return;

        if (!CanMove())
            return;

        if (soldierController == null || !soldierController.IsAlive)
            return;

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            prototypeBodyPreferredDistance,
            prototypeBodyOverlapBuffer,
            ~0,
            QueryTriggerInteraction.Collide);

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hit = prototypeBodyOverlapBuffer[hitIndex];

            if (hit == null)
                continue;

            SoldierController otherSoldier =
                hit.GetComponentInParent<SoldierController>();

            if (!IsValidPrototypeBodyNeighbor(otherSoldier))
                continue;

            if (WasPrototypeBodyNeighborAlreadyCounted(
                    hitIndex,
                    otherSoldier))
            {
                continue;
            }

            // Strict pair ownership prevents both SoldierMotors from resolving the
            // same contact and doubling the correction in one frame.
            if (!OwnsPrototypeBodyPair(otherSoldier))
                continue;

            ResolvePrototypeBodyPair(otherSoldier);
        }
    }

    void ResolvePrototypeBodyPair(SoldierController otherSoldier)
    {
        if (otherSoldier == null || otherSoldier.Motor == null)
            return;

        Vector3 awayFromOther =
            transform.position - otherSoldier.transform.position;

        awayFromOther.y = 0f;

        float distance = awayFromOther.magnitude;

        if (distance >= prototypeBodyPreferredDistance)
            return;

        if (distance <= 0.0001f)
        {
            awayFromOther = GetPrototypeBodyFallbackDirection(otherSoldier);
            distance = 0f;
        }
        else
        {
            awayFromOther /= distance;
        }

        bool isFriendly = IsPrototypeBodyFriendly(otherSoldier);
        float minimumDistance = isFriendly
            ? prototypeBodyFriendlyMinimumDistance
            : prototypeBodyEnemyMinimumDistance;

        float softOverlap =
            prototypeBodyPreferredDistance - distance;

        float correctionSpeed =
            softOverlap * prototypeBodySoftPressureStrength;

        if (distance < minimumDistance)
        {
            float hardPenetration = minimumDistance - distance;
            correctionSpeed +=
                hardPenetration * prototypeBodyHardCorrectionStrength;
        }

        correctionSpeed *= isFriendly
            ? prototypeBodyFriendlyCorrectionMultiplier
            : prototypeBodyEnemyCorrectionMultiplier;

        correctionSpeed = Mathf.Min(
            correctionSpeed,
            prototypeBodyMaximumCorrectionSpeed);

        if (correctionSpeed <= 0f)
            return;

        float ownInverseMass = 1f / BodyMass;
        float otherInverseMass = 1f / otherSoldier.Motor.BodyMass;
        float totalInverseMass = ownInverseMass + otherInverseMass;

        float ownShare = totalInverseMass > 0f
            ? ownInverseMass / totalInverseMass
            : 0.5f;

        float otherShare = totalInverseMass > 0f
            ? otherInverseMass / totalInverseMass
            : 0.5f;

        Vector3 pairCorrectionDelta =
            awayFromOther * correctionSpeed * Time.deltaTime;

        ApplyPrototypeBodyCorrection(
            pairCorrectionDelta * ownShare,
            isHardCorrection: distance < minimumDistance);

        otherSoldier.Motor.ApplyPrototypeBodyCorrection(
            -pairCorrectionDelta * otherShare,
            isHardCorrection: distance < minimumDistance);
    }

    void ApplyPrototypeBodyCorrection(
        Vector3 correctionDelta,
        bool isHardCorrection)
    {
        if (!prototypeBodyPressureEnabled)
            return;

        if (!CanMove())
            return;

        if (soldierController == null || !soldierController.IsAlive)
            return;

        correctionDelta.y = 0f;

        if (correctionDelta.sqrMagnitude <= 0.000001f)
            return;

        // Active melee soldiers retain only a small amount of cosmetic soft
        // separation, but hard minimum-distance correction always remains active.
        if (!isHardCorrection &&
            soldierController.Role == SoldierRole.Frontline &&
            soldierController.Squad != null &&
            soldierController.Squad.State == SquadState.InCombat)
        {
            correctionDelta *= prototypeBodyFrontlineSoftPressureMultiplier;
        }

        agent.Move(correctionDelta);

        if (!agent.hasPath && Time.deltaTime > 0f)
        {
            manualMovementVelocity = correctionDelta / Time.deltaTime;
            manualMovementVelocityValidUntil = Time.time + 0.12f;
        }
    }

    Vector3 ResolvePrototypeBodyBlockedMovementDelta(Vector3 requestedDelta)
    {
        if (!prototypeBodyPressureEnabled)
            return requestedDelta;

        if (soldierController == null || !soldierController.IsAlive)
            return requestedDelta;

        requestedDelta.y = 0f;

        if (requestedDelta.sqrMagnitude <= 0.000001f)
            return requestedDelta;

        float queryRadius =
            prototypeBodyPreferredDistance + requestedDelta.magnitude;

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            queryRadius,
            prototypeBodyOverlapBuffer,
            ~0,
            QueryTriggerInteraction.Collide);

        Vector3 resolvedDelta = requestedDelta;

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hit = prototypeBodyOverlapBuffer[hitIndex];

            if (hit == null)
                continue;

            SoldierController otherSoldier =
                hit.GetComponentInParent<SoldierController>();

            if (!IsValidPrototypeBodyNeighbor(otherSoldier))
                continue;

            if (WasPrototypeBodyNeighborAlreadyCounted(
                    hitIndex,
                    otherSoldier))
            {
                continue;
            }

            Vector3 toOther =
                otherSoldier.transform.position - transform.position;

            toOther.y = 0f;

            float currentDistance = toOther.magnitude;

            if (currentDistance <= 0.0001f)
                continue;

            Vector3 contactNormal = toOther / currentDistance;
            float inwardDistance = Vector3.Dot(
                resolvedDelta,
                contactNormal);

            if (inwardDistance <= 0f)
                continue;

            bool isFriendly = IsPrototypeBodyFriendly(otherSoldier);
            float minimumDistance = isFriendly
                ? prototypeBodyFriendlyMinimumDistance
                : prototypeBodyEnemyMinimumDistance;

            float allowedInwardDistance = Mathf.Max(
                0f,
                currentDistance - minimumDistance);

            if (inwardDistance <= allowedInwardDistance)
                continue;

            // Remove only the excess inward component. The perpendicular/tangent
            // component survives, allowing shoulder sliding and edge flow instead
            // of turning every contact into a complete stop.
            float blockedInwardDistance =
                inwardDistance - allowedInwardDistance;

            float blockingMultiplier = isFriendly
                ? prototypeBodyFriendlyInwardBlockingMultiplier
                : prototypeBodyEnemyInwardBlockingMultiplier;

            resolvedDelta -=
                contactNormal * (blockedInwardDistance * blockingMultiplier);
        }

        return resolvedDelta;
    }

    bool IsValidPrototypeBodyNeighbor(
        SoldierController otherSoldier)
    {
        if (otherSoldier == null || otherSoldier == soldierController)
            return false;

        return otherSoldier.IsAlive;
    }

    bool WasPrototypeBodyNeighborAlreadyCounted(
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

    bool OwnsPrototypeBodyPair(SoldierController otherSoldier)
    {
        if (otherSoldier == null)
            return false;

        return gameObject.GetInstanceID() <
               otherSoldier.gameObject.GetInstanceID();
    }

    bool IsPrototypeBodyFriendly(SoldierController otherSoldier)
    {
        if (soldierController == null || otherSoldier == null)
            return false;

        if (soldierController.Faction == null || otherSoldier.Faction == null)
            return soldierController.Squad == otherSoldier.Squad;

        return soldierController.Faction.teamId ==
               otherSoldier.Faction.teamId;
    }

    Vector3 GetPrototypeBodyFallbackDirection(
        SoldierController otherSoldier)
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
            allowTransfer);
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
            allowTransfer: true);
    }

    /// Clears all currently stored custom push velocity. Normal path/formation
    /// movement is not affected.
    public void ClearExternalImpulse()
    {
        prototypeExternalPushVelocity = Vector3.zero;
        prototypeExternalPushTimeRemaining = 0f;
        prototypeExternalImpulseCanTransfer = false;
        prototypeExternalImpulseHasTransferred = false;
        prototypeImpulseTransferCooldownRemaining = 0f;
    }

    void ApplyResolvedImpulse(
        Vector3 direction,
        float impulseMagnitude,
        float duration,
        bool allowTransfer)
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

        if (allowTransfer)
        {
            prototypeExternalImpulseCanTransfer = true;
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

        Vector3 resolvedPushDelta = ResolvePrototypeBodyBlockedMovementDelta(
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

        if (!prototypeExternalImpulseCanTransfer ||
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

        transferTarget.Motor.ApplyExternalImpulse(
            pushDirection,
            transferredImpulseMagnitude,
            prototypeImpulseTransferDuration,
            allowTransfer: false);

        prototypeExternalPushVelocity *=
            Mathf.Max(0f, 1f - resolvedTransferRatio);

        prototypeExternalImpulseHasTransferred = true;
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
            ~0,
            QueryTriggerInteraction.Collide);

        float bestScore = float.NegativeInfinity;

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hit = prototypeBodyOverlapBuffer[hitIndex];

            if (hit == null)
                continue;

            SoldierController candidate =
                hit.GetComponentInParent<SoldierController>();

            if (!IsValidPrototypeBodyNeighbor(candidate) ||
                candidate.Motor == null)
            {
                continue;
            }

            if (WasPrototypeBodyNeighborAlreadyCounted(
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
    public void MoveByFormationDelta(
        Vector3 movementDelta,
        Vector3 fallbackFacingDirection,
        float speedLimit = -1f,
        bool faceMovementDirection = true)
    {
        if (!CanMove())
            return;

        if (IsMovementRequestLocked())
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

        movementDelta = ResolvePrototypeBodyBlockedMovementDelta(
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

        RotateTowardDirection(visualFacingDirection);
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

    bool CanMove()
    {
        return agent != null &&
               agent.enabled &&
               agent.isActiveAndEnabled &&
               agent.isOnNavMesh;
    }

    bool IsMovementRequestLocked()
    {
        return soldierController != null &&
               (soldierController.IsMovementLocked ||
                soldierController.IsCombatMoveLocked);
    }

    void RotateTowardVelocity()
    {
        if (Time.time < velocityRotationSuppressedUntil)
            return;

        if (IsMovementRequestLocked())
            return;

        if (agent == null)
            return;

        Vector3 velocity = Velocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude < 0.01f)
            return;

        RotateTowardDirection(velocity.normalized);
    }

    void RotateTowardDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            direction.normalized,
            Vector3.up);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime);
    }
}

