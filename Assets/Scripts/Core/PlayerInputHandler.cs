using System.Collections.Generic;
using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    public static PlayerInputHandler Instance { get; private set; }
    
    [SerializeField] private BuildOptionData townCenterData;
    [SerializeField] private BuildOptionData barracksData;

    private CommandContext currentContext = CommandContext.Default;
    private bool isPlacingBuilding = false;
    private List<ISelectable> currentSelections = new List<ISelectable>();

    private Dictionary<HotkeySlot, KeyCode> slotToKey = new Dictionary<HotkeySlot, KeyCode>()
    {
        { HotkeySlot.Q, KeyCode.Q }, { HotkeySlot.W, KeyCode.W }, { HotkeySlot.E, KeyCode.E },
        { HotkeySlot.R, KeyCode.R }, { HotkeySlot.T, KeyCode.T }, { HotkeySlot.A, KeyCode.A },
        { HotkeySlot.S, KeyCode.S }, { HotkeySlot.D, KeyCode.D }, { HotkeySlot.F, KeyCode.F },
        { HotkeySlot.G, KeyCode.G }, { HotkeySlot.Z, KeyCode.Z }, { HotkeySlot.X, KeyCode.X },
        { HotkeySlot.C, KeyCode.C }, { HotkeySlot.V, KeyCode.V }, { HotkeySlot.B, KeyCode.B },
    };

    
    // TODO: Replace with GameEvents.OnBuildSubmenuChanged(bool) when more systems need to react
    private bool inBuildSubmenu = false;
    public void SetBuildSubmenuActive(bool b) => inBuildSubmenu = b;
    

    private Camera mainCamera;


    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
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
        if (inBuildSubmenu || isPlacingBuilding)
        {
            HandleBuildSubmenuHotkeys();
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
    
    void HandlePlacementModeChanged(bool isPlacing) => isPlacingBuilding = isPlacing;

    // Determines command context from current selection
    void UpdateContext()
    {
        if (currentSelections.Count == 0) { currentContext = CommandContext.Default; return; }

        if (currentSelections[0] is BuildingController) { currentContext = CommandContext.BuildingSelected; return; }

        if (currentSelections[0].GetGameObject().TryGetComponent(out UnitController unit))
        {
            currentContext = unit.Stats.entityTag == EntityTag.Villager
                ? CommandContext.EconomicUnitSelected
                : CommandContext.MilitaryUnitSelected;
            return;
        }

        currentContext = CommandContext.Default;
    }

    // Handles right click — resolves hit context and issues orders directly to units
    void HandleRightClick()
    {
        if (!Input.GetMouseButtonDown(1)) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        List<UnitController> units = new List<UnitController>();
        foreach (ISelectable s in currentSelections)
            if (s.GetGameObject().TryGetComponent(out UnitController u))
                units.Add(u);

        if (units.Count == 0) return;

        // Resolve what was hit once — shared across all units
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

        // Issue orders to each unit
        foreach (UnitController unit in units)
        {
            if (hitEnemy)
                unit.OrderAttack(hitEntity);
            else if (hitNode && unit.Stats.gatherAmount > 0 && unit.TryGetComponent(out VillagerController vil)) // Update: refactor to include new VilController
                vil.OrderGather(node);
            else
                unit.OrderMove(hit.point);
        }
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
            units[i].OrderMove(destination + new Vector3(offsetX, 0f, offsetZ));
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
            case CommandContext.MilitaryUnitSelected:
                HandleUnitHotkeys();
                break;
            case CommandContext.BuildingSelected:
                HandleProductionBuildingHotkeys();
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

    // Routes unit hotkeys through CommandController — explicit commands only
    void HandleUnitHotkeys()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Physics.Raycast(ray, out RaycastHit hit);

        foreach (ISelectable s in currentSelections)
        {
            if (!s.GetGameObject().TryGetComponent(out CommandController cc)) continue;
            if (!s.GetGameObject().TryGetComponent(out UnitController unit)) continue;

            foreach (KeyValuePair<HotkeySlot, KeyCode> kvp in slotToKey)
            {
                if (!Input.GetKeyDown(kvp.Value)) continue;

                // Build command - Villager Only
                CommandData matchingCmd = unit.Stats.baseData.baseCommands.Find(c => c.hotkey == kvp.Key);
                if (matchingCmd != null && matchingCmd.commandType == CommandType.Build)
                {
                    UIManager.Instance.ShowActionPanelBuildSubmenu(unit);
                    return;
                }

                // Execute Command (all command types besides right-click/build commands)
                cc.ExecuteHotkeyCommand(kvp.Key, hit);
                return;
            }
        }
    }

    // Routes building hotkeys to production queue
    void HandleProductionBuildingHotkeys()
    {
        if (currentSelections.Count != 1) return;
        if (currentSelections[0] is not BuildingController building) return;

        foreach (ProductionOptionData option in building.Stats.baseData.productionOptions)
        {
            if (option.hotkey == HotkeySlot.None) continue;
            if (Input.GetKeyDown(slotToKey[option.hotkey]))
            {
                building.EnqueueProduction(option);
                return;
            }
        }
    }
    
    
    void HandleBuildSubmenuHotkeys() // Villager Only
    {
        // Handles villager build hotkeys while in placement mode
        if (currentSelections.Count == 0) return; // Could be != 1
        if (currentSelections[0] is not UnitController unit) return;
        if (unit.Stats.baseData.unitType != UnitType.Villager) return;

        foreach (BuildOptionData option in unit.Stats.baseData.buildOptions)
        {
            if (Input.GetKeyDown(slotToKey[option.hotkey]))
            {
                BuildingPlacer.Instance.StartPlacing(option);
                return;
            }
        }
    }

}