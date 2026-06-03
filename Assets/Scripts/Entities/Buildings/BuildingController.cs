using System.Collections.Generic;
using UnityEngine;

public class BuildingController : EntityController
{
    [SerializeField] private GameObject rallyFlag;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform pivotPoint;
    
    // Production
    private List<ProductionOptionData> productionQueue = new List<ProductionOptionData>();
    public List<ProductionOptionData> ProductionQueue => productionQueue;
    private float productionTimer = 0f;
    private bool isProducing = false;

    public override bool IsDragSelectable => false;
    

    protected override void Awake()
    {
        gameObject.transform.rotation = Quaternion.Euler(0, 90, 0); // TEMP
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        if (rallyFlag != null) rallyFlag.SetActive(false);
    }

    public override void OnSelect()
    {
        base.OnSelect();
        if (rallyFlag != null) rallyFlag.SetActive(true);
    }
    
    public override void OnDeselect()
    {
        base.OnDeselect();
        if (rallyFlag != null) rallyFlag.SetActive(false);
    }

    protected virtual void Update()
    {
        HandleProduction();
    }

    // Production Section
    public void EnqueueProduction(ProductionOptionData option)
    {
        const int cap = 10;
        if (productionQueue.Count >= cap) return;

        if (option.productionType == ProductionType.Unit)
            if (!GameManager.Instance.CanSpawn(stats.faction)) return;

        if (option.productionType == ProductionType.Upgrade)
            if (GameManager.Instance.IsUpgradeApplied(option.upgradeData, stats.faction)) return;

        if (!GameManager.Instance.CanAfford(option.cost, stats.faction)) return;

        GameManager.Instance.SpendResources(option.cost, stats.faction);
        productionQueue.Add(option);

        if (!isProducing)
            StartNextProduction();

        GameEvents.ProductionQueueChanged(this);
    }

    public void CancelProduction(int index)
    {
        if (index < 0 || index >= productionQueue.Count) return;

        GameManager.Instance.AddResources(productionQueue[index].cost, stats.faction);
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

    private void HandleProduction()
    {
        if (!isProducing || productionQueue.Count == 0) return;
        productionTimer -= Time.deltaTime;
        if (productionTimer <= 0)
            CompleteProduction();
    }

    private void CompleteProduction()
    {
        ProductionOptionData completed = productionQueue[0];
        productionQueue.RemoveAt(0);
        isProducing = false;

        if (completed.productionType == ProductionType.Unit)
            SpawnUnit(completed);
        else if (completed.productionType == ProductionType.Upgrade)
            ApplyUpgrade(completed);

        if (productionQueue.Count > 0)
            StartNextProduction();

        GameEvents.ProductionQueueChanged(this);
    }

    private void StartNextProduction()
    {
        if (productionQueue.Count == 0) return;
        productionTimer = productionQueue[0].productionTime / stats.productionSpeed;
        isProducing = true;
        GameEvents.ProductionQueueChanged(this);
    }

    private void SpawnUnit(ProductionOptionData option)
    {
        if (!GameManager.Instance.CanSpawn(stats.faction) || option.prefab == null) return;
        GameObject spawned = EntityFactory.Spawn(option.prefab, GetSpawnPosition(), Quaternion.identity, stats.faction);

        if (rallyFlag != null && spawned.TryGetComponent(out UnitController uc))
        {
            Vector3 rallyPos = rallyFlag.transform.position;
            
            // Spread units around rally point
            Vector3 rallyOffset = new Vector3(
                Random.Range(-2f, 2f),
                0f,
                Random.Range(-2f, 2f)
            );
            
            uc.OrderMove(rallyPos + rallyOffset);
        }
    }

    private void ApplyUpgrade(ProductionOptionData option)
    {
        if (option.upgradeData == null) return;
        GameManager.Instance.RegisterUpgrade(option.upgradeData, stats.faction);
    }

    private Vector3 GetSpawnPosition(bool randomize = true, float randomOffset = 2f)
    {
        if (spawnPoint == null) { Debug.LogWarning("No Spawn Point"); return Vector3.zero; }
        if (randomize)
            return GridManager.Instance.SnapToGrid(spawnPoint.position) +
                   new Vector3(Random.Range(-randomOffset, randomOffset), 0f, Random.Range(-randomOffset, randomOffset));
        return GridManager.Instance.SnapToGrid(spawnPoint.position);
    }

    public float GetProductionProgress()
    {
        if (!isProducing || productionQueue.Count == 0) return 0f;
        return 1f - (productionTimer / productionQueue[0].productionTime);
    }


    public void SetRallyPoint(Vector3 position)
    {
        if (rallyFlag != null)
            rallyFlag.transform.position = position;
    }
}