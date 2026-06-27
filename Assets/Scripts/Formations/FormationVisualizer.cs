using System.Collections.Generic;
using UnityEngine;

public class FormationVisualizer : MonoBehaviour
{
    public static FormationVisualizer Instance { get ; private set; }

    [SerializeField] private GameObject slotIndicatorPrefab;
    [SerializeField] private int poolSize = 50;
    [SerializeField] private float hideDelay = 1.0f;

    [Header("Placement")]
    [SerializeField] private float indicatorHeightOffset = 0.05f;

    private List<GameObject> pool = new List<GameObject>();
    private float hideTimer = 0f;
    private bool isShowing = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < poolSize; i++)
        {
            GameObject go = Instantiate(slotIndicatorPrefab, transform);
            go.SetActive(false);
            pool.Add(go);
        }
    }

    void Update()
    {
        if (!isShowing)
            return;

        hideTimer -= Time.deltaTime;

        if (hideTimer <= 0f)
            HideAll();
    }

    public void ShowSlots(
        List<Vector3> positions,
        bool autoHide = true)
    {
        ShowSlots(positions, Vector3.forward, autoHide);
    }

    public void ShowSlots(
        List<Vector3> positions,
        Vector3 facing,
        bool autoHide = true)
    {
        HideAll();

        facing.y = 0f;

        if (facing == Vector3.zero)
            facing = Vector3.forward;

        float yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;

        for (int i = 0; i < positions.Count && i < pool.Count; i++)
        {
            pool[i].transform.position =
                positions[i] + Vector3.up * indicatorHeightOffset;

            pool[i].transform.rotation = Quaternion.Euler(
                0f,
                yaw,
                0f);

            pool[i].SetActive(true);
        }

        if (autoHide)
        {
            hideTimer = hideDelay;
            isShowing = true;
        }
        else
        {
            hideTimer = 0f;
            isShowing = false;
        }
    }

    public void HideAll()
    {
        foreach (GameObject go in pool)
            go.SetActive(false);

        isShowing = false;
    }
}