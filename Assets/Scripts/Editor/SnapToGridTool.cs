#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class GridSnapTool : EditorWindow
{
    [MenuItem("Tools/Snap Selection To Grid")]
    static void SnapToGrid()
    {
        float cellSize = 2f; // match your GridManager

        foreach (GameObject obj in Selection.gameObjects)
        {
            Vector3 pos = obj.transform.position;
            pos.x = Mathf.Round(pos.x / cellSize) * cellSize;
            pos.z = Mathf.Round(pos.z / cellSize) * cellSize;
            obj.transform.position = pos;
            
            Undo.RecordObject(obj.transform, "Snap To Grid");
        }
    }
}
#endif