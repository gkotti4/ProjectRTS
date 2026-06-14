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

        // List<UnitController> units = GetCurrentSelectedUnits();
        List<VillagerController> villagers = GetCurrentSelectedVillagers();
        if (villagers.Count > 0)
            HandleVillagerRightClick(villagers, hit);
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

    void HandleVillagerRightClick(List<VillagerController> villagers, RaycastHit hit)
    {
        if (villagers.Count == 0) return;

        EntityController hitEntity = hit.collider.GetComponentInParent<EntityController>();

        bool hitEnemy =
            hitEntity &&
            hitEntity.Stats &&
            hitEntity.Stats.IsEnemy(villagers[0].Stats);

        ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();

        foreach (VillagerController villager in villagers)
        {
            if (hitEnemy)
            {
                villager.OrderAttack(hitEntity);
            }
            else if (node && villager.Stats.gatherAmount > 0)
            {
                villager.OrderGather(node);
            }
            else
            {
                villager.OrderMove(hit.point);
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
                HandleSelectedVillagerHotkeys();
                //HandleSelectedUnitHotkeys();
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

    // void HandleSelectedUnitHotkeys() // DEPRECIATED
    // {
    //     HotkeySlot pressedSlot = GetPressedHotkeySlot();
    //     if (pressedSlot == HotkeySlot.None) return;
    //
    //     Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
    //     Physics.Raycast(ray, out RaycastHit hit);
    //
    //     foreach (ISelectable selectable in currentSelected)
    //     {
    //         GameObject go = selectable.GetGameObject();
    //
    //         if (!go.TryGetComponent(out CommandController commandController)) continue;
    //         if (!go.TryGetComponent(out VillagerController villager)) continue;
    //
    //         CommandData command = villager.Stats.baseData.baseCommands.Find(c => c.hotkey == pressedSlot);
    //         if (!command) continue;
    //
    //         if (command.commandType == CommandType.Build)
    //         {
    //             UIManager.Instance.ShowActionPanelBuildSubmenu(villager);
    //             return;
    //         }
    //
    //         commandController.ExecuteHotkeyCommand(pressedSlot, hit);
    //     }
    // }
    
    void HandleSelectedVillagerHotkeys()
    {
        HotkeySlot pressedSlot = GetPressedHotkeySlot();
        if (pressedSlot == HotkeySlot.None) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Physics.Raycast(ray, out RaycastHit hit);

        foreach (ISelectable selectable in currentSelected)
        {
            GameObject go = selectable.GetGameObject();

            if (!go.TryGetComponent(out CommandController commandController)) continue;
            if (!go.TryGetComponent(out VillagerController villager)) continue;

            CommandData command = villager.Stats.baseDetails.baseCommands.Find(c => c.hotkey == pressedSlot);
            if (command == null) continue;

            if (command.commandType == CommandType.Build)
            {
                UIManager.Instance.ShowActionPanelBuildSubmenu(villager);
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
                type = building.Stats.baseDetails.buildingType;
                firstBuilding = building;
            }
            else if (building.Stats.baseDetails.buildingType != type)
            {
                return;
            }
        }

        if (firstBuilding == null) return;

        foreach (ProductionOptionData option in firstBuilding.Stats.baseDetails.productionOptions)
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

        if (currentSelected[0].GetGameObject() == null) return;

        if (!currentSelected[0].GetGameObject().TryGetComponent(out VillagerController villager))
            return;

        foreach (BuildOptionData option in villager.Stats.baseDetails.buildOptions)
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
                squad.SetStance(SquadStance.Aggressive);
                return;

            case CommandType.Defensive:
                squad.SetStance(SquadStance.Defensive);
                return;

            case CommandType.StandGround:
                squad.SetStance(SquadStance.StandGround);
                return;

            case CommandType.NoAttack:
                squad.SetStance(SquadStance.NoAttack);
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

    List<UnitController> GetCurrentSelectedUnits() // DEPRECIATED kinda
    {
        return currentSelected
            .Select(s => s.GetGameObject().GetComponent<UnitController>())
            .Where(u => u != null)
            .ToList();
    }
    
    List<VillagerController> GetCurrentSelectedVillagers()
    {
        return currentSelected
            .Select(s => s.GetGameObject().GetComponent<VillagerController>())
            .Where(v => v != null)
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

