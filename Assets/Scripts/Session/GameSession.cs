using UnityEngine;

/// -----------------------------------------------------------------------------
/// GameSession
/// -----------------------------------------------------------------------------
///
/// Persistent application/session layer that exists above one instantiated game
/// runtime. GameSession survives scene changes; GameManager represents the live
/// game world and may come and go with gameplay scenes.
///
/// First-pass responsibilities:
/// - provide the persistent session singleton
/// - own access to generic save-file persistence
/// - request explicit runtime snapshots through GameManager
/// - retain the last captured runtime snapshot for handoff/debugging
///
/// Game-mode-specific run state (Contract Mercenary, Last Outpost, etc.) will be
/// owned/referenced here as those modes are implemented. GameSession should not
/// search the scene for squads/factions itself; the GameManager runtime boundary
/// provides that information.
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    private SaveManager saveManager;
    private GameRuntimeSnapshot lastRuntimeSnapshot;
    private GameRuntimeSetup pendingRuntimeSetup;
    private ContractMercenaryRunState contractMercenaryRunState;

    public SaveManager Saves => saveManager;
    public GameRuntimeSnapshot LastRuntimeSnapshot => lastRuntimeSnapshot;
    public GameRuntimeSetup PendingRuntimeSetup => pendingRuntimeSetup;
    public bool HasPendingRuntimeSetup => pendingRuntimeSetup != null;
    public ContractMercenaryRunState ContractMercenaryRunState => contractMercenaryRunState;
    public bool HasContractMercenaryRun => contractMercenaryRunState != null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        saveManager = new SaveManager();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Requests one explicit snapshot of the currently instantiated game runtime.
    /// No scene objects are searched here; GameManager is the runtime gateway.
    /// </summary>
    public bool TryCaptureCurrentRuntime(
        out GameRuntimeSnapshot runtimeSnapshot)
    {
        runtimeSnapshot = null;

        if (GameManager.Instance == null)
            return false;

        runtimeSnapshot = GameManager.Instance.CaptureRuntimeSnapshot();
        lastRuntimeSnapshot = runtimeSnapshot;
        return runtimeSnapshot != null;
    }

    public GameRuntimeSnapshot CaptureCurrentRuntime()
    {
        TryCaptureCurrentRuntime(out GameRuntimeSnapshot runtimeSnapshot);
        return runtimeSnapshot;
    }

    public void ClearCapturedRuntimeSnapshot()
    {
        lastRuntimeSnapshot = null;
    }

    #region Runtime Setup Handoff

    /// <summary>
    /// Stores the setup that the next instantiated GameManager should consume.
    /// This is an in-memory scene/runtime handoff, not permanent save data.
    /// </summary>
    public bool SetPendingRuntimeSetup(GameRuntimeSetup runtimeSetup)
    {
        if (runtimeSetup == null || !runtimeSetup.IsValid)
        {
            Debug.LogError(
                "GameSession.SetPendingRuntimeSetup failed: runtime setup is null or invalid.",
                this);
            return false;
        }

        pendingRuntimeSetup = runtimeSetup;
        return true;
    }

    /// <summary>
    /// Returns and clears the pending setup so one scene transition cannot
    /// accidentally initialize later gameplay scenes from stale runtime data.
    /// </summary>
    public bool TryConsumePendingRuntimeSetup(
        out GameRuntimeSetup runtimeSetup)
    {
        runtimeSetup = pendingRuntimeSetup;

        if (runtimeSetup == null)
            return false;

        pendingRuntimeSetup = null;
        return true;
    }

    public void ClearPendingRuntimeSetup()
    {
        pendingRuntimeSetup = null;
    }

    #endregion
    #region Contract Mercenary Run

    /// <summary>
    /// Stores the active Contract Mercenary run above scene lifetime.
    /// The game-mode controller owns the rules for creating/updating this state;
    /// GameSession only owns its cross-scene lifetime.
    /// </summary>
    public void SetContractMercenaryRunState(
        ContractMercenaryRunState runState)
    {
        contractMercenaryRunState = runState;
    }

    public void ClearContractMercenaryRunState()
    {
        contractMercenaryRunState = null;
    }

    #endregion

}
