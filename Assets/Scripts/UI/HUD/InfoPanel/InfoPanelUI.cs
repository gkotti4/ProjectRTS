using TMPro;
using UnityEngine;

public class InfoPanelUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI entityText;

    void Start()
    {
        GameEvents.OnSelectionChanged += HandleSelectionChanged;
        GameEvents.OnDeselected += Hide;
        Hide();
    }

    void OnDestroy()
    {
        GameEvents.OnSelectionChanged -= HandleSelectionChanged;
        GameEvents.OnDeselected -= Hide;
    }

    void HandleSelectionChanged()
    {
        var selected = SelectionManager.Instance.GetSelectedObjects();

        if (selected.Count != 1)
        {
            Hide();
            return;
        }

        switch (selected[0])
        {
            case SquadController squad:
                ShowSquad(squad);
                return;

            case WorkerController worker:
                ShowWorker(worker);
                return;

            case BuildingController building:
                ShowBuilding(building);
                return;

            default:
                Hide();
                return;
        }
    }

    void ShowSquad(SquadController squad)
    {
        gameObject.SetActive(true);

        string squadName = squad.Data != null ? squad.Data.squadName : squad.name;

        entityText.text =
            "Name: " + squadName + "\n" +
            "Type: Squad\n" +
            "Category: " + squad.Category + "\n" +
            "State: " + squad.State + "\n" +
            "Stance: " + squad.Stance + "\n" +
            "Soldiers: " + squad.Health.LivingSoldiers + " / " + squad.Health.TotalSoldiers + "\n" +
            "Health: " + squad.Health.CurrentHealth + " / " + squad.Health.MaxHealth;
    }

    void ShowWorker(WorkerController worker)
    {
        gameObject.SetActive(true);

        string workerName = worker.Data != null ? worker.Data.workerName : worker.name;

        entityText.text =
            "Name: " + workerName + "\n" +
            "Type: Worker";
    }

    void ShowBuilding(BuildingController building)
    {
        gameObject.SetActive(true);

        string buildingName = building.Data != null ? building.Data.buildingName : building.name;

        entityText.text =
            "Name: " + buildingName + "\n" +
            "Type: Building\n" +
            "Category: " + (building.Data != null ? building.Data.category.ToString() : "None");
    }

    void Hide()
    {
        gameObject.SetActive(false);
    }
}