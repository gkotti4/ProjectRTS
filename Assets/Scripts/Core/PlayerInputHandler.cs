// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;
//
// public class PlayerInputHandler : MonoBehaviour
// {
//     public static PlayerInputHandler Instance { get; private set; }
//     
//     #region Fields
//     [SerializeField] private BuildOptionData townCenterData;
//     [SerializeField] private BuildOptionData barracksData;
//
//     private Camera mainCamera;
//     
//     private CommandContext currentContext = CommandContext.Default;
//     private bool isPlacingBuilding = false;
//     private List<ISelectable> currentSelected = new List<ISelectable>();
//     private List<UnitController> GetCurrentSelectedUnits() =>
//         currentSelected
//             .Select(s => s.GetGameObject().GetComponent<UnitController>())
//             .Where(u => u != null)
//             .ToList();
//     
//     private Dictionary<HotkeySlot, KeyCode> slotToKey = new Dictionary<HotkeySlot, KeyCode>()
//     {
//         { HotkeySlot.Q, KeyCode.Q }, { HotkeySlot.W, KeyCode.W }, { HotkeySlot.E, KeyCode.E },
//         { HotkeySlot.R, KeyCode.R }, { HotkeySlot.T, KeyCode.T }, { HotkeySlot.A, KeyCode.A },
//         { HotkeySlot.S, KeyCode.S }, { HotkeySlot.D, KeyCode.D }, { HotkeySlot.F, KeyCode.F },
//         { HotkeySlot.G, KeyCode.G }, { HotkeySlot.Z, KeyCode.Z }, { HotkeySlot.X, KeyCode.X },
//         { HotkeySlot.C, KeyCode.C }, { HotkeySlot.V, KeyCode.V }, { HotkeySlot.B, KeyCode.B },
//     };
//     private readonly KeyCode[] numberKeys = {
//         KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
//         KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
//         KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
//     }; // must be same length as SelectionManagers controlGroups[9]
//
//     
//     // TODO: Replace with GameEvents.OnBuildSubmenuChanged(bool) when more systems need to react
//     private bool inBuildSubmenu = false;
//     public void SetBuildSubmenuActive(bool b) => inBuildSubmenu = b;
//
//     // Drag order (tells units to face a certain way after right click/drag for a move order)
//     private Vector3 rightClickStart;
//     private bool isDragOrdering = false;
//     private float dragThreshold = 30f; // pixels
//     #endregion
//
//     #region Unity Lifecycle
//     void Awake()
//     {
//         if (Instance != null && Instance != this)
//         {
//             Destroy(this.gameObject);
//             return;
//         }
//         Instance = this;
//     }
//     
//     void Start()
//     {
//         mainCamera = Camera.main;
//         GameEvents.OnSelectionChanged += HandleSelectionChanged;
//         GameEvents.OnPlacementModeChanged += HandlePlacementModeChanged;
//     }
//
//     void OnDestroy()
//     {
//         GameEvents.OnSelectionChanged -= HandleSelectionChanged;
//         GameEvents.OnPlacementModeChanged -= HandlePlacementModeChanged;
//     }
//
//     void Update()
//     {
//         HandleEscape();
//         
//         HandleControlGroups();
//         
//         if (inBuildSubmenu || isPlacingBuilding)
//         {
//             HandleBuildSubmenuHotkeys();
//             return;
//         }
//         HandleHotkeys();
//         HandleRightClick();
//     }
//     #endregion
//
//     
//     #region Selection & Context
//
//     /// Centralized escape - cancels any active mode
//     void HandleEscape()
//     {
//         if (!Input.GetKeyDown(KeyCode.Escape)) return;
//         
//         // Cancel drag right click order
//         if (isDragOrdering)
//         {
//             isDragOrdering = false;
//             FormationVisualizer.Instance.HideAll();
//         }
//         
//         // Move other cancel functions from other scripts here? like BuildingPlacer cancel placement or inBuildSubmenu?
//     }
//     
//     // Updates current selection and context when selection changes
//     void HandleSelectionChanged()
//     {
//         currentSelected = SelectionManager.Instance.GetSelectedObjects();
//         UpdateContext();
//     }
//     
//     void HandlePlacementModeChanged(bool isPlacing) => isPlacingBuilding = isPlacing;
//
//     // Determines command context from current selection
//     void UpdateContext()
//     {
//         if (currentSelected.Count == 0) { currentContext = CommandContext.Default; return; }
//
//         if (currentSelected[0] is BuildingController) { currentContext = CommandContext.BuildingSelected; return; }
//
//         if (currentSelected[0].GetGameObject().TryGetComponent(out UnitController unit))
//         {
//             currentContext = unit.Stats.entityTag == EntityTag.Villager
//                 ? CommandContext.EconomicUnitSelected
//                 : CommandContext.MilitaryUnitSelected;
//             return;
//         }
//
//         currentContext = CommandContext.Default;
//     }
//     #endregion
//
//     #region Control Groups
//     void HandleControlGroups()
//     {
//         for (int i = 0; i < numberKeys.Length; i++)
//         {
//             if (!Input.GetKeyDown(numberKeys[i])) continue;
//
// #if UNITY_EDITOR
//             // Use Alt in editor to avoid editor shortcut conflicts
//             if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
//                 SelectionManager.Instance.AssignControlGroup(i);
// #else
//         // Use Ctrl in builds — standard RTS controls
//         if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
//             SelectionManager.Instance.AssignControlGroup(i);
// #endif
//             else
//                 SelectionManager.Instance.SelectControlGroup(i);
//
//             return;
//         }
//     }
//     
//     /// Returns the shared control group index if all units belong to the same group, -1 otherwise.
//     int GetSharedControlGroup(List<UnitController> units)
//     {
//         if (units.Count == 0) return -1;
//     
//         int group = units[0].GetComponent<EntityController>()?.controlGroup ?? -1;
//         if (group < 0) return -1;
//
//         foreach (UnitController u in units)
//             if (u.GetComponent<EntityController>()?.controlGroup != group)
//                 return -1;
//
//         return group;
//     }
//     
//     #endregion
//     
//     #region Formations
//     List<Vector2> GetFormationOffsets(List<UnitController> military, int sharedGroup)
//     {
//         UnitFormation formation = UnitFormation.Line;
//         float width = -1f;
//         List<Vector2> saved = null;
//
//         if (sharedGroup >= 0)
//         {
//             ControlGroup cg = SelectionManager.Instance.GetControlGroup(sharedGroup);
//             formation = cg.formation;
//             width = cg.formationWidth;
//             saved = cg.formationOffsets;
//
//             if (saved != null && saved.Count == military.Count)
//                 return saved;
//         }
//
//         // No saved offsets — calculate default width per formation type
//         float defaultWidth = formation switch
//         {
//             UnitFormation.Line   => Mathf.Min(military.Count, 10) * FormationManager.Instance.DefaultSpacing,
//             UnitFormation.Spread => Mathf.Sqrt(military.Count) * FormationManager.Instance.DefaultSpacing * 1.5f,
//             UnitFormation.Box    => Mathf.Sqrt(military.Count) * FormationManager.Instance.DefaultSpacing,
//             UnitFormation.Circle => military.Count * FormationManager.Instance.DefaultSpacing / (2f * Mathf.PI),
//             UnitFormation.Wedge  => military.Count * FormationManager.Instance.DefaultSpacing,
//             _ => military.Count * FormationManager.Instance.DefaultSpacing
//         };
//
//         float finalWidth = width > 0 ? width : defaultWidth;
//         return FormationManager.Instance.CalculateOffsets(military.Count, finalWidth, formation);
//     }
//     #endregion
//
//     #region Right Click
//     // Handles right click — resolves hit context and issues orders directly to units
//
//
//     void HandleRightClick()
//     {
//         if (Input.GetMouseButtonDown(1))
//         {
//             rightClickStart = Input.mousePosition;
//             isDragOrdering = false;
//         }
//
//         if (Input.GetMouseButton(1))
//         {
//             if (!isDragOrdering && 
//                 // Calc.OutOfRange(rightClickStart, Input.mousePosition, dragThreshold)) // Note: uses sqr magnitude (works when z=0)
//                 Calc.OutOfRangeRealDistance(rightClickStart, Input.mousePosition, dragThreshold))
//                 isDragOrdering = true;
//
//             if (isDragOrdering)
//                 UpdateDragFormationPreview();
//         }
//
//         if (Input.GetMouseButtonUp(1))
//         {
//             if (isDragOrdering)
//                 HandleDragRightClick();
//             else
//                 HandleNormalRightClick();
//             isDragOrdering = false;
//         }
//     }
//     
//     
//     void HandleNormalRightClick()
//     {
//         //if (!Input.GetMouseButtonDown(1)) return;
//
//         Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
//         if (!Physics.Raycast(ray, out RaycastHit hit)) return;
//
//         // Buildings
//         bool anyBuilding = false;
//         foreach (ISelectable s in currentSelected)
//         {
//             if (s.GetGameObject().TryGetComponent(out BuildingController b))
//             {
//                 b.SetRallyPoint(hit.point); // currently all buildings deal with rally point
//                 anyBuilding = true;
//             }
//         }
//         if (anyBuilding) return;
//         
//
//         // Units
//         List<UnitController> units = new List<UnitController>();
//         foreach (ISelectable s in currentSelected)
//         {
//             if (s.GetGameObject().TryGetComponent(out UnitController u))
//                 units.Add(u);
//         }
//
//         if (units.Count == 0) return;
//
//         // Resolve what was hit once — shared across all units
//         bool hitEnemy = hit.collider.TryGetComponent(out EntityController hitEntity) &&
//                         hitEntity.Stats.IsEnemy(units[0].Stats);
//         bool hitNode = hit.collider.TryGetComponent(out ResourceNode node);
//         bool isGroundClick = !hitEnemy && !hitNode;
//
//         // Ground click with multiple units — formation move
//         if (isGroundClick && units.Count > 1)
//         {
//             HandleGroupMove(units, hit.point);
//             return;
//         }
//
//         // (formation_mode_refactor)
//         // Orders override formation mode and should be considered
//         // TODO: come back and reconsider ^
//         
//         // Issue orders to each unit
//         foreach (UnitController unit in units)
//         {
//             if (hitEnemy)
//                 unit.OrderAttack(hitEntity);
//             else if (hitNode && unit.Stats.gatherAmount > 0 && unit.TryGetComponent(out VillagerController vil)) // Update: refactor to include new VilController
//                 vil.OrderGather(node);
//             else
//                 unit.OrderMove(hit.point);
//         }
//     }
//
//
//     void HandleDragRightClick()
//     {
//         FormationVisualizer.Instance.HideAll();
//
//         List<UnitController> units = GetCurrentSelectedUnits();
//         List<UnitController> military = units.FindAll(u => u is not VillagerController);
//         if (military.Count == 0) return;
//
//         Ray startRay = mainCamera.ScreenPointToRay(rightClickStart);
//         Ray endRay = mainCamera.ScreenPointToRay(Input.mousePosition);
//
//         if (!Physics.Raycast(startRay, out RaycastHit startHit)) return;
//         if (!Physics.Raycast(endRay, out RaycastHit endHit)) return;
//
//         Vector3 destination = startHit.point;
//         Vector3 facingDirection = Calc.DirectionFlat(startHit.point, endHit.point);
//         float width = Calc.RealDistance(startHit.point, endHit.point);
//
//         // Get shared control group if any
//         int sharedGroup = GetSharedControlGroup(military);
//
//         // Calculate offsets based on alt held or not
//         List<Vector2> offsets;
//         if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
//         {
//             // Alt+drag — define custom formation width
//             UnitFormation formation = sharedGroup >= 0
//                 ? SelectionManager.Instance.GetControlGroup(sharedGroup).formation
//                 : UnitFormation.Line;
//
//             offsets = FormationManager.Instance.CalculateOffsets(
//                 military.Count, width, formation);
//
//             // Save to control group
//             if (sharedGroup >= 0)
//             {
//                 SelectionManager.Instance.GetControlGroup(sharedGroup).formationOffsets = offsets;
//                 SelectionManager.Instance.GetControlGroup(sharedGroup).formationWidth = width;
//             }
//         }
//         else
//         {
//             // Normal drag — use saved offsets or default
//             offsets = GetFormationOffsets(military, sharedGroup);
//         }
//
//         // // Issue move orders
//         // List<Vector3> worldPositions = FormationManager.Instance.OffsetsToWorldPositions(
//         //     offsets, destination, facingDirection);
//         //
//         // var slots = FormationManager.Instance.AssignNearestSlots(military, worldPositions);
//         // foreach (var slot in slots)
//         //     slot.Key.OrderMove(slot.Value);
//         //
//         // // Villagers go direct
//         // foreach (UnitController v in units.FindAll(u => u is VillagerController))
//         //     v.OrderMove(destination);
//         //
//         // FormationVisualizer.Instance.ShowSlots(new List<Vector3>(slots.Values));
//         
//         // (formation_mode_refactor)
//         List<Vector3> worldPositions = FormationManager.Instance.OffsetsToWorldPositions(
//             offsets, destination, facingDirection);
//         
//         // Villagers go direct - RECONSIDER Villager in Control Groups?
//         foreach (UnitController v in units.FindAll(u => u is VillagerController))
//              v.OrderMove(destination);
//         
//         if (sharedGroup >= 0 && !Input.GetKey(KeyCode.LeftAlt))
//         {
//             // Use anchor — same as HandleGroupMove
//             ControlGroup cg = SelectionManager.Instance.GetControlGroup(sharedGroup);
//             float speed = military.Min(u => u.Stats.moveSpeed);
//             if (cg.anchor == null)
//                 cg.anchor = new FormationAnchor(GetAveragePosition(military), facingDirection, speed);
//             cg.anchor.speed = speed;
//             cg.anchor.MoveTo(destination, facingDirection);
//             FormationVisualizer.Instance.ShowSlots(worldPositions);
//         }
//         else
//         {
//             var slots = FormationManager.Instance.AssignNearestSlots(military, worldPositions);
//             foreach (var slot in slots) slot.Key.OrderMove(slot.Value);
//             FormationVisualizer.Instance.ShowSlots(new List<Vector3>(slots.Values));
//         }
//     }
//     
//     // Sends units to formation positions centered around destination (now using Line formation as default)
// void HandleGroupMove(List<UnitController> units, Vector3 destination,
//     Vector3 facingDirection = default)
// {
//     List<UnitController> military = units.FindAll(u => u is not VillagerController);
//     List<UnitController> villagers = units.FindAll(u => u is VillagerController);
//
//     foreach (UnitController v in villagers)
//         v.OrderMove(destination);
//
//     if (military.Count == 0) return;
//
//     Vector3 facing = facingDirection != default
//         ? facingDirection
//         : Calc.DirectionFlat(GetAveragePosition(military), destination);
//
//     int sharedGroup = GetSharedControlGroup(military);
//
//     if (sharedGroup >= 0)
//     {
//         ControlGroup cg = SelectionManager.Instance.GetControlGroup(sharedGroup);
//
//         // Recalculate offsets if needed
//         if (cg.formationOffsets.Count != military.Count)
//         {
//             float width = cg.formationWidth > 0 ? cg.formationWidth : military.Count * 2f;
//             cg.formationOffsets = FormationManager.Instance.CalculateOffsets(
//                 military.Count, width, cg.formation);
//         }
//
//         // Move anchor to destination
//         float speed = military.Min(u => u.Stats.moveSpeed);
//         if (cg.anchor == null)
//         {
//             Vector3 center = GetAveragePosition(military);
//             cg.anchor = new FormationAnchor(center, facing, speed);
//         }
//
//         cg.anchor.speed = speed;
//         cg.anchor.MoveTo(destination, facing);
//
//         // Show visualizer at destination
//         List<Vector3> destSlots = FormationManager.Instance.OffsetsToWorldPositions(
//             cg.formationOffsets, destination, facing);
//         FormationVisualizer.Instance.ShowSlots(destSlots);
//     }
//     else
//     {
//         // No control group — direct formation move, no anchor
//         List<Vector2> offsets = GetFormationOffsets(military, -1);
//         List<Vector3> worldPositions = FormationManager.Instance.OffsetsToWorldPositions(
//             offsets, destination, facing);
//         var slots = FormationManager.Instance.AssignNearestSlots(military, worldPositions);
//         foreach (var slot in slots) slot.Key.OrderMove(slot.Value);
//         FormationVisualizer.Instance.ShowSlots(new List<Vector3>(slots.Values));
//     }
// }
//
//     Vector3 GetAveragePosition(List<UnitController> units)
//     {
//         Vector3 avg = Vector3.zero;
//         foreach (UnitController u in units) avg += u.transform.position;
//         return avg / units.Count;
//     }
//
//     void UpdateDragFormationPreview()
//     {
//         List<UnitController> units = GetCurrentSelectedUnits();
//         List<UnitController> military = units.FindAll(u => u is not VillagerController);
//         if (military.Count == 0) return;
//
//         Ray startRay = mainCamera.ScreenPointToRay(rightClickStart);
//         Ray endRay = mainCamera.ScreenPointToRay(Input.mousePosition);
//
//         if (!Physics.Raycast(startRay, out RaycastHit startHit)) return;
//         if (!Physics.Raycast(endRay, out RaycastHit endHit)) return;
//
//         Vector3 destination = startHit.point;
//         Vector3 facingDirection = Calc.DirectionFlat(startHit.point, endHit.point);
//         float width = Calc.RealDistance(startHit.point, endHit.point);
//
//         int sharedGroup = GetSharedControlGroup(military);
//
//         List<Vector2> offsets;
//         if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
//         {
//             UnitFormation formation = sharedGroup >= 0
//                 ? SelectionManager.Instance.GetControlGroup(sharedGroup).formation
//                 : UnitFormation.Line;
//             offsets = FormationManager.Instance.CalculateOffsets(military.Count, width, formation);
//         }
//         else
//             offsets = GetFormationOffsets(military, sharedGroup);
//
//         List<Vector3> worldPositions = FormationManager.Instance.OffsetsToWorldPositions(
//             offsets, destination, facingDirection);
//
//         FormationVisualizer.Instance.ShowSlots(worldPositions, persistent: true);
//     }
//     #endregion
//
//     #region Hotkeys
//     // Routes hotkeys based on current context
//     void HandleHotkeys()
//     {
//         switch (currentContext)
//         {
//             case CommandContext.Default:
//                 HandleDefaultHotkeys();
//                 break;
//             case CommandContext.EconomicUnitSelected:
//             case CommandContext.MilitaryUnitSelected:
//                 HandleUnitHotkeys();
//                 break;
//             case CommandContext.BuildingSelected:
//                 HandleProductionBuildingHotkeys();
//                 break;
//         }
//     }
//
//     // Default hotkeys — building placement when nothing selected
//     void HandleDefaultHotkeys()
//     {
//         if (Input.GetKeyDown(KeyCode.T))
//             BuildingPlacer.Instance.StartPlacing(townCenterData);
//         else if (Input.GetKeyDown(KeyCode.B))
//             BuildingPlacer.Instance.StartPlacing(barracksData);
//     }
//
//     // Routes unit hotkeys through CommandController — explicit commands only
//     void HandleUnitHotkeys()
//     {
//         Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
//         Physics.Raycast(ray, out RaycastHit hit);
//
//         // Find pressed key first
//         HotkeySlot pressedSlot = HotkeySlot.None;
//         foreach (KeyValuePair<HotkeySlot, KeyCode> kvp in slotToKey)
//             if (Input.GetKeyDown(kvp.Value)) { pressedSlot = kvp.Key; break; }
//     
//         if (pressedSlot == HotkeySlot.None) return;
//
//         // Apply to ALL selected units
//         foreach (ISelectable s in currentSelected)
//         {
//             if (!s.GetGameObject().TryGetComponent(out CommandController cc)) continue;
//             if (!s.GetGameObject().TryGetComponent(out UnitController unit)) continue;
//
//             CommandData matchingCmd = unit.Stats.baseData.baseCommands.Find(c => c.hotkey == pressedSlot);
//             if (matchingCmd != null && matchingCmd.commandType == CommandType.Build)
//             {
//                 UIManager.Instance.ShowActionPanelBuildSubmenu(unit);
//                 return;
//             }
//
//             // Execute Unit Command
//             
//             // Check group scope before routing - should it execute on all units or once for a group
//             if (matchingCmd.commandGroupScope == CommandGroupScope.Group)
//             {
//                 ExecuteGroupCommand(matchingCmd.commandType);
//                 return;
//             }
//             
//             // PerUnit Scope - execute on all selected units as usual
//             cc.ExecuteHotkeyCommand(pressedSlot, hit);
//         }
//     }
//
//     // Routes building hotkeys to production queue
//     void HandleProductionBuildingHotkeys()
//     {
//         // Handles buildings of same type and their production options
//         BuildingController firstBuilding = null;
//         BuildingType type = BuildingType.None;
//     
//         foreach (ISelectable s in currentSelected)
//         {
//             if (s is not BuildingController bc) return;
//             if (type == BuildingType.None) { type = bc.Stats.baseData.buildingType; firstBuilding = bc; }
//             else if (bc.Stats.baseData.buildingType != type) return;
//         }
//
//         if (firstBuilding == null) return;
//
//         foreach (ProductionOptionData option in firstBuilding.Stats.baseData.productionOptions)
//         {
//             if (option.hotkey == HotkeySlot.None) continue;
//             if (!Input.GetKeyDown(slotToKey[option.hotkey])) continue;
//
//             foreach (ISelectable s in currentSelected)
//                 if (s is BuildingController bc)
//                     bc.EnqueueProduction(option);
//             return;
//         }
//     }
//     
//     void HandleBuildSubmenuHotkeys() // Villager Only
//     {
//         // Handles villager build hotkeys while in placement mode
//         if (currentSelected.Count == 0) return; // Could be != 1
//         if (currentSelected[0] is not UnitController unit) return;
//         if (unit.Stats.baseData.unitType != UnitType.Villager) return;
//
//         foreach (BuildOptionData option in unit.Stats.baseData.buildOptions.Where(option => Input.GetKeyDown(slotToKey[option.hotkey])))
//         {
//             BuildingPlacer.Instance.StartPlacing(option);
//             return;
//         }
//     }
//     #endregion
//     
//     
//     
//     #region Global Commands
//
//     void ExecuteGroupCommand(CommandType cmd)
//     {
//         switch (cmd)
//         {
//             case CommandType.ToggleFormationMode:
//                 break;
//         }
//     }
//     
//     #endregion
//     
// }


using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    public static PlayerInputHandler Instance { get; private set; }
    
    #region Fields
    [SerializeField] private BuildOptionData townCenterData;
    [SerializeField] private BuildOptionData barracksData;

    private Camera mainCamera;
    
    private CommandContext currentContext = CommandContext.Default;
    private bool isPlacingBuilding = false;
    private List<ISelectable> currentSelected = new List<ISelectable>();
    private List<UnitController> GetCurrentSelectedUnits() =>
        currentSelected
            .Select(s => s.GetGameObject().GetComponent<UnitController>())
            .Where(u => u != null)
            .ToList();
    
     private Dictionary<HotkeySlot, KeyCode> slotToKey = new Dictionary<HotkeySlot, KeyCode>()
     {
         { HotkeySlot.Q, KeyCode.Q }, { HotkeySlot.W, KeyCode.W }, { HotkeySlot.E, KeyCode.E },
         { HotkeySlot.R, KeyCode.R }, { HotkeySlot.T, KeyCode.T }, { HotkeySlot.A, KeyCode.A },
         { HotkeySlot.S, KeyCode.S }, { HotkeySlot.D, KeyCode.D }, { HotkeySlot.F, KeyCode.F },
         { HotkeySlot.G, KeyCode.G }, { HotkeySlot.Z, KeyCode.Z }, { HotkeySlot.X, KeyCode.X },
         { HotkeySlot.C, KeyCode.C }, { HotkeySlot.V, KeyCode.V }, { HotkeySlot.B, KeyCode.B },
     };
     private readonly KeyCode[] numberKeys = {
         KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
         KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
         KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
     }; // must be same length as SelectionManagers controlGroups[9]

    // TODO: Replace with GameEvents.OnBuildSubmenuChanged(bool) when more systems need to react
    private bool inBuildSubmenu = false;
    public void SetBuildSubmenuActive(bool b) => inBuildSubmenu = b;

    // Drag order
    private Vector3 rightClickStart;
    private bool isDragOrdering = false;
    private float dragThreshold = 30f; // pixels
    #endregion

    #region Unity Lifecycle
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this.gameObject); return; }
        Instance = this;
    }
    
    void Start()
    {
        mainCamera = Camera.main;
        GameEvents.OnSelectionChanged += HandleSelectionChanged;
        GameEvents.OnPlacementModeChanged += HandlePlacementModeChanged;
    }

    void OnDestroy()
    {
        GameEvents.OnSelectionChanged -= HandleSelectionChanged;
        GameEvents.OnPlacementModeChanged -= HandlePlacementModeChanged;
    }

    void Update()
    {
        HandleEscape();
        HandleControlGroups();
        
        if (inBuildSubmenu || isPlacingBuilding)
        {
            HandleBuildSubmenuHotkeys();
            return;
        }
        HandleHotkeys();
        HandleRightClick();
    }
    #endregion

    #region Selection & Context
    /// Centralized escape — cancels any active mode
    void HandleEscape()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        
        if (isDragOrdering)
        {
            isDragOrdering = false;
            FormationVisualizer.Instance.HideAll();
        }
    }
    
    void HandleSelectionChanged()
    {
        currentSelected = SelectionManager.Instance.GetSelectedObjects();
        UpdateContext();
    }
    
    void HandlePlacementModeChanged(bool isPlacing) => isPlacingBuilding = isPlacing;

    void UpdateContext()
    {
        if (currentSelected.Count == 0) { currentContext = CommandContext.Default; return; }
        if (currentSelected[0] is BuildingController) { currentContext = CommandContext.BuildingSelected; return; }

        if (currentSelected[0].GetGameObject().TryGetComponent(out UnitController unit))
        {
            currentContext = unit.Stats.entityTag == EntityTag.Villager
                ? CommandContext.EconomicUnitSelected
                : CommandContext.MilitaryUnitSelected;
            return;
        }

        currentContext = CommandContext.Default;
    }
    #endregion

    #region Control Groups
    void HandleControlGroups()
    {
        for (int i = 0; i < numberKeys.Length; i++)
        {
            if (!Input.GetKeyDown(numberKeys[i])) continue;

#if UNITY_EDITOR
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                ControlGroupManager.Instance.AssignControlGroup(i);
#else
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                ControlGroupManager.Instance.AssignControlGroup(i);
#endif
            else
                ControlGroupManager.Instance.SelectControlGroup(i);

            return;
        }
    }
    
    /// Returns the shared control group index if all units belong to the same group, -1 otherwise.
    int GetSharedControlGroup(List<UnitController> units)
    {
        if (units.Count == 0) return -1;
    
        int group = units[0].GetComponent<EntityController>()?.controlGroup ?? -1;
        if (group < 0) return -1;

        foreach (UnitController u in units)
            if (u.GetComponent<EntityController>()?.controlGroup != group)
                return -1;

        return group;
    }
    #endregion
    
    #region Formations
    List<Vector2> GetFormationOffsets(List<UnitController> military, int sharedGroup)
    {
        UnitFormation formation = UnitFormation.Line;
        float width = -1f;
        List<Vector2> saved = null;

        if (sharedGroup >= 0)
        {
            ControlGroup cg = ControlGroupManager.Instance.GetControlGroup(sharedGroup);
            formation = cg.formation;
            width = cg.formationWidth;
            saved = cg.formationOffsets;

            if (saved != null && saved.Count == military.Count)
                return saved;
        }

        float defaultWidth = formation switch
        {
            UnitFormation.Line   => Mathf.Min(military.Count, 10) * FormationManager.Instance.DefaultSpacing,
            UnitFormation.Spread => Mathf.Sqrt(military.Count) * FormationManager.Instance.DefaultSpacing * 1.5f,
            UnitFormation.Box    => Mathf.Sqrt(military.Count) * FormationManager.Instance.DefaultSpacing,
            UnitFormation.Circle => military.Count * FormationManager.Instance.DefaultSpacing / (2f * Mathf.PI),
            UnitFormation.Wedge  => military.Count * FormationManager.Instance.DefaultSpacing,
            _ => military.Count * FormationManager.Instance.DefaultSpacing
        };

        float finalWidth = width > 0 ? width : defaultWidth;
        return FormationManager.Instance.CalculateOffsets(military.Count, finalWidth, formation);
    }
    #endregion

    #region Right Click
    void HandleRightClick()
    {
        if (Input.GetMouseButtonDown(1))
        {
            rightClickStart = Input.mousePosition;
            isDragOrdering = false;
        }

        if (Input.GetMouseButton(1))
        {
            if (!isDragOrdering && 
                Calc.OutOfRange(rightClickStart, Input.mousePosition, dragThreshold))
                isDragOrdering = true;

            if (isDragOrdering)
                UpdateDragFormationPreview();
        }

        if (Input.GetMouseButtonUp(1))
        {
            if (isDragOrdering)
                HandleDragRightClick();
            else
                HandleNormalRightClick();
            isDragOrdering = false;
        }
    }
    
    void HandleNormalRightClick()
    {
        Debug.Log("HandleNormalRightClick");
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        // Buildings
        bool anyBuilding = false;
        foreach (ISelectable s in currentSelected)
        {
            if (s.GetGameObject().TryGetComponent(out BuildingController b))
            {
                b.SetRallyPoint(hit.point);
                anyBuilding = true;
            }
        }
        if (anyBuilding) return;

        // Units
        List<UnitController> units = new List<UnitController>();
        foreach (ISelectable s in currentSelected)
            if (s.GetGameObject().TryGetComponent(out UnitController u))
                units.Add(u);

        if (units.Count == 0) return;

        bool hitEnemy = hit.collider.TryGetComponent(out EntityController hitEntity) &&
                        hitEntity.Stats.IsEnemy(units[0].Stats);
        bool hitNode = hit.collider.TryGetComponent(out ResourceNode node);
        bool isGroundClick = !hitEnemy && !hitNode;

        
        // Ground click with multiple units — formation move
        if (isGroundClick && units.Count > 1)
        {
            HandleGroupMove(units, hit.point);
            return;
        }

        // TODO: reconsider direct orders vs formation mode
        foreach (UnitController unit in units)
        {
            if (hitEnemy)
                unit.OrderAttack(hitEntity);
            else if (hitNode && unit.Stats.gatherAmount > 0 && unit.TryGetComponent(out VillagerController vil))
                vil.OrderGather(node);
            else
                unit.OrderMove(hit.point);
        }
    }

    // void HandleDragRightClick()
    // {
    //     Debug.Log("HandleDragRightClick");
    //     FormationVisualizer.Instance.HideAll();
    //
    //     List<UnitController> units = GetCurrentSelectedUnits();
    //     List<UnitController> military = units.FindAll(u => u is not VillagerController);
    //     if (military.Count == 0) return;
    //
    //     Ray startRay = mainCamera.ScreenPointToRay(rightClickStart);
    //     Ray endRay = mainCamera.ScreenPointToRay(Input.mousePosition);
    //
    //     if (!Physics.Raycast(startRay, out RaycastHit startHit)) return;
    //     if (!Physics.Raycast(endRay, out RaycastHit endHit)) return;
    //
    //     Vector3 destination = startHit.point;
    //     Vector3 facingDirection = Calc.DirectionFlat(startHit.point, endHit.point);
    //     float width = Calc.RealDistance(startHit.point, endHit.point);
    //
    //     int sharedGroup = GetSharedControlGroup(military);
    //
    //     List<Vector2> offsets;
    //     if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
    //     {
    //         // Alt+drag — define custom formation width, save to group
    //         UnitFormation formation = sharedGroup >= 0
    //             ? ControlGroupManager.Instance.GetControlGroup(sharedGroup).formation
    //             : UnitFormation.Line;
    //
    //         offsets = FormationManager.Instance.CalculateOffsets(military.Count, width, formation);
    //
    //         if (sharedGroup >= 0)
    //         {
    //             ControlGroupManager.Instance.GetControlGroup(sharedGroup).formationOffsets = offsets;
    //             ControlGroupManager.Instance.GetControlGroup(sharedGroup).formationWidth = width;
    //         }
    //     }
    //     else
    //     {
    //         // Normal drag — use saved offsets or default
    //         offsets = GetFormationOffsets(military, sharedGroup);
    //     }
    //
    //     List<Vector3> worldPositions = FormationManager.Instance.OffsetsToWorldPositions(
    //         offsets, destination, facingDirection);
    //
    //     // Villagers go direct
    //     Vector3 tempVilMoveOffset = new  Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f)); // new check temp
    //     foreach (UnitController v in units.FindAll(u => u is VillagerController))
    //         v.OrderMove(destination +  tempVilMoveOffset);
    //
    //     if (sharedGroup >= 0 && !Input.GetKey(KeyCode.LeftAlt))
    //     {
    //         // Control group — move anchor
    //         ControlGroup cg = ControlGroupManager.Instance.GetControlGroup(sharedGroup);
    //         float speed = military.Min(u => u.Stats.moveSpeed);
    //         if (cg.anchor == null)
    //             cg.anchor = new FormationAnchor(GetAveragePosition(military), facingDirection, speed);
    //         cg.anchor.speed = speed;
    //         cg.anchor.MoveTo(destination, facingDirection);
    //         FormationVisualizer.Instance.ShowSlots(worldPositions);
    //     }
    //     else
    //     {
    //         // No control group or alt drag — direct slot assignment
    //         var slots = FormationManager.Instance.AssignNearestSlots(military, worldPositions);
    //         foreach (var slot in slots) slot.Key.OrderMove(slot.Value);
    //         FormationVisualizer.Instance.ShowSlots(new List<Vector3>(slots.Values));
    //     }
    // }
    
    void HandleDragRightClick()
    {
        FormationVisualizer.Instance.HideAll();

        List<UnitController> units = GetCurrentSelectedUnits();
        List<UnitController> military = units.FindAll(u => u is not VillagerController);

        if (military.Count == 0)
            return;

        Ray startRay = mainCamera.ScreenPointToRay(rightClickStart);
        Ray endRay = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(startRay, out RaycastHit startHit))
            return;

        if (!Physics.Raycast(endRay, out RaycastHit endHit))
            return;

        Vector3 destination = startHit.point;
        Vector3 facingDirection = Calc.DirectionFlat(startHit.point, endHit.point);
        float width = Calc.RealDistance(startHit.point, endHit.point);

        if (facingDirection == Vector3.zero)
            facingDirection = military[0].transform.forward;

        int sharedGroup = GetSharedControlGroup(military);

        // Determine formation offsets
        List<Vector2> offsets;

        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
        {
            UnitFormation formation = sharedGroup >= 0
                ? ControlGroupManager.Instance.GetControlGroup(sharedGroup).formation
                : UnitFormation.Line;

            offsets = FormationManager.Instance.CalculateOffsets(
                military.Count,
                width,
                formation);

            // Save custom width/offsets to control group
            if (sharedGroup >= 0)
            {
                ControlGroup cg = ControlGroupManager.Instance.GetControlGroup(sharedGroup);

                cg.formationOffsets = offsets;
                cg.formationWidth = width;
            }
        }
        else
        {
            offsets = GetFormationOffsets(military, sharedGroup);
        }

        List<Vector3> worldPositions =
            FormationManager.Instance.OffsetsToWorldPositions(
                offsets,
                destination,
                facingDirection);

        // Villagers move directly
        foreach (UnitController villager in units.FindAll(u => u is VillagerController))
        {
            Vector3 offset = new Vector3(
                Random.Range(-2f, 2f),
                0f,
                Random.Range(-2f, 2f));

            villager.OrderMove(destination + offset);
        }

        // Control group logic
        if (sharedGroup >= 0)
        {
            ControlGroup cg = ControlGroupManager.Instance.GetControlGroup(sharedGroup);

            float speed = military.Min(u => u.Stats.moveSpeed);

            if (cg.anchor == null)
            {
                cg.anchor = new FormationAnchor(
                    GetAveragePosition(military),
                    facingDirection,
                    speed);
            }

            cg.anchor.speed = speed;
            cg.anchor.MoveTo(destination, facingDirection);

            if (!cg.formationMode)
            {
                var slots = FormationManager.Instance.AssignNearestSlots(
                    military,
                    worldPositions);

                foreach (var slot in slots)
                    slot.Key.OrderMove(slot.Value);
            }

            FormationVisualizer.Instance.ShowSlots(worldPositions);
            return;
        }

        // Temporary formation (no control group)
        {
            var slots = FormationManager.Instance.AssignNearestSlots(
                military,
                worldPositions);

            foreach (var slot in slots)
                slot.Key.OrderMove(slot.Value);

            FormationVisualizer.Instance.ShowSlots(
                new List<Vector3>(slots.Values));
        }
    }
    
    void HandleGroupMove(List<UnitController> units, Vector3 destination,
        Vector3 facingDirection = default)
    {
        List<UnitController> military = units.FindAll(u => u is MilitaryController);
        List<UnitController> villagers = units.FindAll(u => u is VillagerController);

        foreach (UnitController v in villagers)
            v.OrderMove(destination);

        if (military.Count == 0) return;

        Vector3 facing = facingDirection != default
            ? facingDirection
            : Calc.DirectionFlat(GetAveragePosition(military), destination);
        
        if (facing == Vector3.zero)
            facing = military[0].transform.forward; 

        
        // Control Group Move
        int sharedGroup = GetSharedControlGroup(military);
        
        if (sharedGroup >= 0)
        {
            Debug.Log("HandleGroupMove - Control Group Move: " + sharedGroup);

            ControlGroup cg = ControlGroupManager.Instance.GetControlGroup(sharedGroup);

            if (cg.formationOffsets.Count != military.Count)
            {
                float width = cg.formationWidth > 0
                    ? cg.formationWidth
                    : military.Count * FormationManager.Instance.DefaultSpacing;

                cg.formationOffsets = FormationManager.Instance.CalculateOffsets(
                    military.Count,
                    width,
                    cg.formation);
            }

            float speed = military.Min(u => u.Stats.moveSpeed);

            if (cg.anchor == null)
                cg.anchor = new FormationAnchor(GetAveragePosition(military), facing, speed);

            cg.anchor.speed = speed;

            List<Vector3> destSlots = FormationManager.Instance.OffsetsToWorldPositions(
                cg.formationOffsets,
                destination,
                facing);

            if (cg.formationMode)
            {
                List<MilitaryController> mil = military
                    .OfType<MilitaryController>()
                    .ToList();

                Vector3 anchorStart = FormationManager.Instance.EstimateAnchorPosition(
                    mil,
                    cg.formationOffsets,
                    facing);

                cg.anchor.position = anchorStart;
                cg.anchor.facing = facing;
                cg.anchor.destination = anchorStart;
                cg.anchor.isMoving = false;

                cg.anchor.MoveTo(destination, facing);

                FormationVisualizer.Instance.ShowSlots(destSlots);
            }
            else
            {
                var slots = FormationManager.Instance.AssignNearestSlots(military, destSlots);

                foreach (var slot in slots)
                    slot.Key.OrderMove(slot.Value);

                FormationVisualizer.Instance.ShowSlots(new List<Vector3>(slots.Values));
            }

            return;
        }
        else
        {
            Debug.Log("No control group - direct formation move");
            // No control group - direct formation move
            List<Vector2> offsets = GetFormationOffsets(military, -1);
            List<Vector3> worldPositions = FormationManager.Instance.OffsetsToWorldPositions(
                offsets, destination, facing);
            var slots = FormationManager.Instance.AssignNearestSlots(military, worldPositions);
            foreach (var slot in slots) slot.Key.OrderMove(slot.Value);
            FormationVisualizer.Instance.ShowSlots(new List<Vector3>(slots.Values));
        }
    }

    Vector3 GetAveragePosition(List<UnitController> units)
    {
        Vector3 avg = Vector3.zero;
        foreach (UnitController u in units) avg += u.transform.position;
        return avg / units.Count;
    }

    void UpdateDragFormationPreview()
    {
        List<UnitController> units = GetCurrentSelectedUnits();
        List<UnitController> military = units.FindAll(u => u is not VillagerController);
        if (military.Count == 0) return;

        Ray startRay = mainCamera.ScreenPointToRay(rightClickStart);
        Ray endRay = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(startRay, out RaycastHit startHit)) return;
        if (!Physics.Raycast(endRay, out RaycastHit endHit)) return;

        Vector3 destination = startHit.point;
        Vector3 facingDirection = Calc.DirectionFlat(startHit.point, endHit.point);
        float width = Calc.RealDistance(startHit.point, endHit.point);

        int sharedGroup = GetSharedControlGroup(military);

        List<Vector2> offsets;
        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
        {
            UnitFormation formation = sharedGroup >= 0
                ? ControlGroupManager.Instance.GetControlGroup(sharedGroup).formation
                : UnitFormation.Line;
            offsets = FormationManager.Instance.CalculateOffsets(military.Count, width, formation);
        }
        else
            offsets = GetFormationOffsets(military, sharedGroup);

        List<Vector3> worldPositions = FormationManager.Instance.OffsetsToWorldPositions(
            offsets, destination, facingDirection);

        FormationVisualizer.Instance.ShowSlots(worldPositions, persistent: true);
    }
    #endregion

    #region Hotkeys
    void HandleHotkeys()
    {
        switch (currentContext)
        {
            case CommandContext.Default:
                HandleDefaultHotkeys();
                break;
            case CommandContext.EconomicUnitSelected:
            case CommandContext.MilitaryUnitSelected:
                HandleUnitHotkeys();
                break;
            case CommandContext.BuildingSelected:
                HandleProductionBuildingHotkeys();
                break;
        }
    }

    void HandleDefaultHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.T))
            BuildingPlacer.Instance.StartPlacing(townCenterData);
        else if (Input.GetKeyDown(KeyCode.B))
            BuildingPlacer.Instance.StartPlacing(barracksData);
    }

    void HandleUnitHotkeys()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Physics.Raycast(ray, out RaycastHit hit);

        HotkeySlot pressedSlot = HotkeySlot.None;
        foreach (KeyValuePair<HotkeySlot, KeyCode> kvp in slotToKey)
            if (Input.GetKeyDown(kvp.Value)) { pressedSlot = kvp.Key; break; }
    
        if (pressedSlot == HotkeySlot.None) return;
        
        Debug.Log(pressedSlot);

        foreach (ISelectable s in currentSelected)
        {
            if (!s.GetGameObject().TryGetComponent(out CommandController cc)) continue;
            if (!s.GetGameObject().TryGetComponent(out UnitController unit)) continue;

            CommandData matchingCmd = unit.Stats.baseData.baseCommands.Find(c => c.hotkey == pressedSlot);
            if (matchingCmd == null) continue;

            // Build command — open submenu
            if (matchingCmd.commandType == CommandType.Build)
            {
                UIManager.Instance.ShowActionPanelBuildSubmenu(unit);
                return;
            }

            // Group scope — execute once then return
            if (matchingCmd.commandScope == CommandScope.Group) // Temporary?
            {
                ExecuteGroupCommand(matchingCmd.commandType);
                return;
            }

            // Per unit scope — execute on all selected units
            cc.ExecuteHotkeyCommand(pressedSlot, hit);
        }
    }

    void HandleProductionBuildingHotkeys()
    {
        BuildingController firstBuilding = null;
        BuildingType type = BuildingType.None;
    
        foreach (ISelectable s in currentSelected)
        {
            if (s is not BuildingController bc) return;
            if (type == BuildingType.None) { type = bc.Stats.baseData.buildingType; firstBuilding = bc; }
            else if (bc.Stats.baseData.buildingType != type) return;
        }

        if (firstBuilding == null) return;

        foreach (ProductionOptionData option in firstBuilding.Stats.baseData.productionOptions)
        {
            if (option.hotkey == HotkeySlot.None) continue;
            if (!Input.GetKeyDown(slotToKey[option.hotkey])) continue;

            foreach (ISelectable s in currentSelected)
                if (s is BuildingController bc)
                    bc.EnqueueProduction(option);
            return;
        }
    }
    
    void HandleBuildSubmenuHotkeys()
    {
        if (currentSelected.Count == 0) return;
        if (currentSelected[0] is not UnitController unit) return;
        if (unit.Stats.baseData.unitType != UnitType.Villager) return;

        foreach (BuildOptionData option in unit.Stats.baseData.buildOptions
            .Where(option => Input.GetKeyDown(slotToKey[option.hotkey])))
        {
            BuildingPlacer.Instance.StartPlacing(option);
            return;
        }
    }
    #endregion

    #region Group Commands
    /// Executes a group-scoped command — acts on control group, not individual units
    public void ExecuteGroupCommand(CommandType cmd)
    {
        // Get shared group from current military selection
        List<UnitController> military = GetCurrentSelectedUnits()
            .FindAll(u => u is not VillagerController);
        int sharedGroup = GetSharedControlGroup(military);

        switch (cmd)
        {
            case CommandType.ToggleFormationMode:
                if (sharedGroup >= 0)
                    ControlGroupManager.Instance.ToggleFormationMode(sharedGroup);
                break;

            //     case CommandType.FormationLine:
            //         if (sharedGroup >= 0) ControlGroupManager.Instance.SetFormation(sharedGroup, UnitFormation.Line);
            //         else FormationManager.Instance.ReformTemporaryWithFormation(military, UnitFormation.Line);
            //         break;
            //     case CommandType.FormationSpread:
            //         if (sharedGroup >= 0) ControlGroupManager.Instance.SetFormation(sharedGroup, UnitFormation.Spread);
            //         else FormationManager.Instance.ReformTemporaryWithFormation(military, UnitFormation.Spread);
            //         break;
            //     case CommandType.FormationBox:
            //         if (sharedGroup >= 0) ControlGroupManager.Instance.SetFormation(sharedGroup, UnitFormation.Box);
            //         else FormationManager.Instance.ReformTemporaryWithFormation(military, UnitFormation.Box);
            //         break;
            //     case CommandType.FormationCircle:
            //         if (sharedGroup >= 0) ControlGroupManager.Instance.SetFormation(sharedGroup, UnitFormation.Circle);
            //         else FormationManager.Instance.ReformTemporaryWithFormation(military, UnitFormation.Circle);
            //         break;
            //     case CommandType.FormationWedge:
            //         if (sharedGroup >= 0) ControlGroupManager.Instance.SetFormation(sharedGroup, UnitFormation.Wedge);
            //         else FormationManager.Instance.ReformTemporaryWithFormation(military, UnitFormation.Wedge);
            //         break;
            // }

            case CommandType.FormationLine:
                if (sharedGroup >= 0) ControlGroupManager.Instance.SetFormation(sharedGroup, UnitFormation.Line);
                else FormationManager.Instance.ReformTemporaryWithFormation(military, UnitFormation.Line);
                break;
            case CommandType.FormationSpread:
                if (sharedGroup >= 0) ControlGroupManager.Instance.SetFormation(sharedGroup, UnitFormation.Spread);
                else FormationManager.Instance.ReformTemporaryWithFormation(military, UnitFormation.Spread);
                break;
            case CommandType.FormationBox:
                if (sharedGroup >= 0) ControlGroupManager.Instance.SetFormation(sharedGroup, UnitFormation.Box);
                else FormationManager.Instance.ReformTemporaryWithFormation(military, UnitFormation.Box);
                break;
            case CommandType.FormationCircle:
                if (sharedGroup >= 0) ControlGroupManager.Instance.SetFormation(sharedGroup, UnitFormation.Circle);
                else FormationManager.Instance.ReformTemporaryWithFormation(military, UnitFormation.Circle);
                break;
            case CommandType.FormationWedge:
                if (sharedGroup >= 0) ControlGroupManager.Instance.SetFormation(sharedGroup, UnitFormation.Wedge);
                else FormationManager.Instance.ReformTemporaryWithFormation(military, UnitFormation.Wedge);
                break;
        }
    }
        
    // void ReformTemporaryWithFormation(List<UnitController> military, UnitFormation formation) // IN FORMATION MANAGER
    // {
    //     Debug.Log("PlayerInputHandler.ReformTemporaryWithFormation: " + formation.ToString());
    //     float width = Mathf.Min(military.Count, 10) * FormationManager.Instance.DefaultSpacing;
    //     List<Vector2> offsets = FormationManager.Instance.CalculateOffsets(military.Count, width, formation);
    //     FormationManager.Instance.ReformInPlaceTemporary(SelectionManager.Instance.GetSelectedObjects(), offsets);
    // }
    #endregion
    
    
}