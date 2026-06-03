using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    [SerializeField] private LayerMask selectableLayers;
    private List<ISelectable> selectedObjects = new List<ISelectable>();
    private List<ISelectable> allSelectables = new List<ISelectable>();
    private Camera mainCamera;

    // Drag select
    private Vector2 dragStart;
    private bool isDragging = false;
    private Texture2D boxTexture;
    [SerializeField] private float dragThreshold = 5f;

    // Double click
    private readonly float doubleClickTime = 0.3f;
    private float lastClickTime = 0f;
    private ISelectable lastClicked = null;

    // Placement mode
    private bool isPlacingBuilding = false;

    // Hover
    private EntityStats hoveredES = null;

    #region Unity Lifecycle
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
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
        HandleHover();
    }
    #endregion

    #region Registration
    public void RegisterSelectable(ISelectable selectable)
    {
        allSelectables.Add(selectable);
    }

    public void UnregisterSelectable(ISelectable selectable)
    {
        selectable.OnDeselect();
        selectedObjects.Remove(selectable);
        allSelectables.Remove(selectable);
        ControlGroupManager.Instance.RemoveFromGroup(selectable); // Move to another class later
        GameEvents.SelectionChanged();
    }
    #endregion

    #region Hover
    void HandleHover()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, selectableLayers))
        {
            EntityStats entity = hit.collider.GetComponentInParent<EntityStats>();

            if (entity != hoveredES)
            {
                if (hoveredES != null && hoveredES.HealthBar != null && !hoveredES.HealthBar.IsSelected)
                    hoveredES.HealthBar.Hide();

                hoveredES = entity;
                if (hoveredES != null && hoveredES.HealthBar != null)
                    hoveredES.HealthBar.Show();
            }
        }
        else if (hoveredES != null)
        {
            if (hoveredES.HealthBar != null && !hoveredES.HealthBar.IsSelected)
                hoveredES.HealthBar.Hide();
            hoveredES = null;
        }
    }
    #endregion

    #region Selection Input
    void HandleSelectionInput()
    {
        if (isPlacingBuilding) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            DeselectAll();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;
            dragStart = Input.mousePosition;
        }

        if (Input.GetMouseButton(0))
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;
            if (Vector2.Distance(dragStart, Input.mousePosition) > dragThreshold)
                isDragging = true;
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (isDragging)
                HandleDragSelect();
            else if (!EventSystem.current.IsPointerOverGameObject())
                HandleClickSelect();

            isDragging = false;
        }
    }

    void HandleClickSelect()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, selectableLayers))
        {
            if (hit.collider.TryGetComponent(out ISelectable selectable))
            {
                bool isDoubleClick = (Time.time - lastClickTime) < doubleClickTime &&
                                     lastClicked == selectable;

                if (isDoubleClick)
                    SelectSameTypeInRange(selectable);
                else if (Input.GetKey(KeyCode.LeftShift))
                {
                    if (selectedObjects.Contains(selectable))
                        Deselect(selectable);
                    else
                        Select(selectable);
                }
                else
                {
                    DeselectAll();
                    Select(selectable);
                }

                lastClickTime = Time.time;
                lastClicked = selectable;
            }
            else
            {
                if (!Input.GetKey(KeyCode.LeftShift))
                    DeselectAll();
            }
        }
        else
        {
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
            Vector3 screenPos = mainCamera.WorldToScreenPoint(
                selectable.GetGameObject().transform.position);
            if (screenPos.z > 0 && selectionRect.Contains(screenPos, true) &&
                selectable.IsDragSelectable)
                Select(selectable);
        }
    }

    void SelectSameTypeInRange(ISelectable selectable)
    {
        if (!selectable.GetGameObject().TryGetComponent(out EntityStats stats)) return;

        DeselectAll();

        if (stats.baseData.entityType == EntityType.Unit)
        {
            UnitType unitType = stats.baseData.unitType;
            float range = 20f;

            foreach (ISelectable other in allSelectables)
            {
                if (!other.GetGameObject().TryGetComponent(out EntityStats otherStats)) continue;
                if (otherStats.baseData.unitType != unitType) continue;
                if (Calc.OutOfRange(selectable.GetGameObject().transform.position,
                        other.GetGameObject().transform.position, range)) continue;
                Select(other);
            }
        }
        else if (stats.baseData.entityType == EntityType.Building)
        {
            BuildingType buildingType = stats.baseData.buildingType;
            float range = 40f;

            foreach (ISelectable other in allSelectables)
            {
                if (!other.GetGameObject().TryGetComponent(out EntityStats otherStats)) continue;
                if (otherStats.baseData.buildingType != buildingType) continue;
                if (Calc.OutOfRange(selectable.GetGameObject().transform.position,
                        other.GetGameObject().transform.position, range)) continue;
                Select(other);
            }
        }
    }
    #endregion

    #region Select / Deselect
    void Select(ISelectable selectable)
    {
        if (selectedObjects.Contains(selectable)) return;

        // Mixed selection prevention — units and buildings can't be selected together
        if (selectedObjects.Count > 0)
        {
            bool incomingIsUnit = selectable.GetGameObject().TryGetComponent(out UnitController _);
            bool existingIsUnit = selectedObjects[0].GetGameObject().TryGetComponent(out UnitController _);

            if (incomingIsUnit != existingIsUnit)
                DeselectAll();
        }

        selectedObjects.Add(selectable);
        selectable.OnSelect();
        GameEvents.SelectionChanged();
    }

    /// External selection entry point — used by ControlGroupManager
    public void SelectExternal(ISelectable selectable) => Select(selectable);

    void Deselect(ISelectable selectable)
    {
        selectedObjects.Remove(selectable);
        selectable.OnDeselect();
        GameEvents.SelectionChanged();
    }

    public void DeselectAll()
    {
        foreach (ISelectable selectable in selectedObjects)
            selectable.OnDeselect();
        selectedObjects.Clear();
        GameEvents.Deselected();
        GameEvents.SelectionChanged();
    }
    #endregion

    #region Getters
    public List<ISelectable> GetSelectedObjects() => selectedObjects;
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
        if (!isDragging) return;
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