using UnityEngine;

[CreateAssetMenu(fileName = "UnitSO", menuName = "Scriptable Objects/Unit")]
public class UnitSO : ScriptableObject
{
    // Unit Identifiers
    public string unitName = "Unit";
    public int unitID = 0;
    public UnitType unitType;
    
    // Base Unit Specs
    public int maxHealth = 100;
    public int attackDamage = 5;
    public float attackRange = 2f;
    public float attackInterval = 1f;
    
    // Pathfinding Specs
    public float moveSpeed = 3.5f;
    //public float navMeshBaseOffset = 1f; // set per-model-prefab

    
    // Resource Gathering Specs
    public int gatherAmount = 1;
    public float gatherRange = 3f;
    public float gatherInterval = 1f;
    
}
