using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SquadSelection : MonoBehaviour
{
    [Header("Squad Root Visuals")]
    [SerializeField] private DecalProjector selectionDecal;
    [SerializeField] private GameObject hoverVisual;

    private SquadController squad;
    private SquadRoster roster;

    public void Initialize(
        SquadController owner,
        SquadRoster squadRoster)
    {
        squad = owner;
        roster = squadRoster;

        SetRootSelection(false);
        SetRootHover(false);
        SetSoldierSelection(false);
        SetSoldierHover(false);
    }

    public void OnSelected()
    {
        SetRootSelection(true);
        SetSoldierSelection(true);
    }

    public void OnDeselected()
    {
        SetRootSelection(false);
        SetSoldierSelection(false);
        SetSoldierHover(false);
    }

    public void OnHoverEnter()
    {
        SetRootHover(true);
        SetSoldierHover(true);
    }

    public void OnHoverExit()
    {
        SetRootHover(false);
        SetSoldierHover(false);
    }

    void SetRootSelection(bool visible)
    {
        if (selectionDecal != null)
            selectionDecal.enabled = visible;
    }

    void SetRootHover(bool visible)
    {
        if (hoverVisual != null)
            hoverVisual.SetActive(visible);
    }

    void SetSoldierSelection(bool visible)
    {
        if (roster == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null)
                continue;

            soldier.SetSelectionVisual(visible);
        }
    }

    void SetSoldierHover(bool visible)
    {
        if (roster == null)
            return;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null)
                continue;

            soldier.SetHoverVisual(visible);
        }
    }
}