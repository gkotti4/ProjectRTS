using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Purpose-built army controller for Battle Test Mode.
///
/// First-pass responsibilities:
/// - receives the spawned armies from BattleGameModeController
/// - waits briefly before issuing the opening orders
/// - spreads friendly squads across viable enemy targets
/// - preserves valid engagements instead of constantly resetting SquadCombat
/// - replaces dead or invalid targets
/// - reissues an order if a living squad unexpectedly becomes idle
///
/// SquadController and SquadCombat remain responsible for movement, approach,
/// melee/ranged behavior, local targeting, attacks, and combat resolution.
/// </summary>
[DisallowMultipleComponent]
public class BattleArmyAI : MonoBehaviour
{
    #region Tuning

    [Header("Battle Controller")]
    [SerializeField] private BattleGameModeController battleGameModeController;

    [Header("AI Timing")]
    [Min(0f)]
    [SerializeField] private float battleOpeningDelay = 1.25f;

    [Min(0.1f)]
    [SerializeField] private float aiThinkInterval = 0.75f;

    [Header("Target Assignment")]
    [Tooltip("Added to a target's score for every friendly squad already assigned to it. Higher values spread the army across more targets.")]
    [Min(0f)]
    [SerializeField] private float aiTargetAssignmentCrowdingPenalty = 14f;

    [Tooltip("Keeps a squad on its current valid target unless that target dies or the squad becomes idle.")]
    [SerializeField] private bool aiPreserveValidTargets = true;

    [Tooltip("When enabled, an idle squad with a valid old target receives that attack order again.")]
    [SerializeField] private bool aiReissueOrdersToIdleSquads = true;

    [Header("Debug")]
    [SerializeField] private bool aiLogOrders = false;

    #endregion

    #region Runtime

    private readonly List<SquadController> aiSquads = new List<SquadController>();
    private readonly List<SquadController> enemySquads = new List<SquadController>();

    private readonly Dictionary<SquadController, SquadController> assignedTargets =
        new Dictionary<SquadController, SquadController>();

    private readonly Dictionary<SquadController, int> targetAssignmentCounts =
        new Dictionary<SquadController, int>();

    private float battleOpeningTimer;
    private float aiThinkTimer;
    private bool hasReceivedArmies;
    private bool hasIssuedOpeningOrders;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (battleGameModeController == null)
            battleGameModeController = GetComponent<BattleGameModeController>();

        if (battleGameModeController == null)
            battleGameModeController = BattleGameModeController.Instance;
    }

    void OnEnable()
    {
        TrySubscribeToBattleController();
    }

    void Start()
    {
        TrySubscribeToBattleController();
        TryBindExistingBattle();
    }

    void Update()
    {
        if (battleGameModeController == null)
        {
            battleGameModeController = BattleGameModeController.Instance;
            TrySubscribeToBattleController();
        }

        if (battleGameModeController == null ||
            !battleGameModeController.IsBattleActive)
        {
            return;
        }

        if (!hasReceivedArmies)
            TryBindExistingBattle();

        if (!hasReceivedArmies)
            return;

        if (!hasIssuedOpeningOrders)
        {
            battleOpeningTimer -= Time.deltaTime;

            if (battleOpeningTimer <= 0f)
            {
                hasIssuedOpeningOrders = true;
                ThinkAndIssueOrders(forceAllOrders: true);
            }

            return;
        }

        aiThinkTimer -= Time.deltaTime;

        if (aiThinkTimer > 0f)
            return;

        aiThinkTimer = Mathf.Max(0.1f, aiThinkInterval);
        ThinkAndIssueOrders(forceAllOrders: false);
    }

    void OnDisable()
    {
        UnsubscribeFromBattleController();
    }

    #endregion

    #region Battle Events

    void TrySubscribeToBattleController()
    {
        if (battleGameModeController == null)
            return;

        battleGameModeController.OnArmiesSpawned -= HandleArmiesSpawned;
        battleGameModeController.OnBattleStateChanged -= HandleBattleStateChanged;

        battleGameModeController.OnArmiesSpawned += HandleArmiesSpawned;
        battleGameModeController.OnBattleStateChanged += HandleBattleStateChanged;
    }

    void UnsubscribeFromBattleController()
    {
        if (battleGameModeController == null)
            return;

        battleGameModeController.OnArmiesSpawned -= HandleArmiesSpawned;
        battleGameModeController.OnBattleStateChanged -= HandleBattleStateChanged;
    }

    void HandleArmiesSpawned(
        IReadOnlyList<SquadController> playerArmy,
        IReadOnlyList<SquadController> enemyArmy)
    {
        BindArmies(enemyArmy, playerArmy);
    }

    void HandleBattleStateChanged(BattleGameState newState)
    {
        if (newState == BattleGameState.Battle)
            return;

        hasIssuedOpeningOrders = false;
        hasReceivedArmies = false;
        assignedTargets.Clear();
        targetAssignmentCounts.Clear();
    }

    void TryBindExistingBattle()
    {
        if (battleGameModeController == null ||
            !battleGameModeController.IsBattleActive)
        {
            return;
        }

        BindArmies(
            battleGameModeController.EnemySquads,
            battleGameModeController.PlayerSquads);
    }

    void BindArmies(
        IReadOnlyList<SquadController> controlledArmy,
        IReadOnlyList<SquadController> opposingArmy)
    {
        aiSquads.Clear();
        enemySquads.Clear();
        assignedTargets.Clear();
        targetAssignmentCounts.Clear();

        CopyLivingSquads(controlledArmy, aiSquads);
        CopyLivingSquads(opposingArmy, enemySquads);

        hasReceivedArmies = aiSquads.Count > 0 && enemySquads.Count > 0;
        hasIssuedOpeningOrders = false;
        battleOpeningTimer = Mathf.Max(0f, battleOpeningDelay);
        aiThinkTimer = Mathf.Max(0.1f, aiThinkInterval);
    }

    #endregion

    #region AI Think

    void ThinkAndIssueOrders(bool forceAllOrders)
    {
        RemoveInvalidSquads(aiSquads);
        RemoveInvalidSquads(enemySquads);
        RemoveInvalidAssignments();

        if (aiSquads.Count == 0 || enemySquads.Count == 0)
            return;

        RebuildTargetAssignmentCounts();

        for (int squadIndex = 0; squadIndex < aiSquads.Count; squadIndex++)
        {
            SquadController aiSquad = aiSquads[squadIndex];

            if (!IsLivingSquad(aiSquad))
                continue;

            assignedTargets.TryGetValue(aiSquad, out SquadController assignedTarget);

            SquadController combatTarget =
                aiSquad.Combat != null
                    ? aiSquad.Combat.TargetSquad
                    : null;

            bool hasValidCombatTarget = IsLivingSquad(combatTarget);
            bool hasValidAssignedTarget = IsLivingSquad(assignedTarget);

            if (aiPreserveValidTargets && hasValidCombatTarget)
            {
                assignedTargets[aiSquad] = combatTarget;
                continue;
            }

            bool shouldReissueExistingTarget =
                aiReissueOrdersToIdleSquads &&
                aiSquad.State == SquadState.Idle &&
                hasValidAssignedTarget;

            if (!forceAllOrders &&
                hasValidAssignedTarget &&
                !shouldReissueExistingTarget)
            {
                continue;
            }

            SquadController bestTarget = shouldReissueExistingTarget
                ? assignedTarget
                : FindBestTarget(aiSquad);

            if (bestTarget == null)
                continue;

            RegisterAssignment(aiSquad, assignedTarget, bestTarget);
            aiSquad.OrderAttack(bestTarget);

            if (aiLogOrders)
            {
                Debug.Log(
                    $"{name}: {aiSquad.name} ordered to attack {bestTarget.name}.",
                    this);
            }
        }
    }

    SquadController FindBestTarget(SquadController aiSquad)
    {
        if (aiSquad == null)
            return null;

        SquadController bestTarget = null;
        float bestScore = float.PositiveInfinity;

        Vector3 aiPosition = Flatten(aiSquad.transform.position);

        for (int targetIndex = 0; targetIndex < enemySquads.Count; targetIndex++)
        {
            SquadController candidate = enemySquads[targetIndex];

            if (!IsLivingSquad(candidate))
                continue;

            float distance = Vector3.Distance(
                aiPosition,
                Flatten(candidate.transform.position));

            targetAssignmentCounts.TryGetValue(
                candidate,
                out int assignmentCount);

            float score =
                distance +
                assignmentCount * aiTargetAssignmentCrowdingPenalty;

            if (score >= bestScore)
                continue;

            bestScore = score;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    void RegisterAssignment(
        SquadController aiSquad,
        SquadController previousTarget,
        SquadController newTarget)
    {
        if (aiSquad == null || newTarget == null)
            return;

        if (previousTarget != null &&
            targetAssignmentCounts.TryGetValue(
                previousTarget,
                out int previousCount))
        {
            targetAssignmentCounts[previousTarget] =
                Mathf.Max(0, previousCount - 1);
        }

        assignedTargets[aiSquad] = newTarget;

        targetAssignmentCounts.TryGetValue(
            newTarget,
            out int newCount);

        targetAssignmentCounts[newTarget] = newCount + 1;
    }

    #endregion

    #region Runtime Cleanup

    void RebuildTargetAssignmentCounts()
    {
        targetAssignmentCounts.Clear();

        foreach (KeyValuePair<SquadController, SquadController> pair in assignedTargets)
        {
            if (!IsLivingSquad(pair.Key) || !IsLivingSquad(pair.Value))
                continue;

            targetAssignmentCounts.TryGetValue(
                pair.Value,
                out int currentCount);

            targetAssignmentCounts[pair.Value] = currentCount + 1;
        }
    }

    void RemoveInvalidAssignments()
    {
        if (assignedTargets.Count == 0)
            return;

        List<SquadController> invalidKeys = null;

        foreach (KeyValuePair<SquadController, SquadController> pair in assignedTargets)
        {
            if (IsLivingSquad(pair.Key) && IsLivingSquad(pair.Value))
                continue;

            invalidKeys ??= new List<SquadController>();
            invalidKeys.Add(pair.Key);
        }

        if (invalidKeys == null)
            return;

        for (int index = 0; index < invalidKeys.Count; index++)
            assignedTargets.Remove(invalidKeys[index]);
    }

    void RemoveInvalidSquads(List<SquadController> squads)
    {
        for (int index = squads.Count - 1; index >= 0; index--)
        {
            if (!IsLivingSquad(squads[index]))
                squads.RemoveAt(index);
        }
    }

    void CopyLivingSquads(
        IReadOnlyList<SquadController> source,
        List<SquadController> destination)
    {
        if (source == null)
            return;

        for (int index = 0; index < source.Count; index++)
        {
            SquadController squad = source[index];

            if (IsLivingSquad(squad))
                destination.Add(squad);
        }
    }

    bool IsLivingSquad(SquadController squad)
    {
        return squad != null &&
               squad.IsInitialized &&
               squad.Roster != null &&
               squad.Roster.HasLivingSoldiers;
    }

    Vector3 Flatten(Vector3 position)
    {
        position.y = 0f;
        return position;
    }

    #endregion
}
