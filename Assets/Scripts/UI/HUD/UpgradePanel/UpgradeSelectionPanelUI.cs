using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Post-battle upgrade-choice coordinator for Battle Test Mode.
///
/// Responsibilities:
/// - reads the current BattleDefinitionData.rewardUpgradePool
/// - filters to faction upgrades the player can currently apply
/// - presents a small random set of unique UpgradeData choices
/// - applies exactly one chosen reward to the player faction
/// - advances the battle run after a successful selection
///
/// The GridLayoutGroup / card arrangement stays authored entirely in the scene.
/// </summary>
[DisallowMultipleComponent]
public class UpgradeSelectionPanelUI : MonoBehaviour
{
    #region References

    [Header("Battle")]
    [SerializeField] private BattleGameModeController battleGameModeController;

    [Header("Panel")]
    [Tooltip("Not required. Automatically created during runtime.")]
    [SerializeField] private CanvasGroup upgradeCanvasGroup;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private UpgradeCardUI upgradeCardPrefab;

    #endregion

    #region Tuning

    [Header("Choices")]
    [Min(1)]
    [SerializeField] private int upgradeChoiceCount = 3;

    #endregion

    #region Runtime

    private readonly List<UpgradeCardUI> spawnedCards =
        new List<UpgradeCardUI>();

    private readonly List<UpgradeData> validUpgradeBuffer =
        new List<UpgradeData>();

    private bool selectionCommitted = false;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (upgradeCanvasGroup == null)
            upgradeCanvasGroup = GetComponent<CanvasGroup>();

        if (upgradeCanvasGroup == null)
            upgradeCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        SetPanelVisible(false);
    }

    #endregion

    #region Public API

    public bool ShowUpgradeChoices()
    {
        ResolveBattleController();
        ClearCards();
        selectionCommitted = false;

        if (!TryBuildValidUpgradePool())
        {
            SetPanelVisible(false);
            return false;
        }

        int choiceCount = Mathf.Min(
            Mathf.Max(1, upgradeChoiceCount),
            validUpgradeBuffer.Count);

        Shuffle(validUpgradeBuffer);

        FactionInstance playerFaction = GameManager.Instance.PlayerFaction;

        for (int index = 0; index < choiceCount; index++)
        {
            UpgradeData upgrade = validUpgradeBuffer[index];

            UpgradeCardUI card = Instantiate(
                upgradeCardPrefab,
                cardContainer);

            card.Initialize(
                upgrade,
                playerFaction.GetUpgradeStackCount(upgrade),
                HandleUpgradeSelected);

            spawnedCards.Add(card);
        }

        SetPanelVisible(spawnedCards.Count > 0);
        return spawnedCards.Count > 0;
    }

    public void HideUpgradeChoices()
    {
        SetPanelVisible(false);
        ClearCards();
        selectionCommitted = false;
    }

    #endregion

    #region Choice Generation

    bool TryBuildValidUpgradePool()
    {
        validUpgradeBuffer.Clear();

        if (battleGameModeController == null ||
            battleGameModeController.BattleDefinition == null ||
            GameManager.Instance == null ||
            GameManager.Instance.PlayerFaction == null)
        {
            return false;
        }

        List<UpgradeData> rewardPool =
            battleGameModeController.BattleDefinition.rewardUpgradePool;

        if (rewardPool == null || rewardPool.Count == 0)
            return false;

        FactionInstance playerFaction = GameManager.Instance.PlayerFaction;

        for (int index = 0; index < rewardPool.Count; index++)
        {
            UpgradeData upgrade = rewardPool[index];

            if (upgrade == null)
                continue;

            // Battle Test Mode currently grants faction-wide run rewards.
            // Squad-local rewards need a separate "which squad?" selection step.
            if (upgrade.scope != UpgradeScope.Faction)
                continue;

            if (!playerFaction.CanApplyUpgrade(upgrade))
                continue;

            if (validUpgradeBuffer.Contains(upgrade))
                continue;

            validUpgradeBuffer.Add(upgrade);
        }

        return validUpgradeBuffer.Count > 0;
    }

    void Shuffle(List<UpgradeData> upgrades)
    {
        for (int index = upgrades.Count - 1; index > 0; index--)
        {
            int swapIndex = Random.Range(0, index + 1);

            (upgrades[index], upgrades[swapIndex]) =
                (upgrades[swapIndex], upgrades[index]);
        }
    }

    #endregion

    #region Selection

    void HandleUpgradeSelected(UpgradeData upgrade)
    {
        if (selectionCommitted || upgrade == null)
            return;

        if (GameManager.Instance == null ||
            GameManager.Instance.PlayerFaction == null)
        {
            return;
        }

        selectionCommitted = true;
        SetCardsInteractable(false);

        bool applied = GameManager.Instance.TryApplyFactionUpgrade(
            upgrade,
            GameManager.Instance.PlayerFaction,
            UpgradeGrantSource.ScenarioReward);

        if (!applied)
        {
            selectionCommitted = false;
            SetCardsInteractable(true);

            Debug.LogWarning(
                $"{name}: Could not apply post-battle upgrade '{upgrade.upgradeName}'.",
                this);

            return;
        }

        HideUpgradeChoices();

        if (battleGameModeController != null)
            battleGameModeController.AdvanceBattleRun();
    }

    void SetCardsInteractable(bool interactable)
    {
        for (int index = 0; index < spawnedCards.Count; index++)
        {
            UpgradeCardUI card = spawnedCards[index];

            if (card == null)
                continue;

            UnityEngine.UI.Button button = card.GetComponent<UnityEngine.UI.Button>();

            if (button != null)
                button.interactable = interactable;
        }
    }

    #endregion

    #region Panel

    void ResolveBattleController()
    {
        if (battleGameModeController == null)
            battleGameModeController = BattleGameModeController.Instance;
    }

    void SetPanelVisible(bool visible)
    {
        if (upgradeCanvasGroup == null)
            return;

        upgradeCanvasGroup.alpha = visible ? 1f : 0f;
        upgradeCanvasGroup.interactable = visible;
        upgradeCanvasGroup.blocksRaycasts = visible;
    }

    void ClearCards()
    {
        for (int index = spawnedCards.Count - 1; index >= 0; index--)
        {
            UpgradeCardUI card = spawnedCards[index];

            if (card != null)
                Destroy(card.gameObject);
        }

        spawnedCards.Clear();
    }

    #endregion
}
