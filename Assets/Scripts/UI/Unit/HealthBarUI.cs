using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private float hideDelay = 3f;

    private Camera mainCamera;
    private float hideTimer = 0f;
    private bool isSelected = false;
    public bool IsSelected => isSelected;
    private Canvas canvas;
    
    void Start()
    {
        mainCamera = Camera.main;
        canvas = GetComponent<Canvas>();
        canvas.worldCamera = mainCamera;
        Hide();
    }

    void Update()
    {
        // Always face camera
        transform.rotation = mainCamera.transform.rotation;

        if (isSelected) return;
        
        // Hide after delay
        if (hideTimer > 0f)
        {
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0f)
                Hide();
        }
    }

    public void OnDamaged(int currentHealth, int maxHealth)
    {
        //Debug.Log("health bar on damaged");
        Show();
        fillImage.fillAmount = (float)currentHealth / (float)maxHealth;
        hideTimer = hideDelay;
    }
    
    public void OnSelected() { isSelected = true; Show(); }
    public void OnDeselected() { isSelected = false; Hide(); }
    
    // public void Show() => canvas.enabled = true;
    public void Show() {
        if (!canvas)
        {
            Debug.LogError("canvas not found on " +  gameObject.name); // CHECK: this triggered after exiting (figure out the closing order and why its caused. - happened after Squads)
            return;
        }
        canvas.enabled = true;
    }
    // public void Hide() => canvas.enabled = false;
    public void Hide() {
        if (!canvas)
        {
            Debug.LogError("canvas not found on " + gameObject.name);
            return;
        }
        canvas.enabled = false;
    }

    
}
