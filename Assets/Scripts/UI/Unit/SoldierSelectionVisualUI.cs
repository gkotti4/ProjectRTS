using UnityEngine;
using UnityEngine.UI;

public class SoldierSelectionVisualUI : MonoBehaviour
{
    [Header("Visual Roots")]
    [SerializeField] private GameObject selectionRoot;
    [SerializeField] private GameObject hoverRoot;

    void Awake()
    {
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
    }

    public void SetSelected(bool selected)
    {
        if (selectionRoot != null)
            selectionRoot.SetActive(selected);
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
}