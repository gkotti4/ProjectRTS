using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(EntityStats))]

public abstract class EntityController : MonoBehaviour, ISelectable, IDamageable
{
    [SerializeField] protected DecalProjector selectionDecal;

    protected EntityStats stats;
    public EntityStats Stats => stats;
    protected bool isSelected;

    // IDamageable
    public virtual void TakeDamage(int damage) => stats.TakeDamage(damage);

    // ISelectable
    public virtual void OnSelect()
    {
        isSelected = true;
        if (selectionDecal) selectionDecal.enabled = true;
    }
    public virtual void OnDeselect()
    {
        isSelected = false;
        if (selectionDecal) selectionDecal.enabled = false;
    }

    public virtual bool IsDragSelectable => false;
    public GameObject GetGameObject() => gameObject;

    protected virtual void Awake()
    {
        stats = GetComponent<EntityStats>();
        if (selectionDecal) selectionDecal.enabled = false;
    }

    protected virtual void Start()
    {
        SelectionManager.Instance.RegisterSelectable(this);
    }

    protected virtual void OnDestroy()
    {
        SelectionManager.Instance.UnregisterSelectable(this);
    }
}