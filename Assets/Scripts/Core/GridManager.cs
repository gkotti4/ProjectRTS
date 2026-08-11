using System;
using UnityEngine;


/*
 * Start from gridStartPosition bottom left to top right
 */

public class GridManager : MonoBehaviour // Used by all factions
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Origin")]
    [Tooltip("World-space X/Z position of the grid's bottom-left corner. Cell (0,0) begins here.")]
    [SerializeField] private Vector2 gridStartPosition = Vector2.zero;
    
    [SerializeField] private int gridWidth = 250;
    [SerializeField] private int gridHeight = 250;
    [SerializeField] private float cellSize = 2f; // integer?

    private bool[,] occupiedCells;

    public int GetGridWidth() => gridWidth;
    public int GetGridHeight() => gridHeight;
    public float GetCellSize() => cellSize;
    public Vector2 GetGridStartPosition() => gridStartPosition;

    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        occupiedCells = new bool[gridWidth, gridHeight];
    }

    public Vector2Int WorldToCell(Vector3 worldPos) // Converts world position to grid cell position
    {
        float localX = worldPos.x - gridStartPosition.x;
        float localZ = worldPos.z - gridStartPosition.y;

        int x = Mathf.FloorToInt(localX / cellSize);
        int y = Mathf.FloorToInt(localZ / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 CellToWorld(Vector2Int cell) // Converts grid cell coordinate to a world position
    {
        float x = gridStartPosition.x + cell.x * cellSize + cellSize / 2f;
        float z = gridStartPosition.y + cell.y * cellSize + cellSize / 2f;
        
        // // Raycast down to find actual terrain height - FOR NON-FLAT TERRAIN
        // if (Physics.Raycast(new Vector3(x, 100f, z), Vector3.down, out RaycastHit hit, 200f))
        //     return new Vector3(x, hit.point.y, z);
        
        return new Vector3(x, 0f, z);
    }
    
    public Vector3 SnapToGrid(Vector3 worldPos) // Snaps a world position to the nearest grid cell center
    {
        Vector2Int cell = WorldToCell(worldPos);
        return CellToWorld(cell);
    }

    public bool IsFree(Vector2Int orgin, int width=1, int height=1) // Checks if a rectangular area of cells is free
    {
        for (int x = orgin.x; x < orgin.x + width; x++)
        {
            for (int y = orgin.y; y < orgin.y + height; y++)
            {
                if (!IsInBounds(x, y) || occupiedCells[x, y])
                    return false;
            }
        }
        return true;
    }

    public void SetOccupied(Vector2Int orgin, int width=1, int height=1) // Marks a rectangular area of cells as occupied
    {
        for (int x = orgin.x; x < orgin.x + width; x++)
        {
            for (int y = orgin.y; y < orgin.y + height; y++)
            {
                if (IsInBounds(x, y))
                    occupiedCells[x, y] = true;
            }
        }
    }

    public void SetFree(Vector2Int orign, int width = 1, int height = 1) // Clears a rectangular area of cells (buildings destroyed/moved)
    {
        for (int x = orign.x; x < orign.x + width; x++)
        {
            for (int y = orign.y; y < orign.y + height; y++)
            {
                if(IsInBounds(x, y))
                    occupiedCells[x, y] = false;
            }
        }
    }

    
    private bool IsInBounds(int x, int y) // is cell within grid bounds
    {
        return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
    }

}
