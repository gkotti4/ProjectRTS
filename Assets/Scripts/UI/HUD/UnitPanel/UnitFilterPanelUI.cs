using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Category-filter controls beneath the battle unit cards.
///
/// This component does not own units or selection state. It only forwards filter
/// requests to UnitSelectionPanelUI and refreshes the configured filter buttons.
/// </summary>
[DisallowMultipleComponent]
public class UnitFilterPanelUI : MonoBehaviour
{
    #region References

    [Header("Buttons")]
    [SerializeField] private UnitFilterButtonUI allUnitsButton;
    [SerializeField] private List<UnitFilterButtonUI> categoryButtons =
        new List<UnitFilterButtonUI>();

    #endregion

    #region Runtime

    private UnitSelectionPanelUI unitSelectionPanel;

    #endregion

    #region Public API

    public void Initialize(UnitSelectionPanelUI owner)
    {
        unitSelectionPanel = owner;

        if (allUnitsButton != null)
            allUnitsButton.InitializeAll(this);

        for (int index = 0; index < categoryButtons.Count; index++)
        {
            UnitFilterButtonUI button = categoryButtons[index];

            if (button != null)
                button.InitializeCategory(this);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (unitSelectionPanel == null)
            return;

        if (allUnitsButton != null)
        {
            allUnitsButton.RefreshVisuals(
                unitSelectionPanel.IsShowingAllCategories,
                unitSelectionPanel.GetTotalUnitCardCount());
        }

        for (int index = 0; index < categoryButtons.Count; index++)
        {
            UnitFilterButtonUI button = categoryButtons[index];

            if (button == null)
                continue;

            SquadCategory category = button.Category;
            int categoryCount = unitSelectionPanel.GetUnitCategoryCount(category);

            button.RefreshVisuals(
                unitSelectionPanel.IsShowingCategory(category),
                categoryCount);
        }
    }

    public void RequestShowAll()
    {
        unitSelectionPanel?.ShowAllUnitCards();
    }

    public void RequestShowCategory(SquadCategory category)
    {
        unitSelectionPanel?.ShowUnitCategory(category);
    }

    #endregion
}
