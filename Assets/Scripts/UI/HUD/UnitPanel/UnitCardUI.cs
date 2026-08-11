using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// One battle-HUD card representing one player squad.
///
/// Responsibilities:
/// - displays squad identity, health/manpower, and ranged ammunition
/// - mirrors current SelectionManager selection state
/// - normal click selects only this squad
/// - Shift-click toggles this squad in the current selection
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UnitCardUI : MonoBehaviour
{
    #region UI References

    [Header("Card")]
    [SerializeField] private Button cardButton;
    [SerializeField] private GameObject selectedVisual;

    [FormerlySerializedAs("squadIconImage")]
    [Header("Identity")]
    [SerializeField] private Image squadPortraitImage;
    [SerializeField] private TextMeshProUGUI squadNameText;

    [FormerlySerializedAs("healthFillImage")]
    [Header("Health / Manpower")]
    [Tooltip("Image should use Filled image type. Fill amount is driven from SquadHealth.HealthPercent.")]
    [SerializeField] private TextMeshProUGUI healthPercentText;
    [SerializeField] private TextMeshProUGUI manpowerText;

    [FormerlySerializedAs("rangedAmmunitionGroup")]
    [Header("Ranged Ammunition")]
    [SerializeField] private GameObject rangedAmmunitionSection;
    [SerializeField] private TextMeshProUGUI rangedAmmunitionText;

    #endregion

    #region Runtime

    private SquadController squad;
    private SquadHealth squadHealth;
    private int displayedRangedAmmunition = int.MinValue;
    private int displayedMaximumRangedAmmunition = int.MinValue;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (cardButton == null)
            cardButton = GetComponent<Button>();

        cardButton.onClick.AddListener(HandleCardClicked);
    }

    void Update()
    {
        // Squad ammo currently has no change event. This tiny comparison is only
        // performed by visible battle cards and updates the label only when needed.
        RefreshRangedAmmunitionIfChanged();
    }

    void OnDestroy()
    {
        UnbindSquad();

        if (cardButton != null)
            cardButton.onClick.RemoveListener(HandleCardClicked);
    }

    #endregion

    #region Public API

    public SquadController Squad => squad;
    public SquadCategory Category =>
        squad != null ? squad.Category : SquadCategory.Infantry;

    public void Initialize(SquadController targetSquad)
    {
        UnbindSquad();

        squad = targetSquad;
        squadHealth = squad != null ? squad.Health : null;

        if (squadHealth != null)
            squadHealth.OnSquadHealthChanged += HandleSquadHealthChanged;

        GameEvents.OnSelectionChanged += HandleSelectionChanged;

        RefreshIdentity();
        RefreshHealth();
        ForceRefreshRangedAmmunition();
        RefreshSelectedVisual();
    }

    public void SetFilterVisible(bool isVisible)
    {
        gameObject.SetActive(isVisible);
    }

    #endregion

    #region Selection

    void HandleCardClicked()
    {
        if (squad == null || SelectionManager.Instance == null)
            return;

        bool additiveSelection =
            Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);

        if (additiveSelection)
        {
            SelectionManager.Instance.ToggleSelectExternal(squad);
            return;
        }

        SelectionManager.Instance.DeselectAll();
        SelectionManager.Instance.SelectExternal(squad);
    }

    void HandleSelectionChanged()
    {
        RefreshSelectedVisual();
    }

    void RefreshSelectedVisual()
    {
        if (selectedVisual == null)
            return;

        bool isSelected = false;

        if (squad != null && SelectionManager.Instance != null)
        {
            var selectedObjects = SelectionManager.Instance.GetSelectedObjects();

            for (int index = 0; index < selectedObjects.Count; index++)
            {
                if (selectedObjects[index] == squad)
                {
                    isSelected = true;
                    break;
                }
            }
        }

        selectedVisual.SetActive(isSelected);
    }

    #endregion

    #region Visual Refresh

    void RefreshIdentity()
    {
        if (squad == null)
            return;

        SquadData squadData = squad.Data;

        if (squadPortraitImage != null)
        {
            squadPortraitImage.sprite = squadData != null
                ? squadData.squadIcon
                : null;

            squadPortraitImage.enabled = squadPortraitImage.sprite != null;
        }

        if (squadNameText != null)
        {
            squadNameText.text = squadData != null
                ? squadData.squadName
                : squad.name;
        }
    }

    void HandleSquadHealthChanged(SquadHealth changedHealth)
    {
        if (changedHealth != squadHealth)
            return;

        RefreshHealth();
    }

    void RefreshHealth()
    {
        if (squadHealth == null)
            return;

        if (healthPercentText != null)
            healthPercentText.text = $"{Math.Round(squadHealth.HealthPercent * 100, 0)}% <3";
            // healthFillImage.fillAmount = squadHealth.HealthPercent;

        if (manpowerText != null)
        {
            manpowerText.text =
                $"{squadHealth.LivingSoldiers}/{squadHealth.TotalSoldiers}";
        }
    }

    void ForceRefreshRangedAmmunition()
    {
        displayedRangedAmmunition = int.MinValue;
        displayedMaximumRangedAmmunition = int.MinValue;
        RefreshRangedAmmunitionIfChanged();
    }

    void RefreshRangedAmmunitionIfChanged()
    {
        if (squad == null || squad.Combat == null)
        {
            if (rangedAmmunitionSection != null)
                rangedAmmunitionSection.SetActive(false);

            return;
        }

        int currentAmmunition = squad.Combat.CurrentRangedAmmunition;
        int maximumAmmunition = squad.Combat.MaximumRangedAmmunition;
        bool hasRangedAmmunitionPool =
            squad.Combat.HasUnlimitedRangedAmmunition ||
            maximumAmmunition > 0;

        if (rangedAmmunitionSection != null)
            rangedAmmunitionSection.SetActive(hasRangedAmmunitionPool);

        if (!hasRangedAmmunitionPool || rangedAmmunitionText == null)
            return;

        if (currentAmmunition == displayedRangedAmmunition &&
            maximumAmmunition == displayedMaximumRangedAmmunition)
        {
            return;
        }

        displayedRangedAmmunition = currentAmmunition;
        displayedMaximumRangedAmmunition = maximumAmmunition;

        rangedAmmunitionText.text = squad.Combat.HasUnlimitedRangedAmmunition
            ? "∞"
            : $"{currentAmmunition}/{maximumAmmunition}";
    }

    #endregion

    #region Cleanup

    void UnbindSquad()
    {
        if (squadHealth != null)
            squadHealth.OnSquadHealthChanged -= HandleSquadHealthChanged;

        GameEvents.OnSelectionChanged -= HandleSelectionChanged;

        squad = null;
        squadHealth = null;
    }

    #endregion
}
