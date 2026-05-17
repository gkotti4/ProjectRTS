using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Serialization;

[RequireComponent(typeof(Button))]


/*
 * Action Panel:
 *  - Handles bottom left panel of screen when selecting either a Unit or Building (Entity)
 * 
 *  - For Units:
 *   - Show Commands
 * 
 *  - For Buildings:
 *   - Show ProductionOptions
 */

public class ActionButtonUI : MonoBehaviour
{
    // Data
    private ProductionOptionData productionOption; // Building
    private CommandData commandData; // Unit
    private BuildOptionData buildOption; // Villager
    private BuildingController targetBuilding;
    private UnitController targetUnit;

    // UI refs
    private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private TextMeshProUGUI hotkeyLabel;
    
    [SerializeField] private Color affordableColor = Color.lightBlue; // Production button
    [SerializeField] private Color unaffordableColor = Color.red; // Production button

    private bool canAfford = false;
    private enum ButtonMode { Production, Command, Build } // Based on selection, Action panel can contain one of these types of buttons
    private ButtonMode mode;

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

    // Initialize for building production option
    public void InitializeFromProductionOption(ProductionOptionData option, BuildingController building)
    {
        mode = ButtonMode.Production;
        productionOption = option;
        commandData = null;
        buildOption = null;
        targetBuilding = building;
        targetUnit = null;

        if (iconImage != null)
            iconImage.sprite = option.icon != null ? option.icon : null;

        if (label != null)
            label.text = option.productionName;
        
        if (hotkeyLabel != null)
            hotkeyLabel.text = option.hotkey.ToString();

        canAfford = GameManager.Instance.CanAfford(productionOption.cost, GameManager.Instance.PlayerFaction);
        button.image.color = canAfford ? affordableColor : unaffordableColor;
    }

    // Initialize for unit command
    public void InitializeFromCommand(CommandData cmd, UnitController unit)
    {
        if (cmd == null) return;
        mode = ButtonMode.Command;
        commandData = cmd;
        productionOption = null;
        buildOption = null;
        targetUnit = unit;
        targetBuilding = null;

        if (iconImage != null && cmd.icon != null)
            iconImage.sprite = cmd.icon;

        if (label != null)
            label.text = cmd.commandName;
        
        if (hotkeyLabel != null)
            hotkeyLabel.text = cmd.hotkey.ToString();

        button.image.color = affordableColor;
    }

    public void InitializeFromBuildOption(BuildOptionData option, UnitController unit)
    {
        if (option == null) return;
        mode = ButtonMode.Build;
        buildOption = option;
        productionOption = null;
        commandData = null;
        targetUnit = unit;
        targetBuilding = null;
        
        if (iconImage != null && option.icon != null)
            iconImage.sprite = option.icon;

        if (label != null)
            label.text = option.buildingName;
        
        if (hotkeyLabel != null)
            hotkeyLabel.text = option.hotkey.ToString();
        
        button.image.color = affordableColor;

    }

    void OnClick()
    {
        if (mode == ButtonMode.Production)
        {
            if (targetBuilding == null || productionOption == null) return;
            if (canAfford)
                targetBuilding.EnqueueProduction(productionOption);
        }
        else if (mode == ButtonMode.Command)
        {
            if (targetUnit == null || commandData == null) return;
            if (commandData.commandType == CommandType.Build) // Build command - access build submenu
            {
                UIManager.Instance.ShowActionPanelBuildSubmenu(targetUnit);
            }
            else if (targetUnit.TryGetComponent(out CommandController cc)) // Execute regular command
            {
                cc.ExecuteCommand(commandData.commandType);
            }
        }
        else if (mode == ButtonMode.Build)
        {
            if (buildOption == null) return;
            BuildingPlacer.Instance.StartPlacing(buildOption);
        }
    }

    void UpdateAffordability(FactionInstance f)
    {
        if (f != GameManager.Instance.PlayerFaction) return;
        if (mode != ButtonMode.Production || productionOption == null) return; // removed activeSelf check
        if (GetComponent<CanvasGroup>().alpha == 0f) return; // hidden check
        canAfford = GameManager.Instance.CanAfford(productionOption.cost, GameManager.Instance.PlayerFaction);
        button.image.color = canAfford ? affordableColor : unaffordableColor;
    }
    
    
}



// public void ShowSubmenu(CommandSubmenu type, UnitController unit)
// {
//     switch (type)
//     {
//         case CommandSubmenu.Build:
//             actionPanelUI.ShowBuildSubmenu(unit);
//             PlayerInputHandler.Instance.SetBuildSubmenuActive(true);
//             break;
//         // Future:
//         // case CommandSubmenu.Stance: actionPanelUI.ShowStanceSubmenu(unit); break;
//         // case CommandSubmenu.Formation: actionPanelUI.ShowFormationSubmenu(unit); break;
//     }
// }