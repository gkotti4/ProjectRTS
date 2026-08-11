using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float panSpeed = 20f;
    [SerializeField] private float dragSpeed = 15f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 30f;
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 50f;

    [Header("Battle Map Bounds")]
    [SerializeField] private BattleMap battleMap;

    [Tooltip("Keeps the camera root this far inside the playable battlefield edge.")]
    [Min(0f)]
    [SerializeField] private float cameraBoundsPadding = 0f;

    [SerializeField] private bool cameraClampToBattleMap = true;

    // TODO - scale Speed's with current zoom

    [SerializeField] private Vector3 startPosition = new Vector3(30, 10, 20);

    private Camera cam;
    private Vector3 lastMousePosition;

    void Start()
    {
        cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError("No camera found!");
            return;
        }

        if (battleMap == null)
            battleMap = BattleMap.Instance;

        transform.position = startPosition;
        // cam.transform.rotation = Quaternion.Euler(40, -45, 0);

        ClampCameraToBattleMap();
    }

    void Update()
    {
        HandlePan();
        HandleZoom();
        HandleMiddleMousePan();

        // One authoritative clamp after all camera movement for this frame.
        ClampCameraToBattleMap();
    }

    void HandlePan() // Possibly move to PlayerInputHandler - not really needed atm
    {
        if (cam == null)
            return;

        // Only Arrow Keys
        float x = 0f;
        float z = 0f;

        if (Input.GetKey(KeyCode.RightArrow)) x = 1f;
        else if (Input.GetKey(KeyCode.LeftArrow)) x = -1f;

        if (Input.GetKey(KeyCode.UpArrow)) z = 1f;
        else if (Input.GetKey(KeyCode.DownArrow)) z = -1f;

        // Get camera's flat forward and right, ignore Y tilt
        Vector3 forward = cam.transform.forward;
        Vector3 right = cam.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = (forward * z + right * x) * (panSpeed * Time.deltaTime);
        transform.position += move;
    }

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
            // Reach the height boundary, but do not continue crawling
            // forward/backward along the camera's angled forward direction.
            currentPosition.y = Mathf.Clamp(
                proposedY,
                minZoom,
                maxZoom);

            transform.position = currentPosition;
            return;
        }

        transform.position = currentPosition + zoomDelta;
    }

    void HandleMiddleMousePan()
    {
        if (cam == null)
            return;

        if (Input.GetMouseButtonDown(2))
            lastMousePosition = Input.mousePosition;

        if (Input.GetMouseButton(2))
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;

            // Convert screen delta to world movement
            Vector3 right = cam.transform.right;
            Vector3 up = cam.transform.forward;
            right.y = 0f;
            up.y = 0f;
            right.Normalize();
            up.Normalize();

            Vector3 move = (-right * delta.x + -up * delta.y) * (dragSpeed * Time.deltaTime);

            transform.position += move;
            lastMousePosition = Input.mousePosition;
        }
    }

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
}
