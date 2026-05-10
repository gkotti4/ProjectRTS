using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    // Cursor
    [Header("Cursor")]
    [SerializeField] private Texture2D defaultCursor;
    [SerializeField] private Texture2D buildCursor;
    [SerializeField] private Texture2D orderCursor;
    
    // Panels
    [Header("Panels")]
    [SerializeField] private BuildingPanelUI buildingPanel;
    
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
    
    void Update()
    {
        if (Input.GetMouseButtonDown(1)) // Check if we this here
        {
            SetCursor(orderCursor);
            StartCoroutine(SetDefaultCursorAfterDelay());
        }
    }


    // Cursor
    public void SetCursor(Texture2D cursor, Vector2 hotspot = default(Vector2))
    {
        Cursor.SetCursor(cursor, hotspot, CursorMode.Auto);
    }
    public void SetDefaultCursor() => SetCursor(defaultCursor);
    public void SetBuildCursor() => SetCursor(buildCursor);
    public void SetOrderCursor() => SetCursor(orderCursor);

    private void HandleIsPlacingBuildingChanged(bool isPlacing)
    {
        if (isPlacing)
        {
            SetCursor(buildCursor);
        }
        else
        {
            SetCursor(defaultCursor);
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
        
        // Single building selected
        if (selected.Count == 1 && selected[0] is BuildingController building)
        {
            HideAllPanels();
            buildingPanel.ShowBuildingButtons(building);
            return;
        }
        
        // TODO - single unit, multi unit, resource node panels
        HideAllPanels();
    }
    

    void HideAllPanels()
    {
        //buildingPanel.gameObject.SetActive(false);
        buildingPanel.HideAll();
    }



    IEnumerator SetDefaultCursorAfterDelay(float delay=0.225f)
    {
        yield return new WaitForSeconds(delay);
        SetCursor(defaultCursor);
    }
    
}
