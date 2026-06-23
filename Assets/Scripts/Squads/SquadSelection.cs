using UnityEngine;

public class SquadSelection : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SquadBannerUI bannerUI;

    private SquadController squad;
    private SquadRoster roster;

    public void Initialize(
        SquadController owner,
        SquadRoster squadRoster)
    {
        squad = owner;
        roster = squadRoster;

        if (bannerUI == null)
            bannerUI = GetComponentInChildren<SquadBannerUI>(true);

        ApplyTeamVisualColors();

        if (bannerUI != null)
        {
            bannerUI.Initialize(squad);
            bannerUI.SetSelected(false);
            bannerUI.SetHovered(false);
        }
    }

    public void OnSelected()
    {
        SetSoldierSelectionVisuals(true);
        SetSoldierHoverVisuals(false);

        if (bannerUI != null)
        {
            bannerUI.SetSelected(true);
            bannerUI.SetHovered(false);
        }
    }

    public void OnDeselected()
    {
        SetSoldierSelectionVisuals(false);
        SetSoldierHoverVisuals(false);

        if (bannerUI != null)
        {
            bannerUI.SetSelected(false);
            bannerUI.SetHovered(false);
        }
    }

    public void OnHoverEnter()
    {
        if (squad != null && squad.IsSelected)
            return;

        SetSoldierHoverVisuals(true);

        if (bannerUI != null)
            bannerUI.SetHovered(true);
    }

    public void OnHoverExit()
    {
        SetSoldierHoverVisuals(false);

        if (bannerUI != null)
            bannerUI.SetHovered(false);
    }

    void ApplyTeamVisualColors()
    {
        if (squad == null || squad.Faction == null || roster == null)
            return;

        TeamVisualSettings visuals = squad.Faction.Visuals;

        foreach (SoldierController soldier in roster.Soldiers)
        {
            if (soldier == null)
                continue;

            soldier.ApplyTeamColors(
                visuals.selectionColor,
                visuals.hoverColor);
        }
    }

    void SetSoldierSelectionVisuals(bool visible)
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

    void SetSoldierHoverVisuals(bool visible)
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


// using UnityEngine;
// using UnityEngine.Rendering.Universal;
//
// public class SquadSelection : MonoBehaviour
// {
//     [Header("Squad Root Visuals")]
//     [SerializeField] SquadBannerUI bannerUI;
//
//     private SquadController squad;
//     private SquadRoster roster;
//
//     public void Initialize(
//         SquadController owner,
//         SquadRoster squadRoster)
//     {
//         squad = owner;
//         roster = squadRoster;
//
//         if (bannerUI == null)
//             bannerUI = GetComponentInChildren<SquadBannerUI>(true);
//
//         ApplyTeamVisualColors();
//
//         if (bannerUI != null)
//         {
//             bannerUI.SetSelected(false);
//             bannerUI.SetHovered(false);
//         }
//     }
//
//     public void OnSelected()
//     {
//         SetSoldierSelectionVisuals(true);
//
//         if (bannerUI != null)
//             bannerUI.SetSelected(true);
//     }
//
//     public void OnDeselected()
//     {
//         SetSoldierSelectionVisuals(false);
//         SetSoldierHoverVisuals(false);
//
//         if (bannerUI != null)
//         {
//             bannerUI.SetSelected(false);
//             bannerUI.SetHovered(false);
//         }
//     }
//
//     public void OnHoverEnter()
//     {
//         SetSoldierHoverVisuals(true);
//
//         if (bannerUI != null)
//             bannerUI.SetHovered(true);
//     }
//
//     public void OnHoverExit()
//     {
//         SetSoldierHoverVisuals(false);
//
//         if (bannerUI != null)
//             bannerUI.SetHovered(false);
//     }
//
//     void ApplyTeamVisualColors()
//     {
//         if (squad == null || squad.Faction == null || roster == null)
//             return;
//
//         TeamVisualSettings visuals = squad.Faction.Visuals;
//
//         foreach (SoldierController soldier in roster.Soldiers)
//         {
//             if (soldier == null)
//                 continue;
//
//             soldier.ApplyTeamColors(
//                 visuals.selectionColor,
//                 visuals.hoverColor);
//         }
//     }
//
//     void SetSoldierSelectionVisuals(bool visible)
//     {
//         if (roster == null)
//             return;
//
//         foreach (SoldierController soldier in roster.Soldiers)
//         {
//             if (soldier == null)
//                 continue;
//
//             soldier.SetSelectionVisual(visible);
//         }
//     }
//
//     void SetSoldierHoverVisuals(bool visible)
//     {
//         if (roster == null)
//             return;
//
//         foreach (SoldierController soldier in roster.Soldiers)
//         {
//             if (soldier == null)
//                 continue;
//
//             if (squad != null && squad.IsSelected)
//                 continue;
//
//             soldier.SetHoverVisual(visible);
//         }
//     }
//     
//     
// }