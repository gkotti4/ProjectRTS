using UnityEngine;

public class GameLayers : MonoBehaviour
{
    public static GameLayers Instance { get; private set; }

    [Header("World")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("Interaction")]
    [SerializeField] private LayerMask selectableLayers;
    [SerializeField] private LayerMask targetableLayers;

    public LayerMask GroundLayers => groundLayers;
    public LayerMask ObstacleLayers => obstacleLayers;
    public LayerMask SelectableLayers => selectableLayers;
    public LayerMask TargetableLayers => targetableLayers;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
}
