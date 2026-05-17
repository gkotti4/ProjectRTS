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
            actionPanelUI.ShowProductionPanel(building);
            queuePanelUI.ShowPanel(building);
            return;
        }

        if (selected.Count == 1 && selected[0] is UnitController unit)
        {
            HideAllPanels();
            actionPanelUI.ShowUnitPanel(unit);
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
    // TODO: Replace with event if more systems need to react to build submenu state
    public void ShowActionPanelBuildSubmenu(UnitController unit)
    {
        actionPanelUI.ShowBuildPanel(unit);
        PlayerInputHandler.Instance.SetBuildSubmenuActive(true);
    }

    public void ExitBuildSubmenu()
    {
        actionPanelUI.ExitBuildPanel();
        PlayerInputHandler.Instance.SetBuildSubmenuActive(false);
    }
}