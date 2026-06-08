// SESSION: Squad Control

using System.Net;
using Unity.VisualScripting;
using UnityEngine;

public class TemporaryTestSquadSpawner : MonoBehaviour
{
    [SerializeField] private GameObject squadPrefab;
    [SerializeField] private GameObject memberPrefab;
    [SerializeField] private int memberCount = 6;
    [SerializeField] private SquadFormation formation = SquadFormation.Line;
    [SerializeField] private CombatStance stance = CombatStance.Aggressive;

    [SerializeField] private LayerMask spawnSquadSelectableLayers;

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
            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, spawnSquadSelectableLayers))
                return;
            Vector3 spawnPos = new Vector3(hit.point.x, 0.1f, hit.point.z);
            SquadFactory.SpawnSquadWithMembers(squadPrefab, memberPrefab, memberCount, spawnPos, Quaternion.identity, factionInstance);
        }
    }
}
