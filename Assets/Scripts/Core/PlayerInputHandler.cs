using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles player input commands and routes them to the appropriate game systems.
/// Owns intentional player actions (building placement, hotkeys, game commands).
/// Does not own state - only reads input and calls other systems - unlike Managers.
/// </summary>

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private EntityData townCenterData;
    [SerializeField] private EntityData barracksData;

    private CommandContext currentContext = CommandContext.Default;
    private bool isPlacingBuilding = false;
    private List<ISelectable> currentSelections = new List<ISelectable>();

    private Dictionary<HotkeySlot, KeyCode> slotToKey = new Dictionary<HotkeySlot, KeyCode>() // quick access bar - left panel
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
    
    void Awake()
    {
        
    }
    void Start()
    {
        //SelectionManager.Instance.OnSelectionChanged += HandleSelectionChanged;
        GameEvents.OnSelectionChanged += HandleSelectionChanged;
        //BuildingPlacer.Instance.OnPlacingModeChanged += (isPlacing) => isPlacingBuilding = isPlacing;
        GameEvents.OnPlacementModeChanged += (isPlacing) => isPlacingBuilding = isPlacing;
        GameEvents.OnPlacementModeChanged += HandlePlacementModeChanged;

    }

    void OnDestroy()
    {
        //SelectionManager.Instance.OnSelectionChanged -= HandleSelectionChanged;
        GameEvents.OnSelectionChanged -= HandleSelectionChanged;
        GameEvents.OnPlacementModeChanged -= HandlePlacementModeChanged;
    }

    void Update()
    {
        if (isPlacingBuilding)
        {
            HandleEconomicUnitHotkeys(); // double check - reasoning: since vils start buildings
            return;
        }
        HandleHotkeys();
        HandleRightClick();
    }

    void HandleSelectionChanged()
    {
        currentSelections = SelectionManager.Instance.GetSelectedObjects();
        UpdateContext();
    }
    
    void UpdateContext()
    {
        // Default
        if (currentSelections.Count == 0)
        {
            currentContext = CommandContext.Default;
            return;
        }
        
        // Building Context
        if (currentSelections[0] is BuildingController)
        {
            currentContext = CommandContext.BuildingSelected;
            return;
        }

        // Unit Context
        if (currentSelections[0].GetGameObject().TryGetComponent(out UnitController unit))
        {
            // Economic (Villager currently) | Military (Infantry, Range, Cav, Siege later)
            currentContext = unit is VillagerController
                ? CommandContext.EconomicUnitSelected
                : CommandContext.MilitaryUnitSelected;
            return;
        }

        currentContext = CommandContext.Default;
    }

    void HandleRightClick() // CHECK
    {
        if (!Input.GetMouseButtonDown(1)) return;
        
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;
        
        List<ISelectable> selected = SelectionManager.Instance.GetSelectedObjects();

        if (selected.Count > 1) // Multiple units selected
        {
            List<UnitController> units = new List<UnitController>();
            foreach (ISelectable s in selected)
                if (s.GetGameObject().TryGetComponent(out UnitController unit))
                    units.Add(unit);

            HandleGroupMove(units, hit.point);
            return;
        }
        
        // Single unit or building
        foreach (ISelectable s in selected)
            if(s.GetGameObject().TryGetComponent(out UnitController unit))
                unit.MoveToTarget(hit);

    }

    private void HandleGroupMove(List<UnitController> units, Vector3 destination) // CHECK DRUKN
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

    void HandleHotkeys()
    {
        switch (currentContext)
        {            
            case CommandContext.Default:
                HandleDefaultHotkeys();
                break;
            case CommandContext.EconomicUnitSelected:
                HandleEconomicUnitHotkeys();
                break;
            case CommandContext.MilitaryUnitSelected:
                break;
            case CommandContext.BuildingSelected:
                HandleBuildingHotkeys();
                break;
        }
    }


    void HandleDefaultHotkeys()
    {
        if (currentSelections.Count == 1) return; // check
        
        if (Input.GetKeyDown(KeyCode.T))
        {
            BuildingPlacer.Instance.StartPlacing(townCenterData);
        }
        else if (Input.GetKeyDown(KeyCode.B))
        {
            BuildingPlacer.Instance.StartPlacing(barracksData);
        }
    }
    
    void HandleEconomicUnitHotkeys()
    {
        if (currentSelections.Count != 1) return;
        if (currentSelections[0] is not UnitController unit) return;
        
        foreach (ProductionOptionData option in unit.Stats.baseData.productionOptions)
        {
            // if (Input.GetKeyDown(slotToKey[option.hotkeySlot]))
            // {
            //     //unit.EnqueueProduction(option);
            //     return;
            // }
        }
    }

    
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


    void HandlePlacementModeChanged(bool isPlacing)
    {
        isPlacingBuilding = isPlacing;
    }
}
