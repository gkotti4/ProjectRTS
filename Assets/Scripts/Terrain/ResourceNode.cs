using UnityEngine;

public class ResourceNode : MonoBehaviour, ISelectable
{
    public void OnSelect() { isSelected = true; }
    public void OnDeselect() { isSelected = false; }
    public GameObject GetGameObject() { return gameObject; }
    public bool IsBoxSelectable => false;
    
    
    public ResourceNodeData resourceNodeData;
    private int remainingResources;
    private bool isSelected;
    void Start()
    {
        remainingResources = resourceNodeData.totalResources;
        isSelected = false;
    }
    
    public bool HasResources()
    {
        return remainingResources > 0;
    }
    
    public int Harvest(int amount)
    {
        int harvested = Mathf.Min(amount, remainingResources);
        remainingResources -= harvested;

        if (remainingResources <= 0)
        {
            Deplete();
        }
        Debug.Log("Resource node harvested: " + harvested);
        return harvested;
    }
    
    void Deplete()
    {
        Debug.Log("Resource node depleted.");
        gameObject.SetActive(false);
    }
    
    
}
