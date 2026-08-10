using UnityEngine;

public class GridVisualizerUI : MonoBehaviour
{
    [SerializeField] private bool showGrid = true;
    [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.2f);
    private Material lineMaterial;

    void Awake()
    {
        // Create a simple unlit material for GL drawing
        lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

    void OnRenderObject()
    {
        if (!showGrid) return;

        int gridWidth = GridManager.Instance.GetGridWidth();
        int gridHeight = GridManager.Instance.GetGridHeight();
        float cellSize = GridManager.Instance.GetCellSize();

        lineMaterial.SetPass(0);
        GL.Begin(GL.LINES);
        GL.Color(gridColor);

        // Draw vertical lines
        for (int x = 0; x <= gridWidth; x++)
        {
            GL.Vertex3(x * cellSize, 0.01f, 0);
            GL.Vertex3(x * cellSize, 0.01f, gridHeight * cellSize);
        }

        // Draw horizontal lines
        for (int z = 0; z <= gridHeight; z++)
        {
            GL.Vertex3(0, 0.01f, z * cellSize);
            GL.Vertex3(gridWidth * cellSize, 0.01f, z * cellSize);
        }

        GL.End();
    }
}