using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(EntityStats))]

public abstract class EntityController : MonoBehaviour, ISelectable, IDamageable
{
    [SerializeField] protected DecalProjector selectionDecal;
    protected HealthBarUI healthBar;

    protected EntityStats stats;
    public EntityStats Stats => stats;
    protected bool isSelected;
    public bool IsSelected => isSelected;
    
    // Control Groups
    public int controlGroup = -1;

    // IDamageable
    public virtual void TakeDamage(int damage) => stats.TakeDamage(damage);

    // ISelectable
    public virtual void OnSelect()
    {
        isSelected = true;
        if (selectionDecal) selectionDecal.enabled = true;
        if (healthBar != null)
            healthBar.OnSelected();
    }
    public virtual void OnDeselect()
    {
        isSelected = false;
        if (selectionDecal) selectionDecal.enabled = false;
        if (healthBar != null)
            healthBar.OnDeselected();
    }

    public virtual bool IsDragSelectable => false;

    public GameObject GetGameObject()
    {
        if (gameObject == null) return null;
        return gameObject;
    }
    
    public virtual void OnHoverEnter()
    {
        if (selectionDecal != null)
            selectionDecal.enabled = true;

        if (healthBar != null)
            healthBar.Show();
    }

    public virtual void OnHoverExit()
    {
        // Do not remove selected visuals just because hover ended.
        if (isSelected)
            return;

        if (selectionDecal != null)
            selectionDecal.enabled = false;

        if (healthBar != null && !healthBar.IsSelected)
            healthBar.Hide();
    }

    protected virtual void Awake()
    {
        stats = GetComponent<EntityStats>();
        if (selectionDecal) selectionDecal.enabled = false;
        healthBar = GetComponentInChildren<HealthBarUI>();
    }

    protected virtual void Start()
    {
        SelectionManager.Instance.RegisterSelectable(this);
    }

    protected virtual void OnDestroy()
    {
        //SelectionManager.Instance.UnregisterSelectable(this); // called through EntityStats Die(), was needed for ghosts but made their own prefabs without controllers
    }
}