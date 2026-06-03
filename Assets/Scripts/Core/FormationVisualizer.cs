using System.Collections.Generic;
using UnityEngine;

// Attached to FormationManager GameObject
public class FormationVisualizer : MonoBehaviour
{
    public static FormationVisualizer Instance { get ; private set; }

    [SerializeField] private GameObject slotIndicatorPrefab; // simple flat cylinder or quad
    [SerializeField] private int poolSize = 50;
    [SerializeField] private float hideDelay = 1f;
    
    private List<GameObject> pool = new List<GameObject>();
    private float hideTimer = 0f;
    private bool isShowing = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Pre-spawn pool
        for (int i = 0; i < poolSize; i++)
        {
            GameObject go = Instantiate(slotIndicatorPrefab, transform);
            go.SetActive(false);
            pool.Add(go);
        }
    }
    

    void Update()
    {
        if (!isShowing) return;
        hideTimer -= Time.deltaTime;
        if (hideTimer <= 0f)
            HideAll();
    }


    /// Shows slot indicators at given positions then fades after delay.
    public void ShowSlots(List<Vector3> positions, bool persistent=false)
    {
        HideAll();

        for (int i = 0; i < positions.Count && i < pool.Count; i++)
        {
            pool[i].transform.position = positions[i]; // + Vector3.up * 0.05f;
            pool[i].SetActive(true);
        }

        if (!persistent)
        {
            hideTimer = hideDelay;
            isShowing = true;
        }
    }
    
    public void HideAll()
    {
        foreach (GameObject go in pool)
            go.SetActive(false);
        isShowing = false;
    }
}
