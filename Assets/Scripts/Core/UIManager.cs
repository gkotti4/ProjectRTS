using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    // Cursor
    [Header("Cursor")]
    [SerializeField] private Texture2D defaultCursor;
    //[SerializeField] private Texture2D buildCursor;
    
    // Panels
    [Header("Panels")]
    [SerializeField] private ActionPanelUI actionPanelUI;
    [SerializeField] private QueuePanelUI queuePanelUI;
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }
    
    void Start()
    {
        SetCursor(defaultCursor);
        SelectionManager.Instance.OnSelectionChanged += HandleSelectionChanged;
        BuildingPlacer.Instance.OnPlacingModeChanged += HandleIsPlacingBuildingChanged;
    }
    
    void OnDestroy()
    {
        SelectionManager.Instance.OnSelectionChanged -= HandleSelectionChanged;
    }
    
    
    // Cursor
    public void SetCursor(Texture2D cursor, Vector2 hotspot = default(Vector2))
    {
        Cursor.SetCursor(cursor, hotspot, CursorMode.Auto);
    }
    public void SetDefaultCursor() => SetCursor(defaultCursor);

    private void HandleIsPlacingBuildingChanged(bool isPlacing)
    {
        if (isPlacing)
        {
            Cursor.visible = false;
        }
        else
        {
            SetDefaultCursor();
            Cursor.visible = true;
        }
    }
    
    
    // Panels
    void HandleSelectionChanged()
    {
        List<ISelectable> selected = SelectionManager.Instance.GetSelectedObjects();

        if (selected.Count == 0)
        {
            HideAllPanels();
            return;
        }
        
        // ACTION PANEL
        // Single building selected
        if (selected.Count == 1 && selected[0] is BuildingController building)
        {
            HideAllPanels();
            actionPanelUI.ShowPanel(building);
            queuePanelUI.ShowPanel(building);
            return;
        }
        else if (selected.Count == 1 && selected[0] is UnitController unit)
        {
            // TODO
            
        }
        
        // INFO PANEL
        // TODO - Info Panel - units, buildings, resource nodes 
        HideAllPanels();
    }
    

    void HideAllPanels()
    {
        actionPanelUI.HidePanel();
        queuePanelUI.HidePanel();
    }

    public void RefreshQueuePanel()
    {
        queuePanelUI.Refresh();
    }
    
}
