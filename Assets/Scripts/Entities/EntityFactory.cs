using UnityEngine;

public static class EntityFactory
{
    // Spawns an entity and assigns faction atomically
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, FactionInstance faction)
    {
        if (prefab == null)
        {
            Debug.LogError("EntityFactory.Spawn — prefab is null");
            return null;
        }

        GameObject entity = Object.Instantiate(prefab, position, rotation);

        if (entity.TryGetComponent(out EntityStats stats))
            stats.faction = faction;
        else
            Debug.Log("EntityFactory.Spawn — " + prefab.name + " has no EntityStats");
        
        // Once we do not use pre-spawned units, we could put SelectionManager.Instance.RegisterSelectable here too if we want

        return entity;
    }
    
}