using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Health))]

public class BuildingController : MonoBehaviour, ISelectable, IDamageable
{
    public virtual void OnSelect() { isSelected = true; if (selectionDecal) selectionDecal.enabled = true; }
    public virtual void OnDeselect() { isSelected = false; if (selectionDecal) selectionDecal.enabled = false; }
    public GameObject GetGameObject() => gameObject;
    public bool IsBoxSelectable => false;
    
    public void TakeDamage(int damage){ health.TakeDamage(damage); }
    
    
    [SerializeField] protected BuildingSO buildingData;
    [SerializeField] private DecalProjector selectionDecal;
    protected Health health;
    protected bool isSelected;

    //protected Vector3 rallyPoint; // TODO
    
    protected virtual void Start()
    {
        if (buildingData == null)
        {
            Debug.LogError($"No UnitSO assigned on {gameObject.name}.");
            return;
        }
        if (selectionDecal == null)
        {
            Debug.LogError($"No Decal projector assigned on {gameObject.name}.");
            return;
        }
        
        gameObject.transform.rotation = Quaternion.Euler(0, 90, 0); // rotate 90 degrees so its facing our camera view better
        
        health = GetComponent<Health>();
        health.Initialize(buildingData.buildingHealth);
        
        isSelected = false;
        selectionDecal.enabled = false;
    }

    protected virtual void Update()
    {
        
    }
}
