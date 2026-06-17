// SESSION: Squad Control Refactor

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class ActionButtonUI : MonoBehaviour
{
    #region Types

    private enum ButtonMode
    {
        None,
        Production,
        //UnitCommand, // DEPRECIATED
        VillagerCommand,
        SquadCommand,
        Build
    }

    #endregion

    #region Fields

    [Header("UI Refs")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private TextMeshProUGUI hotkeyLabel;

    [Header("Colors")]
    [SerializeField] private Color affordableColor = Color.lightBlue;
    [SerializeField] private Color unaffordableColor = Color.red;

    private Button button;

    private ButtonMode mode = ButtonMode.None;

    private ProductionOptionData productionOption;
    private CommandData commandData;
    private BuildOptionData buildOption;

    private BuildingController targetBuilding;
    private VillagerController targetVillager;
    private SquadController targetSquad;

    private bool canAfford = false;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    void Start()
    {
        GameEvents.OnResourcesChanged += UpdateAffordability;
    }

    void OnDestroy()
    {
        GameEvents.OnResourcesChanged -= UpdateAffordability;
    }

    #endregion

    #region Initialize

    public void InitializeFromProductionOption(
        ProductionOptionData option,
        BuildingController building)
    {
        Clear();

        if (!option || !building)
            return;

        mode = ButtonMode.Production;
        productionOption = option;
        targetBuilding = building;

        SetVisuals(
            option.icon,
            option.productionName,
            option.hotkey);

        canAfford = GameManager.Instance.CanAfford(
            productionOption.cost,
            GameManager.Instance.PlayerFaction);

        SetButtonColor(canAfford ? affordableColor : unaffordableColor);
    }
    
    public void InitializeFromVillagerCommand(
        CommandData command,
        VillagerController villager)
    {
        Clear();

        if (!command || !villager)
            return;

        mode = ButtonMode.VillagerCommand;
        commandData = command;
        targetVillager = villager;

        SetVisuals(
            command.icon,
            command.commandName,
            command.hotkey);

        SetButtonColor(affordableColor);
    }

    public void InitializeFromSquadCommand(
        CommandData command,
        SquadController squad)
    {
        Clear();
        if (!command || !squad)
            return;

        mode = ButtonMode.SquadCommand;
        commandData = command;
        targetSquad = squad;

        SetVisuals(
            command.icon,
            command.commandName,
            command.hotkey);

        SetButtonColor(affordableColor);
    }

    public void InitializeFromBuildOption(
        BuildOptionData option,
        VillagerController villager)
    {
        Clear();

        if (!option || !villager)
            return;

        mode = ButtonMode.Build;
        buildOption = option;
        targetVillager = villager;

        SetVisuals(
            option.icon,
            option.buildingName,
            option.hotkey);

        SetButtonColor(affordableColor);
    }

    public void Clear()
    {
        mode = ButtonMode.None;

        productionOption = null;
        commandData = null;
        buildOption = null;

        targetBuilding = null;
        targetVillager = null;
        targetSquad = null;

        canAfford = false;

        if (iconImage)
            iconImage.sprite = null;

        if (label)
            label.text = string.Empty;

        if (hotkeyLabel)
            hotkeyLabel.text = string.Empty;
    }

    #endregion

    #region Click

    void OnClick()
    {
        switch (mode)
        {
            case ButtonMode.Production:
                HandleProductionClick();
                return;

            case ButtonMode.VillagerCommand:
                HandleVillagerCommandClick();
                return;

            case ButtonMode.SquadCommand:
                HandleSquadCommandClick();
                return;

            case ButtonMode.Build:
                HandleBuildClick();
                return;
        }
    }

    void HandleProductionClick()
    {
        if (productionOption == null) return;
        if (!canAfford) return;

        List<ISelectable> selected = SelectionManager.Instance.GetSelectedObjects();

        foreach (ISelectable selectable in selected)
        {
            if (selectable is not BuildingController building)
                continue;

            if (!GameManager.Instance.CanAfford(
                    productionOption.cost,
                    GameManager.Instance.PlayerFaction))
                return;

            building.EnqueueProduction(productionOption);
        }
    }
    
    void HandleVillagerCommandClick()
    {
        if (commandData == null) return;

        if (commandData.commandType == CommandType.Build)
        {
            if (targetVillager != null)
                UIManager.Instance.ShowActionPanelBuildSubmenu(targetVillager);

            return;
        }

        List<ISelectable> selected = SelectionManager.Instance.GetSelectedObjects();

        foreach (ISelectable selectable in selected)
        {
            if (selectable == null || selectable.GetGameObject() == null)
                continue;

            if (!selectable.GetGameObject().TryGetComponent(out VillagerController _))
                continue;

            if (selectable.GetGameObject().TryGetComponent(out CommandController commandController))
                commandController.ExecuteCommand(commandData.commandType);
        }
    }

    void HandleSquadCommandClick()
    {
        if (commandData == null) return;

        PlayerInputHandler.Instance.ExecuteSquadCommand(commandData.commandType);
    }

    void HandleBuildClick()
    {
        if (buildOption == null) return;

        BuildingPlacer.Instance.StartPlacing(buildOption);
    }

    #endregion

    #region Resources

    void UpdateAffordability(FactionInstance faction)
    {
        if (faction != GameManager.Instance.PlayerFaction) return;
        if (mode != ButtonMode.Production) return;
        if (!productionOption) return;

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup && canvasGroup.alpha == 0f)
            return;

        canAfford = GameManager.Instance.CanAfford(
            productionOption.cost,
            GameManager.Instance.PlayerFaction);

        SetButtonColor(canAfford ? affordableColor : unaffordableColor);
    }

    #endregion

    #region Visuals

    void SetVisuals(Sprite icon, string text, HotkeySlot hotkey)
    {
        if (iconImage)
            iconImage.sprite = icon;

        if (label)
            label.text = text;

        if (hotkeyLabel)
            hotkeyLabel.text = hotkey == HotkeySlot.None
                ? string.Empty
                : hotkey.ToString();
    }

    void SetButtonColor(Color color)
    {
        if (button && button.image)
            button.image.color = color;
    }

    #endregion
}

