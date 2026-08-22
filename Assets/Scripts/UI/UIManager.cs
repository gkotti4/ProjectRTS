using UnityEngine;

/// -----------------------------------------------------------------------------
/// UIManager
/// -----------------------------------------------------------------------------
///
/// Scene-level coordinator for major UI systems.
///
/// This is intentionally NOT DontDestroyOnLoad. Persistent game/session state belongs
/// to GameSession; each scene owns and rebuilds its own presentation layer.
///
/// Responsibilities:
/// - provide one scene-level access point for major UI compositions
/// - coordinate visibility at major UI-system boundaries
/// - remain ignorant of individual HUD panels and game-mode page contents
///
/// Child UI systems own their own presentation details:
/// - BattleHUDUI owns the tactical/gameplay HUD
/// - ContractMercenaryUI owns Contract Mercenary Hub/Battle/Results presentation flow
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    #region References

    [Header("Major UI Systems")]
    [SerializeField] private BattleHUDUI battleHUDUI;
    [SerializeField] private ContractMercenaryUI contractMercenaryUI;

    public BattleHUDUI BattleHUD => battleHUDUI;
    public ContractMercenaryUI ContractMercenary => contractMercenaryUI;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
    }

    void Start()
    {
        ResolveReferences();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region Major UI Boundaries

    /// <summary>
    /// Shows or hides the complete tactical/gameplay HUD.
    /// UIManager does not control tactical input; input remains owned by the Input
    /// and Selection systems and is coordinated by the active game-mode UI flow.
    /// </summary>
    public void SetBattleHUDVisible(bool visible)
    {
        ResolveBattleHUD();
        battleHUDUI?.SetVisible(visible);
    }

    #endregion

    #region Reference Resolution

    void ResolveReferences()
    {
        ResolveBattleHUD();

        if (contractMercenaryUI == null)
            contractMercenaryUI = ContractMercenaryUI.Instance;
    }

    void ResolveBattleHUD()
    {
        if (battleHUDUI == null)
            battleHUDUI = BattleHUDUI.Instance;
    }

    #endregion
}
