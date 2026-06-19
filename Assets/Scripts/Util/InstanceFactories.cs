using UnityEngine;

public static class WorkerFactory
{
    public static WorkerController SpawnWorker(
        WorkerData workerData,
        Vector3 position,
        Quaternion rotation,
        FactionInstance factionInstance)
    {
        if (workerData == null)
        {
            Debug.LogError("SpawnWorker failed: WorkerData is null.");
            return null;
        }

        if (workerData.prefab == null)
        {
            Debug.LogError($"SpawnWorker failed: {workerData.name} has no prefab.");
            return null;
        }

        WorkerController worker = Object.Instantiate(
            workerData.prefab,
            position,
            rotation);

        FactionOwner factionOwner = worker.GetComponent<FactionOwner>();
        if (factionOwner == null)
            factionOwner = worker.gameObject.AddComponent<FactionOwner>();

        factionOwner.Initialize(factionInstance);

        return worker;
    }
}

public static class SquadFactory
{
    public static SquadController SpawnSquad(
        SquadData squadData,
        Vector3 position,
        Quaternion rotation,
        FactionInstance factionInstance)
    {
        if (squadData == null)
        {
            Debug.LogError("SpawnSquad failed: SquadData is null.");
            return null;
        }

        if (squadData.squadPrefab == null)
        {
            Debug.LogError($"SpawnSquad failed: {squadData.name} has no squadPrefab.");
            return null;
        }

        if (squadData.soldierData == null)
        {
            Debug.LogError($"SpawnSquad failed: {squadData.name} has no soldierData.");
            return null;
        }

        if (squadData.soldierData.prefab == null)
        {
            Debug.LogError($"SpawnSquad failed: {squadData.soldierData.name} has no Soldier prefab.");
            return null;
        }

        SquadController squad = Object.Instantiate(
            squadData.squadPrefab,
            position,
            rotation);

        FactionOwner factionOwner = squad.GetComponent<FactionOwner>();
        if (factionOwner == null)
            factionOwner = squad.gameObject.AddComponent<FactionOwner>();

        factionOwner.Initialize(factionInstance);

        squad.Initialize(squadData, factionInstance);

        return squad;
    }
}