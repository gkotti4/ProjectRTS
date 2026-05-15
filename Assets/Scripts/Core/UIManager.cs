using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Cursor")]
    [SerializeField] private Texture2D defaultCursor;

    [Header("Panels")]
    [SerializeField] private ActionPanelUI actionPanelUI;
    [SerializeField] private QueuePanelUI queuePanelUI;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
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
        GameEvents.OnProductionQueueChanged -= HandleProductionQueueChanged;
        GameEvents.OnPlacementModeChanged -= HandlePlacementModeChanged;
    }

    // Cursor
    public void SetCursor(Texture2D cursor, Vector2 hotspot = default)
    {
        Cursor.SetCursor(cursor, hotspot, CursorMode.Auto);
    }

    public void SetDefaultCursor() => SetCursor(defaultCursor);

    void HandlePlacementModeChanged(bool isPlacing)
    {
        Cursor.visible = !isPlacing;
        if (!isPlacing)
        {
            SetDefaultCursor();
            // Exit build submenu when placement ends — cancel or confirm
            ExitBuildSubmenu();
        }
    }

    // Selection routing
    void HandleSelectionChanged()
    {
        List<ISelectable> selected = SelectionManager.Instance.GetSelectedObjects();

        if (selected.Count == 0) { HideAllPanels(); return; }

        if (selected.Count == 1 && selected[0] is BuildingController building)
        {
            HideAllPanels();
            actionPanelUI.ShowPanel(building);
            queuePanelUI.ShowPanel(building);
            return;
        }

        if (selected.Count == 1 && selected[0] is UnitController unit)
        {
            HideAllPanels();
            actionPanelUI.ShowUnitButtons(unit);
            return;
        }

        HideAllPanels();
    }

    // Listens to production changes — only refreshes if firing building is selected
    void HandleProductionQueueChanged(BuildingController building)
    {
        List<ISelectable> selected = SelectionManager.Instance.GetSelectedObjects();
        if (selected.Count == 1 && selected[0] is BuildingController selectedBuilding
            && selectedBuilding == building)
        {
            queuePanelUI.Refresh();
        }
    }

    void HideAllPanels()
    {
        actionPanelUI.HidePanel();
        queuePanelUI.HidePanel();
    }

    // Build submenu
    public void ShowActionPanelBuildSubmenu(UnitController unit) // Could replace with event if methods get hacky 
    {
        actionPanelUI.ShowBuildSubmenu(unit);
        PlayerInputHandler.Instance.SetBuildSubmenuActive(true);
    }

    public void ExitBuildSubmenu()
    {
        actionPanelUI.ExitBuildSubmenu();
        PlayerInputHandler.Instance.SetBuildSubmenuActive(false);
    }
}