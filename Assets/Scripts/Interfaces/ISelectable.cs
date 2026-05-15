using UnityEngine;

public interface ISelectable
{
    void OnSelect();
    void OnDeselect();
    GameObject GetGameObject(); // needed so SelectionManager can work with the actual GO
    bool IsDragSelectable { get; } // units return true, buildings/resources should return false
}
