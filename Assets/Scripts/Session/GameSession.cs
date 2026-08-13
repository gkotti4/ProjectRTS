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

    public SaveManager Saves => saveManager;
    public GameRuntimeSnapshot LastRuntimeSnapshot => lastRuntimeSnapshot;

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
}
