using UnityEngine;

public class CameraController : MonoBehaviour
{
    #region Tuning

    [Header("Movement")]
    [SerializeField] private float panSpeed = 20f;
    [SerializeField] private float dragSpeed = 15f;

    [Header("Rotation")]
    [Tooltip("Degrees of camera rotation applied per pixel of middle-mouse drag.")]
    [Min(0.01f)]
    [SerializeField] private float rotationMouseSensitivity = 0.20f;

    [Tooltip("Lowest downward viewing angle allowed while rotating the camera.")]
    [Range(5f, 89f)]
    [SerializeField] private float rotationMinimumPitch = 20f;

    [Tooltip("Steepest downward viewing angle allowed while rotating the camera.")]
    [Range(5f, 89f)]
    [SerializeField] private float rotationMaximumPitch = 75f;

    [Tooltip("Invert vertical middle-mouse camera rotation.")]
    [SerializeField] private bool rotationInvertVertical = false;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 30f;
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 50f;

    [Header(" - Game Mode Dependent -")]
    [Header("Game Mode Controller")]
    [SerializeField] private BattleGameModeController battleController;
    
    [Header("Battle Map Bounds")]
    [SerializeField] private BattleMap battleMap;

    [Tooltip("Keeps the camera root this far inside the playable battlefield edge.")]
    [Min(0f)]
    [SerializeField] private float cameraBoundsPadding = 0f;

    [SerializeField] private bool cameraClampToBattleMap = true;

    [Header("Starting View")]
    [SerializeField] private Vector3 startPosition = new Vector3(30f, 10f, 20f);
    [SerializeField] private float startPitch = 40f;
    [SerializeField] private float startYaw = 0f;//-45f;
    
    #endregion

    #region Runtime

    private Camera cam;
    private Vector3 lastMousePosition;

    private float currentPitch;
    private float currentYaw;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError("No camera found!");
            return;
        }
        
        if (battleController == null)
            battleController = BattleGameModeController.Instance;
        
        startPosition = battleController.PlayerStartPosition + new Vector3(0f, 45f); // NEW - defeats purpose of start pos atm
        
        if (battleMap == null)
            battleMap = BattleMap.Instance;

        
        
        transform.position = startPosition;

        currentPitch = Mathf.Clamp(
            startPitch,
            rotationMinimumPitch,
            rotationMaximumPitch);

        currentYaw = startYaw;
        ApplyCameraRotation();

        ClampCameraToBattleMap();
    }

    void OnValidate()
    {
        panSpeed = Mathf.Max(0f, panSpeed);
        dragSpeed = Mathf.Max(0f, dragSpeed);

        rotationMouseSensitivity = Mathf.Max(0.01f, rotationMouseSensitivity);
        rotationMinimumPitch = Mathf.Clamp(rotationMinimumPitch, 5f, 89f);
        rotationMaximumPitch = Mathf.Clamp(rotationMaximumPitch, 5f, 89f);

        if (rotationMaximumPitch < rotationMinimumPitch)
            rotationMaximumPitch = rotationMinimumPitch;

        zoomSpeed = Mathf.Max(0f, zoomSpeed);
        minZoom = Mathf.Max(0.1f, minZoom);
        maxZoom = Mathf.Max(minZoom, maxZoom);

        startPitch = Mathf.Clamp(
            startPitch,
            rotationMinimumPitch,
            rotationMaximumPitch);
    }

    void Update()
    {
        HandlePan();
        HandleZoom();
        HandleMiddleMouseInput();

        // One authoritative clamp after all camera movement for this frame.
        ClampCameraToBattleMap();
    }

    #endregion

    #region Movement

    void HandlePan()
    {
        if (cam == null)
            return;

        // Arrow-key movement remains camera-relative as the view rotates.
        float horizontalInput = 0f;
        float forwardInput = 0f;

        if (Input.GetKey(KeyCode.RightArrow))
            horizontalInput = 1f;
        else if (Input.GetKey(KeyCode.LeftArrow))
            horizontalInput = -1f;

        if (Input.GetKey(KeyCode.UpArrow))
            forwardInput = 1f;
        else if (Input.GetKey(KeyCode.DownArrow))
            forwardInput = -1f;

        Vector3 forward = GetFlatCameraForward();
        Vector3 right = GetFlatCameraRight();

        Vector3 move =
            (forward * forwardInput + right * horizontalInput) *
            (panSpeed * Time.deltaTime);

        transform.position += move;
    }

    void HandleMiddleMouseInput()
    {
        if (cam == null)
            return;

        if (Input.GetMouseButtonDown(2))
            lastMousePosition = Input.mousePosition;

        if (!Input.GetMouseButton(2))
            return;

        Vector3 mouseDelta = Input.mousePosition - lastMousePosition;
        lastMousePosition = Input.mousePosition;

        bool rotateModifierHeld =
            Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);

        if (rotateModifierHeld)
        {
            HandleMiddleMouseRotation(mouseDelta);
            return;
        }
        
        HandleMiddleMousePan(mouseDelta);
    }

    void HandleMiddleMousePan(Vector3 mouseDelta)
    {
        Vector3 right = GetFlatCameraRight();
        Vector3 forward = GetFlatCameraForward();

        Vector3 move =
            (-right * mouseDelta.x + -forward * mouseDelta.y) *
            (dragSpeed * Time.deltaTime);

        transform.position += move;
    }

    Vector3 GetFlatCameraForward()
    {
        Vector3 forward = cam.transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            return Vector3.forward;

        return forward.normalized;
    }

    Vector3 GetFlatCameraRight()
    {
        Vector3 right = cam.transform.right;
        right.y = 0f;

        if (right.sqrMagnitude <= 0.0001f)
            return Vector3.right;

        return right.normalized;
    }

    #endregion

    #region Rotation

    void HandleMiddleMouseRotation(Vector3 mouseDelta)
    {
        currentYaw += mouseDelta.x * rotationMouseSensitivity;

        float verticalDirection = rotationInvertVertical ? 1f : -1f;

        currentPitch +=
            mouseDelta.y *
            rotationMouseSensitivity *
            verticalDirection;

        currentPitch = Mathf.Clamp(
            currentPitch,
            rotationMinimumPitch,
            rotationMaximumPitch);

        ApplyCameraRotation();
    }

    void ApplyCameraRotation()
    {
        if (cam == null)
            return;

        cam.transform.rotation = Quaternion.Euler(
            currentPitch,
            currentYaw,
            0f);
    }

    #endregion

    #region Zoom

    void HandleZoom()
    {
        if (cam == null)
            return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Approximately(scroll, 0f))
            return;

        Vector3 currentPosition = transform.position;
        Vector3 zoomDelta = cam.transform.forward * (scroll * zoomSpeed);

        float proposedY = currentPosition.y + zoomDelta.y;
        bool wouldExceedZoomLimits =
            proposedY < minZoom ||
            proposedY > maxZoom;

        if (wouldExceedZoomLimits)
        {
            // Reach the height boundary without continuing to crawl horizontally
            // along the angled camera-forward vector.
            currentPosition.y = Mathf.Clamp(
                proposedY,
                minZoom,
                maxZoom);

            transform.position = currentPosition;
            return;
        }

        transform.position = currentPosition + zoomDelta;
    }

    #endregion

    #region Battle Map Bounds

    void ClampCameraToBattleMap()
    {
        if (!cameraClampToBattleMap)
            return;

        if (battleMap == null)
            battleMap = BattleMap.Instance;

        if (battleMap == null)
            return;

        transform.position = battleMap.ClampWorldPosition(
            transform.position,
            cameraBoundsPadding);
    }

    #endregion
}
