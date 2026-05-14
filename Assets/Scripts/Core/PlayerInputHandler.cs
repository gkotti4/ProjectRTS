using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles player input commands and routes them to the appropriate game systems.
/// Owns intentional player actions (building placement, hotkeys, game commands).
/// Does not own state - only reads input and calls other systems.
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private EntityData townCenterData;
    [SerializeField] private EntityData barracksData;

    private CommandContext currentContext = CommandContext.Default;
    private bool isPlacingBuilding = false;
    private List<ISelectable> currentSelections = new List<ISelectable>();

    private Dictionary<HotkeySlot, KeyCode> slotToKey = new Dictionary<HotkeySlot, KeyCode>()
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

    void Start()
    {
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
        if (isPlacingBuilding)
        {
            HandleBuildPlacementHotkeys();
            return;
        }
        HandleHotkeys();
        HandleRightClick();
    }

    // Updates current selection and context when selection changes
    void HandleSelectionChanged()
    {
        currentSelections = SelectionManager.Instance.GetSelectedObjects();
        UpdateContext();
    }

    // Determines command context from current selection
    void UpdateContext()
    {
        if (currentSelections.Count == 0)
        {
            currentContext = CommandContext.Default;
            return;
        }

        if (currentSelections[0] is BuildingController)
        {
            currentContext = CommandContext.BuildingSelected;
            return;
        }

        if (currentSelections[0].GetGameObject().TryGetComponent(out UnitController unit))
        {
            currentContext = unit.Stats.entityTag == EntityTag.Villager
                ? CommandContext.EconomicUnitSelected
                : CommandContext.MilitaryUnitSelected;
            return;
        }

        currentContext = CommandContext.Default;
    }

    // Routes right click through CommandController on selected units
    void HandleRightClick()
    {
        if (!Input.GetMouseButtonDown(1)) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        List<ISelectable> selected = SelectionManager.Instance.GetSelectedObjects();

        // Group move — multiple units selected
        if (selected.Count > 1)
        {
            List<UnitController> units = new List<UnitController>();
            foreach (ISelectable s in selected)
                if (s.GetGameObject().TryGetComponent(out UnitController u))
                    units.Add(u);
            HandleGroupMove(units, hit.point);
            return;
        }

        // Single unit — route through CommandController
        foreach (ISelectable s in selected)
            if (s.GetGameObject().TryGetComponent(out CommandController commandController))
                commandController.ExecuteContextCommand(hit);
    }

    // Sends units to formation positions centered around destination
    void HandleGroupMove(List<UnitController> units, Vector3 destination)
    {
        int columns = Mathf.CeilToInt(Mathf.Sqrt(units.Count));
        float spacing = 2f;

        for (int i = 0; i < units.Count; i++)
        {
            int row = i / columns;
            int col = i % columns;

            float offsetX = (col - (columns - 1) / 2f) * spacing;
            float offsetZ = -row * spacing;

            Vector3 unitDestination = destination + new Vector3(offsetX, 0f, offsetZ);
            units[i].MoveTo(unitDestination);
        }
    }

    // Routes hotkeys based on current context
    void HandleHotkeys()
    {
        switch (currentContext)
        {
            case CommandContext.Default:
                HandleDefaultHotkeys();
                break;
            case CommandContext.EconomicUnitSelected:
                HandleUnitHotkeys();
                break;
            case CommandContext.MilitaryUnitSelected:
                HandleUnitHotkeys();
                break;
            case CommandContext.BuildingSelected:
                HandleBuildingHotkeys();
                break;
        }
    }

    // Default hotkeys — building placement when nothing selected
    void HandleDefaultHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.T))
            BuildingPlacer.Instance.StartPlacing(townCenterData);
        else if (Input.GetKeyDown(KeyCode.B))
            BuildingPlacer.Instance.StartPlacing(barracksData);
    }

    // Routes unit hotkeys through CommandController
    void HandleUnitHotkeys()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Physics.Raycast(ray, out RaycastHit hit);

        foreach (ISelectable s in currentSelections)
        {
            if (!s.GetGameObject().TryGetComponent(out CommandController commandController)) continue;

            foreach (KeyValuePair<HotkeySlot, KeyCode> kvp in slotToKey)
            {
                if (Input.GetKeyDown(kvp.Value))
                {
                    commandController.ExecuteHotkeyCommand(kvp.Key, hit);
                    return;
                }
            }
        }
    }

    // Routes building hotkeys to production queue
    void HandleBuildingHotkeys()
    {
        if (currentSelections.Count != 1) return;
        if (currentSelections[0] is not BuildingController building) return;

        foreach (ProductionOptionData option in building.Stats.baseData.productionOptions)
        {
            if (Input.GetKeyDown(slotToKey[option.hotkeySlot]))
            {
                building.EnqueueProduction(option);
                return;
            }
        }
    }

    // Handles villager build hotkeys while in placement mode
    void HandleBuildPlacementHotkeys()
    {
        if (currentSelections.Count != 1) return;
        if (currentSelections[0] is not UnitController unit) return;
        if (unit.Stats.entityTag != EntityTag.Villager) return;

        foreach (BuildingOptionData option in unit.Stats.baseData.buildOptions) // New
        {
            if (Input.GetKeyDown(slotToKey[option.hotkeySlot]))
            {
                BuildingPlacer.Instance.StartPlacing(option.buildingData);
                return;
            }
        }
    }

    // Handles placement mode change
    void HandlePlacementModeChanged(bool isPlacing)
    {
        isPlacingBuilding = isPlacing;
    }
}