using System;
using UnityEngine;

public static class GameEvents
{
    // Selection
    public static event Action<BuildingController> OnBuildingSelected;
    public static event Action<UnitController> OnUnitSelected;
    public static event Action OnDeselected;

    // Production
    public static event Action<BuildingController> OnProductionQueueChanged;

    // Combat / Death
    public static event Action<GameObject> OnUnitDied;
    public static event Action<GameObject> OnBuildingDied;

    // Game State
    public static event Action OnPlayerWin;
    public static event Action OnPlayerLose;

    // Firing methods — keeps invoke calls clean
    public static void BuildingSelected(BuildingController b) => OnBuildingSelected?.Invoke(b);
    public static void UnitSelected(UnitController u) => OnUnitSelected?.Invoke(u);
    public static void Deselected() => OnDeselected?.Invoke();
    public static void ProductionQueueChanged(BuildingController b) => OnProductionQueueChanged?.Invoke(b);
    public static void UnitDied(GameObject go) => OnUnitDied?.Invoke(go);
    public static void BuildingDied(GameObject go) => OnBuildingDied?.Invoke(go);
    public static void PlayerWin() => OnPlayerWin?.Invoke();
    public static void PlayerLose() => OnPlayerLose?.Invoke();
}
