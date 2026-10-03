using UnityEngine;

/// -----------------------------------------------------------------------------
/// SoldierImpactPresentation
/// -----------------------------------------------------------------------------
///
/// Visual-only launch / knockdown presentation for a living soldier.
///
/// Gameplay authority stays on the grounded SoldierController/SoldierMotor root.
/// This component offsets and tilts only an authored visual child while the real
/// body continues to receive normal NavMesh-safe horizontal impulses.
///
/// Setup rule:
/// impactVisualRoot should contain presentation objects only (model/animators/
/// equipment visuals). Do not put gameplay colliders, SelectionTarget, or the
/// SoldierController root under it.
///
/// Design role:
/// Sell heavy artillery / explosion impacts without handing a living soldier to
/// Rigidbody physics or requiring ragdoll recovery / NavMesh reattachment.
/// -----------------------------------------------------------------------------
[DisallowMultipleComponent]
[RequireComponent(typeof(SoldierController))]
public class SoldierImpactPresentation : MonoBehaviour
{
    #region References

    [Header("References")]
    [Tooltip("Visual-only child root that may be lifted/tilted independently of the grounded gameplay root. Create an ImpactVisualRoot parent if the prefab does not already have a shared visual root.")]
    [SerializeField] private Transform impactVisualRoot;

    private SoldierController soldierController;

    #endregion

    #region Runtime Launch State

    private Vector3 impactVisualBaseLocalPosition;
    private Quaternion impactVisualBaseLocalRotation = Quaternion.identity;
    private bool impactVisualBaseCaptured = false;

    private bool impactLaunchActive = false;
    private float impactLaunchElapsed = 0f;
    private float impactLaunchDuration = 0f;
    private float impactLaunchHeight = 0f;
    private float impactLaunchRotationDegrees = 0f;
    private Vector3 impactLaunchDirectionWorld = Vector3.forward;

    public bool IsLaunchActive => impactLaunchActive;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        soldierController = GetComponent<SoldierController>();
        CaptureVisualBaseTransform();
    }

    void Update()
    {
        TickLaunchPresentation();
    }

    void OnDisable()
    {
        ResetVisualTransform();
        impactLaunchActive = false;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Plays a temporary visual launch while the gameplay root remains grounded.
    /// worldDirection should point in the horizontal direction the impact pushes.
    /// </summary>
    public bool PlayLaunch(
        Vector3 worldDirection,
        float visualHeight,
        float duration,
        float rotationDegrees)
    {
        if (impactVisualRoot == null ||
            soldierController == null ||
            !soldierController.IsAlive)
        {
            return false;
        }

        visualHeight = Mathf.Max(0f, visualHeight);
        duration = Mathf.Max(0.05f, duration);
        rotationDegrees = Mathf.Max(0f, rotationDegrees);

        if (visualHeight <= 0.001f && rotationDegrees <= 0.001f)
            return false;

        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude <= 0.0001f)
            worldDirection = transform.forward;

        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude <= 0.0001f)
            worldDirection = Vector3.forward;

        worldDirection.Normalize();

        // Preserve normalized progress when a stronger launch arrives during an
        // existing reaction. This avoids snapping the model back to the ground.
        float previousProgress = impactLaunchActive && impactLaunchDuration > 0f
            ? Mathf.Clamp01(impactLaunchElapsed / impactLaunchDuration)
            : 0f;

        impactLaunchActive = true;
        impactLaunchDuration = duration;
        impactLaunchElapsed = previousProgress * impactLaunchDuration;
        impactLaunchHeight = Mathf.Max(impactLaunchHeight, visualHeight);
        impactLaunchRotationDegrees = Mathf.Max(
            impactLaunchRotationDegrees,
            rotationDegrees);
        impactLaunchDirectionWorld = worldDirection;

        soldierController.ApplyImpactMovementLock(duration);
        return true;
    }

    [ContextMenu("Debug Play Launch")]
    void DebugPlayLaunch()
    {
        if (!Application.isPlaying)
            return;

        PlayLaunch(
            transform.forward,
            visualHeight: 1.25f,
            duration: 0.65f,
            rotationDegrees: 55f);
    }

    #endregion

    #region Launch Presentation

    void TickLaunchPresentation()
    {
        if (!impactLaunchActive || impactVisualRoot == null)
            return;

        if (soldierController == null || !soldierController.IsAlive)
        {
            EndLaunchPresentation();
            return;
        }

        impactLaunchElapsed += Time.deltaTime;

        float normalizedTime = Mathf.Clamp01(
            impactLaunchElapsed / Mathf.Max(0.05f, impactLaunchDuration));

        // Symmetric parabola: 0 at launch/landing, 1 at midpoint.
        float arc = 4f * normalizedTime * (1f - normalizedTime);

        Vector3 worldVerticalOffset =
            Vector3.up * (impactLaunchHeight * arc);

        Vector3 localVerticalOffset = impactVisualRoot.parent != null
            ? impactVisualRoot.parent.InverseTransformVector(worldVerticalOffset)
            : worldVerticalOffset;

        impactVisualRoot.localPosition =
            impactVisualBaseLocalPosition + localVerticalOffset;

        Vector3 worldTiltAxis = Vector3.Cross(
            impactLaunchDirectionWorld,
            Vector3.up);

        if (worldTiltAxis.sqrMagnitude <= 0.0001f)
            worldTiltAxis = Vector3.right;

        worldTiltAxis.Normalize();

        Vector3 localTiltAxis = impactVisualRoot.parent != null
            ? impactVisualRoot.parent.InverseTransformDirection(worldTiltAxis).normalized
            : worldTiltAxis;

        impactVisualRoot.localRotation =
            Quaternion.AngleAxis(
                impactLaunchRotationDegrees * arc,
                localTiltAxis) *
            impactVisualBaseLocalRotation;

        if (normalizedTime >= 1f)
            EndLaunchPresentation();
    }

    void EndLaunchPresentation()
    {
        ResetVisualTransform();

        impactLaunchActive = false;
        impactLaunchElapsed = 0f;
        impactLaunchDuration = 0f;
        impactLaunchHeight = 0f;
        impactLaunchRotationDegrees = 0f;
    }

    void CaptureVisualBaseTransform()
    {
        if (impactVisualRoot == null)
            return;

        impactVisualBaseLocalPosition = impactVisualRoot.localPosition;
        impactVisualBaseLocalRotation = impactVisualRoot.localRotation;
        impactVisualBaseCaptured = true;
    }

    void ResetVisualTransform()
    {
        if (impactVisualRoot == null || !impactVisualBaseCaptured)
            return;

        impactVisualRoot.localPosition = impactVisualBaseLocalPosition;
        impactVisualRoot.localRotation = impactVisualBaseLocalRotation;
    }

    #endregion
}
