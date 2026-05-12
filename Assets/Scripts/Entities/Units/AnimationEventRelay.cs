using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    private UnitAnimator unitAnimator;
    
    void Awake()
    {
        unitAnimator = GetComponentInParent<UnitAnimator>();
        if (unitAnimator == null) Debug.LogError("UnitAnimator component not found on: " + gameObject.name);
    }

    void OnAttackImpact()
    {
        unitAnimator?.OnAttackImpact();
    }
}
