using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BuildingPlacer : MonoBehaviour
{
    public static BuildingPlacer Instance { get; private set; }

    [SerializeField] private LayerMask placementBlockingLayers;
    
    private BuildOptionData selectedBuildOption;
    private GameObject ghostObject;
    private bool isPlacing = false; // placement mode
    private bool isSwapping = false;
    public bool IsPlacing => isPlacing;
    private Camera mainCamera;

    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }
    
    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (!isPlacing) return;
        UpdateGhost();
        HandlePlacementInput();
    }

    public void StartPlacing(BuildOptionData buildOption) // Called by UI button or hotkey to enter placement mode (Entry Point)
    {
        if (isPlacing)
        {
            isSwapping = true;
            CancelPlacement();
            isSwapping = false;
        }
        
        // Check resource cost
        if (!GameManager.Instance.CanAfford(buildOption.cost, GameManager.Instance.PlayerFaction))
        {
            Debug.Log("Can't afford building");
            CancelPlacement();
            return;
        }
        
        // Enter Placement Mode
        selectedBuildOption = buildOption;
        isPlacing = true;
        GameEvents.PlacementModeChanged(isPlacing);
        
        // Spawn ghost preview
        ghostObject = Instantiate(selectedBuildOption.buildingData.prefab); 
        if (ghostObject == null) { Debug.LogWarning("Ghost Object is null in BuildingPlacer."); return; }

        // Disable all colliders on ghost and children so it doesn't interact with physics
        foreach (Collider col in ghostObject.GetComponentsInChildren<Collider>()) // Note: Might be a little overboard
            col.enabled = false;
        
        // Disable NavMesh Obstacle
        if (ghostObject.TryGetComponent(out NavMeshObstacle navObstacle))
            navObstacle.enabled = false;

        SetGhostMaterial(validMaterial);
        //Debug.Log("Placing: " + buildingData.entityName);
        
        // Spend Resources
        GameManager.Instance.SpendResources(selectedBuildOption.cost, GameManager.Instance.PlayerFaction);
    }
    
    private void HandlePlacementInput() // Placement mode // Handles confirm and cancel input during placement
    {
        // Cancel on right-click or escape
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
            return;
        }
        
        // Confirm placement on left-click
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 snappedPos = GridManager.Instance.SnapToGrid(hit.point);
                //snappedPos.y = 0; 
                Vector2Int cell = GridManager.Instance.WorldToCell(snappedPos);
                
                if (CanPlace(snappedPos, cell))
                    PlaceBuilding(snappedPos, cell);
            }
        }
        
    }
    
    private void UpdateGhost() // Moves ghost to snapped grid position and updates valid/invalid material
    {
        if (ghostObject == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 snappedPos = GridManager.Instance.SnapToGrid(hit.point);
            //snappedPos.y = 0; 
            ghostObject.transform.position = snappedPos;
            
            Vector2Int cell = GridManager.Instance.WorldToCell(snappedPos);
            //bool canPlace = GridManager.Instance.IsFree(cell, selectedBuildOption.gridWidth, selectedBuildOption.gridHeight);
            bool canPlace = CanPlace(snappedPos, cell);
            
            SetGhostMaterial(canPlace ? validMaterial : invalidMaterial);
        }
    }


    private void PlaceBuilding(Vector3 position, Vector2Int cell) // SPAWN building and marks grid cells
    {
        if (selectedBuildOption.buildingData.prefab == null) return;
        
        // Place building (Todo: Building construction)
        EntityFactory.Spawn(selectedBuildOption.buildingData.prefab, position, Quaternion.identity, GameManager.Instance.PlayerFaction);
        
        GridManager.Instance.SetOccupied(cell, selectedBuildOption.buildingData.gridWidth, selectedBuildOption.buildingData.gridHeight);
        //GameManager.Instance.SpendResources(selectedBuildOption.buildingCost, GameManager.Instance.PlayerFaction); // spent at start 
        
        ExitPlacementMode(); // No refund, resources stay spent
    }
    
    private void CancelPlacement() // cancel = refund
    {
        if (selectedBuildOption != null)
            GameManager.Instance.AddResources(selectedBuildOption.cost, GameManager.Instance.PlayerFaction);
        ExitPlacementMode();
    }

    private void ExitPlacementMode() // shared cleanup
    {
        isPlacing = false;
        selectedBuildOption = null;
        if (!isSwapping)
            GameEvents.PlacementModeChanged(false);
        if (ghostObject != null) { Destroy(ghostObject); ghostObject = null; }
    }
    
    private void SetGhostMaterial(Material mat)
    {
        if (ghostObject == null || mat == null) return;
        foreach (Renderer r in ghostObject.GetComponentsInChildren<Renderer>())
        {
            // Handle multiple materials per renderer (submeshes)
            Material[] mats = new Material[r.materials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
            r.materials = mats;
        }
    }



    private bool CanPlace(Vector3 position, Vector2Int cell)
    {
        // Is grid space free?
        // Is building placement over object?
        
        return GridManager.Instance.IsFree(cell, selectedBuildOption.buildingData.gridWidth, selectedBuildOption.buildingData.gridHeight) &&
               !IsPlacementBlockedByObject(position);
        // Add cost check?
    }
    
    
    private readonly Collider[] overlapResults = new Collider[16];
    private bool IsPlacementBlockedByObject(Vector3 position)
    {
        Vector3 halfExtents = new Vector3(
            selectedBuildOption.buildingData.gridWidth * GridManager.Instance.GetCellSize() / 2f,
            1f,
            selectedBuildOption.buildingData.gridHeight * GridManager.Instance.GetCellSize() / 2f
        );

        int hitCount = Physics.OverlapBoxNonAlloc(position, halfExtents, overlapResults, Quaternion.identity, placementBlockingLayers);
    
        for (int i = 0; i < hitCount; i++)
        {
            if (overlapResults[i].gameObject == ghostObject) continue;
            if (overlapResults[i].TryGetComponent(out UnitController _)) return true;
            if (overlapResults[i].TryGetComponent(out BuildingController _)) return true;
            if (overlapResults[i].TryGetComponent(out ResourceNode _)) return true;
        }
        return false;
    }
    
}
