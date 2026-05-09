using System.Collections.Generic;
using UnityEngine;

public class TownCenterController : BuildingController
{
    
    private List<SpawnOption> spawnOptions;
    private float spawnTimer;
    private int totalVillagersSpawned = 0;
    
    protected override void Start()
    {
        base.Start();
        spawnOptions = buildingData.spawnOptions;
        spawnTimer = spawnOptions[0].spawnTime; // TEMPORARY 
    }

    protected override void Update()
    {
        base.Update();
        HandleSpawning();
    }

    void HandleSpawning()
    {
        if (!GameManager.Instance.CanSpawn())
            return;
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            //SpawnUnit(spawnOptions[0]); // TEMPORARY VALUE
            spawnTimer = spawnOptions[0].spawnTime;
        }
    }

    void SpawnUnit(SpawnOption option)
    {
        if (option.prefab == null) return;

        Vector3 spawnPos = transform.position + transform.forward * 10f; // TEMPORARY - use spawnLocations in SO later when rally points added.
        Instantiate(option.prefab, spawnPos, Quaternion.identity);
        totalVillagersSpawned++; // SEMI-TEMPORARY
        
        Debug.Log(option.unitName + "spawned");
    }
    
    
}
