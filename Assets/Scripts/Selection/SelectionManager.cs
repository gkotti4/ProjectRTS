using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/*
 * Click Selecting an Object - Can select from GameLayers.SelectableLayers LayerMask;
 *                              Resolves ISelectable in parent from hit collider object;
 *                              If SelectableProxy present, route selection behavior to target selectable (used for soldiers in squad); 
 *
 * Drag Selecting an Object - "For drag select, your current system does not box-test colliders.
 *                              It checks the registered selectable object’s root screen position."
 *
 * SelectionTarget - We are now using SelectionTarget script to route selection behavior to the correct ISelectable target,
 *                    Anything searching for ISelectable directly is DEPRECIATED
 */

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    #region Fields

    [Header("Selection")]
    private LayerMask selectableLayers;
    [SerializeField] private float dragThreshold = 5f;

    [Header("Double Click")]
    [SerializeField] private float doubleClickTime = 0.3f;
    [SerializeField] private float fallbackDoubleClickRange = 35f;

    private readonly List<ISelectable> selectedObjects = new List<ISelectable>();
    private readonly List<ISelectable> allSelectables = new List<ISelectable>();
    
    private readonly HashSet<IHoverable> dragHoveredObjects = new HashSet<IHoverable>();

    private Camera mainCamera;

    private Vector2 dragStart;
    private bool isDragging = false;
    private Texture2D boxTexture;

    private float lastClickTime = 0f;
    private ISelectable lastClicked = null;

    private bool isPlacingBuilding = false;

    private IHoverable hoveredObject = null;

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

        boxTexture = new Texture2D(1, 1);
        boxTexture.SetPixel(0, 0, new Color(0.6f, 0.8f, 1f, 0.25f));
        boxTexture.Apply();
    }

    void Start()
    {
        mainCamera = Camera.main;
        GameEvents.OnPlacementModeChanged += HandlePlacementModeChanged;

        selectableLayers = GameLayers.Instance.SelectableLayers;
    }

    void OnDestroy()
    {
        GameEvents.OnPlacementModeChanged -= HandlePlacementModeChanged;
    }

    void Update()
    {
        HandleSelectionInput();
        HandleHover();
    }

    #endregion

    #region Registration

    public void RegisterSelectable(ISelectable selectable)
    {
        if (selectable == null)
            return;

        if (selectable.GetGameObject() == null)
            return;

        if (allSelectables.Contains(selectable))
            return;

        allSelectables.Add(selectable);
    }

    public void UnregisterSelectable(ISelectable selectable)
    {
        if (selectable == null)
            return;

        if (selectedObjects.Contains(selectable))
        {
            selectable.OnDeselect();
            selectedObjects.Remove(selectable);
        }

        allSelectables.Remove(selectable);

        if (ControlGroupManager.Instance != null)
            ControlGroupManager.Instance.RemoveFromGroup(selectable);

        GameEvents.SelectionChanged();
    }

    #endregion

    #region Input

    void HandleSelectionInput()
    {
        if (isPlacingBuilding)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            DeselectAll();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
                return;

            dragStart = Input.mousePosition;
            isDragging = false;
        }

        if (Input.GetMouseButton(0))
        {
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
                return;

            if (Vector2.Distance(dragStart, Input.mousePosition) > dragThreshold)
                isDragging = true;
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (isDragging)
            {
                HandleDragSelect();
            }
            else if (EventSystem.current == null ||
                     !EventSystem.current.IsPointerOverGameObject())
            {
                HandleClickSelect();
            }

            isDragging = false;
            ClearDragHover();
        }
    }

    void HandleClickSelect()
    {
        if (!TryGetSelectableUnderMouse(out ISelectable selectable))
        {
            if (!Input.GetKey(KeyCode.LeftShift))
                DeselectAll();

            return;
        }

        bool isDoubleClick =
            Time.time - lastClickTime < doubleClickTime &&
            lastClicked == selectable;

        if (isDoubleClick)
        {
            SelectSameTypeInRange(selectable);
        }
        else if (Input.GetKey(KeyCode.LeftShift))
        {
            ToggleSelect(selectable);
        }
        else
        {
            DeselectAll();
            Select(selectable);
        }

        lastClickTime = Time.time;
        lastClicked = selectable;
    }

    void HandleDragSelect()
    {
        if (!Input.GetKey(KeyCode.LeftShift))
            DeselectAll();

        Rect selectionRect = GetScreenRectRaw(dragStart, Input.mousePosition);

        foreach (ISelectable selectable in allSelectables)
        {
            if (selectable == null)
                continue;

            if (!selectable.IsDragSelectable)
                continue;

            GameObject go = selectable.GetGameObject();
            if (go == null)
                continue;

            Vector3 screenPos = mainCamera.WorldToScreenPoint(go.transform.position);
            
            if (screenPos.z > 0f && selectionRect.Contains(screenPos, true))
                Select(selectable);
        }
    }

    #endregion

    #region Hover
    
    void HandleHover()
    {
        if (isDragging)
        {
            ClearNormalHover();
            HandleDragHover();
            return;
        }

        ClearDragHover();

        IHoverable newHover = null;

        if (TryGetSelectableUnderMouse(out ISelectable selectable))
            newHover = selectable as IHoverable;

        if (hoveredObject == newHover)
            return;

        if (hoveredObject != null)
            hoveredObject.OnHoverExit();

        hoveredObject = newHover;

        if (hoveredObject != null)
            hoveredObject.OnHoverEnter();
    }
    
    void HandleDragHover()
    {
        Rect selectionRect = GetScreenRectRaw(dragStart, Input.mousePosition);

        HashSet<IHoverable> newHovered = new HashSet<IHoverable>();

        foreach (ISelectable selectable in allSelectables)
        {
            if (selectable == null)
                continue;

            if (!selectable.IsDragSelectable)
                continue;

            if (selectable is not IHoverable hoverable)
                continue;

            GameObject go = selectable.GetGameObject();
            if (go == null)
                continue;

            Vector3 screenPos = mainCamera.WorldToScreenPoint(go.transform.position);

            if (screenPos.z > 0f && selectionRect.Contains(screenPos, true))
                newHovered.Add(hoverable);
        }

        List<IHoverable> toRemove = new List<IHoverable>();

        foreach (IHoverable oldHover in dragHoveredObjects)
        {
            if (!newHovered.Contains(oldHover))
                toRemove.Add(oldHover);
        }

        foreach (IHoverable hoverable in toRemove)
        {
            hoverable.OnHoverExit();
            dragHoveredObjects.Remove(hoverable);
        }

        foreach (IHoverable hoverable in newHovered)
        {
            if (dragHoveredObjects.Contains(hoverable))
                continue;

            hoverable.OnHoverEnter();
            dragHoveredObjects.Add(hoverable);
        }
    }

    void ClearNormalHover()
    {
        if (hoveredObject == null)
            return;

        hoveredObject.OnHoverExit();
        hoveredObject = null;
    }

    void ClearDragHover()
    {
        if (dragHoveredObjects.Count == 0)
            return;

        foreach (IHoverable hoverable in dragHoveredObjects)
        {
            if (hoverable == null)
                continue;

            hoverable.OnHoverExit();
        }

        dragHoveredObjects.Clear();
    }

    #endregion

    #region Selection Operations

    void Select(ISelectable selectable)
    {
        if (selectable == null)
            return;

        if (selectable.GetGameObject() == null)
            return;

        if (selectedObjects.Contains(selectable))
            return;

        if (!CanAddToCurrentSelection(selectable))
            DeselectAll();

        selectedObjects.Add(selectable);
        selectable.OnSelect();

        GameEvents.SelectionChanged();
    }

    public void SelectExternal(ISelectable selectable)
    {
        Select(selectable);
    }

    void ToggleSelect(ISelectable selectable)
    {
        if (selectable == null)
            return;

        if (selectedObjects.Contains(selectable))
            Deselect(selectable);
        else
            Select(selectable);
    }

    void Deselect(ISelectable selectable)
    {
        if (selectable == null)
            return;

        if (!selectedObjects.Contains(selectable))
            return;

        selectedObjects.Remove(selectable);
        selectable.OnDeselect();

        GameEvents.SelectionChanged();
    }

    public void DeselectAll()
    {
        foreach (ISelectable selectable in selectedObjects)
        {
            if (selectable == null)
                continue;

            selectable.OnDeselect();
        }

        selectedObjects.Clear();

        GameEvents.Deselected();
        GameEvents.SelectionChanged();
    }

    bool CanAddToCurrentSelection(ISelectable incoming)
    {
        if (incoming == null)
            return false;

        if (selectedObjects.Count == 0)
            return true;

        ISelectable first = selectedObjects[0];

        if (first == null)
            return true;

        return first.SelectionKind == incoming.SelectionKind;
    }

    #endregion

    #region Double Click

    void SelectSameTypeInRange(ISelectable source)
    {
        if (source == null)
            return;

        GameObject sourceGO = source.GetGameObject();
        if (sourceGO == null)
            return;

        DeselectAll();

        float range = fallbackDoubleClickRange;

        if (source is ISelectionComparable comparable)
            range = comparable.DoubleClickSelectRange;

        foreach (ISelectable candidate in allSelectables)
        {
            if (candidate == null)
                continue;

            if (candidate.GetGameObject() == null)
                continue;

            if (!candidate.IsDragSelectable)
                continue;

            if (!IsSameSelectionType(source, candidate))
                continue;

            if (Calc.OutOfRange(
                    sourceGO.transform.position,
                    candidate.GetGameObject().transform.position,
                    range))
                continue;

            Select(candidate);
        }
    }

    bool IsSameSelectionType(ISelectable source, ISelectable candidate)
    {
        if (source == null || candidate == null)
            return false;

        if (source is ISelectionComparable comparable)
            return comparable.IsSameSelectionType(candidate);

        return source.SelectionKind == candidate.SelectionKind;
    }

    #endregion

    #region Resolve

    bool TryGetSelectableUnderMouse(out ISelectable selectable)
    {
        selectable = null;

        if (mainCamera == null)
            return false;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, selectableLayers))
            return false;

        return TryResolveSelectable(hit.collider, out selectable);
    }

    // bool TryResolveSelectable(Collider collider, out ISelectable selectable)
    // {
    //     selectable = null;
    //
    //     if (collider == null)
    //         return false;
    //
    //     SelectableProxy proxy = collider.GetComponentInParent<SelectableProxy>(); // For soldiers in squads right?
    //
    //     if (proxy != null && proxy.TryGetTarget(out selectable))
    //         return true;
    //
    //     MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>();
    //
    //     foreach (MonoBehaviour behaviour in behaviours)
    //     {
    //         if (behaviour is ISelectable found &&
    //             found.GetGameObject() != null)
    //         {
    //             selectable = found;
    //             return true;
    //         }
    //     }
    //
    //     return false;
    // }
    
    bool TryResolveSelectable(Collider hitCollider, out ISelectable selectable)
    {
        selectable = null;

        if (hitCollider == null)
            return false;

        SelectionTarget proxy = hitCollider.GetComponent<SelectionTarget>(); // WAS GetComponentInParent (we now use SelectionTarget child object for selection)

        if (proxy != null &&
            proxy.TryGetTarget(out selectable) &&
            selectable != null &&
            selectable.GetGameObject() != null)
        {
            return true;
        }

        Debug.Log("Was unable to resolve SelectionTarget - add SelectionTarget component");
        selectable = hitCollider.GetComponentInParent<ISelectable>();

        return selectable != null &&
               selectable.GetGameObject() != null;
    }

    #endregion

    #region Getters

    public List<ISelectable> GetSelectedObjects()
    {
        return selectedObjects;
    }

    public IReadOnlyList<ISelectable> GetAllSelectables()
    {
        return allSelectables;
    }

    #endregion

    #region Rect / GUI

    Rect GetScreenRectRaw(Vector2 start, Vector2 end)
    {
        return Rect.MinMaxRect(
            Mathf.Min(start.x, end.x),
            Mathf.Min(start.y, end.y),
            Mathf.Max(start.x, end.x),
            Mathf.Max(start.y, end.y)
        );
    }

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

    void OnGUI()
    {
        if (!isDragging)
            return;

        Rect screenRect = GetScreenRectGUI(dragStart, Input.mousePosition);
        GUI.DrawTexture(screenRect, boxTexture);
    }

    #endregion

    #region Placement Mode

    void HandlePlacementModeChanged(bool isPlacing)
    {
        isPlacingBuilding = isPlacing;

        if (!isPlacingBuilding)
        {
            isDragging = false;
            dragStart = Input.mousePosition;
        }
    }

    #endregion
}


