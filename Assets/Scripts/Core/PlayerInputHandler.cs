// SESSION: Squad Control Refactor

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    public static PlayerInputHandler Instance { get; private set; }

    #region Fields

    [Header("Default Build Hotkeys")]
    [SerializeField] private BuildOptionData townCenterData;
    [SerializeField] private BuildOptionData barracksData;

    [Header("Input")]
    [SerializeField] private float dragThreshold = 30f;

    [Header("Squad Multi-Move")]
    [SerializeField] private float multiSquadSpacing = 6f;

    private Camera mainCamera;

    private CommandContext currentContext = CommandContext.Default;
    private bool isPlacingBuilding = false;
    private bool inBuildSubmenu = false;

    private List<ISelectable> currentSelected = new List<ISelectable>();

    private Vector3 rightClickStart;
    private bool isDragOrdering = false;

    private readonly Dictionary<HotkeySlot, KeyCode> slotToKey = new Dictionary<HotkeySlot, KeyCode>()
    {
        { HotkeySlot.Q, KeyCode.Q },
        { HotkeySlot.W, KeyCode.W },
        { HotkeySlot.E, KeyCode.E },
        { HotkeySlot.R, KeyCode.R },
        { HotkeySlot.T, KeyCode.T },

        { HotkeySlot.A, KeyCode.A },
        { HotkeySlot.S, KeyCode.S },
        { HotkeySlot.D, KeyCode.D },
        { HotkeySlot.F, KeyCode.F },
        { HotkeySlot.G, KeyCode.G },

        { HotkeySlot.Z, KeyCode.Z },
        { HotkeySlot.X, KeyCode.X },
        { HotkeySlot.C, KeyCode.C },
        { HotkeySlot.V, KeyCode.V },
        { HotkeySlot.B, KeyCode.B },
    };

    private readonly KeyCode[] numberKeys =
    {
        KeyCode.Alpha1,
        KeyCode.Alpha2,
        KeyCode.Alpha3,
        KeyCode.Alpha4,
        KeyCode.Alpha5,
        KeyCode.Alpha6,
        KeyCode.Alpha7,
        KeyCode.Alpha8,
        KeyCode.Alpha9
    };

    public void SetBuildSubmenuActive(bool b) => inBuildSubmenu = b;

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
        HandleControlGroupHotkeys();

        if (inBuildSubmenu || isPlacingBuilding)
        {
            HandleBuildSubmenuHotkeys();
            return;
        }

        HandleHotkeys();
        HandleRightClick();
    }

    #endregion

    #region Selection / Context

    void HandleSelectionChanged()
    {
        currentSelected = SelectionManager.Instance.GetSelectedObjects();
        UpdateContext();
    }

    void HandlePlacementModeChanged(bool isPlacing)
    {
        isPlacingBuilding = isPlacing;
    }

    void UpdateContext()
    {
        if (currentSelected.Count == 0)
        {
            currentContext = CommandContext.Default;
            return;
        }

        ISelectable first = currentSelected[0];

        if (first is BuildingController)
        {
            currentContext = CommandContext.BuildingSelected;
            return;
        }

        if (first is SquadController)
        {
            currentContext = CommandContext.MilitarySquadSelected;
            return;
        }

        if (first.GetGameObject().TryGetComponent(out VillagerController _))
        {
            currentContext = CommandContext.EconomicUnitSelected;
            return;
        }

        currentContext = CommandContext.Default;
    }

    #endregion

    #region Escape

    void HandleEscape()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (isDragOrdering)
        {
            isDragOrdering = false;
            FormationVisualizer.Instance?.HideAll();
            return;
        }

        SelectionManager.Instance.DeselectAll();
    }

    #endregion

    #region Control Groups

    void HandleControlGroupHotkeys()
    {
        // ControlGroupManager will become simple selection bookmarks later.
        // For now this is intentionally disabled so old formation/control-group behavior cannot leak in.

        for (int i = 0; i < numberKeys.Length; i++)
        {
            if (!Input.GetKeyDown(numberKeys[i]))
                continue;

            // Later:
            // Ctrl/Alt + number = assign current selected squads/buildings/villagers.
            // number = select bookmarked objects.

            return;
        }
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
            {
                isDragOrdering = true;
            }

            if (isDragOrdering)
                UpdateDragSquadPreview();
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
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        List<BuildingController> buildings = GetCurrentSelectedBuildings();
        if (buildings.Count > 0)
        {
            foreach (BuildingController building in buildings)
                building.SetRallyPoint(hit.point);

            return;
        }

        List<SquadController> squads = GetCurrentSelectedSquads();
        if (squads.Count > 0)
        {
            HandleSquadRightClick(squads, hit);
            return;
        }

        List<UnitController> units = GetCurrentSelectedUnits();
        if (units.Count > 0)
            HandleUnitRightClick(units, hit);
    }

    void HandleDragRightClick()
    {
        FormationVisualizer.Instance?.HideAll();

        List<SquadController> squads = GetCurrentSelectedSquads();

        if (squads.Count > 0)
        {
            HandleSquadDragRightClick(squads);
            return;
        }

        // Drag-right-click is squad-only.
        // Villagers keep normal right-click behavior.
    }

    void HandleSquadRightClick(List<SquadController> squads, RaycastHit hit)
    {
        bool hitEnemy = hit.collider.TryGetComponent(out EntityController hitEntity) &&
                        IsEnemyToSquads(squads, hitEntity);

        if (hitEnemy)
        {
            foreach (SquadController squad in squads)
                squad.OrderAttack(hitEntity);

            return;
        }

        Vector3 groupFacing = ResolveFacingForSquads(squads, hit.point);

        for (int i = 0; i < squads.Count; i++)
        {
            Vector3 offset = GetSquadMoveOffset(i, squads.Count, groupFacing);
            squads[i].OrderMove(hit.point + offset);
        }
    }

    void HandleSquadDragRightClick(List<SquadController> squads)
    {
        Ray startRay = mainCamera.ScreenPointToRay(rightClickStart);
        Ray endRay = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(startRay, out RaycastHit startHit)) return;
        if (!Physics.Raycast(endRay, out RaycastHit endHit)) return;

        Vector3 destination = startHit.point;
        Vector3 facing = Calc.DirectionFlat(startHit.point, endHit.point);
        float width = Calc.RealDistance(startHit.point, endHit.point);

        if (facing == Vector3.zero)
            facing = ResolveFacingForSquads(squads, destination);

        for (int i = 0; i < squads.Count; i++)
        {
            Vector3 offset = GetSquadMoveOffset(i, squads.Count, facing);
            squads[i].OrderMove(destination + offset, facing, width);
        }
    }

    void UpdateDragSquadPreview()
    {
        List<SquadController> squads = GetCurrentSelectedSquads();
        if (squads.Count == 0) return;

        Ray startRay = mainCamera.ScreenPointToRay(rightClickStart);
        Ray endRay = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(startRay, out RaycastHit startHit)) return;
        if (!Physics.Raycast(endRay, out RaycastHit endHit)) return;

        Vector3 destination = startHit.point;
        Vector3 facing = Calc.DirectionFlat(startHit.point, endHit.point);
        float width = Calc.RealDistance(startHit.point, endHit.point);

        if (facing == Vector3.zero)
            facing = ResolveFacingForSquads(squads, destination);

        List<Vector3> previewSlots = new List<Vector3>();

        for (int i = 0; i < squads.Count; i++)
        {
            Vector3 offset = GetSquadMoveOffset(i, squads.Count, facing);

            previewSlots.AddRange(
                squads[i].GetPreviewSlots(
                    destination + offset,
                    facing,
                    width));
        }

        FormationVisualizer.Instance?.ShowSlots(previewSlots, persistent: true);
    }

    #endregion

    #region Unit Right Click

    void HandleUnitRightClick(List<UnitController> units, RaycastHit hit)
    {
        if (units.Count == 0) return;

        bool hitEnemy = hit.collider.TryGetComponent(out EntityController hitEntity) &&
                        hitEntity.Stats.IsEnemy(units[0].Stats);

        bool hitNode = hit.collider.TryGetComponent(out ResourceNode node);

        foreach (UnitController unit in units)
        {
            if (hitEnemy)
            {
                unit.OrderAttack(hitEntity);
            }
            else if (hitNode &&
                     unit.TryGetComponent(out VillagerController villager) &&
                     unit.Stats.gatherAmount > 0)
            {
                villager.OrderGather(node);
            }
            else
            {
                unit.OrderMove(hit.point);
            }
        }
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
                HandleSelectedUnitHotkeys();
                break;

            case CommandContext.MilitarySquadSelected:
                HandleSelectedSquadHotkeys();
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

    void HandleSelectedSquadHotkeys()
    {
        HotkeySlot pressedSlot = GetPressedHotkeySlot();
        if (pressedSlot == HotkeySlot.None) return;

        CommandType? command = GetSquadCommandForHotkey(pressedSlot);
        if (command == null) return;

        ExecuteSquadCommand(command.Value);
    }

    void HandleSelectedUnitHotkeys()
    {
        HotkeySlot pressedSlot = GetPressedHotkeySlot();
        if (pressedSlot == HotkeySlot.None) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Physics.Raycast(ray, out RaycastHit hit);

        foreach (ISelectable selectable in currentSelected)
        {
            GameObject go = selectable.GetGameObject();

            if (!go.TryGetComponent(out CommandController commandController)) continue;
            if (!go.TryGetComponent(out UnitController unit)) continue;

            CommandData command = unit.Stats.baseData.baseCommands.Find(c => c.hotkey == pressedSlot);
            if (command == null) continue;

            if (command.commandType == CommandType.Build)
            {
                UIManager.Instance.ShowActionPanelBuildSubmenu(unit);
                return;
            }

            commandController.ExecuteHotkeyCommand(pressedSlot, hit);
        }
    }

    void HandleProductionBuildingHotkeys()
    {
        BuildingController firstBuilding = null;
        BuildingType type = BuildingType.None;

        foreach (ISelectable selectable in currentSelected)
        {
            if (selectable is not BuildingController building)
                return;

            if (type == BuildingType.None)
            {
                type = building.Stats.baseData.buildingType;
                firstBuilding = building;
            }
            else if (building.Stats.baseData.buildingType != type)
            {
                return;
            }
        }

        if (firstBuilding == null) return;

        foreach (ProductionOptionData option in firstBuilding.Stats.baseData.productionOptions)
        {
            if (option.hotkey == HotkeySlot.None) continue;
            if (!slotToKey.ContainsKey(option.hotkey)) continue;
            if (!Input.GetKeyDown(slotToKey[option.hotkey])) continue;

            foreach (ISelectable selectable in currentSelected)
            {
                if (selectable is BuildingController building)
                    building.EnqueueProduction(option);
            }

            return;
        }
    }

    void HandleBuildSubmenuHotkeys()
    {
        if (currentSelected.Count == 0) return;
        if (currentSelected[0] is not UnitController unit) return;
        if (unit.Stats.baseData.unitType != UnitType.Villager) return;

        foreach (BuildOptionData option in unit.Stats.baseData.buildOptions)
        {
            if (option.hotkey == HotkeySlot.None) continue;
            if (!slotToKey.ContainsKey(option.hotkey)) continue;
            if (!Input.GetKeyDown(slotToKey[option.hotkey])) continue;

            BuildingPlacer.Instance.StartPlacing(option);
            return;
        }
    }

    HotkeySlot GetPressedHotkeySlot()
    {
        foreach (KeyValuePair<HotkeySlot, KeyCode> kvp in slotToKey)
        {
            if (Input.GetKeyDown(kvp.Value))
                return kvp.Key;
        }

        return HotkeySlot.None;
    }

    #endregion

    #region Squad Commands

    // Keep this public method for ActionButtonUI compatibility.
    // Internally, this is squad-first now.
    public void ExecuteGroupCommand(CommandType commandType)
    {
        ExecuteSquadCommand(commandType);
    }

    public void ExecuteSquadCommand(CommandType commandType)
    {
        List<SquadController> squads = GetCurrentSelectedSquads();
        if (squads.Count == 0) return;

        foreach (SquadController squad in squads)
            ExecuteSquadCommandOnSquad(squad, commandType);
    }

    void ExecuteSquadCommandOnSquad(SquadController squad, CommandType commandType)
    {
        if (squad == null) return;

        switch (commandType)
        {
            case CommandType.Stop:
                squad.OrderStop();
                return;

            case CommandType.Aggressive:
                squad.SetStance(CombatStance.Aggressive);
                return;

            case CommandType.Defensive:
                squad.SetStance(CombatStance.Defensive);
                return;

            case CommandType.StandGround:
                squad.SetStance(CombatStance.StandGround);
                return;

            case CommandType.NoAttack:
                squad.SetStance(CombatStance.NoAttack);
                return;

            case CommandType.FormationLine:
                squad.SetFormation(SquadFormation.Line);
                return;

            case CommandType.FormationSpread:
                squad.SetFormation(SquadFormation.Spread);
                return;

            case CommandType.FormationBox:
                squad.SetFormation(SquadFormation.Box);
                return;

            case CommandType.FormationCircle:
                squad.SetFormation(SquadFormation.Circle);
                return;

            case CommandType.FormationWedge:
                squad.SetFormation(SquadFormation.Wedge);
                return;

            case CommandType.AttackMove:
                // TODO:
                // Later this should call something like squad.OrderAttackMove(destination)
                // or enter an attack-move targeting mode.
                return;
        }
    }

    CommandType? GetSquadCommandForHotkey(HotkeySlot slot)
    {
        // Temporary hardcoded squad command map.
        // Later this can come from SquadCommandData / SquadController.GetAllCommands().

        switch (slot)
        {
            case HotkeySlot.Q:
                return CommandType.Stop;

            case HotkeySlot.A:
                return CommandType.Aggressive;

            case HotkeySlot.S:
                return CommandType.Defensive;

            case HotkeySlot.D:
                return CommandType.StandGround;

            case HotkeySlot.F:
                return CommandType.NoAttack;

            case HotkeySlot.Z:
                return CommandType.FormationLine;

            case HotkeySlot.X:
                return CommandType.FormationSpread;

            case HotkeySlot.C:
                return CommandType.FormationBox;

            case HotkeySlot.V:
                return CommandType.FormationCircle;

            case HotkeySlot.B:
                return CommandType.FormationWedge;
        }

        return null;
    }

    #endregion

    #region Helpers

    List<SquadController> GetCurrentSelectedSquads()
    {
        return currentSelected
            .Select(s => s.GetGameObject().GetComponent<SquadController>())
            .Where(s => s != null)
            .ToList();
    }

    List<UnitController> GetCurrentSelectedUnits()
    {
        return currentSelected
            .Select(s => s.GetGameObject().GetComponent<UnitController>())
            .Where(u => u != null)
            .ToList();
    }

    List<BuildingController> GetCurrentSelectedBuildings()
    {
        return currentSelected
            .OfType<BuildingController>()
            .ToList();
    }

    bool IsEnemyToSquads(List<SquadController> squads, EntityController target)
    {
        if (target == null || target.Stats == null)
            return false;

        foreach (SquadController squad in squads)
        {
            if (squad == null || squad.Faction == null) continue;
            if (target.Stats.faction == null) continue;

            if (target.Stats.faction.teamId != squad.Faction.teamId)
                return true;
        }

        return false;
    }

    Vector3 ResolveFacingForSquads(List<SquadController> squads, Vector3 destination)
    {
        if (squads == null || squads.Count == 0)
            return Vector3.forward;

        Vector3 center = GetAverageSquadPosition(squads);
        Vector3 facing = Calc.DirectionFlat(center, destination);

        if (facing == Vector3.zero)
            facing = squads[0].transform.forward;

        facing.y = 0f;

        return facing == Vector3.zero
            ? Vector3.forward
            : facing.normalized;
    }

    Vector3 GetAverageSquadPosition(List<SquadController> squads)
    {
        Vector3 avg = Vector3.zero;
        int count = 0;

        foreach (SquadController squad in squads)
        {
            if (squad == null) continue;

            avg += squad.transform.position;
            count++;
        }

        return count > 0 ? avg / count : Vector3.zero;
    }

    Vector3 GetSquadMoveOffset(int index, int count, Vector3 facing)
    {
        if (count <= 1)
            return Vector3.zero;

        facing.y = 0f;

        if (facing == Vector3.zero)
            facing = Vector3.forward;

        facing.Normalize();

        Vector3 right = Calc.Perpendicular(facing);

        int rowSize = Mathf.CeilToInt(Mathf.Sqrt(count));
        int row = index / rowSize;
        int col = index % rowSize;

        float x = (col - (rowSize - 1) * 0.5f) * multiSquadSpacing;
        float z = -row * multiSquadSpacing;

        return right * x + facing * z;
    }

    #endregion
}




// // SESSION: Squad Control
//
// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;
//
// public class PlayerInputHandler : MonoBehaviour
// {
//     public static PlayerInputHandler Instance { get; private set; }
//
//     #region Fields
//
//     [Header("Default Build Hotkeys")]
//     [SerializeField] private BuildOptionData townCenterData;
//     [SerializeField] private BuildOptionData barracksData;
//
//     [Header("Input")]
//     [SerializeField] private float dragThreshold = 30f;
//
//     [Header("Squad Multi-Move")]
//     [SerializeField] private float multiSquadSpacing = 6f;
//
//     private Camera mainCamera;
//
//     private CommandContext currentContext = CommandContext.Default;
//     private bool isPlacingBuilding = false;
//     private bool inBuildSubmenu = false;
//
//     private List<ISelectable> currentSelected = new List<ISelectable>();
//
//     private Vector3 rightClickStart;
//     private bool isDragOrdering = false;
//
//     private readonly Dictionary<HotkeySlot, KeyCode> slotToKey = new Dictionary<HotkeySlot, KeyCode>()
//     {
//         { HotkeySlot.Q, KeyCode.Q }, { HotkeySlot.W, KeyCode.W }, { HotkeySlot.E, KeyCode.E },
//         { HotkeySlot.R, KeyCode.R }, { HotkeySlot.T, KeyCode.T }, { HotkeySlot.A, KeyCode.A },
//         { HotkeySlot.S, KeyCode.S }, { HotkeySlot.D, KeyCode.D }, { HotkeySlot.F, KeyCode.F },
//         { HotkeySlot.G, KeyCode.G }, { HotkeySlot.Z, KeyCode.Z }, { HotkeySlot.X, KeyCode.X },
//         { HotkeySlot.C, KeyCode.C }, { HotkeySlot.V, KeyCode.V }, { HotkeySlot.B, KeyCode.B },
//     };
//
//     private readonly KeyCode[] numberKeys =
//     {
//         KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
//         KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
//         KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
//     };
//
//     public void SetBuildSubmenuActive(bool b) => inBuildSubmenu = b;
//
//     #endregion
//
//     #region Unity Lifecycle
//
//     void Awake()
//     {
//         if (Instance != null && Instance != this)
//         {
//             Destroy(gameObject);
//             return;
//         }
//
//         Instance = this;
//     }
//
//     void Start()
//     {
//         mainCamera = Camera.main;
//
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
//         HandleControlGroupHotkeys();
//
//         if (inBuildSubmenu || isPlacingBuilding)
//         {
//             HandleBuildSubmenuHotkeys();
//             return;
//         }
//
//         HandleHotkeys();
//         HandleRightClick();
//     }
//
//     #endregion
//
//     #region Selection / Context
//
//     void HandleSelectionChanged()
//     {
//         currentSelected = SelectionManager.Instance.GetSelectedObjects();
//         UpdateContext();
//     }
//
//     void HandlePlacementModeChanged(bool isPlacing)
//     {
//         isPlacingBuilding = isPlacing;
//     }
//
//     void UpdateContext()
//     {
//         if (currentSelected.Count == 0)
//         {
//             currentContext = CommandContext.Default;
//             return;
//         }
//
//         ISelectable first = currentSelected[0];
//
//         if (first is BuildingController)
//         {
//             currentContext = CommandContext.BuildingSelected;
//             return;
//         }
//
//         if (first is SquadController)
//         {
//             currentContext = CommandContext.MilitarySquadSelected;
//             return;
//         }
//
//         if (first.GetGameObject().TryGetComponent(out VillagerController _))
//         {
//             currentContext = CommandContext.EconomicUnitSelected;
//             return;
//         }
//
//         currentContext = CommandContext.Default;
//     }
//
//     #endregion
//
//     #region Escape
//
//     void HandleEscape()
//     {
//         if (!Input.GetKeyDown(KeyCode.Escape)) return;
//
//         if (isDragOrdering)
//         {
//             isDragOrdering = false;
//             FormationVisualizer.Instance?.HideAll();
//             return;
//         }
//
//         SelectionManager.Instance.DeselectAll();
//     }
//
//     #endregion
//
//     #region Control Groups
//
//     void HandleControlGroupHotkeys()
//     {
//         // TEMP:
//         // ControlGroupManager is being refactored into simple selection bookmarks.
//         // Leave this disabled until the new SelectionGroup/ControlGroup system is rewritten.
//
//         for (int i = 0; i < numberKeys.Length; i++)
//         {
//             if (!Input.GetKeyDown(numberKeys[i])) continue;
//
//             // Later:
//             // Ctrl/Alt + number = assign current selection bookmark.
//             // number = select bookmark.
//
//             return;
//         }
//     }
//
//     #endregion
//
//     #region Right Click
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
//                 Calc.OutOfRange(rightClickStart, Input.mousePosition, dragThreshold))
//             {
//                 isDragOrdering = true;
//             }
//
//             if (isDragOrdering)
//                 UpdateDragSquadPreview();
//         }
//
//         if (Input.GetMouseButtonUp(1))
//         {
//             if (isDragOrdering)
//                 HandleDragRightClick();
//             else
//                 HandleNormalRightClick();
//
//             isDragOrdering = false;
//         }
//     }
//
//     void HandleNormalRightClick()
//     {
//         Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
//         if (!Physics.Raycast(ray, out RaycastHit hit)) return;
//
//         List<SquadController> squads = GetCurrentSelectedSquads();
//         List<UnitController> units = GetCurrentSelectedUnits();
//         List<BuildingController> buildings = GetCurrentSelectedBuildings();
//
//         if (buildings.Count > 0)
//         {
//             foreach (BuildingController building in buildings)
//                 building.SetRallyPoint(hit.point);
//
//             return;
//         }
//
//         if (squads.Count > 0)
//         {
//             HandleSquadRightClick(squads, hit);
//             return;
//         }
//
//         if (units.Count > 0)
//         {
//             HandleUnitRightClick(units, hit);
//         }
//     }
//
//     void HandleDragRightClick()
//     {
//         FormationVisualizer.Instance?.HideAll();
//
//         List<SquadController> squads = GetCurrentSelectedSquads();
//
//         if (squads.Count > 0)
//         {
//             HandleSquadDragRightClick(squads);
//             return;
//         }
//
//         // For now, drag-right-click is squad-only.
//         // Villagers still use normal right-click movement.
//     }
//
//     void HandleSquadRightClick(List<SquadController> squads, RaycastHit hit)
//     {
//         bool hitEnemy = hit.collider.TryGetComponent(out EntityController hitEntity) &&
//                         IsEnemyToSquads(squads, hitEntity);
//
//         if (hitEnemy)
//         {
//             foreach (SquadController squad in squads)
//                 squad.OrderAttack(hitEntity);
//
//             return;
//         }
//
//         Vector3 groupFacing = ResolveFacingForSquads(squads, hit.point);
//
//         for (int i = 0; i < squads.Count; i++)
//         {
//             Vector3 offset = GetSquadMoveOffset(i, squads.Count, groupFacing);
//             squads[i].OrderMove(hit.point + offset);
//         }
//     }
//
//     void HandleSquadDragRightClick(List<SquadController> squads)
//     {
//         Ray startRay = mainCamera.ScreenPointToRay(rightClickStart);
//         Ray endRay = mainCamera.ScreenPointToRay(Input.mousePosition);
//
//         if (!Physics.Raycast(startRay, out RaycastHit startHit)) return;
//         if (!Physics.Raycast(endRay, out RaycastHit endHit)) return;
//
//         Vector3 destination = startHit.point;
//         Vector3 facing = Calc.DirectionFlat(startHit.point, endHit.point);
//         float width = Calc.RealDistance(startHit.point, endHit.point);
//
//         if (facing == Vector3.zero)
//             facing = ResolveFacingForSquads(squads, destination);
//
//         for (int i = 0; i < squads.Count; i++)
//         {
//             Vector3 offset = GetSquadMoveOffset(i, squads.Count, facing);
//             squads[i].OrderMove(destination + offset, facing, width);
//         }
//     }
//
//     void UpdateDragSquadPreview()
//     {
//         List<SquadController> squads = GetCurrentSelectedSquads();
//         if (squads.Count == 0) return;
//
//         Ray startRay = mainCamera.ScreenPointToRay(rightClickStart);
//         Ray endRay = mainCamera.ScreenPointToRay(Input.mousePosition);
//
//         if (!Physics.Raycast(startRay, out RaycastHit startHit)) return;
//         if (!Physics.Raycast(endRay, out RaycastHit endHit)) return;
//
//         Vector3 destination = startHit.point;
//         Vector3 facing = Calc.DirectionFlat(startHit.point, endHit.point);
//         float width = Calc.RealDistance(startHit.point, endHit.point);
//
//         if (facing == Vector3.zero)
//             facing = ResolveFacingForSquads(squads, destination);
//
//         List<Vector3> previewSlots = new List<Vector3>();
//
//         for (int i = 0; i < squads.Count; i++)
//         {
//             Vector3 offset = GetSquadMoveOffset(i, squads.Count, facing);
//             previewSlots.AddRange(
//                 squads[i].GetPreviewSlots(destination + offset, facing, width));
//         }
//
//         FormationVisualizer.Instance?.ShowSlots(previewSlots, persistent: true);
//     }
//
//     #endregion
//
//     #region Unit Right Click
//
//     void HandleUnitRightClick(List<UnitController> units, RaycastHit hit)
//     {
//         if (units.Count == 0) return;
//
//         bool hitEnemy = hit.collider.TryGetComponent(out EntityController hitEntity) &&
//                         hitEntity.Stats.IsEnemy(units[0].Stats);
//
//         bool hitNode = hit.collider.TryGetComponent(out ResourceNode node);
//
//         foreach (UnitController unit in units)
//         {
//             if (hitEnemy)
//             {
//                 unit.OrderAttack(hitEntity);
//             }
//             else if (hitNode &&
//                      unit.TryGetComponent(out VillagerController villager) &&
//                      unit.Stats.gatherAmount > 0)
//             {
//                 villager.OrderGather(node);
//             }
//             else
//             {
//                 unit.OrderMove(hit.point);
//             }
//         }
//     }
//
//     #endregion
//
//     #region Hotkeys
//
//     void HandleHotkeys()
//     {
//         switch (currentContext)
//         {
//             case CommandContext.Default:
//                 HandleDefaultHotkeys();
//                 break;
//
//             case CommandContext.EconomicUnitSelected:
//             case CommandContext.MilitarySquadSelected:
//                 HandleSelectedCommandHotkeys();
//                 break;
//
//             case CommandContext.BuildingSelected:
//                 HandleProductionBuildingHotkeys();
//                 break;
//         }
//     }
//
//     void HandleDefaultHotkeys()
//     {
//         if (Input.GetKeyDown(KeyCode.T))
//             BuildingPlacer.Instance.StartPlacing(townCenterData);
//         else if (Input.GetKeyDown(KeyCode.B))
//             BuildingPlacer.Instance.StartPlacing(barracksData);
//     }
//
//     void HandleSelectedCommandHotkeys()
//     {
//         HotkeySlot pressedSlot = GetPressedHotkeySlot();
//         if (pressedSlot == HotkeySlot.None) return;
//
//         List<SquadController> squads = GetCurrentSelectedSquads();
//
//         if (squads.Count > 0)
//         {
//             CommandData squadCommand = FindSquadCommandByHotkey(squads[0], pressedSlot);
//             if (squadCommand == null) return;
//
//             ExecuteSquadCommand(squadCommand.commandType);
//             return;
//         }
//
//         HandleUnitCommandHotkey(pressedSlot);
//     }
//
//     void HandleUnitCommandHotkey(HotkeySlot pressedSlot)
//     {
//         Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
//         Physics.Raycast(ray, out RaycastHit hit);
//
//         foreach (ISelectable selectable in currentSelected)
//         {
//             GameObject go = selectable.GetGameObject();
//
//             if (!go.TryGetComponent(out CommandController commandController)) continue;
//             if (!go.TryGetComponent(out UnitController unit)) continue;
//
//             CommandData command = unit.Stats.baseData.baseCommands.Find(c => c.hotkey == pressedSlot);
//             if (command == null) continue;
//
//             if (command.commandType == CommandType.Build)
//             {
//                 UIManager.Instance.ShowActionPanelBuildSubmenu(unit);
//                 return;
//             }
//
//             commandController.ExecuteHotkeyCommand(pressedSlot, hit);
//         }
//     }
//
//     void HandleProductionBuildingHotkeys()
//     {
//         BuildingController firstBuilding = null;
//         BuildingType type = BuildingType.None;
//
//         foreach (ISelectable selectable in currentSelected)
//         {
//             if (selectable is not BuildingController building)
//                 return;
//
//             if (type == BuildingType.None)
//             {
//                 type = building.Stats.baseData.buildingType;
//                 firstBuilding = building;
//             }
//             else if (building.Stats.baseData.buildingType != type)
//             {
//                 return;
//             }
//         }
//
//         if (firstBuilding == null) return;
//
//         foreach (ProductionOptionData option in firstBuilding.Stats.baseData.productionOptions)
//         {
//             if (option.hotkey == HotkeySlot.None) continue;
//             if (!slotToKey.ContainsKey(option.hotkey)) continue;
//             if (!Input.GetKeyDown(slotToKey[option.hotkey])) continue;
//
//             foreach (ISelectable selectable in currentSelected)
//             {
//                 if (selectable is BuildingController building)
//                     building.EnqueueProduction(option);
//             }
//
//             return;
//         }
//     }
//
//     void HandleBuildSubmenuHotkeys()
//     {
//         if (currentSelected.Count == 0) return;
//         if (currentSelected[0] is not UnitController unit) return;
//         if (unit.Stats.baseData.unitType != UnitType.Villager) return;
//
//         foreach (BuildOptionData option in unit.Stats.baseData.buildOptions)
//         {
//             if (option.hotkey == HotkeySlot.None) continue;
//             if (!slotToKey.ContainsKey(option.hotkey)) continue;
//             if (!Input.GetKeyDown(slotToKey[option.hotkey])) continue;
//
//             BuildingPlacer.Instance.StartPlacing(option);
//             return;
//         }
//     }
//
//     HotkeySlot GetPressedHotkeySlot()
//     {
//         foreach (KeyValuePair<HotkeySlot, KeyCode> kvp in slotToKey)
//         {
//             if (Input.GetKeyDown(kvp.Value))
//                 return kvp.Key;
//         }
//
//         return HotkeySlot.None;
//     }
//
//     #endregion
//
//     #region Squad Commands
//
//     // Keep this public name for ActionButtonUI compatibility.
//     // Internally this is now squad-first.
//     public void ExecuteGroupCommand(CommandType commandType)
//     {
//         ExecuteSquadCommand(commandType);
//     }
//
//     public void ExecuteSquadCommand(CommandType commandType)
//     {
//         List<SquadController> squads = GetCurrentSelectedSquads();
//         if (squads.Count == 0) return;
//
//         foreach (SquadController squad in squads)
//             ExecuteSquadCommandOnSquad(squad, commandType);
//     }
//
//     void ExecuteSquadCommandOnSquad(SquadController squad, CommandType commandType)
//     {
//         if (!squad) return;
//
//         switch (commandType)
//         {
//             case CommandType.Stop:
//                 squad.OrderStop();
//                 return;
//
//             case CommandType.Aggressive:
//                 squad.SetStance(CombatStance.Aggressive);
//                 return;
//
//             case CommandType.Defensive:
//                 squad.SetStance(CombatStance.Defensive);
//                 return;
//
//             case CommandType.StandGround:
//                 squad.SetStance(CombatStance.StandGround);
//                 return;
//
//             case CommandType.NoAttack:
//                 squad.SetStance(CombatStance.NoAttack);
//                 return;
//
//             case CommandType.FormationLine:
//                 squad.SetFormation(SquadFormation.Line);
//                 return;
//
//             case CommandType.FormationSpread:
//                 squad.SetFormation(SquadFormation.Spread);
//                 return;
//
//             case CommandType.FormationBox:
//                 squad.SetFormation(SquadFormation.Box);
//                 return;
//
//             case CommandType.FormationCircle:
//                 squad.SetFormation(SquadFormation.Circle);
//                 return;
//
//             case CommandType.FormationWedge:
//                 squad.SetFormation(SquadFormation.Wedge);
//                 return;
//
//             case CommandType.AttackMove:
//                 // TODO:
//                 // Later this should put squad in attack-move order mode.
//                 // For now, no-op.
//                 return;
//         }
//     }
//
//     CommandData FindSquadCommandByHotkey(SquadController squad, HotkeySlot slot)
//     {
//         if (squad == null) return null;
//
//         // TEMP:
//         // Until SquadController owns SquadCommandData directly, use a hardcoded mapping.
//         // Later: squad.GetAllCommands().
//         foreach (CommandData command in GetTemporarySquadCommands())
//         {
//             if (command.hotkey == slot)
//                 return command;
//         }
//
//         return null;
//     }
//
//     List<CommandData> GetTemporarySquadCommands()
//     {
//         // We are not creating ScriptableObjects here.
//         // This method is only for hotkey routing.
//         // UI button routing can still call ExecuteGroupCommand directly from CommandData.
//
//         return new List<CommandData>();
//     }
//
//     #endregion
//
//     #region Helpers
//
//     List<SquadController> GetCurrentSelectedSquads()
//     {
//         return currentSelected
//             .Select(s => s.GetGameObject().GetComponent<SquadController>())
//             .Where(s => s != null)
//             .ToList();
//     }
//
//     List<UnitController> GetCurrentSelectedUnits()
//     {
//         return currentSelected
//             .Select(s => s.GetGameObject().GetComponent<UnitController>())
//             .Where(u => u != null)
//             .ToList();
//     }
//
//     List<BuildingController> GetCurrentSelectedBuildings()
//     {
//         return currentSelected
//             .OfType<BuildingController>()
//             .ToList();
//     }
//
//     bool IsEnemyToSquads(List<SquadController> squads, EntityController target)
//     {
//         if (target == null || target.Stats == null) return false;
//
//         foreach (SquadController squad in squads)
//         {
//             if (squad == null || squad.Faction == null) continue;
//             if (target.Stats.faction == null) continue;
//
//             if (target.Stats.faction.teamId != squad.Faction.teamId)
//                 return true;
//         }
//
//         return false;
//     }
//
//     Vector3 ResolveFacingForSquads(List<SquadController> squads, Vector3 destination)
//     {
//         if (squads == null || squads.Count == 0)
//             return Vector3.forward;
//
//         Vector3 center = GetAverageSquadPosition(squads);
//         Vector3 facing = Calc.DirectionFlat(center, destination);
//
//         if (facing == Vector3.zero)
//             facing = squads[0].transform.forward;
//
//         facing.y = 0f;
//
//         return facing == Vector3.zero
//             ? Vector3.forward
//             : facing.normalized;
//     }
//
//     Vector3 GetAverageSquadPosition(List<SquadController> squads)
//     {
//         Vector3 avg = Vector3.zero;
//         int count = 0;
//
//         foreach (SquadController squad in squads)
//         {
//             if (squad == null) continue;
//             avg += squad.transform.position;
//             count++;
//         }
//
//         return count > 0 ? avg / count : Vector3.zero;
//     }
//
//     Vector3 GetSquadMoveOffset(int index, int count, Vector3 facing)
//     {
//         if (count <= 1)
//             return Vector3.zero;
//
//         facing.y = 0f;
//         if (facing == Vector3.zero)
//             facing = Vector3.forward;
//
//         facing.Normalize();
//
//         Vector3 right = Calc.Perpendicular(facing);
//
//         int rowSize = Mathf.CeilToInt(Mathf.Sqrt(count));
//         int row = index / rowSize;
//         int col = index % rowSize;
//
//         float x = (col - (rowSize - 1) * 0.5f) * multiSquadSpacing;
//         float z = -row * multiSquadSpacing;
//
//         return right * x + facing * z;
//     }
//
//     #endregion
// }








// using System.Collections.Generic;
// using System.Linq;
// using Unity.VisualScripting;
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
//
//     
//      private Dictionary<HotkeySlot, KeyCode> slotToKey = new Dictionary<HotkeySlot, KeyCode>()
//      {
//          { HotkeySlot.Q, KeyCode.Q }, { HotkeySlot.W, KeyCode.W }, { HotkeySlot.E, KeyCode.E },
//          { HotkeySlot.R, KeyCode.R }, { HotkeySlot.T, KeyCode.T }, { HotkeySlot.A, KeyCode.A },
//          { HotkeySlot.S, KeyCode.S }, { HotkeySlot.D, KeyCode.D }, { HotkeySlot.F, KeyCode.F },
//          { HotkeySlot.G, KeyCode.G }, { HotkeySlot.Z, KeyCode.Z }, { HotkeySlot.X, KeyCode.X },
//          { HotkeySlot.C, KeyCode.C }, { HotkeySlot.V, KeyCode.V }, { HotkeySlot.B, KeyCode.B },
//      };
//      private readonly KeyCode[] numberKeys = {
//          KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
//          KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
//          KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
//      }; // must be same length as SelectionManagers controlGroups[9]
//
//     // TODO: Replace with GameEvents.OnBuildSubmenuChanged(bool) when more systems need to react
//     private bool inBuildSubmenu = false;
//     public void SetBuildSubmenuActive(bool b) => inBuildSubmenu = b;
//
//     // Drag order
//     private Vector3 rightClickStart;
//     private bool isDragOrdering = false;
//     private float dragThreshold = 30f; // pixels
//     #endregion
//
//     
//     #region Unity Lifecycle
//     void Awake()
//     {
//         if (Instance != null && Instance != this) { Destroy(this.gameObject); return; }
//         Instance = this;
//     }
//     
//     
//     void Start()
//     {
//         mainCamera = Camera.main;
//         GameEvents.OnSelectionChanged += HandleSelectionChanged;
//         GameEvents.OnPlacementModeChanged += HandlePlacementModeChanged;
//     }
//
//     
//     void OnDestroy()
//     {
//         GameEvents.OnSelectionChanged -= HandleSelectionChanged;
//         GameEvents.OnPlacementModeChanged -= HandlePlacementModeChanged;
//     }
//
//     
//     void Update()
//     {
//         HandleEscape();
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
//     /// Centralized escape — cancels any active mode
//     void HandleEscape()
//     {
//         if (!Input.GetKeyDown(KeyCode.Escape)) return;
//         
//         if (isDragOrdering)
//         {
//             isDragOrdering = false;
//             FormationVisualizer.Instance.HideAll();
//         }
//     }
//     
//     
//     void HandleSelectionChanged()
//     {
//         currentSelected = SelectionManager.Instance.GetSelectedObjects();
//         UpdateContext();
//     }
//     
//     
//     void HandlePlacementModeChanged(bool isPlacing) => isPlacingBuilding = isPlacing;
//
//     
//     // void UpdateContext()
//     // {
//     //     if (currentSelected.Count == 0) { currentContext = CommandContext.Default; return; }
//     //     if (currentSelected[0] is BuildingController) { currentContext = CommandContext.BuildingSelected; return; }
//     //
//     //     if (currentSelected[0].GetGameObject().TryGetComponent(out UnitController unit))
//     //     {
//     //         currentContext = unit.Stats.entityTag == EntityTag.Villager
//     //             ? CommandContext.EconomicUnitSelected
//     //             : CommandContext.MilitaryUnitSelected;
//     //         return;
//     //     }
//     //
//     //     currentContext = CommandContext.Default;
//     // }
//     
//     // SESSION: Squad Control
//     void UpdateContext()
//     {
//         if (currentSelected.Count == 0)
//         {
//             currentContext = CommandContext.Default;
//             return;
//         }
//
//         if (currentSelected[0] is BuildingController)
//         {
//             currentContext = CommandContext.BuildingSelected;
//             return;
//         }
//
//         if (currentSelected[0] is SquadController)
//         {
//             currentContext = CommandContext.MilitaryUnitSelected;
//             return;
//         }
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
//     
//     #region Control Groups
//     void HandleControlGroups()
//     {
//         for (int i = 0; i < numberKeys.Length; i++)
//         {
//             if (!Input.GetKeyDown(numberKeys[i])) continue;
//
// #if UNITY_EDITOR
//             // SESSION: Squad Control
//             // if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
//                 // ControlGroupManager.Instance.AssignControlGroup(i);
// #else
//             if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
//                 ControlGroupManager.Instance.AssignControlGroup(i);
// #endif
//             // else
//                 // ControlGroupManager.Instance.SelectControlGroup(i);
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
//     #endregion
//
//     
//     // #region Formations
//     //
//     // List<Vector2> GetFormationOffsets(List<UnitController> military, int sharedGroup)
//     // {
//     //     UnitFormation formation = UnitFormation.Line;
//     //     float width = -1f;
//     //     List<Vector2> saved = null;
//     //
//     //     if (sharedGroup >= 0)
//     //     {
//     //         ControlGroup cg = ControlGroupManager.Instance.GetControlGroup(sharedGroup);
//     //         formation = cg.formation;
//     //         width = cg.formationWidth;
//     //         saved = cg.formationOffsets;
//     //
//     //         if (saved != null && saved.Count == military.Count)
//     //             return saved;
//     //     }
//     //
//     //     float defaultWidth = formation switch
//     //     {
//     //         UnitFormation.Line   => Mathf.Min(military.Count, 10) * FormationManager.Instance.DefaultSpacing,
//     //         UnitFormation.Spread => Mathf.Sqrt(military.Count) * FormationManager.Instance.DefaultSpacing * 1.5f,
//     //         UnitFormation.Box    => Mathf.Sqrt(military.Count) * FormationManager.Instance.DefaultSpacing,
//     //         UnitFormation.Circle => military.Count * FormationManager.Instance.DefaultSpacing / (2f * Mathf.PI),
//     //         UnitFormation.Wedge  => military.Count * FormationManager.Instance.DefaultSpacing,
//     //         _ => military.Count * FormationManager.Instance.DefaultSpacing
//     //     };
//     //
//     //     float finalWidth = width > 0 ? width : defaultWidth;
//     //     return FormationManager.Instance.CalculateOffsets(military.Count, finalWidth, formation);
//     // }
//     //
//     //
//     // void ExecuteFormationCommand(int sharedGroup, List<UnitController> military, UnitFormation formation)
//     // {
//     //     if (sharedGroup >= 0)
//     //     {
//     //         ControlGroupManager.Instance.SetFormation(sharedGroup, formation);
//     //         return;
//     //     }
//     //
//     //     FormationManager.Instance.ReformTemporaryWithFormation(military, formation);
//     // }
//     //
//     //
//     // void ExecuteFormationMove(
//     //     List<UnitController> military,
//     //     Vector3 destination,
//     //     Vector3 facing,
//     //     List<Vector2> offsets = null,
//     //     int sharedGroup = -2)
//     // {
//     //     if (military == null || military.Count == 0)
//     //         return;
//     //
//     //     if (sharedGroup == -2)
//     //         sharedGroup = GetSharedControlGroup(military);
//     //
//     //     if (sharedGroup >= 0)
//     //     {
//     //         ControlGroup cg = ControlGroupManager.Instance.GetControlGroup(sharedGroup);
//     //
//     //         List<MilitaryController> mil = military
//     //             .OfType<MilitaryController>()
//     //             .ToList();
//     //
//     //         if (offsets != null)
//     //         {
//     //             cg.formationOffsets = offsets;
//     //         }
//     //         else
//     //         {
//     //             FormationManager.Instance.RebuildGroupOffsets(cg, mil.Count);
//     //         }
//     //
//     //         bool isMajorFacingChange = FormationManager.Instance.ShouldReassignUnitsForFacingChange(
//     //             cg,
//     //             facing);
//     //
//     //         if (isMajorFacingChange)
//     //         {
//     //             // Big turn / flip:
//     //             // Snap facing first so reassignment and anchor orientation agree.
//     //             if (cg.anchor != null)
//     //             {
//     //                 cg.anchor.facing = facing;
//     //                 cg.anchor.targetFacing = facing;
//     //             }
//     //
//     //             FormationManager.Instance.ReassignUnitsToOffsetsForFacing(
//     //                 cg,
//     //                 mil,
//     //                 facing);
//     //         }
//     //
//     //         List<Vector3> worldPositions = FormationManager.Instance.ConvertOffsetsToWorldPositions(
//     //             cg.formationOffsets,
//     //             destination,
//     //             facing);
//     //
//     //         bool canUseAnchorMove =
//     //             cg.formationMode &&
//     //             FormationManager.Instance.CanAnchorReachDestination(cg, destination);
//     //
//     //         // Formation Mode Move
//     //         if (canUseAnchorMove)
//     //         {
//     //             FormationManager.Instance.MoveFormationAnchorTo(
//     //                 cg,
//     //                 mil,
//     //                 destination,
//     //                 facing);
//     //
//     //             FormationVisualizer.Instance.ShowSlots(worldPositions);
//     //         }
//     //         // Direct Move Order
//     //         else
//     //         {
//     //             foreach (MilitaryController mc in mil)
//     //             {
//     //                 if (mc.offsetIndex < 0 || mc.offsetIndex >= worldPositions.Count)
//     //                     continue;
//     //
//     //                 mc.OrderMove(worldPositions[mc.offsetIndex]);
//     //             }
//     //         }
//     //
//     //         FormationVisualizer.Instance.ShowSlots(worldPositions);
//     //         return;
//     //     }
//     //
//     //     // No control group: temporary direct formation move.
//     //     if (offsets == null)
//     //         offsets = GetFormationOffsets(military, -1);
//     //
//     //     List<Vector3> tempPositions = FormationManager.Instance.ConvertOffsetsToWorldPositions(
//     //         offsets,
//     //         destination,
//     //         facing);
//     //
//     //     var slots = FormationManager.Instance.AssignUnitsToNearestSlots(
//     //         military,
//     //         tempPositions);
//     //
//     //     foreach (var slot in slots)
//     //         slot.Key.OrderMove(slot.Value);
//     //
//     //     FormationVisualizer.Instance.ShowSlots(new List<Vector3>(slots.Values));
//     // }
//     //
//     // #endregion
//
//     
//     #region Right Click
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
//                 Calc.OutOfRange(rightClickStart, Input.mousePosition, dragThreshold))
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
//     
//     // SESSION: Squad Control
//     void HandleNormalRightClick()
//     {
//         Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
//         if (!Physics.Raycast(ray, out RaycastHit hit)) return;
//
//         List<SquadController> squads = GetCurrentSelectedSquads();
//         List<UnitController> units = GetCurrentSelectedUnits();
//
//         // Buildings selected → set rally point
//         bool anyBuilding = false;
//         foreach (ISelectable s in currentSelected)
//         {
//             if (s.GetGameObject().TryGetComponent(out BuildingController b))
//             {
//                 b.SetRallyPoint(hit.point);
//                 anyBuilding = true;
//             }
//         }
//
//         if (anyBuilding)
//             return;
//
//         // Squad selected → squad-level commands
//         if (squads.Count > 0)
//         {
//             HandleSquadRightClick(squads, hit);
//             return;
//         }
//
//         // Old individual unit behavior, mainly villagers for now
//         if (units.Count > 0)
//         {
//             HandleUnitRightClick(units, hit);
//             return;
//         }
//     }
//     
//     void HandleDragRightClick()
//     {
//         FormationVisualizer.Instance.HideAll();
//         
//         List<UnitController> units = GetCurrentSelectedUnits();
//         List<UnitController> military = units.FindAll(u => u is MilitaryController);
//         
//         if (military.Count == 0)
//             return;
//         
//         Ray startRay = mainCamera.ScreenPointToRay(rightClickStart);
//         Ray endRay = mainCamera.ScreenPointToRay(Input.mousePosition);
//         
//         if (!Physics.Raycast(startRay, out RaycastHit startHit))
//             return;
//         
//         if (!Physics.Raycast(endRay, out RaycastHit endHit))
//             return;
//         
//         // Vector3 destination = startHit.point;
//         // float width = Calc.RealDistance(startHit.point, endHit.point);
//         //
//         // // Vector3 facingDirection = Calc.DirectionFlat(startHit.point, endHit.point);
//         // // if (facingDirection == Vector3.zero)
//         // //     facingDirection = military[0].transform.forward;
//         //
//         // Vector3 facing = FindFacingDir(military, destination, Calc.DirectionFlat(startHit.point, endHit.point));
//         //
//         // int sharedGroup = GetSharedControlGroup(military);
//         //
//         // // Determine formation offsets
//         // List<Vector2> offsets;
//         //
//         // if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
//         // {
//         //     UnitFormation formation = sharedGroup >= 0
//         //         ? ControlGroupManager.Instance.GetControlGroup(sharedGroup).formation
//         //         : UnitFormation.Line;
//         //
//         //     offsets = FormationManager.Instance.CalculateOffsets(
//         //         military.Count,
//         //         width,
//         //         formation);
//         //
//         //     // Save custom width/offsets to control group
//         //     if (sharedGroup >= 0)
//         //     {
//         //         ControlGroup cg = ControlGroupManager.Instance.GetControlGroup(sharedGroup);
//         //
//         //         cg.formationOffsets = offsets;
//         //         cg.formationWidth = width;
//         //     }
//         // }
//         // else
//         // {
//         //     offsets = GetFormationOffsets(military, sharedGroup);
//         // }
//         //
//         // List<Vector3> worldPositions =
//         //     FormationManager.Instance.OffsetsToWorldPositions(
//         //         offsets,
//         //         destination,
//         //         facing);
//         //
//         // // Villagers move directly
//         // // foreach (UnitController villager in units.FindAll(u => u is VillagerController))
//         // // {
//         // //     Vector3 offset = new Vector3(
//         // //         Random.Range(-2f, 2f),
//         // //         0f,
//         // //         Random.Range(-2f, 2f));
//         // //
//         // //     villager.OrderMove(destination + offset);
//         // // }
//         // MoveVillagersDirect(GetVillagerUnits(units), destination, true);
//         //
//         // // Control group logic
//         // if (sharedGroup >= 0)
//         // {
//         //     ControlGroup cg = ControlGroupManager.Instance.GetControlGroup(sharedGroup);
//         //
//         //     float speed = military.Min(u => u.Stats.moveSpeed);
//         //
//         //     if (cg.anchor == null)
//         //     {
//         //         cg.anchor = new FormationAnchor(
//         //             GetAveragePosition(military),
//         //             facing,
//         //             speed);
//         //     }
//         //
//         //     cg.anchor.speed = speed;
//         //
//         //     if (cg.formationMode)
//         //     {
//         //         // Virtual center anchor:
//         //         // Since formation offsets are now centered, the anchor starts from
//         //         // the current average position of the group.
//         //         Vector3 currentCenter = GetAveragePosition(military);
//         //
//         //         cg.anchor.position = currentCenter;
//         //         cg.anchor.facing = facing;
//         //         cg.anchor.destination = currentCenter;
//         //         cg.anchor.isMoving = false;
//         //
//         //         cg.anchor.MoveTo(destination, facing);
//         //     }
//         //     else
//         //     {
//         //         // Formation mode OFF:
//         //         // Use formation slots as direct unit move destinations.
//         //         var slots = FormationManager.Instance.AssignNearestSlots(
//         //             military,
//         //             worldPositions);
//         //
//         //         foreach (var slot in slots)
//         //             slot.Key.OrderMove(slot.Value);
//         //     }
//         //
//         //     FormationVisualizer.Instance.ShowSlots(worldPositions);
//         //     return;
//         // }
//         //
//         // // Temporary formation, no control group
//         // {
//         //     var slots = FormationManager.Instance.AssignNearestSlots(
//         //         military,
//         //         worldPositions);
//         //
//         //     foreach (var slot in slots)
//         //         slot.Key.OrderMove(slot.Value);
//         //
//         //     FormationVisualizer.Instance.ShowSlots(
//         //         new List<Vector3>(slots.Values));
//         // }
//         
//         Vector3 destination = startHit.point;
//         Vector3 rawFacing = Calc.DirectionFlat(startHit.point, endHit.point);
//         Vector3 facing = FindFacingDir(military, destination, rawFacing);
//         float width = Calc.RealDistance(startHit.point, endHit.point);
//
//         int sharedGroup = GetSharedControlGroup(military);
//
//         List<Vector2> offsets;
//
//         // if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
//         // {
//             // SESSION: Squad Control
//             // UnitFormation formation = sharedGroup >= 0
//             //     ? ControlGroupManager.Instance.GetControlGroup(sharedGroup).formation
//             //     : UnitFormation.Line;
//             //
//             // offsets = FormationManager.Instance.CalculateOffsets(
//             //     military.Count,
//             //     width,
//             //     formation);
//             //
//             // if (sharedGroup >= 0)
//             // {
//             //     ControlGroup cg = ControlGroupManager.Instance.GetControlGroup(sharedGroup);
//             //     cg.formationOffsets = offsets;
//             //     cg.formationWidth = width;
//             // }
//         // }
//         // else
//         // {
//         //     offsets = GetFormationOffsets(military, sharedGroup);
//         // }
//
//         MoveVillagersDirect(GetVillagerUnitsFrom(units), destination, scatter: true);
//
//         // SESSION: Squad Control
//         // ExecuteFormationMove(
//         //     military,
//         //     destination,
//         //     facing,
//         //     offsets,
//         //     sharedGroup);
//     }
//     
//
//     void UpdateDragFormationPreview()
//     {
//         List<UnitController> units = GetCurrentSelectedUnits();
//         List<UnitController> military = units.FindAll(u => u is MilitaryController);
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
//         // if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
//         // {
//         //     // SESSION: Squad Control
//         //     // UnitFormation formation = sharedGroup >= 0
//         //     //     ? ControlGroupManager.Instance.GetControlGroup(sharedGroup).formation
//         //     //     : UnitFormation.Line;
//         //     // offsets = FormationManager.Instance.CalculateOffsets(military.Count, width, formation);
//         // }
//         // else
//         //     offsets = GetFormationOffsets(military, sharedGroup);
//
//         // SESSION: Squad Control
//         // List<Vector3> worldPositions = FormationManager.Instance.ConvertOffsetsToWorldPositions(
//         //     offsets, destination, facingDirection);
//         //
//         // FormationVisualizer.Instance.ShowSlots(worldPositions, persistent: true);
//     }
//     
//     #endregion
//
//     
//     #region Group Move
//     
//     void MoveVillagersDirect(List<UnitController> villagers, Vector3 destination, bool scatter = false)
//     {
//         foreach (UnitController villager in villagers)
//         {
//             Vector3 offset = scatter
//                 ? new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f))
//                 : Vector3.zero;
//
//             villager.OrderMove(destination + offset);
//         }
//     }
//     #endregion
//
//     
//     #region Hotkeys
//     
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
//     
//     void HandleDefaultHotkeys()
//     {
//         if (Input.GetKeyDown(KeyCode.T))
//             BuildingPlacer.Instance.StartPlacing(townCenterData);
//         else if (Input.GetKeyDown(KeyCode.B))
//             BuildingPlacer.Instance.StartPlacing(barracksData);
//     }
//
//     
//     void HandleUnitHotkeys()
//     {
//         Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
//         Physics.Raycast(ray, out RaycastHit hit);
//
//         HotkeySlot pressedSlot = HotkeySlot.None;
//         foreach (KeyValuePair<HotkeySlot, KeyCode> kvp in slotToKey)
//             if (Input.GetKeyDown(kvp.Value)) { pressedSlot = kvp.Key; break; }
//     
//         if (pressedSlot == HotkeySlot.None) return;
//         
//         Debug.Log(pressedSlot);
//
//         foreach (ISelectable s in currentSelected)
//         {
//             if (!s.GetGameObject().TryGetComponent(out CommandController cc)) continue;
//             if (!s.GetGameObject().TryGetComponent(out UnitController unit)) continue;
//
//             CommandData matchingCmd = unit.Stats.baseData.baseCommands.Find(c => c.hotkey == pressedSlot);
//             if (matchingCmd == null) continue;
//
//             // Build command — open submenu
//             if (matchingCmd.commandType == CommandType.Build)
//             {
//                 UIManager.Instance.ShowActionPanelBuildSubmenu(unit);
//                 return;
//             }
//
//             // Group scope — execute once then return
//             if (matchingCmd.commandScope == CommandScope.Squad) // Temporary?
//             {
//                 ExecuteGroupCommand(matchingCmd.commandType);
//                 return;
//             }
//
//             // Per unit scope — execute on all selected units
//             cc.ExecuteHotkeyCommand(pressedSlot, hit);
//         }
//     }
//     
//
//     void HandleProductionBuildingHotkeys()
//     {
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
//     
//     void HandleBuildSubmenuHotkeys()
//     {
//         if (currentSelected.Count == 0) return;
//         if (currentSelected[0] is not UnitController unit) return;
//         if (unit.Stats.baseData.unitType != UnitType.Villager) return;
//
//         foreach (BuildOptionData option in unit.Stats.baseData.buildOptions
//             .Where(option => Input.GetKeyDown(slotToKey[option.hotkey])))
//         {
//             BuildingPlacer.Instance.StartPlacing(option);
//             return;
//         }
//     }
//     #endregion
//
//     
//     #region Group Commands
//     /// Executes a group-scoped command — acts on control group, not individual units
//     // public void ExecuteGroupCommand(CommandType cmd)
//     // {
//     //     // Get shared group from current military selection
//     //     List<UnitController> military = GetUnitsFromSelection()
//     //         .FindAll(u => u is not VillagerController);
//     //     int sharedGroup = GetSharedControlGroup(military);
//     //
//     //     switch (cmd)
//     //     {
//     //         case CommandType.ToggleFormationMode:
//     //             if (sharedGroup >= 0)
//     //                 ControlGroupManager.Instance.ToggleFormationMode(sharedGroup);
//     //             break;
//     //
//     //         case CommandType.FormationLine:
//     //             if (sharedGroup >= 0) ControlGroupManager.Instance.SetFormation(sharedGroup, UnitFormation.Line);
//     //             else FormationManager.Instance.ReformTemporaryWithFormation(military, UnitFormation.Line);
//     //             break;
//     //         case CommandType.FormationSpread:
//     //             if (sharedGroup >= 0) ControlGroupManager.Instance.SetFormation(sharedGroup, UnitFormation.Spread);
//     //             else FormationManager.Instance.ReformTemporaryWithFormation(military, UnitFormation.Spread);
//     //             break;
//     //         case CommandType.FormationBox:
//     //             if (sharedGroup >= 0) ControlGroupManager.Instance.SetFormation(sharedGroup, UnitFormation.Box);
//     //             else FormationManager.Instance.ReformTemporaryWithFormation(military, UnitFormation.Box);
//     //             break;
//     //         case CommandType.FormationCircle:
//     //             if (sharedGroup >= 0) ControlGroupManager.Instance.SetFormation(sharedGroup, UnitFormation.Circle);
//     //             else FormationManager.Instance.ReformTemporaryWithFormation(military, UnitFormation.Circle);
//     //             break;
//     //         case CommandType.FormationWedge:
//     //             if (sharedGroup >= 0) ControlGroupManager.Instance.SetFormation(sharedGroup, UnitFormation.Wedge);
//     //             else FormationManager.Instance.ReformTemporaryWithFormation(military, UnitFormation.Wedge);
//     //             break;
//     //     }
//     // }
//     
//     public void ExecuteGroupCommand(CommandType cmd)
//     {
//         List<UnitController> military = GetCurrentSelectedUnits()
//             .FindAll(u => u is not VillagerController);
//
//         int sharedGroup = GetSharedControlGroup(military);
//
//         switch (cmd)
//         {
//             // SESSION: Squad Control 
//             // case CommandType.ToggleFormationMode:
//             //     if (sharedGroup >= 0)
//             //         ControlGroupManager.Instance.ToggleFormationMode(sharedGroup);
//             //     return;
//
//             // case CommandType.FormationLine:
//             //     ExecuteFormationCommand(sharedGroup, military, UnitFormation.Line);
//             //     return;
//             //
//             // case CommandType.FormationSpread:
//             //     ExecuteFormationCommand(sharedGroup, military, UnitFormation.Spread);
//             //     return;
//             //
//             // case CommandType.FormationBox:
//             //     ExecuteFormationCommand(sharedGroup, military, UnitFormation.Box);
//             //     return;
//             //
//             // case CommandType.FormationCircle:
//             //     ExecuteFormationCommand(sharedGroup, military, UnitFormation.Circle);
//             //     return;
//             //
//             // case CommandType.FormationWedge:
//             //     ExecuteFormationCommand(sharedGroup, military, UnitFormation.Wedge);
//             //     return;
//         }
//     }
//     #endregion
//
//     
//     #region Helpers
//     
//     Vector3 FindFacingDir(List<UnitController> military, Vector3 destination, Vector3 providedFacing = default)
//     {
//         Vector3 facing = providedFacing != default
//             ? providedFacing
//             : Calc.DirectionFlat(GetAveragePosition(military), destination);
//
//         if (facing == Vector3.zero && military.Count > 0)
//             facing = military[0].transform.forward;
//
//         facing.y = 0f;
//         return facing.normalized;
//     }
//
//     
//     Vector3 GetAveragePosition(List<UnitController> units)
//     {
//         Vector3 avg = Vector3.zero;
//         foreach (UnitController u in units) avg += u.transform.position;
//         return avg / units.Count;
//     }
//
//     
//     List<UnitController> GetCurrentSelectedUnits()
//     {
//         return currentSelected
//             .Select(s => s.GetGameObject().GetComponent<UnitController>())
//             .Where(u => u != null)
//             .ToList();
//     }
//
//     
//     List<UnitController> GetMilitaryUnitsFrom(List<UnitController> units)
//     {
//         return units
//             .Where(u => u is MilitaryController)
//             .ToList();
//     }
//
//     
//     List<UnitController> GetVillagerUnitsFrom(List<UnitController> units)
//     {
//         return units
//             .Where(u => u is VillagerController)
//             .ToList();
//     }
//
//
//     #region Squad Helpers
//
//     void HandleSquadRightClick(List<SquadController> squads, RaycastHit hit)
//     {
//         bool hitEnemy = hit.collider.TryGetComponent(out EntityController hitEntity) &&
//                         IsEnemyToSquads(squads, hitEntity);
//
//         if (hitEnemy)
//         {
//             foreach (SquadController squad in squads)
//                 squad.OrderAttack(hitEntity);
//
//             return;
//         }
//
//         // Ground move
//         for (int i = 0; i < squads.Count; i++)
//         {
//             Vector3 offset = GetSquadMoveOffset(i, squads.Count);
//             squads[i].OrderMove(hit.point + offset);
//         }
//     }
//
//     
//     void HandleUnitRightClick(List<UnitController> units, RaycastHit hit)
//     {
//         bool hitEnemy = hit.collider.TryGetComponent(out EntityController hitEntity) &&
//                         hitEntity.Stats.IsEnemy(units[0].Stats);
//
//         bool hitNode = hit.collider.TryGetComponent(out ResourceNode node);
//
//         foreach (UnitController unit in units)
//         {
//             if (hitEnemy)
//             {
//                 unit.OrderAttack(hitEntity);
//             }
//             else if (hitNode && unit.Stats.gatherAmount > 0 &&
//                      unit.TryGetComponent(out VillagerController vil))
//             {
//                 vil.OrderGather(node);
//             }
//             else
//             {
//                 unit.OrderMove(hit.point);
//             }
//         }
//     }
//
//     
//     bool IsEnemyToSquads(List<SquadController> squads, EntityController target)
//     {
//         if (target == null || target.Stats == null) return false;
//
//         foreach (SquadController squad in squads)
//         {
//             if (squad == null || squad.Faction == null) continue;
//
//             if (target.Stats.faction != null &&
//                 target.Stats.faction.teamId != squad.Faction.teamId)
//                 return true;
//         }
//
//         return false;
//     }
//
//     
//     Vector3 GetSquadMoveOffset(int index, int count)
//     {
//         if (count <= 1)
//             return Vector3.zero;
//
//         int rowSize = Mathf.CeilToInt(Mathf.Sqrt(count));
//         int row = index / rowSize;
//         int col = index % rowSize;
//
//         float spacing = 5f;
//
//         float x = (col - (rowSize - 1) / 2f) * spacing;
//         float z = -row * spacing;
//
//         return new Vector3(x, 0f, z);
//     }
//
//     
//     List<SquadController> GetCurrentSelectedSquads()
//     {
//         return currentSelected
//             .Select(s => s.GetGameObject().GetComponent<SquadController>())
//             .Where(s => s != null)
//             .ToList();
//     }
//
//     #endregion
//     #endregion
//     
//     
//     
//     
// }