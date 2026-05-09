using UnityEngine;

[CreateAssetMenu(fileName = "ResourceNodeSO", menuName = "Scriptable Objects/ResourceNodeSO")]
public class ResourceNodeSO : ScriptableObject
{
    // Resource Identifiers
    public string resourceName = "Resource";
    public int resourceID = 0;
    public ResourceType resourceType = ResourceType.Wood;
    
    // Resource Specs
    public int totalResources = 100;

}
