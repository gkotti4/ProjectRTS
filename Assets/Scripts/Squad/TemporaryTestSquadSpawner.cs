// SESSION: Squad Control

using System.Net;
using Unity.VisualScripting;
using UnityEngine;

public class TemporaryTestSquadSpawner : MonoBehaviour
{
    [SerializeField] private SquadData squadDataTest;
    
    private FactionInstance factionInstance;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        
        
        factionInstance = GameManager.Instance.PlayerFaction;
        if (factionInstance == null)
        {
            Debug.LogError("InitializeSquad failed: squad members have not spawned on a faction.");
            return;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            factionInstance = GameManager.Instance.PlayerFaction;
            if (factionInstance == null)
            {
                Debug.LogError("InitializeSquad failed: squad members have not spawned on a faction.");
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, GameLayers.Instance.GroundLayers))
                return;
            Vector3 spawnPos = new Vector3(hit.point.x, 0.1f, hit.point.z);
            SquadFactory.SpawnSquadWithMembers(squadDataTest, spawnPos, Quaternion.identity, factionInstance);
        }
    }
}
