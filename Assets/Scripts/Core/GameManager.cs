using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // * Resources
    [SerializeField] private int startingResources = 0;
    private Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();
    
    // * Population
    [SerializeField] private int currentPopulation = 0;
    [SerializeField] private int populationCap = 10; // increases when houses are built; hard coded for now
    
    void Awake()
    {
        if (Instance is not null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // persists across scene loads
    }

    void Start()
    {
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            resources[type] = startingResources;
    }

    // ** Resources
    public int GetCurrentResources(ResourceType type) => resources[type];
    public void AddResources(ResourceType type, int amount)
    {
        resources[type] += amount;
        Debug.Log(type + ": " + resources[type]);
    }
    
    public void SpendResources(ResourceCost cost)
    {
        resources[ResourceType.Wood] -= cost.wood; 
        resources[ResourceType.Food] -= cost.food;
        resources[ResourceType.Gold] -= cost.gold;
        resources[ResourceType.Stone] -= cost.stone;
    }

    public bool CanAfford(ResourceCost cost)
    {
        return resources[ResourceType.Wood] >= cost.wood &&
               resources[ResourceType.Gold] >= cost.gold &&
               resources[ResourceType.Food] >= cost.food &&
               resources[ResourceType.Stone] >= cost.stone;
    }
    
    
    // ** Population
    public bool CanSpawn()
    {
        return currentPopulation < populationCap;
    }

    public void OnUnitSpawned()
    {
        currentPopulation++;
    }

    public void OnUnitDespawned()
    {
        currentPopulation--;
    }
    
}





