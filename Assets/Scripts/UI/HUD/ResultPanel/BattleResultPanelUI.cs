using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BattleResultPanelUI : MonoBehaviour
{
    #region References

    [Header("Battle")]
    [SerializeField] private BattleGameModeController battleGameModeController;
    [SerializeField] private UpgradeSelectionPanelUI upgradeSelectionPanel;

    [Header("Panel")]
    [SerializeField] private CanvasGroup resultCanvasGroup;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private TextMeshProUGUI battleTimeText;
    [SerializeField] private TextMeshProUGUI survivorsText;

    [Header("Button")]
    [SerializeField] private Button continueButton;

    #endregion

    #region Presentation

    [Header("Labels")]
    [SerializeField] private string victoryLabel = "VICTORY";
    [SerializeField] private string defeatLabel = "DEFEAT";
    [SerializeField] private string battleTimeLabel = "Battle Time";
    [SerializeField] private string survivorsLabel = "Survivors";

    #endregion

    #region Runtime

    private int startingPlayerSoldierCount = 0;
    private bool isSubscribedToBattleController = false;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (resultCanvasGroup == null)
            resultCanvasGroup = GetComponent<CanvasGroup>();

        if (resultCanvasGroup == null)
            resultCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (continueButton != null)
            continueButton.onClick.AddListener(HandleContinueClicked);

        SetPanelVisible(false);
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
        CaptureExistingArmyIfAvailable();
        RefreshFromCurrentBattleState();
    }

    void OnDisable()
    {
        UnsubscribeFromBattleController();
    }

    void OnDestroy()
    {
        UnsubscribeFromBattleController();

        if (continueButton != null)
            continueButton.onClick.RemoveListener(HandleContinueClicked);
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
        if (battleGameModeController == null ||
            isSubscribedToBattleController)
        {
            return;
        }

        battleGameModeController.OnArmiesSpawned += HandleArmiesSpawned;
        battleGameModeController.OnBattleStateChanged += HandleBattleStateChanged;
        isSubscribedToBattleController = true;
    }

    void UnsubscribeFromBattleController()
    {
        if (battleGameModeController == null ||
            !isSubscribedToBattleController)
        {
            return;
        }

        battleGameModeController.OnArmiesSpawned -= HandleArmiesSpawned;
        battleGameModeController.OnBattleStateChanged -= HandleBattleStateChanged;
        isSubscribedToBattleController = false;
    }

    void HandleArmiesSpawned(
        IReadOnlyList<SquadController> playerArmy,
        IReadOnlyList<SquadController> enemyArmy)
    {
        startingPlayerSoldierCount = CountTotalSoldiers(playerArmy);
        SetPanelVisible(false);
    }

    void HandleBattleStateChanged(BattleGameState newState)
    {
        switch (newState)
        {
            case BattleGameState.Victory:
                ShowResult(victoryLabel);
                break;

            case BattleGameState.Defeat:
                ShowResult(defeatLabel);
                break;

            default:
                SetPanelVisible(false);
                break;
        }
    }

    void CaptureExistingArmyIfAvailable()
    {
        if (battleGameModeController == null)
            return;

        if (startingPlayerSoldierCount > 0)
            return;

        startingPlayerSoldierCount = CountTotalSoldiers(
            battleGameModeController.PlayerSquads);
    }

    void RefreshFromCurrentBattleState()
    {
        if (battleGameModeController == null)
            return;

        HandleBattleStateChanged(battleGameModeController.State);
    }

    #endregion

    #region Result Presentation

    void ShowResult(string resultLabel)
    {
        if (battleGameModeController == null)
            return;

        if (resultText != null)
            resultText.text = resultLabel;

        if (battleTimeText != null)
        {
            battleTimeText.text =
                $"{battleTimeLabel}\n{FormatBattleTime(battleGameModeController.BattleElapsedTime)}";
        }

        if (survivorsText != null)
        {
            int livingSoldiers = CountLivingSoldiers(
                battleGameModeController.PlayerSquads);

            survivorsText.text =
                $"{survivorsLabel}\n{livingSoldiers} / {startingPlayerSoldierCount}";
        }

        SetPanelVisible(true);
    }

    void SetPanelVisible(bool visible)
    {
        if (resultCanvasGroup == null)
            return;

        resultCanvasGroup.alpha = visible ? 1f : 0f;
        resultCanvasGroup.interactable = visible;
        resultCanvasGroup.blocksRaycasts = visible;
    }

    string FormatBattleTime(float elapsedSeconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        return $"{minutes:00}:{seconds:00}";
    }

    #endregion

    #region Soldier Counts

    int CountLivingSoldiers(IReadOnlyList<SquadController> squads)
    {
        if (squads == null)
            return 0;

        int count = 0;

        for (int index = 0; index < squads.Count; index++)
        {
            SquadController squad = squads[index];

            if (squad == null || squad.Health == null)
                continue;

            count += Mathf.Max(0, squad.Health.LivingSoldiers);
        }

        return count;
    }

    int CountTotalSoldiers(IReadOnlyList<SquadController> squads)
    {
        if (squads == null)
            return 0;

        int count = 0;

        for (int index = 0; index < squads.Count; index++)
        {
            SquadController squad = squads[index];

            if (squad == null || squad.Health == null)
                continue;

            count += Mathf.Max(0, squad.Health.TotalSoldiers);
        }

        return count;
    }

    #endregion

    #region Continue

    void HandleContinueClicked()
    {
        if (battleGameModeController == null)
            return;

        SetPanelVisible(false);

        if (upgradeSelectionPanel != null &&
            upgradeSelectionPanel.ShowUpgradeChoices())
        {
            return;
        }

        // No valid reward choices are available. Keep the loop playable instead
        // of trapping the player on an empty upgrade screen.
        battleGameModeController.RestartBattle();
    }

    #endregion
}
