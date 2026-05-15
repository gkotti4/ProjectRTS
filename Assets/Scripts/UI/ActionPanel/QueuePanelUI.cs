using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class QueuePanelUI : MonoBehaviour
{
    [SerializeField] private GameObject queueButtonPrefab;
    [SerializeField] private int maxQueueSlots = 5;
    
    private List<QueueButtonUI> slots = new List<QueueButtonUI>();
    private BuildingController currentBuilding;
    
    
    void Start()
    {
        // Pre-spawn all queue slots, hide them, 1 row
        for (int i = 0; i < maxQueueSlots; i++)
        {
            GameObject btn = Instantiate(queueButtonPrefab, transform);
            slots.Add(btn.GetComponent<QueueButtonUI>());
            btn.SetActive(false);
        }
    }

    void Update()
    {
        // Updates progress on slot 0 every frame while building is selected
        if (currentBuilding != null && slots.Count > 0 && slots[0].gameObject.activeSelf)
        {
            slots[0].UpdateProgress(currentBuilding.GetProductionProgress());
        }
    }

    public void ShowPanel(BuildingController building) // Called by UIManager when a building is selected // was ShowQueue()
    {
        //Debug.Log("Showing queue");
        currentBuilding = building;
        Refresh();
    }

    public void HidePanel() 
    {
        currentBuilding = null; // CHECK - undid to get 
        foreach (QueueButtonUI slot in slots)
        {
            slot.gameObject.SetActive(false);
        }
        //Debug.Log("Hiding queue");
    }
    
    public void Refresh() // Refreshes all queue slots from current building queue
    {
        foreach (QueueButtonUI slot in slots)
            slot.gameObject.SetActive(false);
        
        if (currentBuilding == null) return;

        List<ProductionOptionData> queue = currentBuilding.ProductionQueue;
        for (int i = 0; i < queue.Count && i < maxQueueSlots; i++) // Initialize all slots from production queue 
        {
            slots[i].Initialize(queue[i], currentBuilding, i, Refresh);
            slots[i].gameObject.SetActive(true);
        }
    }
    


}
