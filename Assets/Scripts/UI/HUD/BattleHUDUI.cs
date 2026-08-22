using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// BattleHUDUI
/// -----------------------------------------------------------------------------
///
/// Complete tactical/gameplay HUD composition.
///
/// Responsibilities:
/// - own the BattleHUD CanvasGroup visibility boundary
/// - route tactical selection state into ActionPanel / QueuePanel presentation
/// - own BattleHUD cursor presentation
/// - expose BattleHUD-specific UI operations such as the worker build submenu
///
/// This class does NOT own gameplay input enable/disable state and does not know why
/// another UI mode may hide the HUD.
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
public class BattleHUDUI : MonoBehaviour
{
    public static BattleHUDUI Instance { get; private set; }

    #region References

    [Header("HUD Root")]
    [Tooltip("CanvasGroup on the complete tactical/gameplay HUD root.")]
    [SerializeField] private CanvasGroup battleHUDCanvasGroup;

    [Header("Cursor")]
    [SerializeField] private Texture2D defaultCursor;

    [Header("Panels")]
    [SerializeField] private ActionPanelUI actionPanelUI;
    [SerializeField] private QueuePanelUI queuePanelUI;

    public bool IsVisible =>
        battleHUDCanvasGroup == null ||
        battleHUDCanvasGroup.alpha > 0.001f;

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

        if (battleHUDCanvasGroup == null)
            battleHUDCanvasGroup = GetComponent<CanvasGroup>();
    }

    void Start()
    {
        SetCursor(defaultCursor);

        GameEvents.OnSelectionChanged += HandleSelectionChanged;
        GameEvents.OnPlacementModeChanged += HandlePlacementModeChanged;
        GameEvents.OnProductionQueueChanged += HandleProductionQueueChanged;
    }

    void OnDestroy()
    {
        GameEvents.OnSelectionChanged -= HandleSelectionChanged;
        GameEvents.OnPlacementModeChanged -= HandlePlacementModeChanged;
        GameEvents.OnProductionQueueChanged -= HandleProductionQueueChanged;

        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region HUD Visibility

    /// <summary>
    /// Controls the complete BattleHUD presentation boundary.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (battleHUDCanvasGroup != null)
        {
            battleHUDCanvasGroup.alpha = visible ? 1f : 0f;
            battleHUDCanvasGroup.interactable = visible;
            battleHUDCanvasGroup.blocksRaycasts = visible;
        }

        if (!visible)
            HideAllPanels();
        else
            RefreshSelectionPresentation();
    }

    #endregion

    #region Cursor

    public void SetCursor(Texture2D cursor, Vector2 hotspot = default)
    {
        Cursor.SetCursor(cursor, hotspot, CursorMode.Auto);
    }

    public void SetDefaultCursor()
    {
        SetCursor(defaultCursor);
    }

    void HandlePlacementModeChanged(bool isPlacing)
    {
        Cursor.visible = !isPlacing;

        if (!isPlacing)
            SetDefaultCursor();
    }

    #endregion

    #region Selection Routing

    void HandleSelectionChanged()
    {
        if (!IsVisible)
            return;

        RefreshSelectionPresentation();
    }

    void RefreshSelectionPresentation()
    {
        if (SelectionManager.Instance == null)
        {
            HideAllPanels();
            return;
        }

        IReadOnlyList<ISelectable> selected =
            SelectionManager.Instance.GetSelectedObjects();

        if (selected == null || selected.Count == 0)
        {
            HideAllPanels();
            return;
        }

        ISelectable first = selected[0];

        if (first == null)
        {
            HideAllPanels();
            return;
        }

        switch (first.SelectionKind)
        {
            case SelectableKind.Squad:
            case SelectableKind.Worker:
                ShowCommandPanel(first);
                return;

            case SelectableKind.Building:
                ShowBuildingPanel(first as BuildingController);
                return;

            default:
                HideAllPanels();
                return;
        }
    }

    void ShowCommandPanel(ISelectable selectable)
    {
        HideAllPanels();

        if (actionPanelUI != null && selectable is ICommandable commandable)
            actionPanelUI.ShowCommandPanel(commandable);
    }

    void ShowBuildingPanel(BuildingController building)
    {
        HideAllPanels();

        if (building == null)
            return;

        actionPanelUI?.ShowBuildingPanel(building);
        queuePanelUI?.ShowPanel(building);
    }

    void HideAllPanels()
    {
        actionPanelUI?.HidePanel();
        queuePanelUI?.HidePanel();
    }

    void HandleProductionQueueChanged(BuildingController building)
    {
        if (!IsVisible || SelectionManager.Instance == null)
            return;

        IReadOnlyList<ISelectable> selected =
            SelectionManager.Instance.GetSelectedObjects();

        if (selected.Count != 1)
            return;

        if (selected[0] != building)
            return;

        queuePanelUI?.Refresh();
    }

    #endregion

    #region Battle HUD Operations

    public void ShowActionPanelBuildSubmenu(WorkerController worker)
    {
        if (worker == null || actionPanelUI == null)
            return;

        actionPanelUI.ShowBuildPanel(worker);
    }

    #endregion
}
