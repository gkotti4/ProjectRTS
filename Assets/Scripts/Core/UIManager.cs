// SESSION: Squad Control Refactor

using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    #region Fields

    [Header("Cursor")]
    [SerializeField] private Texture2D defaultCursor;

    [Header("Panels")]
    [SerializeField] private ActionPanelUI actionPanelUI;
    [SerializeField] private QueuePanelUI queuePanelUI;

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
        SetCursor(defaultCursor);

        GameEvents.OnSelectionChanged += HandleSelectionChanged;
        GameEvents.OnPlacementModeChanged += HandlePlacementModeChanged;
        GameEvents.OnProductionQueueChanged += HandleProductionQueueChanged;
    }

    void OnDestroy()
    {
        GameEvents.OnSelectionChanged -= HandleSelectionChanged;
        GameEvents.OnPlacementModeChanged -= HandlePlacementModeChanged;
        GameEvents.OnProductionQueueChanged -= HandleProductionQueueChanged;
    }

    #endregion

    #region Cursor

    public void SetCursor(Texture2D cursor, Vector2 hotspot = default)
    {
        Cursor.SetCursor(cursor, hotspot, CursorMode.Auto);
    }

    public void SetDefaultCursor()
    {
        SetCursor(defaultCursor);
    }

    void HandlePlacementModeChanged(bool isPlacing)
    {
        Cursor.visible = !isPlacing;

        if (!isPlacing)
        {
            SetDefaultCursor();
            ExitBuildSubmenu();
        }
    }

    #endregion

    #region Selection Routing

    void HandleSelectionChanged()
    {
        List<ISelectable> selected = SelectionManager.Instance.GetSelectedObjects();

        if (selected.Count == 0)
        {
            HideAllPanels();
            return;
        }

        if (TryShowSingleSelectionPanel(selected))
            return;

        if (TryShowMultiSelectionPanel(selected))
            return;

        HideAllPanels();
    }

    bool TryShowSingleSelectionPanel(List<ISelectable> selected)
    {
        if (selected.Count != 1)
            return false;

        ISelectable selectable = selected[0];

        if (selectable is BuildingController building)
        {
            ShowSingleBuildingPanel(building);
            return true;
        }

        if (selectable is SquadController squad)
        {
            ShowSquadPanel(squad);
            return true;
        }

        if (selectable.GetGameObject().TryGetComponent(out VillagerController villager))
        {
            // ShowUnitPanel DEPRECIATED
            ShowVillagerPanel(villager);
            return true;
        }

        // REFACTOR: Only Squad (military) and Villager selection (not using general Unit anymore)
        // if (selectable.GetGameObject().TryGetComponent(out UnitController unit))
        // {
        //     ShowUnitPanel(unit);
        //     return true;
        // }

        return false;
    }

    bool TryShowMultiSelectionPanel(List<ISelectable> selected)
    {
        if (AllSameBuildingType(selected, out BuildingController firstBuilding))
        {
            ShowMultiBuildingPanel(firstBuilding);
            return true;
        }

        if (AllSquadType(selected, out SquadController firstSquad))
        {
            ShowSquadPanel(firstSquad);
            return true;
        }

        // if (AllSameUnitType(selected, out UnitController firstUnit)) // DEPRECIATED
        // {
        //     ShowUnitPanel(firstUnit);
        //     return true;
        // }

        if (AllSameVillagerType(selected, out VillagerController firstVillager))
        {
            ShowVillagerPanel(firstVillager);
            return true;
        }

        return false;
    }

    void ShowSingleBuildingPanel(BuildingController building)
    {
        HideAllPanels();

        actionPanelUI.ShowProductionPanel(building);
        queuePanelUI.ShowPanel(building);
    }

    void ShowMultiBuildingPanel(BuildingController templateBuilding)
    {
        HideAllPanels();

        actionPanelUI.ShowProductionPanel(templateBuilding);

        // Queue panel is single-building only for now.
        queuePanelUI.HidePanel();
    }

    // void ShowUnitPanel(UnitController unit) // DEPRECIATED: Replacing with Villager specific panel (Squad panel (military) and Villager panel, rather than Unit)
    // {
    //     HideAllPanels();
    //
    //     actionPanelUI.ShowUnitPanel(unit);
    //     queuePanelUI.HidePanel();
    // }

    void ShowVillagerPanel(VillagerController villager)
    {
        HideAllPanels();
        
        actionPanelUI.ShowVillagerPanel(villager);
        queuePanelUI.HidePanel(); 
    }

    void ShowSquadPanel(SquadController squad)
    {
        HideAllPanels();

        actionPanelUI.ShowSquadPanel(squad);
        queuePanelUI.HidePanel();
    }

    #endregion

    #region Selection Helpers

    bool AllSameBuildingType(List<ISelectable> selected, out BuildingController first)
    {
        first = null;
        BuildingType type = BuildingType.None;

        foreach (ISelectable selectable in selected)
        {
            if (selectable is not BuildingController building)
                return false;

            if (type == BuildingType.None)
            {
                type = building.Stats.baseDetails.buildingType;
                first = building;
                continue;
            }

            if (building.Stats.baseDetails.buildingType != type)
                return false;
        }

        return first != null;
    }

    // bool AllSameUnitType(List<ISelectable> selected, out UnitController first) // DEPRECIATED: Moving to Villager specific
    // {
    //     first = null;
    //     UnitType type = UnitType.None;
    //
    //     foreach (ISelectable selectable in selected)
    //     {
    //         if (selectable == null || selectable.GetGameObject() == null)
    //             return false;
    //
    //         // Do not treat squads as units.
    //         if (selectable is SquadController)
    //             return false;
    //
    //         if (!selectable.GetGameObject().TryGetComponent(out UnitController unit))
    //             return false;
    //
    //         if (type == UnitType.None)
    //         {
    //             type = unit.Stats.baseData.unitType;
    //             first = unit;
    //             continue;
    //         }
    //
    //         if (unit.Stats.baseData.unitType != type)
    //             return false;
    //     }
    //
    //     return first != null;
    // }
    
    bool AllSameVillagerType(List<ISelectable> selected, out VillagerController first)
    {
        first = null;
        UnitType type = UnitType.None;

        foreach (ISelectable selectable in selected)
        {
            if (selectable == null || selectable.GetGameObject() == null)
                return false;

            if (!selectable.GetGameObject().TryGetComponent(out VillagerController villager))
                return false;

            if (type == UnitType.None)
            {
                type = villager.Stats.baseDetails.unitType;
                first = villager;
                continue;
            }

            if (villager.Stats.baseDetails.unitType != type)
                return false;
        }

        return first != null;
    }

    bool AllSquadType(List<ISelectable> selected, out SquadController first)
    {
        first = null;

        foreach (ISelectable selectable in selected)
        {
            if (selectable is not SquadController squad)
                return false;

            first ??= squad;
        }

        return first != null;
    }

    #endregion

    #region Production Queue

    void HandleProductionQueueChanged(BuildingController building)
    {
        List<ISelectable> selected = SelectionManager.Instance.GetSelectedObjects();

        if (selected.Count != 1)
            return;

        if (selected[0] is not BuildingController selectedBuilding)
            return;

        if (selectedBuilding != building)
            return;

        queuePanelUI.Refresh();
    }

    #endregion

    #region Panels

    void HideAllPanels()
    {
        actionPanelUI.HidePanel();
        queuePanelUI.HidePanel();
    }

    #endregion

    #region Build Submenu

    public void ShowActionPanelBuildSubmenu(VillagerController villager)
    {
        actionPanelUI.ShowBuildPanel(villager);
        PlayerInputHandler.Instance.SetBuildSubmenuActive(true);
    }

    public void ExitBuildSubmenu()
    {
        actionPanelUI.ExitBuildPanel();
        PlayerInputHandler.Instance.SetBuildSubmenuActive(false);
    }

    #endregion
}

