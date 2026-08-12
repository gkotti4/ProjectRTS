using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Circular battlefield escape area used by routing squads.
/// The center normally sits on the army's starting battlefield edge, so only the
/// inward half of the circle overlaps the playable map.
/// </summary>
public struct BattleRoutZone
{
    public Vector3 center;
    public float radius;

    public BattleRoutZone(Vector3 center, float radius)
    {
        this.center = center;
        this.radius = Mathf.Max(0.1f, radius);
    }

    public bool ContainsFlat(Vector3 worldPosition)
    {
        Vector3 flatCenter = center;
        Vector3 flatPosition = worldPosition;
        flatCenter.y = 0f;
        flatPosition.y = 0f;

        float resolvedRadius = Mathf.Max(0.1f, radius);
        return (flatPosition - flatCenter).sqrMagnitude <=
               resolvedRadius * resolvedRadius;
    }
}

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
    
    [Tooltip("Distance between squad centers within one deployment row.")]
    [Min(0.1f)]
    [SerializeField] private float deploymentSquadSpacing = 12f;

    [Tooltip("Maximum squads placed in one deployment row before starting another row.")]
    [Min(1)]
    [SerializeField] private int deploymentSquadsPerRow = 4;

    [Tooltip("Distance between deployment rows.")]
    [Min(0.1f)]
    [SerializeField] private float deploymentRowSpacing = 10f;

    [Tooltip("Extra gap kept between deployment zones and the battlefield center line.")]
    [Min(0f)]
    [SerializeField] private float deploymentCenterGap = 10f;

    [Header("Routing")]
    [Tooltip("Number of evenly spaced Rout Zones generated along each army's starting battlefield edge.")]
    [Min(1)]
    [SerializeField] private int routingZoneCount = 3;

    [Tooltip("Keeps generated Rout Zone centers this far inside the left/right battlefield corners.")]
    [Min(0f)]
    [SerializeField] private float routingZoneSideInset = 8f;

    [Tooltip("Radius of each circular Rout Zone. Zone centers sit directly on the battlefield edge, creating a usable half-circle inside the map.")]
    [Min(0.1f)]
    [SerializeField] private float routingZoneRadius = 8f;

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
        
        deploymentSquadSpacing = Mathf.Max(
            0.1f,
            deploymentSquadSpacing);

        deploymentSquadsPerRow = Mathf.Max(
            1,
            deploymentSquadsPerRow);

        deploymentRowSpacing = Mathf.Max(
            0.1f,
            deploymentRowSpacing);

        routingZoneCount = Mathf.Max(1, routingZoneCount);
        routingZoneSideInset = Mathf.Max(0f, routingZoneSideInset);
        routingZoneRadius = Mathf.Max(0.1f, routingZoneRadius);

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

    #region Deployment API

    public Vector3 GetDeploymentPosition(
        bool playerSide,
        int squadIndex,
        int totalSquads)
    {
        Bounds deploymentBounds = playerSide
            ? playerDeploymentBounds
            : enemyDeploymentBounds;

        Vector3 facing = playerSide
            ? Vector3.forward
            : Vector3.back;

        Vector3 right = Vector3.Cross(
            Vector3.up,
            facing).normalized;

        int resolvedPerRow = Mathf.Max(
            1,
            deploymentSquadsPerRow);

        int row = squadIndex / resolvedPerRow;
        int column = squadIndex % resolvedPerRow;

        int squadsInRow = Mathf.Min(
            resolvedPerRow,
            totalSquads - row * resolvedPerRow);

        float rowWidth =
            Mathf.Max(0, squadsInRow - 1) *
            deploymentSquadSpacing;

        float lateralOffset =
            column * deploymentSquadSpacing -
            rowWidth * 0.5f;

        // Start near the battlefield-facing edge of the deployment zone
        // and place additional rows deeper into that army's side.
        float frontEdgeZ = playerSide
            ? deploymentBounds.max.z
            : deploymentBounds.min.z;

        float rowDirection = playerSide
            ? -1f
            : 1f;

        Vector3 position = new Vector3(
            deploymentBounds.center.x,
            worldBounds.center.y,
            frontEdgeZ);

        position += right * lateralOffset;
        position += Vector3.forward *
                    rowDirection *
                    row *
                    deploymentRowSpacing;

        position.x = Mathf.Clamp(
            position.x,
            deploymentBounds.min.x,
            deploymentBounds.max.x);

        position.z = Mathf.Clamp(
            position.z,
            deploymentBounds.min.z,
            deploymentBounds.max.z);

        if (battleTerrain != null)
        {
            position.y = battleTerrain.SampleHeight(position) +
                         battleTerrain.transform.position.y;
        }

        return position;
    }

    public Quaternion GetDeploymentRotation(bool playerSide)
    {
        Vector3 facing = playerSide
            ? Vector3.forward
            : Vector3.back;

        return Quaternion.LookRotation(
            facing,
            Vector3.up);
    }

    public Vector3 GetDeploymentCenter(bool playerSide)
    {
        Bounds deploymentBounds = playerSide
            ? playerDeploymentBounds
            : enemyDeploymentBounds;

        Vector3 center = deploymentBounds.center;

        if (battleTerrain != null)
        {
            center.y = battleTerrain.SampleHeight(center) +
                       battleTerrain.transform.position.y;
        }

        return center;
    }

    #endregion
    

    #region Routing API

    public BattleRoutZone GetBestRoutZone(bool playerSide, Vector3 squadPosition)
    {
        IReadOnlyList<BattleRoutZone> zones = GetRoutZones(playerSide);

        if (zones.Count == 0)
        {
            return new BattleRoutZone(
                GetDeploymentCenter(playerSide),
                routingZoneRadius);
        }

        BattleRoutZone bestZone = zones[0];
        float bestDistance = FlatSqrDistance(squadPosition, bestZone.center);

        for (int index = 1; index < zones.Count; index++)
        {
            float distance = FlatSqrDistance(
                squadPosition,
                zones[index].center);

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestZone = zones[index];
        }

        return bestZone;
    }

    public IReadOnlyList<BattleRoutZone> GetRoutZones(bool playerSide)
    {
        List<BattleRoutZone> zones = new List<BattleRoutZone>();

        int count = Mathf.Max(1, routingZoneCount);
        float maximumSideInset = Mathf.Max(0f, worldBounds.size.x * 0.5f - 0.1f);
        float sideInset = Mathf.Min(routingZoneSideInset, maximumSideInset);
        float minX = worldBounds.min.x + sideInset;
        float maxX = worldBounds.max.x - sideInset;

        // Zone centers deliberately sit on the starting battlefield edge. The
        // circular radius therefore creates an inward-facing half-zone on the map.
        float z = playerSide
            ? worldBounds.min.z
            : worldBounds.max.z;

        for (int index = 0; index < count; index++)
        {
            float t = count == 1 ? 0.5f : index / (float)(count - 1);
            Vector3 center = new Vector3(
                Mathf.Lerp(minX, maxX, t),
                worldBounds.center.y,
                z);

            if (battleTerrain != null)
            {
                center.y = battleTerrain.SampleHeight(center) +
                           battleTerrain.transform.position.y;
            }

            zones.Add(new BattleRoutZone(
                center,
                routingZoneRadius));
        }

        return zones;
    }

    float FlatSqrDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return (a - b).sqrMagnitude;
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
        DrawRoutZones(GetRoutZones(true), Color.cyan);
        DrawRoutZones(GetRoutZones(false), Color.red);
    }

    void DrawRoutZones(IReadOnlyList<BattleRoutZone> zones, Color color)
    {
        Gizmos.color = color;

        for (int index = 0; index < zones.Count; index++)
        {
            BattleRoutZone zone = zones[index];
            Gizmos.DrawWireSphere(
                zone.center + Vector3.up * 0.35f,
                zone.radius);
        }
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
