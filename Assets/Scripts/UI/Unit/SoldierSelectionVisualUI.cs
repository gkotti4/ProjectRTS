using UnityEngine;
using UnityEngine.UI;

public class SoldierSelectionVisualUI : MonoBehaviour
{
    [Header("Visual Roots")]
    [SerializeField] private GameObject selectionRoot;
    [SerializeField] private GameObject hoverRoot;
    [SerializeField] private Outline outlineComponent;
    
    private MeshRenderer meshRenderer;

    void Awake()
    {
        if (outlineComponent != null)
        {
            outlineComponent.OutlineMode = Outline.Mode.OutlineVisible;
            outlineComponent.enabled = false;
        }    
        meshRenderer = GetComponentInParent<MeshRenderer>(); // CHECK NEW
        
        SetSelected(false);
        SetHovered(false);
        
    }

    public void ApplyColors(
        Color selectionColor,
        Color hoverColor)
    {
        ApplyColorToRoot(selectionRoot, selectionColor);

        if (hoverRoot != selectionRoot)
            ApplyColorToRoot(hoverRoot, hoverColor);
        
        // NEW Outline color
        ApplyColorToOutline(selectionColor);
    }

    public void SetSelected(bool selected)
    {
        if (selectionRoot != null)
            selectionRoot.SetActive(selected);
        
        if (outlineComponent != null && meshRenderer != null)
        {
            // Debug.Log("outlineComponent.enabled = " + selected);
            outlineComponent.enabled = selected; // Was throwing (tried to acess mesh renderer after component destroyed error, so added mr check)
        }
    }

    public void SetHovered(bool hovered)
    {
        if (hoverRoot != null)
            hoverRoot.SetActive(hovered);
        
    }

    void ApplyColorToRoot(GameObject root, Color color)
    {
        if (root == null)
            return;

        SpriteRenderer[] spriteRenderers =
            root.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
            spriteRenderer.color = color;

        Image[] images =
            root.GetComponentsInChildren<Image>(true);

        foreach (Image image in images)
            image.color = color;
    }

    void ApplyColorToOutline(Color color, float width = 2f)
    {
        if (!outlineComponent) return;
        
        outlineComponent.OutlineColor = color;
        outlineComponent.OutlineWidth = width;
    }
}