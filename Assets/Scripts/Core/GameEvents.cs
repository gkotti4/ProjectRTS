using System;
using UnityEngine;

public static class GameEvents
{
    // Selection
    public static event Action OnSelectionChanged;
    
    public static event Action<BuildingController> OnBuildingSelected;
    public static event Action<UnitController> OnUnitSelected;
    public static event Action OnDeselected;

    // Building Placer
    public static event Action<bool> OnPlacementModeChanged;
    
    // Production
    public static event Action<BuildingController> OnProductionQueueChanged;

    // Combat / Death
    public static event Action<GameObject> OnEntityDied;

    // Game State
    public static event Action OnPlayerWin;
    public static event Action OnPlayerLose;
    
    // Resources and Population (FACTION OWNED)
    public static event Action<FactionInstance> OnResourcesChanged;
    public static event Action<FactionInstance> OnPopulationChanged;
    

    // Firing methods — keeps invoke calls clean
    public static void SelectionChanged() => OnSelectionChanged?.Invoke();
    public static void BuildingSelected(BuildingController b) => OnBuildingSelected?.Invoke(b);
    public static void UnitSelected(UnitController u) => OnUnitSelected?.Invoke(u);
    public static void Deselected() => OnDeselected?.Invoke();
    public static void PlacementModeChanged(bool b) => OnPlacementModeChanged?.Invoke(b);
    public static void ProductionQueueChanged(BuildingController b) => OnProductionQueueChanged?.Invoke(b);
    public static void EntityDied(GameObject go) => OnEntityDied?.Invoke(go);
    public static void PlayerWin() => OnPlayerWin?.Invoke();
    public static void PlayerLose() => OnPlayerLose?.Invoke();
    
    public static void ResourcesChanged(FactionInstance fi) => OnResourcesChanged?.Invoke(fi);
    public static void PopulationChanged(FactionInstance fi) => OnPopulationChanged?.Invoke(fi);
}
