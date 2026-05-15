using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    //public event Action OnSelectionChanged;
    
    [SerializeField] private LayerMask selectableLayers;
    private List<ISelectable> selectedObjects = new List<ISelectable>();
    private Camera mainCamera;
    
    // Drag box variables
    private Vector2 dragStart;
    private bool isDragging = false;
    private Texture2D boxTexture;

    [SerializeField] private float dragThreshold = 2f;

    private List<ISelectable> allSelectables = new List<ISelectable>(); // used in DragSelect

    private bool isPlacingBuilding = false;
    
    public void RegisterSelectable(ISelectable selectable) // used in DragSelect
    {
        allSelectables.Add(selectable);
    }

    public void UnregisterSelectable(ISelectable selectable)
    {
        allSelectables.Remove(selectable);
    }
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        
        boxTexture = new Texture2D(1, 1);
        boxTexture.SetPixel(0, 0, new Color(0.6f, 0.8f, 1f, 0.25f));
        boxTexture.Apply();
    }
    
    void Start()
    {
        mainCamera = Camera.main;
        GameEvents.OnPlacementModeChanged += HandlePlacementModeChanged;
    }

    void OnDestroy()
    {
        GameEvents.OnPlacementModeChanged -= HandlePlacementModeChanged;
    }
    
    void Update()
    {
        HandleSelectionInput();
        // Future Idea: Update the box selected units as box being drawn in update; con: performance
    }

    void HandleSelectionInput()
    {
        if (isPlacingBuilding) return;

        if (Input.GetKeyDown(KeyCode.Escape)) // New
        {
            DeselectAll();
            return;
        }
        
        if (Input.GetMouseButtonDown(0))
        {
            // Don't process selection if clicking on UI
            if (EventSystem.current.IsPointerOverGameObject()) return; 
            
            dragStart = Input.mousePosition;
        }

        if (Input.GetMouseButton(0))
        {
            // Don't process selection if clicking on UI
            if (EventSystem.current.IsPointerOverGameObject()) return; 
            
            // Check if drag has exceeded threshold
            if (Vector2.Distance(dragStart, Input.mousePosition) > dragThreshold)
                isDragging = true;
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (isDragging) // Drag select
                HandleDragSelect();
            else if (!EventSystem.current.IsPointerOverGameObject()) // Click select
                HandleClickSelect();

            isDragging = false;
        }
    }

    void HandleClickSelect()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, selectableLayers))
        {
            if (hit.collider.TryGetComponent(out ISelectable selectable)) // Selected object
            {
                // Debug.Log(hit.collider.name);
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    // Shift click selection - append this object in current selection
                    if (selectedObjects.Contains(selectable))
                        Deselect(selectable);
                    else
                        Select(selectable);
                }
                else
                {
                    // Normal Click - clear selection and select this
                    DeselectAll();
                    Select(selectable);
                }
            }
            else // Selected nothing
            {
                // Clicked empty ground - deselect all unless shift was held
                if(!Input.GetKey(KeyCode.LeftShift))
                    DeselectAll();
            }
        }
        else // Selected NOTHING
        {
            // Ray hit nothing on selectable layers — clicked empty ground
            if (!Input.GetKey(KeyCode.LeftShift))
                DeselectAll();
        }
    }


    void HandleDragSelect()
    {
        if (!Input.GetKey(KeyCode.LeftShift))
            DeselectAll();

        Rect selectionRect = GetScreenRectRaw(dragStart, Input.mousePosition);

        foreach (ISelectable selectable in allSelectables)
        {
            if (selectable == null || selectable.GetGameObject() == null) continue;

            Vector3 screenPos = mainCamera.WorldToScreenPoint(selectable.GetGameObject().transform.position);
            if (screenPos.z > 0 && selectionRect.Contains(screenPos, true) && selectable.IsDragSelectable)
                Select(selectable);
        }
    }

    // Adds object to selection and notifies it
    void Select(ISelectable selectable)
    {
        if (selectedObjects.Contains(selectable)) return;
        selectedObjects.Add(selectable);
        selectable.OnSelect();

        // Fire typed selection events
        if (selectable is BuildingController b) GameEvents.BuildingSelected(b);
        else if (selectable is UnitController u) GameEvents.UnitSelected(u);

        //OnSelectionChanged?.Invoke();
        GameEvents.SelectionChanged();
    }

    // Removes object from selection and notifies it
    void Deselect(ISelectable selectable)
    {
        selectedObjects.Remove(selectable);
        selectable.OnDeselect();
        //OnSelectionChanged?.Invoke();
        GameEvents.SelectionChanged();
    }

    // Clears all selected objects
    void DeselectAll()
    {
        foreach (ISelectable selectable in selectedObjects)
            selectable.OnDeselect();
        selectedObjects.Clear();
        GameEvents.Deselected();
        //OnSelectionChanged?.Invoke();
        GameEvents.SelectionChanged();
    }
    
    // Raw screen rect for contains check — no Y flip
    Rect GetScreenRectRaw(Vector2 start, Vector2 end)
    {
        return Rect.MinMaxRect(
            Mathf.Min(start.x, end.x),
            Mathf.Min(start.y, end.y),
            Mathf.Max(start.x, end.x),
            Mathf.Max(start.y, end.y)
        );
    }

    // Flipped Y rect for GUI drawing only
    Rect GetScreenRectGUI(Vector2 start, Vector2 end)
    {
        Vector2 s = new Vector2(start.x, Screen.height - start.y);
        Vector2 e = new Vector2(end.x, Screen.height - end.y);
        return Rect.MinMaxRect(
            Mathf.Min(s.x, e.x),
            Mathf.Min(s.y, e.y),
            Mathf.Max(s.x, e.x),
            Mathf.Max(s.y, e.y)
        );
    }

    // Draws the drag selection box on screen
    void OnGUI()
    {
        if (!isDragging) return;
        Rect screenRect = GetScreenRectGUI(dragStart, Input.mousePosition);
        GUI.DrawTexture(screenRect, boxTexture);
    }

    // Returns current selection for other systems to read
    public List<ISelectable> GetSelectedObjects()
    {
        return selectedObjects;
    }

    void HandlePlacementModeChanged(bool isPlacing)
    {
        isPlacingBuilding = isPlacing;
        if (!isPlacingBuilding)
        {
            isDragging = false;
            dragStart = Input.mousePosition; // reset drag position
        }
    }
}
