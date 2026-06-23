using UnityEngine;


/// -----------------------------------------------------------------------------
/// SelectionTarget
/// -----------------------------------------------------------------------------
///
/// Selection redirection component for child colliders or soldier objects.
/// Allows clicks on a soldier/child object to resolve to the owning selectable
/// target, usually the parent SquadController.
///
/// This supports the design rule that soldiers exist physically, but the player
/// selects and commands squads.
///
/// Design role:
/// Selection proxy from soldier/child collider to squad-level selectable.
///

/// Redirects selection from this GameObject to a runtime-assigned selectable target.
/// Used for soldiers/child colliders that should select their owning squad instead of themselves.

[DisallowMultipleComponent]
public class SelectionTarget : MonoBehaviour
{
    [SerializeField] bool autoFindTarget = true;
    // Runtime-only target. This is intentionally not serialized because proxies are assigned by code. 
    private ISelectable selectableTarget;

    public ISelectable Target => selectableTarget;


    void Awake()
    {
        if (!autoFindTarget) return;
        selectableTarget = GetComponentInParent<ISelectable>();
        if (selectableTarget == null)
        {
            Debug.LogError(name + " has no selectable target");
        }
    }
    
    /// Assigns the selectable object this proxy should redirect to.
    public void SetTarget(ISelectable target)
    {
        selectableTarget = target;
    }

    /// Attempts to resolve the assigned target as a valid selectable object.
    public bool TryGetTarget(out ISelectable selectable)
    {
        selectable = selectableTarget;
        return selectable != null && selectable.GetGameObject() != null;
    }
}