using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float panSpeed = 20f;
    [SerializeField] private float dragSpeed = 15f;
    [SerializeField] private float zoomSpeed = 30f;
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 50f;
    
    // TODO - scale Speed's with current zoom
    
    [SerializeField] private Vector3 startPosition = new Vector3(30, 10, 20);

    private Camera cam;
    
    private Vector3 lastMousePosition;
    
    void Start()
    {
        cam = Camera.main;
        if (cam == null) Debug.LogError("No camera found!");
        cam.transform.position = startPosition;
        cam.transform.rotation = Quaternion.Euler(40, -45, 0);
    }

    void Update()
    {
        HandlePan();
        HandleZoom();
        HandleMiddleMousePan();
    }

    void HandlePan() // Possibly move to PlayerInputHandler - not really needed atm
    {
        // Default Unity input system (WASD, Up Down Left Right)
        //float x = Input.GetAxisRaw("Horizontal");
        //float z = Input.GetAxisRaw("Vertical");
        
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
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Approximately(scroll, 0f))
            return;

        Vector3 currentPosition = transform.position;
        Vector3 zoomDelta = transform.forward * (scroll * zoomSpeed);

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
    
}
