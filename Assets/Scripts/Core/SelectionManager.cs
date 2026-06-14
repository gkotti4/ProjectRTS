// SESSION: Squad Control Refactor

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    #region Fields

    [Header("Selection")]
    // [SerializeField] private LayerMask selectableLayers;
    [SerializeField] private float dragThreshold = 5f;

    [Header("Double Click")]
    [SerializeField] private float doubleClickTime = 0.3f;
    [SerializeField] private float sameTypeUnitRange = 20f;
    [SerializeField] private float sameTypeBuildingRange = 40f;
    [SerializeField] private float sameCategorySquadRange = 40f;

    private readonly List<ISelectable> selectedObjects = new List<ISelectable>();
    private readonly List<ISelectable> allSelectables = new List<ISelectable>();

    private Camera mainCamera;

    private Vector2 dragStart;
    private bool isDragging = false;
    private Texture2D boxTexture;

    private float lastClickTime = 0f;
    private ISelectable lastClicked = null;

    private bool isPlacingBuilding = false;
    
    private readonly HashSet<ISelectable> dragHoveredObjects = new HashSet<ISelectable>();
    private SquadController hoveredSquad = null;
    private EntityStats hoveredES = null;

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
        if (selectable == null) return;
        if (allSelectables.Contains(selectable)) return;

        allSelectables.Add(selectable);
    }

    public void UnregisterSelectable(ISelectable selectable)
    {
        if (selectable == null) return;

        if (selectedObjects.Contains(selectable))
            selectable.OnDeselect();

        selectedObjects.Remove(selectable);
        allSelectables.Remove(selectable);

        GameEvents.SelectionChanged();
    }

    #endregion

    #region Resolve Selectable

    // Clicking or drag-selecting a squad member selects its SquadController instead.
    ISelectable ResolveSelectable(ISelectable selectable)
    {
        if (selectable == null) return null;

        GameObject go = selectable.GetGameObject();
        if (go == null) return null;

        if (go.TryGetComponent(out SquadMemberController member) &&
            member.Squad != null)
        {
            return member.Squad;
        }

        return selectable;
    }

    ISelectable ResolveSelectableFromHit(RaycastHit hit)
    {
        // 1. If we hit any child/body/collider belonging to a squad member,
        // select the owning SquadController.
        SquadMemberController squadMember =
            hit.collider.GetComponentInParent<SquadMemberController>();

        if (squadMember != null && squadMember.Squad != null)
            return squadMember.Squad;

        // 2. If we hit the SquadController's own collision box,
        // select the SquadController directly.
        SquadController squad =
            hit.collider.GetComponentInParent<SquadController>();

        if (squad != null)
            return squad;

        // 3. Otherwise, normal entities: villagers, buildings, legacy units.
        EntityController entity =
            hit.collider.GetComponentInParent<EntityController>();

        if (entity != null)
            return entity;

        return null;
    }
    
    SquadController ResolveSquadFromHit(RaycastHit hit)
    {
        SquadMemberController squadMember =
            hit.collider.GetComponentInParent<SquadMemberController>();

        if (squadMember != null && squadMember.Squad != null)
            return squadMember.Squad;

        SquadController squad =
            hit.collider.GetComponentInParent<SquadController>();

        return squad;
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

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, GameLayers.Instance.SelectableLayers))
        {
            SquadController squad = ResolveSquadFromHit(hit);

            if (squad != null)
            {
                SetHoveredSquad(squad);
                return;
            }

            EntityStats entity = hit.collider.GetComponentInParent<EntityStats>();
            SetHoveredEntity(entity);
            return;
        }

        ClearNormalHover();
    }
    
    void HandleDragHover()
    {
        Rect selectionRect = GetScreenRectRaw(dragStart, Input.mousePosition);

        HashSet<ISelectable> newHovered = new HashSet<ISelectable>();

        foreach (ISelectable rawSelectable in allSelectables)
        {
            ISelectable selectable = ResolveSelectable(rawSelectable);
            if (selectable == null) continue;
            if (!selectable.IsDragSelectable) continue;

            GameObject rawGO = rawSelectable.GetGameObject();
            if (rawGO == null) continue;

            Vector3 screenPos = mainCamera.WorldToScreenPoint(rawGO.transform.position);

            if (screenPos.z > 0 && selectionRect.Contains(screenPos, true))
                newHovered.Add(selectable);
        }

        // Remove hover from objects that left the drag rectangle.
        List<ISelectable> toRemove = new List<ISelectable>();

        foreach (ISelectable oldHovered in dragHoveredObjects)
        {
            if (!newHovered.Contains(oldHovered))
                toRemove.Add(oldHovered);
        }

        foreach (ISelectable selectable in toRemove)
        {
            ApplyHoverExit(selectable);
            dragHoveredObjects.Remove(selectable);
        }

        // Add hover to objects that entered the drag rectangle.
        foreach (ISelectable selectable in newHovered)
        {
            if (dragHoveredObjects.Contains(selectable))
                continue;

            dragHoveredObjects.Add(selectable);
            ApplyHoverEnter(selectable);
        }
    }

    void SetHoveredSquad(SquadController squad)
    {
        if (hoveredSquad == squad)
            return;

        ClearHover();

        hoveredSquad = squad;
        hoveredSquad.OnHoverEnter();
    }

    void SetHoveredEntity(EntityStats entity)
    {
        if (hoveredES == entity)
            return;

        ClearHover();

        hoveredES = entity;

        if (hoveredES == null)
            return;

        if (hoveredES.TryGetComponent(out EntityController controller))
            controller.OnHoverEnter();
        else if (hoveredES.HealthBar)
            hoveredES.HealthBar.Show();
    }

    void ClearHover()
    {
        if (hoveredSquad)
        {
            hoveredSquad.OnHoverExit();
            hoveredSquad = null;
        }

        if (hoveredES)
        {
            if (hoveredES.TryGetComponent(out EntityController controller))
                controller.OnHoverExit();
            else if (hoveredES.HealthBar && !hoveredES.HealthBar.IsSelected)
                hoveredES.HealthBar.Hide();

            hoveredES = null;
        }
    }
    
    void ClearNormalHover()
    {
        if (hoveredSquad)
        {
            hoveredSquad.OnHoverExit();
            hoveredSquad = null;
        }

        if (hoveredES)
        {
            if (hoveredES.TryGetComponent(out EntityController controller))
                controller.OnHoverExit();
            else if (hoveredES.HealthBar && !hoveredES.HealthBar.IsSelected)
                hoveredES.HealthBar.Hide();

            hoveredES = null;
        }
    }
    
    void ClearDragHover()
    {
        if (dragHoveredObjects.Count == 0)
            return;

        foreach (ISelectable selectable in dragHoveredObjects)
            ApplyHoverExit(selectable);

        dragHoveredObjects.Clear();
    }
    
    void ApplyHoverEnter(ISelectable selectable)
    {
        if (selectable == null) return;

        if (selectable is SquadController squad)
        {
            squad.OnHoverEnter();
            return;
        }

        GameObject go = selectable.GetGameObject();
        if (go == null) return;

        if (go.TryGetComponent(out EntityController entity))
            entity.OnHoverEnter();
    }

    void ApplyHoverExit(ISelectable selectable)
    {
        if (selectable == null) return;

        if (selectable is SquadController squad)
        {
            squad.OnHoverExit();
            return;
        }

        GameObject go = selectable.GetGameObject();
        if (go == null) return;

        if (go.TryGetComponent(out EntityController entity))
            entity.OnHoverExit();
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
            isDragging = false;
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
            {
                HandleDragSelect();
            }
            else if (!EventSystem.current.IsPointerOverGameObject())
            {
                HandleClickSelect();
            }

            isDragging = false;
            ClearDragHover();
        }
    }

    void HandleClickSelect()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, GameLayers.Instance.SelectableLayers))
        {
            if (!Input.GetKey(KeyCode.LeftShift))
                DeselectAll();

            return;
        }

        ISelectable selectable = ResolveSelectableFromHit(hit);

        if (selectable == null)
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

        foreach (ISelectable rawSelectable in allSelectables)
        {
            ISelectable selectable = ResolveSelectable(rawSelectable);
            if (selectable == null) continue;
            if (!selectable.IsDragSelectable) continue;

            GameObject rawGO = rawSelectable.GetGameObject();
            if (!rawGO) continue;

            Vector3 screenPos = mainCamera.WorldToScreenPoint(rawGO.transform.position);

            if (screenPos.z > 0 && selectionRect.Contains(screenPos, true))
                Select(selectable);
        }
    }

    #endregion

    #region Double Click

    void SelectSameTypeInRange(ISelectable selectable)
    {
        selectable = ResolveSelectable(selectable);
        if (selectable == null) return;

        DeselectAll();

        if (selectable is SquadController squad)
        {
            SelectSameSquadCategoryInRange(squad);
            return;
        }

        if (!selectable.GetGameObject().TryGetComponent(out EntityStats stats))
            return;

        if (stats.baseDetails.entityType == EntityType.Unit)
        {
            SelectSameUnitTypeInRange(selectable, stats);
            return;
        }

        if (stats.baseDetails.entityType == EntityType.Building)
        {
            SelectSameBuildingTypeInRange(selectable, stats);
        }
    }

    void SelectSameSquadCategoryInRange(SquadController source)
    {
        foreach (ISelectable rawSelectable in allSelectables)
        {
            ISelectable selectable = ResolveSelectable(rawSelectable);
            if (selectable is not SquadController squad) continue;
            if (squad.Category != source.Category) continue;

            if (Calc.OutOfRange(
                    source.transform.position,
                    squad.transform.position,
                    sameCategorySquadRange))
                continue;

            Select(squad);
        }
    }

    void SelectSameUnitTypeInRange(ISelectable sourceSelectable, EntityStats sourceStats)
    {
        UnitType unitType = sourceStats.baseDetails.unitType;
        Vector3 sourcePos = sourceSelectable.GetGameObject().transform.position;

        foreach (ISelectable rawSelectable in allSelectables)
        {
            ISelectable selectable = ResolveSelectable(rawSelectable);
            if (selectable == null) continue;
            if (selectable is SquadController) continue;

            GameObject go = selectable.GetGameObject();
            if (go == null) continue;
            if (!go.TryGetComponent(out EntityStats otherStats)) continue;
            if (otherStats.baseDetails.unitType != unitType) continue;

            if (Calc.OutOfRange(sourcePos, go.transform.position, sameTypeUnitRange))
                continue;

            Select(selectable);
        }
    }

    void SelectSameBuildingTypeInRange(ISelectable sourceSelectable, EntityStats sourceStats)
    {
        BuildingType buildingType = sourceStats.baseDetails.buildingType;
        Vector3 sourcePos = sourceSelectable.GetGameObject().transform.position;

        foreach (ISelectable rawSelectable in allSelectables)
        {
            ISelectable selectable = ResolveSelectable(rawSelectable);
            if (selectable == null) continue;

            GameObject go = selectable.GetGameObject();
            if (go == null) continue;
            if (!go.TryGetComponent(out EntityStats otherStats)) continue;
            if (otherStats.baseDetails.buildingType != buildingType) continue;

            if (Calc.OutOfRange(sourcePos, go.transform.position, sameTypeBuildingRange))
                continue;

            Select(selectable);
        }
    }

    #endregion

    #region Select / Deselect

    void Select(ISelectable selectable)
    {
        selectable = ResolveSelectable(selectable);

        if (selectable == null) return;
        if (selectedObjects.Contains(selectable)) return;

        if (selectedObjects.Count > 0)
        {
            SelectionKind incomingKind = GetSelectionKind(selectable);
            SelectionKind existingKind = GetSelectionKind(selectedObjects[0]);

            if (incomingKind != existingKind)
                DeselectAll();
        }

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
        selectable = ResolveSelectable(selectable);

        if (selectable == null) return;

        if (selectedObjects.Contains(selectable))
            Deselect(selectable);
        else
            Select(selectable);
    }

    void Deselect(ISelectable selectable)
    {
        selectable = ResolveSelectable(selectable);

        if (selectable == null) return;
        if (!selectedObjects.Contains(selectable)) return;

        selectedObjects.Remove(selectable);
        selectable.OnDeselect();

        GameEvents.SelectionChanged();
    }

    public void DeselectAll()
    {
        foreach (ISelectable selectable in selectedObjects)
        {
            if (selectable == null) continue;
            selectable.OnDeselect();
        }

        selectedObjects.Clear();

        GameEvents.Deselected();
        GameEvents.SelectionChanged();
    }

    #endregion

    #region Getters

    public List<ISelectable> GetSelectedObjects()
    {
        return selectedObjects;
    }

    #endregion

    #region Selection Kind

    enum SelectionKind
    {
        None,
        Unit,
        Building,
        Squad
    }

    SelectionKind GetSelectionKind(ISelectable selectable)
    {
        selectable = ResolveSelectable(selectable);

        if (selectable == null || selectable.GetGameObject() == null)
            return SelectionKind.None;

        GameObject go = selectable.GetGameObject();

        if (go.TryGetComponent(out SquadController _))
            return SelectionKind.Squad;

        if (go.TryGetComponent(out BuildingController _))
            return SelectionKind.Building;

        if (go.TryGetComponent(out UnitController _))
            return SelectionKind.Unit;

        return SelectionKind.None;
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


