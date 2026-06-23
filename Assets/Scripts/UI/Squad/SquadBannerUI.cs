using UnityEngine;
using UnityEngine.UI;

public class SquadBannerUI : MonoBehaviour
{
    
    [Header("References")]
    [SerializeField] private SquadController squad;

    [Header("3D Banner Model")]
    [SerializeField] private Renderer[] bannerRenderers;
    [SerializeField] private string[] colorPropertyNames =
    {
        "_BaseColor",
        "_Color",
        "_TintColor"
    };

    [Header("World UI")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Image manpowerFill;
    [SerializeField] private Image squadIcon;
    // [SerializeField] private Image bannerBackground;
    [SerializeField] private GameObject selectedHighlightRoot;
    [SerializeField] private GameObject hoverHighlightRoot;

    [Header("Billboard")]
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private bool lockVertical = true;

    [Header("Visibility")]
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private bool showWhenSelected = true;
    [SerializeField] private bool showWhenHovered = false;
    [SerializeField] private bool showWhenDamaged = false;
    [SerializeField] private float damagedHealthPercentThreshold = 0.99f;

    private bool isSelected = false;
    private bool isHovered = false;

    private Camera mainCamera;
    private MaterialPropertyBlock propertyBlock;

    
    void Awake()
    {
        if (squad == null)
            squad = GetComponentInParent<SquadController>();

        mainCamera = Camera.main;
        propertyBlock = new MaterialPropertyBlock();

        SetSelectedHighlighted(false);
        SetHoverHighlighted(false);
        RefreshVisibility();
    }

    void OnDestroy()
    {
        UnsubscribeHealth();
    }

    void LateUpdate()
    {
        UpdateBillboard();
    }

    public void Initialize(SquadController owner)
    {
        squad = owner;

        ApplyFactionVisuals();
        ApplySquadDataVisuals();

        SubscribeHealth();
        RefreshHealth();
        RefreshVisibility();
    }

    void SubscribeHealth()
    {
        if (squad == null || squad.Health == null)
            return;

        squad.Health.OnSquadHealthChanged -= HandleSquadHealthChanged;
        squad.Health.OnSquadHealthChanged += HandleSquadHealthChanged;
    }

    void UnsubscribeHealth()
    {
        if (squad == null || squad.Health == null)
            return;

        squad.Health.OnSquadHealthChanged -= HandleSquadHealthChanged;
    }


    void ApplyFactionVisuals()
    {
        if (squad == null || squad.Faction == null)
            return;

        TeamVisualSettings visuals = squad.Faction.Visuals;

        ApplyColorToBannerModel(visuals.bannerColor);

        // if (bannerBackground != null)
        //     bannerBackground.color = visuals.bannerColor;

        if (healthFill != null)
            healthFill.color = visuals.teamColor;

        // if (manpowerFill != null)
        //     manpowerFill.color = visuals.bannerColor;
    }

    void ApplySquadDataVisuals()
    {
        if (squad == null || squad.Data == null)
            return;

        if (squadIcon != null)
            squadIcon.sprite = squad.Data.squadIcon;
    }
    

    void HandleSquadHealthChanged(SquadHealth health)
    {
        RefreshHealth();
        RefreshVisibility();
    }

    void RefreshHealth()
    {
        if (squad == null || squad.Health == null)
            return;

        if (healthFill != null)
            healthFill.fillAmount = squad.Health.HealthPercent;

        if (manpowerFill != null)
            manpowerFill.fillAmount = squad.Health.ManpowerPercent;
    }

    void ApplyColorToBannerModel(Color color)
    {
        if (bannerRenderers == null)
            return;

        foreach (Renderer bannerRenderer in bannerRenderers)
        {
            if (bannerRenderer == null)
                continue;

            ApplyColorToRenderer(bannerRenderer, color);
        }
    }

    void ApplyColorToRenderer(Renderer targetRenderer, Color color)
    {
        if (targetRenderer == null)
            return;

        Material[] sharedMaterials = targetRenderer.sharedMaterials;

        for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
        {
            Material material = sharedMaterials[materialIndex];

            if (material == null)
                continue;

            int colorPropertyId = GetFirstSupportedColorProperty(material);

            if (colorPropertyId == -1)
                continue;

            targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);
            propertyBlock.SetColor(colorPropertyId, color);
            targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
        }
    }

    int GetFirstSupportedColorProperty(Material material)
    {
        foreach (string propertyName in colorPropertyNames)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                continue;

            if (!material.HasProperty(propertyName))
                continue;

            return Shader.PropertyToID(propertyName);
        }

        return -1;
    }

    void UpdateBillboard()
    {
        if (!faceCamera)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        Vector3 direction = transform.position - mainCamera.transform.position;

        if (lockVertical)
            direction.y = 0f;

        if (direction == Vector3.zero)
            return;

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    public void SetSelectedHighlighted(bool highlighted)
    {
        if (selectedHighlightRoot != null)
            selectedHighlightRoot.SetActive(highlighted);
    }

    public void SetHoverHighlighted(bool highlighted)
    {
        if (hoverHighlightRoot != null)
            hoverHighlightRoot.SetActive(highlighted);
    }
    
    
    public void SetSelected(bool selected)
    {
        isSelected = selected;

        SetSelectedHighlighted(selected);
        RefreshVisibility();
    }

    public void SetHovered(bool hovered)
    {
        isHovered = hovered;

        SetHoverHighlighted(hovered && !isSelected);
        RefreshVisibility();
    }

    void RefreshVisibility()
    {
        if (contentRoot == null)
            return;

        bool shouldShow = false;

        if (showWhenSelected && isSelected)
            shouldShow = true;

        if (showWhenHovered && isHovered)
            shouldShow = true;

        if (showWhenDamaged &&
            squad != null &&
            squad.Health != null &&
            squad.Health.HealthPercent < damagedHealthPercentThreshold)
        {
            shouldShow = true;
        }

        contentRoot.SetActive(shouldShow);
    }
    
}