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
        UnitCommand,
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
    private UnitController targetUnit;
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

        if (option == null || building == null)
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

    public void InitializeFromUnitCommand(
        CommandData command,
        UnitController unit)
    {
        Clear();

        if (command == null || unit == null)
            return;

        mode = ButtonMode.UnitCommand;
        commandData = command;
        targetUnit = unit;

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

        if (command == null || squad == null)
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
        UnitController unit)
    {
        Clear();

        if (option == null || unit == null)
            return;

        mode = ButtonMode.Build;
        buildOption = option;
        targetUnit = unit;

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
        targetUnit = null;
        targetSquad = null;

        canAfford = false;

        if (iconImage != null)
            iconImage.sprite = null;

        if (label != null)
            label.text = string.Empty;

        if (hotkeyLabel != null)
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

            case ButtonMode.UnitCommand:
                HandleUnitCommandClick();
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

    void HandleUnitCommandClick()
    {
        if (commandData == null) return;

        if (commandData.commandType == CommandType.Build)
        {
            if (targetUnit != null)
                UIManager.Instance.ShowActionPanelBuildSubmenu(targetUnit);

            return;
        }

        List<ISelectable> selected = SelectionManager.Instance.GetSelectedObjects();

        foreach (ISelectable selectable in selected)
        {
            if (selectable == null || selectable.GetGameObject() == null)
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
        if (productionOption == null) return;

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null && canvasGroup.alpha == 0f)
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
        if (iconImage != null)
            iconImage.sprite = icon;

        if (label != null)
            label.text = text;

        if (hotkeyLabel != null)
            hotkeyLabel.text = hotkey == HotkeySlot.None
                ? string.Empty
                : hotkey.ToString();
    }

    void SetButtonColor(Color color)
    {
        if (button != null && button.image != null)
            button.image.color = color;
    }

    #endregion
}


// using System;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using UnityEngine.Serialization;
//
// [RequireComponent(typeof(Button))]
//
//
// /*
//  * Action Panel:
//  *  - Handles bottom left panel of screen when selecting either a Unit or Building (Entity)
//  * 
//  *  - For Units:
//  *   - Show Commands
//  * 
//  *  - For Buildings:
//  *   - Show ProductionOptions
//  */
//
// public class ActionButtonUI : MonoBehaviour
// {
//     // Data
//     private ProductionOptionData productionOption; // Building
//     private CommandData commandData; // Unit
//     private BuildOptionData buildOption; // Villager
//     private BuildingController targetBuilding;
//     private UnitController targetUnit;
//
//     // UI refs
//     private Button button;
//     [SerializeField] private Image iconImage;
//     [SerializeField] private TextMeshProUGUI label;
//     [SerializeField] private TextMeshProUGUI hotkeyLabel;
//     
//     [SerializeField] private Color affordableColor = Color.lightBlue; // Production button
//     [SerializeField] private Color unaffordableColor = Color.red; // Production button
//
//     private bool canAfford = false;
//     private enum ButtonMode { Production, Command, Build } // Based on selection, Action panel can contain one of these types of buttons
//     private ButtonMode mode;
//
//     void Awake()
//     {
//         button = GetComponent<Button>();
//         button.onClick.AddListener(OnClick);
//     }
//
//     void Start()
//     {
//         GameEvents.OnResourcesChanged += UpdateAffordability;
//     }
//
//     void OnDestroy()
//     {
//         GameEvents.OnResourcesChanged -= UpdateAffordability;
//     }
//
//     // Initialize for building production option
//     public void InitializeFromProductionOption(ProductionOptionData option, BuildingController building)
//     {
//         mode = ButtonMode.Production;
//         productionOption = option;
//         commandData = null;
//         buildOption = null;
//         targetBuilding = building;
//         targetUnit = null;
//
//         if (iconImage != null)
//             iconImage.sprite = option.icon != null ? option.icon : null;
//
//         if (label != null)
//             label.text = option.productionName;
//         
//         if (hotkeyLabel != null)
//             hotkeyLabel.text = option.hotkey.ToString();
//
//         canAfford = GameManager.Instance.CanAfford(productionOption.cost, GameManager.Instance.PlayerFaction);
//         button.image.color = canAfford ? affordableColor : unaffordableColor;
//     }
//
//     // Initialize for unit command
//     public void InitializeFromCommand(CommandData cmd, UnitController unit)
//     {
//         if (cmd == null) return;
//         mode = ButtonMode.Command;
//         commandData = cmd;
//         productionOption = null;
//         buildOption = null;
//         targetUnit = unit;
//         targetBuilding = null;
//
//         if (iconImage != null && cmd.icon != null)
//             iconImage.sprite = cmd.icon;
//
//         if (label != null)
//             label.text = cmd.commandName;
//         
//         if (hotkeyLabel != null)
//             hotkeyLabel.text = cmd.hotkey.ToString();
//
//         button.image.color = affordableColor;
//     }
//
//     public void InitializeFromBuildOption(BuildOptionData option, UnitController unit)
//     {
//         if (option == null) return;
//         mode = ButtonMode.Build;
//         buildOption = option;
//         productionOption = null;
//         commandData = null;
//         targetUnit = unit;
//         targetBuilding = null;
//         
//         if (iconImage != null && option.icon != null)
//             iconImage.sprite = option.icon;
//
//         if (label != null)
//             label.text = option.buildingName;
//         
//         if (hotkeyLabel != null)
//             hotkeyLabel.text = option.hotkey.ToString();
//         
//         button.image.color = affordableColor;
//
//     }
//
//     void OnClick()
//     {
//         // ProductionButtonUI
//         if (mode == ButtonMode.Production)
//         {
//             if (productionOption == null) return;
//             if (!canAfford) return;
//
//             List<ISelectable> selected = SelectionManager.Instance.GetSelectedObjects();
//
//             // Enqueue production on ALL selected buildings
//             foreach (ISelectable s in selected)
//             {
//                 if (!canAfford) return;
//                 if (s is BuildingController bc)
//                 {
//                     bc.EnqueueProduction(productionOption);
//                 }
//             }     
//         }
//         // CommandButtonUI
//         else if (mode == ButtonMode.Command)
//         {
//             if (commandData == null) return;
//
//             if (commandData.commandType == CommandType.Build) // Opens build submenu!
//             {
//                 if (targetUnit != null)
//                     UIManager.Instance.ShowActionPanelBuildSubmenu(targetUnit);
//                 return;
//             }
//
//             
//             // New - Group type Command (Formations currently)
//             if (commandData.commandScope == CommandScope.Squad)
//             {
//                 PlayerInputHandler.Instance.ExecuteGroupCommand(commandData.commandType);
//                 return;
//             }
//             
//             // Execute command on ALL selected units
//             List<ISelectable> selected = SelectionManager.Instance.GetSelectedObjects();
//             foreach (ISelectable s in selected)
//             {
//                 if (s.GetGameObject().TryGetComponent(out CommandController cc))
//                     cc.ExecuteCommand(commandData.commandType);
//             }
//         }
//         // BuildOptionButtonUI
//         else if (mode == ButtonMode.Build) // Builds a build option from button!
//         {
//             if (buildOption == null) return;
//             BuildingPlacer.Instance.StartPlacing(buildOption);
//         }
//     }
//
//     void UpdateAffordability(FactionInstance f)
//     {
//         if (f != GameManager.Instance.PlayerFaction) return;
//         if (mode != ButtonMode.Production || productionOption == null) return; // removed activeSelf check
//         if (GetComponent<CanvasGroup>().alpha == 0f) return; // hidden check
//         canAfford = GameManager.Instance.CanAfford(productionOption.cost, GameManager.Instance.PlayerFaction);
//         button.image.color = canAfford ? affordableColor : unaffordableColor;
//     }
//     
//     
// }
//
//
//
// // public void ShowSubmenu(CommandSubmenu type, UnitController unit)
// // {
// //     switch (type)
// //     {
// //         case CommandSubmenu.Build:
// //             actionPanelUI.ShowBuildSubmenu(unit);
// //             PlayerInputHandler.Instance.SetBuildSubmenuActive(true);
// //             break;
// //         // Future:
// //         // case CommandSubmenu.Stance: actionPanelUI.ShowStanceSubmenu(unit); break;
// //         // case CommandSubmenu.Formation: actionPanelUI.ShowFormationSubmenu(unit); break;
// //     }
// // }