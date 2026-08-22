using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// -----------------------------------------------------------------------------
/// TopBarUI
/// -----------------------------------------------------------------------------
///
/// Owns the static top-bar HUD presentation and button routing.
///
/// Responsibilities:
/// - optional economy/resource section
/// - battle elapsed timer
/// - battle balance-of-power bar
/// - pause / 1x / 2x / 3x game-speed buttons
/// - optional menu-panel toggle
///
/// Gameplay state remains owned by BattleGameModeController and GameTimeController.
///
public class TopBarUI : MonoBehaviour
{
    #region References

    [Header("Economy / Management")]
    [SerializeField] private GameObject economySection;
    [SerializeField] private TextMeshProUGUI resourceText;

    [Header("Battle")]
    [SerializeField] private GameObject battleSection;
    [SerializeField] private TextMeshProUGUI battleTimerText;
    [Tooltip("Image Fill Amount represents the player's share of current combined army health. 0 = enemy, 1 = player.")]
    [SerializeField] private Image balanceOfPowerFill;

    [Header("Game Speed")]
    [SerializeField] private GameTimeController gameTimeController;
    [SerializeField] private TextMeshProUGUI gameSpeedText;

    [Header("Menu")]
    [SerializeField] private GameObject menuPanel; // Menu, Options, Settings button
    [SerializeField] private bool menuPauseGameWhenOpened = true;
    
    [Header("Settings")]
    [SerializeField] private bool disableEconomySection = true;

    #endregion

    #region Runtime

    private BattleGameModeController battleGameModeController;

    private readonly List<SquadHealth> subscribedBattleHealth =
        new List<SquadHealth>();

    private bool menuWasPausedBeforeOpening = false;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        if (!disableEconomySection)
            GameEvents.OnResourcesChanged += HandleResourcesChanged;

        ResolveGameTimeController();
        BindBattleController();

        if (disableEconomySection)
            SetEconomySectionVisible(false);
        else
            RefreshEconomyDisplay();
        RefreshBattleDisplay();
        RefreshGameSpeedDisplay();

        if (menuPanel != null)
            menuPanel.SetActive(false);
    }

    void Update()
    {
        if (battleGameModeController == null)
        {
            BindBattleController();
        }

        RefreshBattleTimer();
    }

    void OnDestroy()
    {
        GameEvents.OnResourcesChanged -= HandleResourcesChanged;

        UnbindBattleController();
        UnsubscribeBattleHealth();

        if (gameTimeController != null)
            gameTimeController.OnGameSpeedChanged -= HandleGameSpeedChanged;
    }

    #endregion

    #region Economy / Management

    public void SetEconomySectionVisible(bool visible)
    {
        if (economySection != null)
            economySection.SetActive(visible);
    }

    void HandleResourcesChanged(FactionInstance faction)
    {
        if (!IsPlayerFaction(faction))
            return;

        RefreshResourceText(faction);
    }

    void RefreshEconomyDisplay()
    {
        if (GameManager.Instance == null || GameManager.Instance.PlayerFaction == null)
            return;

        RefreshResourceText(GameManager.Instance.PlayerFaction);
    }

    void RefreshResourceText(FactionInstance faction)
    {
        if (resourceText == null || faction == null)
            return;

        resourceText.text =
            "Wood: " + faction.GetResources(ResourceType.Wood) +
            "   Food: " + faction.GetResources(ResourceType.Food) +
            "   Gold: " + faction.GetResources(ResourceType.Gold) +
            "   Stone: " + faction.GetResources(ResourceType.Stone);
    }

    bool IsPlayerFaction(FactionInstance faction)
    {
        return faction != null &&
               GameManager.Instance != null &&
               faction == GameManager.Instance.PlayerFaction;
    }

    #endregion

    #region Battle

    public void SetBattleSectionVisible(bool visible)
    {
        if (battleSection != null)
            battleSection.SetActive(visible);
    }

    void BindBattleController()
    {
        BattleGameModeController resolvedController =
            BattleGameModeController.Instance;

        if (resolvedController == null ||
            resolvedController == battleGameModeController)
        {
            return;
        }

        UnbindBattleController();
        battleGameModeController = resolvedController;

        battleGameModeController.OnBattleStateChanged += HandleBattleStateChanged;
        battleGameModeController.OnArmiesSpawned += HandleArmiesSpawned;

        SubscribeBattleHealth(
            battleGameModeController.PlayerSquads,
            battleGameModeController.EnemySquads);

        RefreshBattleDisplay();
    }

    void UnbindBattleController()
    {
        if (battleGameModeController == null)
            return;

        battleGameModeController.OnBattleStateChanged -= HandleBattleStateChanged;
        battleGameModeController.OnArmiesSpawned -= HandleArmiesSpawned;
        battleGameModeController = null;
    }

    void HandleBattleStateChanged(BattleGameState state)
    {
        RefreshBattleDisplay();
    }

    void HandleArmiesSpawned(
        IReadOnlyList<SquadController> playerSquads,
        IReadOnlyList<SquadController> enemySquads)
    {
        SubscribeBattleHealth(playerSquads, enemySquads);
        RefreshBalanceOfPower();
        RefreshBattleTimer();
    }

    void RefreshBattleDisplay()
    {
        RefreshBattleTimer();
        RefreshBalanceOfPower();
    }

    void RefreshBattleTimer()
    {
        if (battleTimerText == null)
            return;

        float elapsedTime = battleGameModeController != null
            ? Mathf.Max(0f, battleGameModeController.BattleElapsedTime)
            : 0f;

        int totalSeconds = Mathf.FloorToInt(elapsedTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        battleTimerText.text = $"{minutes:00}:{seconds:00}";
    }

    void SubscribeBattleHealth(
        IReadOnlyList<SquadController> playerSquads,
        IReadOnlyList<SquadController> enemySquads)
    {
        UnsubscribeBattleHealth();
        SubscribeArmyHealth(playerSquads);
        SubscribeArmyHealth(enemySquads);
    }

    void SubscribeArmyHealth(IReadOnlyList<SquadController> squads)
    {
        if (squads == null)
            return;

        for (int index = 0; index < squads.Count; index++)
        {
            SquadController squad = squads[index];

            if (squad == null || squad.Health == null)
                continue;

            SquadHealth squadHealth = squad.Health;

            if (subscribedBattleHealth.Contains(squadHealth))
                continue;

            squadHealth.OnSquadHealthChanged += HandleSquadHealthChanged;
            subscribedBattleHealth.Add(squadHealth);
        }
    }

    void UnsubscribeBattleHealth()
    {
        for (int index = 0; index < subscribedBattleHealth.Count; index++)
        {
            SquadHealth squadHealth = subscribedBattleHealth[index];

            if (squadHealth != null)
                squadHealth.OnSquadHealthChanged -= HandleSquadHealthChanged;
        }

        subscribedBattleHealth.Clear();
    }

    void HandleSquadHealthChanged(SquadHealth squadHealth)
    {
        RefreshBalanceOfPower();
    }

    void RefreshBalanceOfPower()
    {
        if (balanceOfPowerFill == null)
            return;

        if (battleGameModeController == null)
        {
            balanceOfPowerFill.fillAmount = 0.5f;
            return;
        }

        float playerCurrentHealth = GetArmyCurrentHealth(
            battleGameModeController.PlayerSquads);

        float enemyCurrentHealth = GetArmyCurrentHealth(
            battleGameModeController.EnemySquads);

        float combinedHealth = playerCurrentHealth + enemyCurrentHealth;

        balanceOfPowerFill.fillAmount = combinedHealth > 0f
            ? Mathf.Clamp01(playerCurrentHealth / combinedHealth)
            : 0.5f;
    }

    float GetArmyCurrentHealth(IReadOnlyList<SquadController> squads)
    {
        if (squads == null)
            return 0f;

        float currentHealth = 0f;

        for (int index = 0; index < squads.Count; index++)
        {
            SquadController squad = squads[index];

            if (squad == null || squad.Health == null)
                continue;

            currentHealth += Mathf.Max(0, squad.Health.CurrentHealth);
        }

        return currentHealth;
    }

    #endregion

    #region Game Speed

    void ResolveGameTimeController()
    {
        if (gameTimeController == null)
            gameTimeController = GameTimeController.Instance;

        if (gameTimeController != null)
            gameTimeController.OnGameSpeedChanged += HandleGameSpeedChanged;
    }

    public void TogglePause()
    {
        gameTimeController?.TogglePause();
    }

    public void SetNormalGameSpeed()
    {
        gameTimeController?.SetNormalSpeed();
    }

    public void SetFastGameSpeed()
    {
        gameTimeController?.SetFastSpeed();
    }

    public void SetVeryFastGameSpeed()
    {
        gameTimeController?.SetVeryFastSpeed();
    }

    void HandleGameSpeedChanged(float gameSpeed)
    {
        RefreshGameSpeedDisplay();
    }

    void RefreshGameSpeedDisplay()
    {
        if (gameSpeedText == null)
            return;

        if (gameTimeController == null)
        {
            gameSpeedText.text = "1x";
            return;
        }

        gameSpeedText.text = gameTimeController.IsPaused
            ? "Paused"
            : gameTimeController.CurrentGameSpeed.ToString("0.#") + "x";
    }

    #endregion

    #region Menu

    public void ToggleMenuPanel()
    {
        if (menuPanel == null)
            return;

        SetMenuPanelVisible(!menuPanel.activeSelf);
    }

    public void SetMenuPanelVisible(bool visible)
    {
        if (menuPanel == null)
            return;

        if (visible == menuPanel.activeSelf)
            return;

        if (visible)
        {
            menuWasPausedBeforeOpening =
                gameTimeController != null && gameTimeController.IsPaused;

            menuPanel.SetActive(true);

            if (menuPauseGameWhenOpened &&
                gameTimeController != null &&
                !menuWasPausedBeforeOpening)
            {
                gameTimeController.Pause();
            }

            return;
        }

        menuPanel.SetActive(false);

        if (menuPauseGameWhenOpened &&
            gameTimeController != null &&
            !menuWasPausedBeforeOpening)
        {
            gameTimeController.Resume();
        }
    }

    #endregion
}
