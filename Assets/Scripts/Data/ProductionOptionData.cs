using UnityEngine;

[CreateAssetMenu(
    fileName = "ProductionOptionData_",
    menuName = "Scriptable Objects/Production/ProductionOption")]
public class ProductionOptionData : ScriptableObject
{
    [Header("Basic")]
    public string productionName = "Production";
    public Sprite icon;
    public float productionTime = 1f;
    public ResourceCost cost = new ResourceCost();

    public ProductionType productionType;

    [Header("Produced Object")]
    public SquadData squadData;
    public WorkerData workerData;
    public UpgradeData upgradeData;

    [Header("Hotkey")]
    public HotkeySlot hotkey;

    public bool producesSquad => productionType == ProductionType.Squad;

    public SquadController squadPrefab =>
        squadData != null ? squadData.squadPrefab : null;

    public int squadMemberCount =>
        squadData != null ? squadData.ResolvedStartingSoldierCount : 0;

    public SquadFormation startingFormation =>
        squadData != null ? squadData.defaultFormation : SquadFormation.Line;

    public SquadStance startingStance =>
        squadData != null ? squadData.defaultStance : SquadStance.EngageFreely;

    public GameObject prefab
    {
        get
        {
            switch (productionType)
            {
                case ProductionType.Squad:
                    return squadData != null && squadData.squadPrefab != null
                        ? squadData.squadPrefab.gameObject
                        : null;

                case ProductionType.Worker:
                    return workerData != null && workerData.prefab != null
                        ? workerData.prefab.gameObject
                        : null;

                default:
                    return null;
            }
        }
    }
}
