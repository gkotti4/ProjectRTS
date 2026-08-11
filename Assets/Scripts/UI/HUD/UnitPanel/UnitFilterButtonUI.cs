using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One button in UnitFilterPanelUI.
/// Configure categoryFilter in the Inspector for category buttons.
/// The All button ignores categoryFilter.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UnitFilterButtonUI : MonoBehaviour
{
    #region UI References

    [Header("Button")]
    [SerializeField] private Button filterButton;
    [SerializeField] private GameObject selectedVisual;

    [Header("Category")]
    [SerializeField] private SquadCategory categoryFilter = SquadCategory.Infantry;

    [Header("Optional Text")]
    [SerializeField] private TextMeshProUGUI countText;

    #endregion

    #region Runtime

    private UnitFilterPanelUI filterPanel;
    private bool isAllUnitsButton = false;

    public SquadCategory Category => categoryFilter;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (filterButton == null)
            filterButton = GetComponent<Button>();

        filterButton.onClick.AddListener(HandleClicked);
    }

    void OnDestroy()
    {
        if (filterButton != null)
            filterButton.onClick.RemoveListener(HandleClicked);
    }

    #endregion

    #region Public API

    public void InitializeAll(UnitFilterPanelUI owner)
    {
        filterPanel = owner;
        isAllUnitsButton = true;
    }

    public void InitializeCategory(UnitFilterPanelUI owner)
    {
        filterPanel = owner;
        isAllUnitsButton = false;
    }

    public void RefreshVisuals(bool isActiveFilter, int unitCount)
    {
        if (selectedVisual != null)
            selectedVisual.SetActive(isActiveFilter);

        if (countText != null)
            countText.text = Mathf.Max(0, unitCount).ToString();

        if (filterButton != null)
            filterButton.interactable = isAllUnitsButton || unitCount > 0;
    }

    #endregion

    #region Click

    void HandleClicked()
    {
        if (filterPanel == null)
            return;

        if (isAllUnitsButton)
        {
            filterPanel.RequestShowAll();
            return;
        }

        filterPanel.RequestShowCategory(categoryFilter);
    }

    #endregion
}
