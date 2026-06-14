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
            
            case ProductionType.Squad:
                return CanQueueSquadProduction(option);

            case ProductionType.Upgrade:
                return CanQueueUpgradeProduction(option);

            default:
                return false;
        }
    }

    bool CanQueueUnitProduction(ProductionOptionData option)
    {
        // Population check
        if (!GameManager.Instance.CanSpawn(stats.faction))
            return false;

        if (option.productionType != ProductionType.Unit)
            return false;

        if (!option.unitPrefab.gameObject)
            return false;
        
        return true;
    }

    bool CanQueueSquadProduction(ProductionOptionData option)
    {
        // Population check
        if (!GameManager.Instance.CanSpawn(stats.faction, option.squadData.startingMemberCount))
            return false;
        
        if (!option.squadData)
            return false;
        if (!option.squadData.squadPrefab)
            return false;
        if (!option.squadData.memberPrefab)
            return false;
        
        return true;
    }

    bool CanQueueUpgradeProduction(ProductionOptionData option)
    {
        if (!option.upgradeData)
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
                // CompleteUnitProduction(completed);
                SpawnLegacyUnit(completed);
                break;
            
            case ProductionType.Squad:
                // CompleteSquadProduction(completed);
                SpawnSquad(completed);
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

    void SpawnLegacyUnit(ProductionOptionData completed)
    {
        if (completed.unitPrefab.gameObject == null)
            return;

        if (!GameManager.Instance.CanSpawn(stats.faction))
            return;

        GameObject spawned = EntityFactory.Spawn(
            completed.unitPrefab.gameObject,
            GetSpawnPosition(),
            Quaternion.identity,
            stats.faction);

        if (!spawned)
            return;

        SendSpawnedUnitToRally(spawned);
    }

    void SpawnSquad(ProductionOptionData option)
    {
        if (!GameManager.Instance.CanSpawn(stats.faction, option.squadData.startingMemberCount))
            return;

        Vector3 spawnPosition = GetSpawnPosition(randomize: false);

        SquadController squad = SquadFactory.SpawnSquadWithMembers(
            option.squadData,
            spawnPosition,
            Quaternion.identity,
            stats.faction);

        if (!squad)
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



