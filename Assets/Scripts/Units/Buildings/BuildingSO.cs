using System.Collections.Generic;
using UnityEngine;

//[RequireComponent(typeof(Health)]

[CreateAssetMenu(fileName = "BuildingSO", menuName = "Scriptable Objects/BuildingSO")]
public class BuildingSO : ScriptableObject // Inherit from UnitSO?
{
    // Building Identifiers
    public string buildingName = "Building";
    public int buildingID = 0;
    public BuildingType buildingType;
    public GameObject prefab;
    
    // Building Specs
    public ResourceCost buildingCost = new ResourceCost();
    public int buildingHealth = 500;
    
    // Grid Specs
    public int gridWidth = 3;
    public int gridHeight = 3;
    
    // Spawning Specs (If applicable)
    public List<SpawnOption> spawnOptions;
    public List<Vector3> spawnLocations;


}
    // upgrades, armor, vision, size, etc.
