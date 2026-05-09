using UnityEngine;

/// <summary>
/// Handles player input commands and routes them to the appropriate game systems.
/// Owns intentional player actions (building placement, hotkeys, game commands).
/// Does not own state - only reads input and calls other systems - unlike Managers.
/// </summary>

public class PlayerInputHandler : MonoBehaviour
{

    [SerializeField] private EntityData townCenterData;
    
    
    void Start()
    {
        
    }

    void Update()
    {
        HandleBuildingHotkeys();
    }


    void HandleBuildingHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            BuildingPlacer.Instance.StartPlacing(townCenterData);
        }
    }
    
    
}
