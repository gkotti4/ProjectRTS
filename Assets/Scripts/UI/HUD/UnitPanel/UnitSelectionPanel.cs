using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Battle HUD unit-panel controller.
///
/// Owns one UnitCardUI for every player squad spawned by BattleGameModeController
/// and applies the optional category filter selected by UnitFilterPanelUI.
///
/// Recommended hierarchy:
/// UnitPanel
///   - UnitCardSelectionPanel
///   - UnitFilterPanel
/// </summary>
[DisallowMultipleComponent]
public class UnitSelectionPanelUI : MonoBehaviour
{
    #region References

    [Header("Battle")]
    [SerializeField] private BattleGameModeController battleGameModeController;

    [Header("Cards")]
    [SerializeField] private UnitCardUI unitCardPrefab;
    [SerializeField] private Transform cardContainer;

    [Header("Filter")]
    [SerializeField] private UnitFilterPanelUI unitFilterPanel;

    #endregion

    #region Runtime

    private readonly List<UnitCardUI> unitCards = new List<UnitCardUI>();
    private bool isSubscribedToBattleController = false;

    private bool unitFilterUsesCategory = false;
    private SquadCategory unitFilterCategory = SquadCategory.Infantry;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (cardContainer == null)
            cardContainer = transform;

        if (unitFilterPanel == null)
            unitFilterPanel = GetComponentInChildren<UnitFilterPanelUI>(true);
    }

    void OnEnable()
    {
        ResolveBattleController();
        SubscribeToBattleController();
    }

    void Start()
    {
        ResolveBattleController();
        SubscribeToBattleController();

        if (unitFilterPanel != null)
            unitFilterPanel.Initialize(this);

        TryBuildFromExistingBattle();
        RefreshFilterUI();
    }

    void OnDisable()
    {
        UnsubscribeFromBattleController();
    }

    void OnDestroy()
    {
        UnsubscribeFromBattleController();
    }

    #endregion

    #region Public Filter API

    public void ShowAllUnitCards()
    {
        unitFilterUsesCategory = false;
        ApplyCurrentFilter();
        RefreshFilterUI();
    }

    public void ShowUnitCategory(SquadCategory category)
    {
        unitFilterUsesCategory = true;
        unitFilterCategory = category;
        ApplyCurrentFilter();
        RefreshFilterUI();
    }

    public bool IsShowingAllCategories => !unitFilterUsesCategory;

    public bool IsShowingCategory(SquadCategory category)
    {
        return unitFilterUsesCategory && unitFilterCategory == category;
    }

    public int GetUnitCategoryCount(SquadCategory category)
    {
        int count = 0;

        for (int index = 0; index < unitCards.Count; index++)
        {
            UnitCardUI card = unitCards[index];

            if (card != null && card.Category == category)
                count++;
        }

        return count;
    }

    public int GetTotalUnitCardCount()
    {
        return unitCards.Count;
    }

    #endregion

    #region Battle Binding

    void ResolveBattleController()
    {
        if (battleGameModeController == null)
            battleGameModeController = BattleGameModeController.Instance;
    }

    void SubscribeToBattleController()
    {
        if (battleGameModeController == null || isSubscribedToBattleController)
            return;

        battleGameModeController.OnArmiesSpawned += HandleArmiesSpawned;
        isSubscribedToBattleController = true;
    }

    void UnsubscribeFromBattleController()
    {
        if (battleGameModeController != null && isSubscribedToBattleController)
            battleGameModeController.OnArmiesSpawned -= HandleArmiesSpawned;

        isSubscribedToBattleController = false;
    }

    void TryBuildFromExistingBattle()
    {
        if (battleGameModeController == null)
            return;

        if (battleGameModeController.PlayerSquads == null ||
            battleGameModeController.PlayerSquads.Count == 0)
        {
            return;
        }

        RebuildCards(battleGameModeController.PlayerSquads);
    }

    void HandleArmiesSpawned(
        IReadOnlyList<SquadController> playerArmy,
        IReadOnlyList<SquadController> enemyArmy)
    {
        RebuildCards(playerArmy);
    }

    #endregion

    #region Card Management

    void RebuildCards(IReadOnlyList<SquadController> playerArmy)
    {
        ClearCards();

        if (unitCardPrefab == null)
        {
            Debug.LogError(
                $"{name}: UnitSelectionPanelUI requires a UnitCardUI prefab.",
                this);
            return;
        }

        if (playerArmy == null)
        {
            RefreshFilterUI();
            return;
        }

        for (int squadIndex = 0; squadIndex < playerArmy.Count; squadIndex++)
        {
            SquadController squad = playerArmy[squadIndex];

            if (squad == null)
                continue;

            UnitCardUI card = Instantiate(unitCardPrefab, cardContainer);
            card.Initialize(squad);
            unitCards.Add(card);
        }

        ApplyCurrentFilter();
        RefreshFilterUI();
    }

    void ClearCards()
    {
        for (int index = 0; index < unitCards.Count; index++)
        {
            if (unitCards[index] != null)
                Destroy(unitCards[index].gameObject);
        }

        unitCards.Clear();
    }

    void ApplyCurrentFilter()
    {
        for (int index = 0; index < unitCards.Count; index++)
        {
            UnitCardUI card = unitCards[index];

            if (card == null)
                continue;

            bool shouldShow =
                !unitFilterUsesCategory ||
                card.Category == unitFilterCategory;

            card.SetFilterVisible(shouldShow);
        }
    }

    void RefreshFilterUI()
    {
        if (unitFilterPanel != null)
            unitFilterPanel.Refresh();
    }

    #endregion
}
