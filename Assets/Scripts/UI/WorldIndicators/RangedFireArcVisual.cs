using System.Collections.Generic;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// RangedFireArcVisual
/// -----------------------------------------------------------------------------
///
/// World-space tactical indicator for a ranged squad.
///
/// The visual is a truncated forward sector:
/// - starts across the squad's current formation frontage
/// - extends forward to the live ranged attack range
/// - follows the formation's current facing
/// - uses a procedural mesh for the translucent fill
/// ~ REMOVED: uses a LineRenderer for the border
///
/// This is presentation only. It does not restrict targeting or ranged combat.
///
/// Setup:
/// - Attach to the Squad prefab root.
/// - Assign a transparent URP/Unlit fill material.
/// ~ REMOVED: Assign a transparent URP/Unlit border material.
/// - The component creates and owns its MeshFilter, MeshRenderer, and LineRenderer.
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class RangedFireArcVisual : MonoBehaviour
{
    [Header("Squad Controller GameObject")]
    [SerializeField] private SquadController squadController;
    
    #region Fire Arc Visual Tuning

    [Header("Visibility")]
    [Tooltip("Shows the firing arc whenever this squad is selected.")]
    [SerializeField] private bool rangedVisualShowWhenSelected = true;

    [Tooltip("Optional debug override that keeps the arc visible even when the squad is not selected.")]
    [SerializeField] private bool rangedVisualAlwaysShow = false;

    [Header("Shape")]
    [Tooltip("Total horizontal angle of the visual firing arc.")]
    [Range(10f, 180f)]
    [SerializeField] private float rangedVisualArcAngle = 90f;

    [Tooltip("Number of subdivisions used along the curved outer edge.")]
    [Range(4, 128)]
    [SerializeField] private int rangedVisualArcSegments = 32;

    [Tooltip("Extra width added to both sides of the current formation frontage.")]
    [Min(0f)]
    [SerializeField] private float rangedVisualFrontagePadding = 0.5f;

    [Tooltip("Moves the beginning of the visual slightly in front of the formation's front row.")]
    [SerializeField] private float rangedVisualFrontOffset = 0.15f;

    [Tooltip("Keeps the visual slightly above the ground to reduce z-fighting.")]
    [Min(0f)]
    [SerializeField] private float rangedVisualHeightOffset = 0.1f;

    [Header("Refresh")]
    [Tooltip("Minimum movement before the generated visual is refreshed.")]
    [Min(0f)]
    [SerializeField] private float rangedVisualPositionRefreshDistance = 0.05f;

    [Tooltip("Minimum facing change in degrees before the generated visual is refreshed.")]
    [Min(0f)]
    [SerializeField] private float rangedVisualFacingRefreshAngle = 0.5f;

    [Tooltip("Maximum time between refreshes while visible. This also catches formation-width and upgrade changes.")]
    [Min(0.02f)]
    [SerializeField] private float rangedVisualMaximumRefreshInterval = 0.2f;

    [Header("Materials")]
    [SerializeField] private Material rangedVisualFillMaterial;

    [Header("Color")]
    [Tooltip("Uses the owning faction's team color when possible.")]
    [SerializeField] private bool rangedVisualUseFactionColor = true;

    [SerializeField] private Color rangedVisualFallbackColor =
        new Color(0.2f, 0.75f, 1f, 1f);

    [Range(0f, 1f)]
    [SerializeField] private float rangedVisualFillAlpha = 0.08f;
    
    #endregion

    #region Runtime

    private SquadController squad;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh fireArcMesh;

    private readonly List<Vector3> meshVertices = new List<Vector3>();
    private readonly List<int> meshTriangles = new List<int>();

    private MaterialPropertyBlock fillPropertyBlock;

    private Vector3 lastPosition;
    private Vector3 lastFacing = Vector3.forward;
    private float lastRange = -1f;
    private float lastFrontage = -1f;
    private float refreshTimer;
    private bool wasVisible;

    private static readonly int baseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int colorProperty = Shader.PropertyToID("_Color");

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        // squad = GetComponent<SquadController>();
        if (squad == null)
            squad = GetComponentInParent<SquadController>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        fireArcMesh = new Mesh
        {
            name = $"{name}_RangedFireArcMesh"
        };
        fireArcMesh.MarkDynamic();
        meshFilter.sharedMesh = fireArcMesh;

        fillPropertyBlock = new MaterialPropertyBlock();

        ConfigureRenderers();
        SetVisible(false);
    }

    void OnEnable()
    {
        refreshTimer = 0f;
        wasVisible = false;
    }

    void LateUpdate()
    {
        bool shouldShow = ShouldShow();

        if (!shouldShow)
        {
            if (wasVisible)
                SetVisible(false);

            wasVisible = false;
            return;
        }

        if (!TryResolveVisualValues(
                out float attackRange,
                out float formationFrontage,
                out Vector3 formationFacing))
        {
            if (wasVisible)
                SetVisible(false);

            wasVisible = false;
            return;
        }

        refreshTimer -= Time.deltaTime;

        bool shouldRefresh =
            !wasVisible ||
            refreshTimer <= 0f ||
            Vector3.Distance(lastPosition, transform.position) >=
                rangedVisualPositionRefreshDistance ||
            Vector3.Angle(lastFacing, formationFacing) >=
                rangedVisualFacingRefreshAngle ||
            !Mathf.Approximately(lastRange, attackRange) ||
            !Mathf.Approximately(lastFrontage, formationFrontage);

        if (shouldRefresh)
        {
            RebuildVisual(
                attackRange,
                formationFrontage,
                formationFacing);

            lastPosition = transform.position;
            lastFacing = formationFacing;
            lastRange = attackRange;
            lastFrontage = formationFrontage;
            refreshTimer = rangedVisualMaximumRefreshInterval;
        }

        if (!wasVisible)
            SetVisible(true);

        wasVisible = true;
    }

    void OnDestroy()
    {
        if (fireArcMesh != null)
            Destroy(fireArcMesh);
    }

    void OnValidate()
    {
        rangedVisualArcSegments = Mathf.Clamp(
            rangedVisualArcSegments,
            4,
            128);

        rangedVisualArcAngle = Mathf.Clamp(
            rangedVisualArcAngle,
            10f,
            180f);

        rangedVisualFrontagePadding = Mathf.Max(
            0f,
            rangedVisualFrontagePadding);

        rangedVisualHeightOffset = Mathf.Max(
            0f,
            rangedVisualHeightOffset);

        rangedVisualMaximumRefreshInterval = Mathf.Max(
            0.02f,
            rangedVisualMaximumRefreshInterval);

        if (Application.isPlaying)
            ConfigureRenderers();
    }

    #endregion

    #region Setup

    void ConfigureRenderers()
    {
        if (meshRenderer != null)
        {
            meshRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            if (rangedVisualFillMaterial != null)
                meshRenderer.sharedMaterial = rangedVisualFillMaterial;
        }
        
    }

    #endregion

    #region Visibility

    bool ShouldShow()
    {
        if (rangedVisualAlwaysShow)
            return true;

        return rangedVisualShowWhenSelected &&
               squad != null &&
               squad.IsSelected;
    }

    void SetVisible(bool visible)
    {
        if (meshRenderer != null)
            meshRenderer.enabled = visible;
    }

    #endregion

    #region Value Resolution

    bool TryResolveVisualValues(
        out float attackRange,
        out float formationFrontage,
        out Vector3 formationFacing)
    {
        attackRange = 0f;
        formationFrontage = 0f;
        formationFacing = transform.forward;

        if (squad == null ||
            !squad.IsInitialized ||
            squad.Roster == null ||
            squad.Formation == null)
        {
            return false;
        }

        if (!TryGetLivingRangedSoldier(out SoldierController rangedSoldier))
            return false;

        attackRange = rangedSoldier.Stats != null
            ? Mathf.Max(0.1f, rangedSoldier.Stats.ranged.attackRange)
            : rangedSoldier.RangedWeaponProfile != null
                ? Mathf.Max(
                    0.1f,
                    rangedSoldier.RangedWeaponProfile.ranged.attackRange)
                : 0f;

        if (attackRange <= 0f)
            return false;

        FormationBounds formationBounds =
            squad.Formation.GetCurrentFormationBounds();

        formationFrontage = Mathf.Max(
            squad.Formation.Spacing,
            formationBounds.width + rangedVisualFrontagePadding * 2f);

        formationFacing = squad.Formation.Facing;
        formationFacing.y = 0f;

        if (formationFacing.sqrMagnitude <= 0.0001f)
            formationFacing = transform.forward;

        formationFacing.y = 0f;

        if (formationFacing.sqrMagnitude <= 0.0001f)
            formationFacing = Vector3.forward;

        formationFacing.Normalize();
        return true;
    }

    bool TryGetLivingRangedSoldier(
        out SoldierController rangedSoldier)
    {
        rangedSoldier = null;

        IReadOnlyList<SoldierController> soldiers = squad.Roster.Soldiers;

        if (soldiers == null)
            return false;

        for (int index = 0; index < soldiers.Count; index++)
        {
            SoldierController candidate = soldiers[index];

            if (candidate == null || !candidate.IsAlive)
                continue;

            if (!candidate.HasRangedWeapon)
                continue;

            rangedSoldier = candidate;
            return true;
        }

        return false;
    }

    #endregion

    #region Mesh Generation

    void RebuildVisual(
        float attackRange,
        float formationFrontage,
        Vector3 formationFacing)
    {
        if (fireArcMesh == null)
            return;

        FormationBounds formationBounds =
            squad.Formation.GetCurrentFormationBounds();

        float frontDistance =
            formationBounds.depth * 0.5f + rangedVisualFrontOffset;

        Vector3 localForward = transform.InverseTransformDirection(
            formationFacing);
        localForward.y = 0f;

        if (localForward.sqrMagnitude <= 0.0001f)
            localForward = Vector3.forward;

        localForward.Normalize();

        Vector3 localRight = Vector3.Cross(
            Vector3.up,
            localForward).normalized;

        Vector3 frontCenter =
            localForward * frontDistance +
            Vector3.up * rangedVisualHeightOffset;

        float halfFrontage = formationFrontage * 0.5f;

        Vector3 frontLeft =
            frontCenter - localRight * halfFrontage;

        Vector3 frontRight =
            frontCenter + localRight * halfFrontage;

        meshVertices.Clear();
        meshTriangles.Clear();

        // Vertex 0 is the fan center used only for triangulation.
        meshVertices.Add(frontCenter);

        // Boundary begins at the left side of the formation frontage.
        meshVertices.Add(frontLeft);

        float halfAngle = rangedVisualArcAngle * 0.5f;

        for (int segmentIndex = 0;
             segmentIndex <= rangedVisualArcSegments;
             segmentIndex++)
        {
            float normalizedSegment =
                (float)segmentIndex / rangedVisualArcSegments;

            float angle = Mathf.Lerp(
                -halfAngle,
                halfAngle,
                normalizedSegment);

            Vector3 arcDirection =
                Quaternion.AngleAxis(angle, Vector3.up) *
                localForward;

            Vector3 arcPoint =
                frontCenter +
                arcDirection * attackRange;

            meshVertices.Add(arcPoint);
        }

        meshVertices.Add(frontRight);

        int boundaryVertexCount = meshVertices.Count - 1;

        for (int boundaryIndex = 1;
             boundaryIndex < boundaryVertexCount;
             boundaryIndex++)
        {
            meshTriangles.Add(0);
            meshTriangles.Add(boundaryIndex);
            meshTriangles.Add(boundaryIndex + 1);
        }

        // Close the final triangle from the right frontage back to the fan center.
        meshTriangles.Add(0);
        meshTriangles.Add(boundaryVertexCount);
        meshTriangles.Add(1);

        fireArcMesh.Clear();
        fireArcMesh.SetVertices(meshVertices);
        fireArcMesh.SetTriangles(meshTriangles, 0);
        fireArcMesh.RecalculateBounds();
        fireArcMesh.RecalculateNormals();

        ApplyVisualColor();
    }

    #endregion

    #region Color

    void ApplyVisualColor()
    {
        Color resolvedColor = rangedVisualFallbackColor;

        if (rangedVisualUseFactionColor &&
            squad != null &&
            squad.Faction != null)
        {
            resolvedColor = squad.Faction.Visuals.teamColor;
        }

        Color fillColor = resolvedColor;
        fillColor.a = rangedVisualFillAlpha;

        ApplyRendererColor(
            meshRenderer,
            fillPropertyBlock,
            fillColor);
    }

    void ApplyRendererColor(
        Renderer targetRenderer,
        MaterialPropertyBlock propertyBlock,
        Color color)
    {
        if (targetRenderer == null ||
            targetRenderer.sharedMaterial == null)
        {
            return;
        }

        targetRenderer.GetPropertyBlock(propertyBlock);

        Material material = targetRenderer.sharedMaterial;

        if (material.HasProperty(baseColorProperty))
            propertyBlock.SetColor(baseColorProperty, color);

        if (material.HasProperty(colorProperty))
            propertyBlock.SetColor(colorProperty, color);

        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    #endregion
}
