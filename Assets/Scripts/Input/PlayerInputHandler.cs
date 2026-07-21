using System.Collections.Generic;
using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    public static PlayerInputHandler Instance { get; private set; }

    [Header("Default Build Hotkeys")]
    [SerializeField] private BuildOptionData townCenterData;
    [SerializeField] private BuildOptionData barracksData;

    [Header("Input")]
    [SerializeField] private float dragThreshold = 30f;

    [Header("Squad Multi-Move")]
    [Tooltip("Empty lateral gap kept between the calculated formation boxes of neighboring squads.")]
    [Min(0f)]
    [SerializeField] private float multiSquadSpacing = 6f;

    private Camera mainCamera;

    private List<ISelectable> currentSelected = new List<ISelectable>();

    private Vector3 rightClickStart;
    private bool isDragOrdering = false;
    private bool isPlacingBuilding = false;

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

        if (isPlacingBuilding) // Build Submenu??
            return;

        HandleHotkeys();
        HandleRightClick();
        
        HandleDefaultHotkeys();
    }

    void HandleSelectionChanged()
    {
        currentSelected = SelectionManager.Instance.GetSelectedObjects();
    }

    void HandlePlacementModeChanged(bool isPlacing)
    {
        isPlacingBuilding = isPlacing;
    }

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

    void HandleControlGroupHotkeys()
    {
        if (ControlGroupManager.Instance == null)
            return;

        for (int i = 0; i < numberKeys.Length; i++)
        {
            if (!Input.GetKeyDown(numberKeys[i]))
                continue;

            bool assign =
                Input.GetKey(KeyCode.LeftControl) ||
                Input.GetKey(KeyCode.RightControl);

            if (assign)
                ControlGroupManager.Instance.AssignControlGroup(i);
            else
                ControlGroupManager.Instance.SelectControlGroup(i);

            return;
        }
    }

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
                UpdateSquadDragPreview();
        }

        if (Input.GetMouseButtonUp(1))
        {
            if (isDragOrdering)
                HandleSquadDragRightClick();
            else
                HandleNormalRightClick();

            isDragOrdering = false;
        }
    }

    void HandleNormalRightClick()
    {
        if (mainCamera == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        if (TryGetSelectedKind(out SelectableKind kind))
        {
            switch (kind)
            {
                case SelectableKind.Squad:
                    HandleSquadRightClick(GetSelectedSquads(), hit);
                    return;

                case SelectableKind.Worker:
                    HandleWorkerRightClick(GetSelectedWorkers(), hit);
                    return;

                case SelectableKind.Building:
                    HandleBuildingRightClick(GetSelectedBuildings(), hit);
                    return;
            }
        }
    }

    void HandleSquadRightClick(List<SquadController> squads, RaycastHit hit)
    {
        if (squads.Count == 0)
            return;

        SquadController enemySquad = ResolveEnemySquadFromHit(squads[0], hit);

        if (enemySquad != null)
        {
            foreach (SquadController squad in squads)
                squad.OrderAttack(enemySquad);

            return;
        }

        MoveSquadsToPoint(squads, hit.point);
    }

    void HandleWorkerRightClick(List<WorkerController> workers, RaycastHit hit)
    {
        foreach (WorkerController worker in workers)
            worker.OrderMove(hit.point);
    }

    void HandleBuildingRightClick(List<BuildingController> buildings, RaycastHit hit)
    {
        foreach (BuildingController building in buildings)
            building.SetRallyPoint(hit.point);
    }

    void HandleSquadDragRightClick()
    {
        FormationVisualizer.Instance?.HideAll();

        List<SquadController> squads = GetSelectedSquads();

        if (squads.Count == 0)
            return;

        Ray startRay = mainCamera.ScreenPointToRay(rightClickStart);
        Ray endRay = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(startRay, out RaycastHit startHit))
            return;

        if (!Physics.Raycast(endRay, out RaycastHit endHit))
            return;

        Vector3 destination = startHit.point;
        Vector3 facing = Calc.DirectionFlat(startHit.point, endHit.point);

        float width = -1f;

        if (Input.GetKey(KeyCode.LeftAlt))
            width = Calc.RealDistance(startHit.point, endHit.point);

        if (facing == Vector3.zero)
            facing = ResolveFacingForSquads(squads, destination);

        List<SquadController> orderedSquads =
            GetSquadsOrderedForGroupMove(squads, facing);

        List<Vector3> squadOffsets = GetSquadMoveOffsets(
            orderedSquads,
            facing,
            width);

        for (int i = 0; i < orderedSquads.Count; i++)
        {
            orderedSquads[i].OrderMove(
                destination + squadOffsets[i],
                facing,
                width);
        }
    }

    void UpdateSquadDragPreview()
    {
        List<SquadController> squads = GetSelectedSquads();

        if (squads.Count == 0)
            return;

        Ray startRay = mainCamera.ScreenPointToRay(rightClickStart);
        Ray endRay = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(startRay, out RaycastHit startHit))
            return;

        if (!Physics.Raycast(endRay, out RaycastHit endHit))
            return;

        Vector3 destination = startHit.point;
        Vector3 facing = Calc.DirectionFlat(startHit.point, endHit.point);

        float width = -1f;

        if (Input.GetKey(KeyCode.LeftAlt))
            width = Calc.RealDistance(startHit.point, endHit.point);

        if (facing == Vector3.zero)
            facing = ResolveFacingForSquads(squads, destination);

        List<SquadController> orderedSquads =
            GetSquadsOrderedForGroupMove(squads, facing);

        List<Vector3> previewSlots = new List<Vector3>();
        List<Vector3> squadOffsets = GetSquadMoveOffsets(
            orderedSquads,
            facing,
            width);

        for (int i = 0; i < orderedSquads.Count; i++)
        {
            previewSlots.AddRange(
                orderedSquads[i].GetPreviewSlots(
                    destination + squadOffsets[i],
                    facing,
                    width));
        }

        FormationVisualizer.Instance?.ShowSlots(previewSlots, facing, true);
    }

    void MoveSquadsToPoint(List<SquadController> squads, Vector3 point)
    {
        Vector3 groupFacing = ResolveFacingForSquads(squads, point);

        List<SquadController> orderedSquads =
            GetSquadsOrderedForGroupMove(squads, groupFacing);

        List<Vector3> squadOffsets = GetSquadMoveOffsets(
            orderedSquads,
            groupFacing);

        for (int i = 0; i < orderedSquads.Count; i++)
        {
            orderedSquads[i].OrderMove(
                point + squadOffsets[i],
                groupFacing);
        }
    }

    SquadController ResolveEnemySquadFromHit(SquadController source, RaycastHit hit)
    {
        if (source == null || hit.collider == null)
            return null;

        SelectionTarget proxy = hit.collider.GetComponentInParent<SelectionTarget>();

        if (proxy != null &&
            proxy.TryGetTarget(out ISelectable selectable) &&
            selectable is SquadController squad &&
            IsEnemy(source, squad))
        {
            return squad;
        }

        SquadController directSquad = hit.collider.GetComponentInParent<SquadController>();

        if (directSquad != null && IsEnemy(source, directSquad))
            return directSquad;

        return null;
    }

    bool IsEnemy(SquadController source, SquadController target)
    {
        if (source == null || target == null)
            return false;

        if (source.Faction == null || target.Faction == null)
            return false;

        return source.Faction.teamId != target.Faction.teamId;
    }

    void HandleHotkeys()
    {
        // Build Submenu??
        if (currentSelected == null || currentSelected.Count == 0)
        {
            HandleDefaultHotkeys();
            return;
        }

        if (!TryGetSelectedKind(out SelectableKind kind))
            return;

        switch (kind)
        {
            case SelectableKind.Squad:
                HandleSelectedCommandHotkeys();
                return;

            case SelectableKind.Worker:
                HandleSelectedCommandHotkeys();
                return;

            case SelectableKind.Building:
                HandleSelectedBuildingHotkeys();
                return;
        }
    }
    
    void HandleSelectedCommandHotkeys()
    {
        HotkeySlot pressedSlot = GetPressedHotkeySlot();

        if (pressedSlot == HotkeySlot.None)
            return;

        CommandData command = GetSelectedCommandForHotkey(pressedSlot);

        if (command != null)
            ExecuteSelectedCommand(command.commandType);
    }

    void HandleSelectedBuildingHotkeys()
    {
        HotkeySlot pressedSlot = GetPressedHotkeySlot();

        if (pressedSlot == HotkeySlot.None)
            return;

        // Production hotkeys can come back here later.
        // For now, buildings mostly use ActionButtonUI clicks.
    }

    void HandleDefaultHotkeys()
    {
        if (currentSelected.Count > 0)
            return;

        if (Input.GetKeyDown(KeyCode.T) && townCenterData != null)
            BuildingPlacer.Instance.StartPlacing(townCenterData);

        if (Input.GetKeyDown(KeyCode.B) && barracksData != null)
            BuildingPlacer.Instance.StartPlacing(barracksData);
    }

    public void ExecuteSelectedCommand(CommandType commandType)
    {
        if (!TryGetSelectedKind(out SelectableKind kind))
            return;

        switch (kind)
        {
            case SelectableKind.Squad:
                ExecuteSquadCommand(commandType);
                return;

            case SelectableKind.Worker:
                ExecuteWorkerCommand(commandType);
                return;

            case SelectableKind.Building:
                ExecuteBuildingCommand(commandType);
                return;
        }
    }

    void ExecuteSquadCommand(CommandType commandType)
    {
        foreach (SquadController squad in GetSelectedSquads())
        {
            switch (commandType)
            {
                case CommandType.Stop:
                    squad.OrderStop();
                    break;

                case CommandType.EngageStance:
                    squad.SetStance(SquadStance.Engage);
                    break;

                case CommandType.HoldStance:
                    squad.SetStance(SquadStance.Hold);
                    break;

                case CommandType.FormationLine:
                    squad.SetFormation(SquadFormation.Line);
                    break;

                case CommandType.FormationSpread:
                    squad.SetFormation(SquadFormation.Spread);
                    break;

                case CommandType.FormationBox:
                    squad.SetFormation(SquadFormation.Box);
                    break;

                case CommandType.FormationCircle:
                    squad.SetFormation(SquadFormation.Circle);
                    break;

                case CommandType.FormationWedge:
                    squad.SetFormation(SquadFormation.Wedge);
                    break;
            }
        }
    }

    void ExecuteWorkerCommand(CommandType commandType)
    {
        if (commandType == CommandType.Build)
        {
            WorkerController worker = GetFirstSelectedWorker();

            if (worker != null)
                UIManager.Instance.ShowActionPanelBuildSubmenu(worker);

            return;
        }

        if (commandType == CommandType.Stop)
        {
            foreach (WorkerController worker in GetSelectedWorkers())
                worker.OrderStop();
        }
    }

    void ExecuteBuildingCommand(CommandType commandType)
    {
        // Production is handled by ActionButtonUI.
    }

    CommandData GetSelectedCommandForHotkey(HotkeySlot slot)
    {
        if (currentSelected.Count == 0)
            return null;

        if (currentSelected[0] is not ICommandable commandable)
            return null;

        foreach (CommandData command in commandable.GetCommands())
        {
            if (command == null)
                continue;

            if (command.hotkey == slot)
                return command;
        }

        return null;
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

    bool TryGetSelectedKind(out SelectableKind kind)
    {
        kind = SelectableKind.None;

        if (currentSelected == null || currentSelected.Count == 0)
            return false;

        if (currentSelected[0] == null)
            return false;

        kind = currentSelected[0].SelectionKind;
        return kind != SelectableKind.None;
    }

    List<SquadController> GetSelectedSquads()
    {
        List<SquadController> result = new List<SquadController>();

        foreach (ISelectable selectable in currentSelected)
        {
            if (selectable is SquadController squad)
                result.Add(squad);
        }

        return result;
    }

    List<WorkerController> GetSelectedWorkers()
    {
        List<WorkerController> result = new List<WorkerController>();

        foreach (ISelectable selectable in currentSelected)
        {
            if (selectable is WorkerController worker)
                result.Add(worker);
        }

        return result;
    }

    WorkerController GetFirstSelectedWorker()
    {
        foreach (ISelectable selectable in currentSelected)
        {
            if (selectable is WorkerController worker)
                return worker;
        }

        return null;
    }

    List<BuildingController> GetSelectedBuildings()
    {
        List<BuildingController> result = new List<BuildingController>();

        foreach (ISelectable selectable in currentSelected)
        {
            if (selectable is BuildingController building)
                result.Add(building);
        }

        return result;
    }

    Vector3 ResolveFacingForSquads(List<SquadController> squads, Vector3 destination)
    {
        if (squads == null || squads.Count == 0)
            return Vector3.forward;

        Vector3 center = Vector3.zero;

        foreach (SquadController squad in squads)
            center += squad.transform.position;

        center /= squads.Count;

        Vector3 dir = destination - center;
        dir.y = 0f;

        if (dir == Vector3.zero)
            return squads[0].transform.forward;

        return dir.normalized;
    }

    /// Returns a copy of the selected squads ordered from left to right relative
    /// to the final group-facing direction. This preserves the squads' current
    /// lateral battlefield order when assigning final group-move zones, reducing
    /// unnecessary squad crossover.
    List<SquadController> GetSquadsOrderedForGroupMove(
        List<SquadController> squads,
        Vector3 facing)
    {
        List<SquadController> orderedSquads = squads != null
            ? new List<SquadController>(squads)
            : new List<SquadController>();

        if (orderedSquads.Count <= 1)
            return orderedSquads;

        facing.y = 0f;

        if (facing == Vector3.zero)
            facing = Vector3.forward;

        facing.Normalize();

        Vector3 right = Calc.Perpendicular(facing);
        Vector3 groupCenter = Vector3.zero;
        int validSquadCount = 0;

        foreach (SquadController squad in orderedSquads)
        {
            if (squad == null)
                continue;

            groupCenter += squad.transform.position;
            validSquadCount++;
        }

        if (validSquadCount > 0)
            groupCenter /= validSquadCount;

        orderedSquads.Sort((leftSquad, rightSquad) =>
        {
            if (leftSquad == rightSquad)
                return 0;

            if (leftSquad == null)
                return 1;

            if (rightSquad == null)
                return -1;

            Vector3 leftOffset =
                leftSquad.transform.position - groupCenter;

            Vector3 rightOffset =
                rightSquad.transform.position - groupCenter;

            float leftLateralPosition = Vector3.Dot(leftOffset, right);
            float rightLateralPosition = Vector3.Dot(rightOffset, right);

            int lateralComparison =
                leftLateralPosition.CompareTo(rightLateralPosition);

            if (lateralComparison != 0)
                return lateralComparison;

            float leftForwardPosition = Vector3.Dot(leftOffset, facing);
            float rightForwardPosition = Vector3.Dot(rightOffset, facing);

            int forwardComparison =
                leftForwardPosition.CompareTo(rightForwardPosition);

            if (forwardComparison != 0)
                return forwardComparison;

            return leftSquad.GetInstanceID().CompareTo(
                rightSquad.GetInstanceID());
        });

        return orderedSquads;
    }

    List<Vector3> GetSquadMoveOffsets(
        List<SquadController> squads,
        Vector3 facing,
        float requestedFormationWidth = -1f)
    {
        List<Vector3> offsets = new List<Vector3>();

        if (squads == null || squads.Count == 0)
            return offsets;

        facing.y = 0f;

        if (facing == Vector3.zero)
            facing = Vector3.forward;

        facing.Normalize();

        Vector3 right = Calc.Perpendicular(facing);
        List<FormationBounds> formationBounds =
            new List<FormationBounds>(squads.Count);

        float totalGroupWidth = 0f;

        for (int i = 0; i < squads.Count; i++)
        {
            FormationBounds bounds = squads[i] != null
                ? squads[i].Formation.GetFormationBounds(requestedFormationWidth)
                : FormationBounds.Empty;

            formationBounds.Add(bounds);
            totalGroupWidth += bounds.width;
        }

        totalGroupWidth +=
            Mathf.Max(0, squads.Count - 1) *
            Mathf.Max(0f, multiSquadSpacing);

        float layoutCursor = -totalGroupWidth * 0.5f;

        for (int i = 0; i < squads.Count; i++)
        {
            FormationBounds bounds = formationBounds[i];
            float squadCenter = layoutCursor + bounds.HalfWidth;

            offsets.Add(right * squadCenter);

            layoutCursor +=
                bounds.width +
                Mathf.Max(0f, multiSquadSpacing);
        }

        return offsets;
    }

}

