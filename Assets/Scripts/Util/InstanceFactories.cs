using System.Collections.Generic;
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


// SESSION: Squad Control
public static class SquadFactory
{
    // SESSION: Squad Control
    public static SquadController SpawnSquadWithMembers(
        SquadData squadData,
        Vector3 position,
        Quaternion rotation,
        FactionInstance faction,
        float spawnRadius = 2f)
    {
        if (!squadData || !squadData.memberPrefab)
        {
            Debug.LogError("SpawnSquadWithMembers failed: squadData is null or empty.");
            return null;
        }

        GameObject squadGO = Object.Instantiate(squadData.squadPrefab.gameObject, position, rotation);

        if (!squadGO.TryGetComponent(out SquadController squad))
        {
            Debug.LogError("SpawnSquadWithMembers failed: squadPrefab has no SquadController.");
            Object.Destroy(squadGO);
            return null;
        }

        List<SquadMemberController> members = new List<SquadMemberController>();

        for (int i = 0; i < squadData.startingMemberCount; i++)
        {
            Vector3 memberPos = position + GetSpawnOffset(i, squadData.startingMemberCount, spawnRadius);

            // Members are real faction-owned entities.
            GameObject memberGO = EntityFactory.Spawn(
                squadData.memberPrefab.gameObject,
                memberPos,
                rotation,
                faction);

            if (!memberGO.TryGetComponent(out SquadMemberController member))
            {
                Debug.LogError("SpawnSquadWithMembers failed: member prefab has no SquadMemberController.");
                Object.Destroy(memberGO);
                continue;
            }

            members.Add(member);
        }

        squad.InitializeSquad(members, squadData.defaultFormation, squadData.defaultStance);
        return squad;
    }

    public static SquadController CreateSquadFromExistingMembers(
        GameObject squadPrefab,
        List<SquadMemberController> members,
        Vector3 position,
        Quaternion rotation,
        SquadFormation formation = SquadFormation.Line,
        SquadStance stance = SquadStance.Aggressive)
    {
        if (squadPrefab == null)
        {
            Debug.LogError("CreateSquadFromExistingMembers failed: squadPrefab is null.");
            return null;
        }

        if (members == null || members.Count == 0)
        {
            Debug.LogError("CreateSquadFromExistingMembers failed: no members.");
            return null;
        }

        GameObject squadGO = Object.Instantiate(squadPrefab, position, rotation);

        if (!squadGO.TryGetComponent(out SquadController squad))
        {
            Debug.LogError("CreateSquadFromExistingMembers failed: squadPrefab has no SquadController.");
            Object.Destroy(squadGO);
            return null;
        }

        squad.InitializeSquad(members, formation, stance);
        return squad;
    }

    static Vector3 GetSpawnOffset(int index, int count, float spacing)
    {
        if (count <= 1)
            return Vector3.zero;

        int rowSize = Mathf.CeilToInt(Mathf.Sqrt(count));
        int row = index / rowSize;
        int col = index % rowSize;

        float x = (col - (rowSize - 1) * 0.5f) * spacing;
        float z = (row - (rowSize - 1) * 0.5f) * spacing;

        return new Vector3(x, 0f, z);
    }
}