using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation + click handling for one UpgradeData choice.
///
/// This card does not decide whether an upgrade is valid and does not apply it.
/// UpgradeSelectionPanelUI owns offer generation and reward application.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UpgradeCardUI : MonoBehaviour
{
    #region References

    [Header("Card")]
    [SerializeField] private Button cardButton;
    [SerializeField] private Image iconImage;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI stackText;

    #endregion

    #region Runtime

    private UpgradeData upgradeData;
    private Action<UpgradeData> onSelected;

    public UpgradeData UpgradeData => upgradeData;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (cardButton == null)
            cardButton = GetComponent<Button>();

        if (cardButton != null)
            cardButton.onClick.AddListener(HandleClicked);
    }

    void OnDestroy()
    {
        if (cardButton != null)
            cardButton.onClick.RemoveListener(HandleClicked);
    }

    #endregion

    #region Public API

    public void Initialize(
        UpgradeData upgrade,
        int currentStackCount,
        Action<UpgradeData> selectedCallback)
    {
        upgradeData = upgrade;
        onSelected = selectedCallback;

        if (upgradeData == null)
        {
            Clear();
            return;
        }

        if (nameText != null)
            nameText.text = upgradeData.upgradeName;

        if (descriptionText != null)
            descriptionText.text = upgradeData.description;

        if (iconImage != null)
        {
            iconImage.sprite = upgradeData.icon;
            iconImage.enabled = upgradeData.icon != null;
        }

        if (stackText != null)
        {
            stackText.text = upgradeData.repeatable
                ? $"{Mathf.Max(0, currentStackCount)} / {Mathf.Max(1, upgradeData.maximumStacks)}"
                : string.Empty;
        }

        if (cardButton != null)
            cardButton.interactable = true;
    }

    public void Clear()
    {
        upgradeData = null;
        onSelected = null;

        if (nameText != null)
            nameText.text = string.Empty;

        if (descriptionText != null)
            descriptionText.text = string.Empty;

        if (stackText != null)
            stackText.text = string.Empty;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (cardButton != null)
            cardButton.interactable = false;
    }

    #endregion

    #region Click

    void HandleClicked()
    {
        if (upgradeData == null || onSelected == null)
            return;

        onSelected.Invoke(upgradeData);
    }

    #endregion
}
