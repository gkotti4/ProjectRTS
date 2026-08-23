using UnityEngine;

[CreateAssetMenu(fileName = "ResourceNodeData", menuName = "Scriptable Objects/ResourceNode")]
public class ResourceNodeData : ScriptableObject
{
    // Resource Identifiers
    public string resourceName = "Resource";
    public int resourceID = 0;
    public ResourceType resourceType = ResourceType.Wood;
    
    // Resource Specs
    public int totalResources = 100;

}
