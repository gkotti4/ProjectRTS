using UnityEngine;

/// <summary>
/// Scene-level description of a Battle Mode battlefield.
///
/// The Terrain/NavMesh remain authored scene content. BattleMap does not generate
/// terrain; it reads the Terrain's world size and exposes one shared battlefield
/// coordinate system for camera limits, minimap mapping, deployment, and later AI.
/// </summary>
[DisallowMultipleComponent]
public class BattleMap : MonoBehaviour
{
    public static BattleMap Instance { get; private set; }

    #region Setup

    [Header("Terrain Source")]
    [Tooltip("Terrain that defines the battlefield footprint. If empty, BattleMap tries Terrain.activeTerrain.")]
    [SerializeField] private Terrain battleTerrain;

    [Header("Playable Bounds")]
    [Tooltip("Optional margin removed from every side of the Terrain when defining the playable battlefield.")]
    [Min(0f)]
    [SerializeField] private float mapEdgeInset = 0f;

    [Header("Deployment")]
    [Tooltip("Depth of each army deployment zone measured inward from its battlefield edge.")]
    [Min(0.1f)]
    [SerializeField] private float deploymentZoneDepth = 35f;

    [Tooltip("Extra gap kept between deployment zones and the battlefield center line.")]
    [Min(0f)]
    [SerializeField] private float deploymentCenterGap = 10f;

    [Header("Debug")]
    [SerializeField] private bool mapDrawBoundsGizmos = true;

    #endregion

    #region Runtime / Public API

    private Bounds worldBounds;
    private Bounds playerDeploymentBounds;
    private Bounds enemyDeploymentBounds;

    public Terrain BattleTerrain => battleTerrain;
    public Bounds WorldBounds => worldBounds;
    public Bounds PlayerDeploymentBounds => playerDeploymentBounds;
    public Bounds EnemyDeploymentBounds => enemyDeploymentBounds;

    public float WorldWidth => worldBounds.size.x;
    public float WorldDepth => worldBounds.size.z;
    public Vector3 WorldCenter => worldBounds.center;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveTerrain();
        RebuildBounds();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnValidate()
    {
        mapEdgeInset = Mathf.Max(0f, mapEdgeInset);
        deploymentZoneDepth = Mathf.Max(0.1f, deploymentZoneDepth);
        deploymentCenterGap = Mathf.Max(0f, deploymentCenterGap);

        ResolveTerrain();
        RebuildBounds();
    }

    #endregion

    #region Bounds

    public void RebuildBounds()
    {
        if (battleTerrain == null || battleTerrain.terrainData == null)
        {
            worldBounds = new Bounds(transform.position, Vector3.zero);
            playerDeploymentBounds = worldBounds;
            enemyDeploymentBounds = worldBounds;
            return;
        }

        Vector3 terrainSize = battleTerrain.terrainData.size;
        Vector3 terrainMin = battleTerrain.transform.position;
        Vector3 terrainCenter = terrainMin + terrainSize * 0.5f;

        float resolvedInset = Mathf.Min(
            mapEdgeInset,
            Mathf.Max(0f, Mathf.Min(terrainSize.x, terrainSize.z) * 0.5f - 0.1f));

        Vector3 playableSize = new Vector3(
            Mathf.Max(0.1f, terrainSize.x - resolvedInset * 2f),
            Mathf.Max(0.1f, terrainSize.y),
            Mathf.Max(0.1f, terrainSize.z - resolvedInset * 2f));

        worldBounds = new Bounds(terrainCenter, playableSize);
        BuildDeploymentBounds();
    }

    void BuildDeploymentBounds()
    {
        float halfDepth = worldBounds.extents.z;
        float centerGapHalf = Mathf.Min(
            deploymentCenterGap * 0.5f,
            Mathf.Max(0f, halfDepth - 0.1f));

        float maximumDeploymentDepth = Mathf.Max(
            0.1f,
            halfDepth - centerGapHalf);

        float resolvedDeploymentDepth = Mathf.Min(
            deploymentZoneDepth,
            maximumDeploymentDepth);

        float playerMinZ = worldBounds.min.z;
        float playerMaxZ = Mathf.Min(
            playerMinZ + resolvedDeploymentDepth,
            worldBounds.center.z - centerGapHalf);

        float enemyMaxZ = worldBounds.max.z;
        float enemyMinZ = Mathf.Max(
            enemyMaxZ - resolvedDeploymentDepth,
            worldBounds.center.z + centerGapHalf);

        playerDeploymentBounds = BuildFlatBounds(
            worldBounds.min.x,
            worldBounds.max.x,
            playerMinZ,
            playerMaxZ);

        enemyDeploymentBounds = BuildFlatBounds(
            worldBounds.min.x,
            worldBounds.max.x,
            enemyMinZ,
            enemyMaxZ);
    }

    Bounds BuildFlatBounds(
        float minX,
        float maxX,
        float minZ,
        float maxZ)
    {
        Vector3 center = new Vector3(
            (minX + maxX) * 0.5f,
            worldBounds.center.y,
            (minZ + maxZ) * 0.5f);

        Vector3 size = new Vector3(
            Mathf.Max(0f, maxX - minX),
            worldBounds.size.y,
            Mathf.Max(0f, maxZ - minZ));

        return new Bounds(center, size);
    }

    #endregion

    #region Public Mapping / Clamp API

    /// <summary>
    /// Maps a world X/Z position into normalized battlefield coordinates.
    /// (0,0) = south-west / player-left corner, (1,1) = north-east corner.
    /// This is the API the tactical minimap will use.
    /// </summary>
    public Vector2 WorldToNormalizedPosition(Vector3 worldPosition)
    {
        if (worldBounds.size.x <= 0.0001f || worldBounds.size.z <= 0.0001f)
            return new Vector2(0.5f, 0.5f);

        float normalizedX = Mathf.InverseLerp(
            worldBounds.min.x,
            worldBounds.max.x,
            worldPosition.x);

        float normalizedZ = Mathf.InverseLerp(
            worldBounds.min.z,
            worldBounds.max.z,
            worldPosition.z);

        return new Vector2(normalizedX, normalizedZ);
    }

    public Vector3 NormalizedToWorldPosition(Vector2 normalizedPosition, float worldY = 0f)
    {
        normalizedPosition.x = Mathf.Clamp01(normalizedPosition.x);
        normalizedPosition.y = Mathf.Clamp01(normalizedPosition.y);

        return new Vector3(
            Mathf.Lerp(worldBounds.min.x, worldBounds.max.x, normalizedPosition.x),
            worldY,
            Mathf.Lerp(worldBounds.min.z, worldBounds.max.z, normalizedPosition.y));
    }

    public Vector3 ClampWorldPosition(Vector3 worldPosition, float edgePadding = 0f)
    {
        edgePadding = Mathf.Max(0f, edgePadding);

        float minX = worldBounds.min.x + edgePadding;
        float maxX = worldBounds.max.x - edgePadding;
        float minZ = worldBounds.min.z + edgePadding;
        float maxZ = worldBounds.max.z - edgePadding;

        if (minX > maxX)
            minX = maxX = worldBounds.center.x;

        if (minZ > maxZ)
            minZ = maxZ = worldBounds.center.z;

        worldPosition.x = Mathf.Clamp(worldPosition.x, minX, maxX);
        worldPosition.z = Mathf.Clamp(worldPosition.z, minZ, maxZ);
        return worldPosition;
    }

    public bool ContainsWorldPosition(Vector3 worldPosition)
    {
        return worldPosition.x >= worldBounds.min.x &&
               worldPosition.x <= worldBounds.max.x &&
               worldPosition.z >= worldBounds.min.z &&
               worldPosition.z <= worldBounds.max.z;
    }

    #endregion

    #region Helpers / Gizmos

    void ResolveTerrain()
    {
        if (battleTerrain == null)
            battleTerrain = Terrain.activeTerrain;
    }

    void OnDrawGizmosSelected()
    {
        if (!mapDrawBoundsGizmos)
            return;

        ResolveTerrain();
        RebuildBounds();

        DrawBounds(worldBounds, Color.white);
        DrawBounds(playerDeploymentBounds, Color.cyan);
        DrawBounds(enemyDeploymentBounds, Color.red);
    }

    void DrawBounds(Bounds bounds, Color color)
    {
        if (bounds.size == Vector3.zero)
            return;

        Gizmos.color = color;

        Vector3 center = bounds.center;
        center.y = battleTerrain != null
            ? battleTerrain.transform.position.y + 0.25f
            : transform.position.y;

        Gizmos.DrawWireCube(
            center,
            new Vector3(bounds.size.x, 0.05f, bounds.size.z));
    }

    #endregion
}
