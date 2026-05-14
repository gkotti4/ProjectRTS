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

        // Subscribe to GameEvents — no direct calls from game logic needed
        //SelectionManager.Instance.OnSelectionChanged += HandleSelectionChanged;
        GameEvents.OnSelectionChanged += HandleSelectionChanged;
        //BuildingPlacer.Instance.OnPlacingModeChanged += HandlePlacingModeChanged;
        GameEvents.OnPlacementModeChanged += HandlePlacementModeChanged;
        GameEvents.OnProductionQueueChanged += HandleProductionQueueChanged;
    }

    void OnDestroy()
    {
        //SelectionManager.Instance.OnSelectionChanged -= HandleSelectionChanged;
        GameEvents.OnSelectionChanged -= HandleSelectionChanged;
        GameEvents.OnProductionQueueChanged -= HandleProductionQueueChanged;
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
        if (!isPlacing) SetDefaultCursor();
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
            // TODO — unit info panel
            return;
        }

        HideAllPanels();
    }

    // Listens to production changes — no longer called directly by BuildingController
    void HandleProductionQueueChanged(BuildingController building)
    {
        // Only refresh if this building is currently selected
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
}