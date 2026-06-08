// SESSION: Squad Control Refactor

using System.Collections.Generic;
using UnityEngine;

public class BuildingController : EntityController
{
    #region Fields

    [Header("Rally")]
    [SerializeField] private GameObject rallyFlag;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform pivotPoint;

    [Header("Production")]
    [SerializeField] private int queueCap = 10;

    private readonly List<ProductionOptionData> productionQueue = new List<ProductionOptionData>();

    private float productionTimer = 0f;
    private bool isProducing = false;

    public List<ProductionOptionData> ProductionQueue => productionQueue;
    public override bool IsDragSelectable => false;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        // TEMP:
        // Keep your existing building rotation behavior for now.
        transform.rotation = Quaternion.Euler(0, 90, 0);

        base.Awake();
    }

    protected override void Start()
    {
        base.Start();

        if (rallyFlag != null)
            rallyFlag.SetActive(false);
    }

    protected virtual void Update()
    {
        HandleProduction();
    }

    #endregion

    #region Selection

    public override void OnSelect()
    {
        base.OnSelect();

        if (rallyFlag != null)
            rallyFlag.SetActive(true);
    }

    public override void OnDeselect()
    {
        base.OnDeselect();

        if (rallyFlag != null)
            rallyFlag.SetActive(false);
    }

    #endregion

    #region Production Queue

    public void EnqueueProduction(ProductionOptionData option)
    {
        if (!CanQueueProduction(option))
            return;

        GameManager.Instance.SpendResources(option.cost, stats.faction);
        productionQueue.Add(option);

        if (!isProducing)
            StartNextProduction();

        GameEvents.ProductionQueueChanged(this);
    }

    public void CancelProduction(int index)
    {
        if (index < 0 || index >= productionQueue.Count)
            return;

        ProductionOptionData option = productionQueue[index];

        GameManager.Instance.AddResources(option.cost, stats.faction);
        productionQueue.RemoveAt(index);

        if (index == 0)
        {
            isProducing = false;
            productionTimer = 0f;

            if (productionQueue.Count > 0)
                StartNextProduction();
        }

        GameEvents.ProductionQueueChanged(this);
    }

    bool CanQueueProduction(ProductionOptionData option)
    {
        if (option == null)
            return false;

        if (productionQueue.Count >= queueCap)
            return false;

        if (!GameManager.Instance.CanAfford(option.cost, stats.faction))
            return false;

        switch (option.productionType)
        {
            case ProductionType.Unit:
                return CanQueueUnitProduction(option);

            case ProductionType.Upgrade:
                return CanQueueUpgradeProduction(option);

            default:
                return false;
        }
    }

    bool CanQueueUnitProduction(ProductionOptionData option)
    {
        if (!GameManager.Instance.CanSpawn(stats.faction))
            return false;

        // Legacy unit production.
        if (!option.producesSquad)
            return option.prefab != null;

        // Squad production.
        if (option.squadPrefab == null)
            return false;

        if (option.memberPrefab == null)
            return false;

        if (option.squadMemberCount <= 0)
            return false;

        return true;
    }

    bool CanQueueUpgradeProduction(ProductionOptionData option)
    {
        if (option.upgradeData == null)
            return false;

        if (GameManager.Instance.IsUpgradeApplied(option.upgradeData, stats.faction))
            return false;

        return true;
    }

    void HandleProduction()
    {
        if (!isProducing) return;
        if (productionQueue.Count == 0) return;

        productionTimer -= Time.deltaTime;

        if (productionTimer <= 0f)
            CompleteProduction();
    }

    void StartNextProduction()
    {
        if (productionQueue.Count == 0)
            return;

        productionTimer = productionQueue[0].productionTime / stats.productionSpeed;
        isProducing = true;

        GameEvents.ProductionQueueChanged(this);
    }

    void CompleteProduction()
    {
        if (productionQueue.Count == 0)
            return;

        ProductionOptionData completed = productionQueue[0];

        productionQueue.RemoveAt(0);
        isProducing = false;

        switch (completed.productionType)
        {
            case ProductionType.Unit:
                CompleteUnitProduction(completed);
                break;

            case ProductionType.Upgrade:
                ApplyUpgrade(completed);
                break;
        }

        if (productionQueue.Count > 0)
            StartNextProduction();

        GameEvents.ProductionQueueChanged(this);
    }

    #endregion

    #region Unit / Squad Production

    void CompleteUnitProduction(ProductionOptionData option)
    {
        if (option.producesSquad)
        {
            SpawnSquad(option);
            return;
        }

        SpawnLegacyUnit(option);
    }

    void SpawnLegacyUnit(ProductionOptionData option)
    {
        if (option.prefab == null)
            return;

        if (!GameManager.Instance.CanSpawn(stats.faction))
            return;

        GameObject spawned = EntityFactory.Spawn(
            option.prefab,
            GetSpawnPosition(),
            Quaternion.identity,
            stats.faction);

        if (spawned == null)
            return;

        SendSpawnedUnitToRally(spawned);
    }

    void SpawnSquad(ProductionOptionData option)
    {
        if (!GameManager.Instance.CanSpawn(stats.faction))
            return;

        Vector3 spawnPosition = GetSpawnPosition(randomize: false);

        SquadController squad = SquadFactory.SpawnSquadWithMembers(
            option.squadPrefab,
            option.memberPrefab,
            option.squadMemberCount,
            spawnPosition,
            Quaternion.identity,
            stats.faction,
            option.startingFormation,
            option.startingStance);

        if (squad == null)
            return;

        SendSpawnedSquadToRally(squad);
    }

    void SendSpawnedUnitToRally(GameObject spawned)
    {
        if (rallyFlag == null)
            return;

        if (!spawned.TryGetComponent(out UnitController unit))
            return;

        Vector3 rallyPos = GetRallyPositionWithScatter();
        unit.OrderMove(rallyPos);
    }

    void SendSpawnedSquadToRally(SquadController squad)
    {
        if (rallyFlag == null)
            return;

        Vector3 rallyPosition = rallyFlag.transform.position;
        squad.OrderMove(rallyPosition);
    }

    #endregion

    #region Upgrades

    void ApplyUpgrade(ProductionOptionData option)
    {
        if (option.upgradeData == null)
            return;

        GameManager.Instance.RegisterUpgrade(option.upgradeData, stats.faction);
    }

    #endregion

    #region Rally / Spawn Helpers

    Vector3 GetSpawnPosition(bool randomize = true, float randomOffset = 2f)
    {
        if (spawnPoint == null)
        {
            Debug.LogWarning($"{name} has no spawn point.");
            return transform.position;
        }

        Vector3 position = GridManager.Instance.SnapToGrid(spawnPoint.position);

        if (!randomize)
            return position;

        return position + new Vector3(
            Random.Range(-randomOffset, randomOffset),
            0f,
            Random.Range(-randomOffset, randomOffset));
    }

    Vector3 GetRallyPositionWithScatter(float scatter = 2f)
    {
        if (rallyFlag == null)
            return GetSpawnPosition();

        return rallyFlag.transform.position + new Vector3(
            Random.Range(-scatter, scatter),
            0f,
            Random.Range(-scatter, scatter));
    }

    public void SetRallyPoint(Vector3 position)
    {
        if (rallyFlag == null)
            return;

        rallyFlag.transform.position = position;
    }

    #endregion

    #region UI

    public float GetProductionProgress()
    {
        if (!isProducing || productionQueue.Count == 0)
            return 0f;

        return 1f - productionTimer / productionQueue[0].productionTime;
    }

    #endregion
}




// using System.Collections.Generic;
// using UnityEngine;
//
// public class BuildingController : EntityController
// {
//     [SerializeField] private GameObject rallyFlag;
//     [SerializeField] private Transform spawnPoint;
//     [SerializeField] private Transform pivotPoint;
//     
//     // Production
//     private List<ProductionOptionData> productionQueue = new List<ProductionOptionData>();
//     public List<ProductionOptionData> ProductionQueue => productionQueue;
//     private float productionTimer = 0f;
//     private bool isProducing = false;
//
//     public override bool IsDragSelectable => false;
//     
//
//     protected override void Awake()
//     {
//         gameObject.transform.rotation = Quaternion.Euler(0, 90, 0); // TEMP
//         base.Awake();
//     }
//
//     protected override void Start()
//     {
//         base.Start();
//         if (rallyFlag != null) rallyFlag.SetActive(false);
//     }
//
//     public override void OnSelect()
//     {
//         base.OnSelect();
//         if (rallyFlag != null) rallyFlag.SetActive(true);
//     }
//     
//     public override void OnDeselect()
//     {
//         base.OnDeselect();
//         if (rallyFlag != null) rallyFlag.SetActive(false);
//     }
//
//     protected virtual void Update()
//     {
//         HandleProduction();
//     }
//
//     // Production Section
//     public void EnqueueProduction(ProductionOptionData option)
//     {
//         const int cap = 10;
//         if (productionQueue.Count >= cap) return;
//
//         if (option.productionType == ProductionType.Unit)
//             if (!GameManager.Instance.CanSpawn(stats.faction)) return;
//
//         if (option.productionType == ProductionType.Upgrade)
//             if (GameManager.Instance.IsUpgradeApplied(option.upgradeData, stats.faction)) return;
//
//         if (!GameManager.Instance.CanAfford(option.cost, stats.faction)) return;
//
//         GameManager.Instance.SpendResources(option.cost, stats.faction);
//         productionQueue.Add(option);
//
//         if (!isProducing)
//             StartNextProduction();
//
//         GameEvents.ProductionQueueChanged(this);
//     }
//
//     public void CancelProduction(int index)
//     {
//         if (index < 0 || index >= productionQueue.Count) return;
//
//         GameManager.Instance.AddResources(productionQueue[index].cost, stats.faction);
//         productionQueue.RemoveAt(index);
//
//         if (index == 0)
//         {
//             isProducing = false;
//             productionTimer = 0f;
//             if (productionQueue.Count > 0)
//                 StartNextProduction();
//         }
//
//         GameEvents.ProductionQueueChanged(this);
//     }
//
//     private void HandleProduction()
//     {
//         if (!isProducing || productionQueue.Count == 0) return;
//         productionTimer -= Time.deltaTime;
//         if (productionTimer <= 0)
//             CompleteProduction();
//     }
//
//     private void CompleteProduction()
//     {
//         ProductionOptionData completed = productionQueue[0];
//         productionQueue.RemoveAt(0);
//         isProducing = false;
//
//         if (completed.productionType == ProductionType.Unit)
//             SpawnUnit(completed);
//         else if (completed.productionType == ProductionType.Upgrade)
//             ApplyUpgrade(completed);
//
//         if (productionQueue.Count > 0)
//             StartNextProduction();
//
//         GameEvents.ProductionQueueChanged(this);
//     }
//
//     private void StartNextProduction()
//     {
//         if (productionQueue.Count == 0) return;
//         productionTimer = productionQueue[0].productionTime / stats.productionSpeed;
//         isProducing = true;
//         GameEvents.ProductionQueueChanged(this);
//     }
//
//     private void SpawnUnit(ProductionOptionData option)
//     {
//         if (!GameManager.Instance.CanSpawn(stats.faction) || option.prefab == null) return;
//         GameObject spawned = EntityFactory.Spawn(option.prefab, GetSpawnPosition(), Quaternion.identity, stats.faction);
//
//         if (rallyFlag != null && spawned.TryGetComponent(out UnitController uc))
//         {
//             Vector3 rallyPos = rallyFlag.transform.position;
//             
//             // Spread units around rally point
//             Vector3 rallyOffset = new Vector3(
//                 Random.Range(-2f, 2f),
//                 0f,
//                 Random.Range(-2f, 2f)
//             );
//             
//             uc.OrderMove(rallyPos + rallyOffset);
//         }
//     }
//
//     private void ApplyUpgrade(ProductionOptionData option)
//     {
//         if (option.upgradeData == null) return;
//         GameManager.Instance.RegisterUpgrade(option.upgradeData, stats.faction);
//     }
//
//     private Vector3 GetSpawnPosition(bool randomize = true, float randomOffset = 2f)
//     {
//         if (spawnPoint == null) { Debug.LogWarning("No Spawn Point"); return Vector3.zero; }
//         if (randomize)
//             return GridManager.Instance.SnapToGrid(spawnPoint.position) +
//                    new Vector3(Random.Range(-randomOffset, randomOffset), 0f, Random.Range(-randomOffset, randomOffset));
//         return GridManager.Instance.SnapToGrid(spawnPoint.position);
//     }
//
//     public float GetProductionProgress()
//     {
//         if (!isProducing || productionQueue.Count == 0) return 0f;
//         return 1f - (productionTimer / productionQueue[0].productionTime);
//     }
//
//
//     public void SetRallyPoint(Vector3 position)
//     {
//         if (rallyFlag != null)
//             rallyFlag.transform.position = position;
//     }
// }