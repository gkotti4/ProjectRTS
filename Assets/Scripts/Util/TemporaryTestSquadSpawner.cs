// SESSION: Squad Control

using System.Net;
using Unity.VisualScripting;
using UnityEngine;

public class TemporaryTestSquadSpawner : MonoBehaviour
{
    [SerializeField] private SquadData squadDataTest;
    [SerializeField] private SquadData squadDataRangedTest;
    [SerializeField] private SquadData squadDataTestAsEnemy;
    
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        
    }

    void Update()
    {
        // Spawn Squad as Player
        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            FactionInstance _factionInstance = GameManager.Instance.PlayerFaction;
            if (_factionInstance == null)
            {
                Debug.LogError("InitializeSquad failed: squad members have not spawned on a faction.");
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, GameLayers.Instance.GroundLayers))
                return;
            Vector3 spawnPos = new Vector3(hit.point.x, 0.1f, hit.point.z);
            SquadFactory.SpawnSquad(squadDataTest, spawnPos, Quaternion.identity, _factionInstance);
        }
        
        // Spawn Squad as Enemy
        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            FactionInstance _factionInstance = GameManager.Instance.EnemyFaction;
            if (_factionInstance == null)
            {
                Debug.LogError("InitializeSquad failed: squad members have not spawned on a faction.");
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, GameLayers.Instance.GroundLayers))
                return;
            Vector3 spawnPos = new Vector3(hit.point.x, 0.1f, hit.point.z);
            SquadFactory.SpawnSquad(squadDataTestAsEnemy, spawnPos, Quaternion.identity, _factionInstance);
        }

        // Spawn Ranged Squad as Player
        if (Input.GetKeyDown(KeyCode.Backslash))
        {
            FactionInstance _factionInstance = GameManager.Instance.PlayerFaction;
            if (_factionInstance == null)
            {
                Debug.LogError("InitializeSquad failed: squad members have not spawned on a faction.");
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, GameLayers.Instance.GroundLayers))
                return;
            Vector3 spawnPos = new Vector3(hit.point.x, 0.1f, hit.point.z);
            SquadFactory.SpawnSquad(squadDataRangedTest, spawnPos, Quaternion.identity, _factionInstance);
        }
    }
}
