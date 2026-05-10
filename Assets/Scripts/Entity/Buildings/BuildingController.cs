using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(EntityStats))]

public class BuildingController : MonoBehaviour, ISelectable, IDamageable
{
    [SerializeField] private DecalProjector selectionDecal;
    [SerializeField] private Transform spawnPoint;

    protected EntityStats stats;
    public EntityStats Stats => stats;
    protected Health health;
    protected bool isSelected = false;
    
    // Production
    private List<ProductionOptionData> productionQueue = new List<ProductionOptionData>();
    private float productionTimer = 0f;
    private bool isProducing = false;
    
    
    // ISelectable
    public virtual void OnSelect() { isSelected = true; if (selectionDecal) selectionDecal.enabled = true; }
    public virtual void OnDeselect() { isSelected = false; if (selectionDecal) selectionDecal.enabled = false; }
    public GameObject GetGameObject() => gameObject;
    public bool IsBoxSelectable => false;
    
    // IDamageable
    public void TakeDamage(int damage){ health.TakeDamage(damage); }


    protected virtual void Awake()
    {
        gameObject.transform.rotation = Quaternion.Euler(0, 90, 0); // rotate 90 degrees so its facing our camera view better - possibly REMOVE
        
        stats = GetComponent<EntityStats>();
        health = GetComponent<Health>();
        health.Initialize(stats.maxHealth);
        if (selectionDecal) selectionDecal.enabled = false;
    }
    
    protected virtual void Start()
    {
        
    }

    protected virtual void Update()
    {
        HandleProduction();
    }

    public void EnqueueProduction(ProductionOptionData option) // Addds a production option to the queue if under cap
    {
        const int cap = 10;
        if (productionQueue.Count >= cap) return;

        // Unit checks
        if (option.productionType == ProductionType.Unit)
        {
            if (!GameManager.Instance.CanSpawn()) return;
        }
        
        // Upgrade checks
        if (option.productionType == ProductionType.Upgrade)
        {
            if (!GameManager.Instance.IsUpgradeApplied(option.upgradeData)) return;
            // Todo: upgrade path 
        }
        
        if (!GameManager.Instance.CanAfford(option.cost)) return;
        
        GameManager.Instance.SpendResources(option.cost);
        productionQueue.Add(option);

        if (!isProducing)
            StartNextProduction();
    }

    public void CancelProduction(int index)
    {
        if (index < 0 || index >= productionQueue.Count) return;
        
        // Refund resources
        GameManager.Instance.AddResources(productionQueue[index].cost);

        productionQueue.RemoveAt(index);
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
    }


    private void StartNextProduction() 
    {
        if (productionQueue.Count == 0) return;

        ProductionOptionData next = productionQueue[0];
        productionTimer = next.productionTime / stats.productionSpeed;
        isProducing = true;
    }

    private void SpawnUnit(ProductionOptionData option)
    {
        if (!GameManager.Instance.CanSpawn()) return;
        if (option.prefab == null) return;
        
        Vector3 spawnPos = GetSpawnPosition();
        Instantiate(option.prefab, spawnPos, Quaternion.identity);
        GameManager.Instance.RegisterSpawn();
        
        Debug.Log(option.prefab.name + " spawned from " + stats.baseData.entityName + " to " + spawnPos);
    }

    private void ApplyUpgrade(ProductionOptionData option)
    {
        if(option.upgradeData == null) return;
        GameManager.Instance.RegisterUpgrade(option.upgradeData);
        
        Debug.Log(option.upgradeData.upgradeName + " researched");
    }

    private Vector3 GetSpawnPosition() // later - (Vector3 rallyPoint) and use closest spawn position
    {
        if (spawnPoint == null)
        {
            Debug.LogWarning("No Spawn Point");
            return Vector3.zero;
        }
        return GridManager.Instance.SnapToGrid(spawnPoint.position);
    }
}
