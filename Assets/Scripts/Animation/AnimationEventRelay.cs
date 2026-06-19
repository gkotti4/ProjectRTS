using UnityEngine;

[RequireComponent(typeof(Animator))]

public class AnimationEventRelay : MonoBehaviour
{
    private UnitAnimator unitAnimator;
    
    void Awake()
    {
        unitAnimator = GetComponentInParent<UnitAnimator>();
        if (unitAnimator == null) Debug.LogError("UnitAnimator component not found on: " + gameObject.name);
    }

    // Animation relays, triggers animation Events in the unit Animator which can then communicate with the Controller
    void OnAttackImpact()
    {
        unitAnimator?.OnAttackImpact();
    }

    void OnAttackEnd()
    {
        unitAnimator?.OnAttackEnd();
    }
}
