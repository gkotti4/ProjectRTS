using System;
using System.Collections.Generic;
using UnityEngine;

public class BuildingPlacer : MonoBehaviour
{
    public static BuildingPlacer Instance { get; private set; }

    //public event Action<bool> OnPlacingModeChanged;
    
    private EntityData selectedBuilding;
    private GameObject ghostObject;
    private bool isPlacing = false; // placement mode
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

    public void StartPlacing(EntityData buildingData) // Called by UI button or hotkey to enter placement mode
    {
        if (isPlacing) CancelPlacement();
        
        // Check resource cost
        else if (!GameManager.Instance.CanAfford(buildingData.buildingCost))
        {
            Debug.Log("Can't afford building");
            CancelPlacement();
            return;
        }
        
        // Enter Placement Mode
        selectedBuilding = buildingData;
        isPlacing = true;
        // OnPlacingModeChanged?.Invoke(isPlacing);
        GameEvents.PlacementModeChanged(isPlacing);
        
        // Spawn ghost preview
        ghostObject = Instantiate(selectedBuilding.prefab);
        if (ghostObject == null) return;

        SetGhostMaterial(validMaterial);
        //Debug.Log("Placing: " + buildingData.entityName);
    }
    
    private void HandlePlacementInput() // Handles confirm and cancel input during placement
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

                if (GridManager.Instance.IsFree(cell, selectedBuilding.gridWidth, selectedBuilding.gridHeight))
                    ConfirmPlacement(snappedPos, cell);
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
            bool canPlace = GridManager.Instance.IsFree(cell, selectedBuilding.gridWidth, selectedBuilding.gridHeight);
            
            SetGhostMaterial(canPlace ? validMaterial : invalidMaterial);
        }
    }


    private void ConfirmPlacement(Vector3 position, Vector2Int cell) // Places the real building and makrs grid cells
    {
        if (selectedBuilding.prefab == null) return;
        
        // Check resource cost
        if (!GameManager.Instance.CanAfford(selectedBuilding.buildingCost))
        {
            Debug.Log("Cannot afford: " + selectedBuilding.entityName);
            CancelPlacement();
            return;
        }
        
        // Place building (Todo: Building construction)
        Instantiate(selectedBuilding.prefab, position, Quaternion.identity);
        GridManager.Instance.SetOccupied(cell, selectedBuilding.gridWidth, selectedBuilding.gridHeight);
        GameManager.Instance.SpendResources(selectedBuilding.buildingCost);
        
        // Note: we could just place the ghost building but somewhat cleaner to just instantiate a new one fresh.
        
        //Debug.Log("Placed: " + selectedBuilding.entityName);
        CancelPlacement();
    }
    
    private void CancelPlacement() // Exits placement mode and destorys ghost
    {
        isPlacing = false;
        selectedBuilding = null;
        // OnPlacingModeChanged?.Invoke(isPlacing);
        GameEvents.PlacementModeChanged(isPlacing);
        
        if (ghostObject != null)
        {
            Destroy(ghostObject);
            ghostObject = null;
        }
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
    
    
}
