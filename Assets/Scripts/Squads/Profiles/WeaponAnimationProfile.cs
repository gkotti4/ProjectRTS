// using System.Collections.Generic;
// using UnityEngine;
//
// /// <summary>
// /// Weapon-owned animation package. It overrides weapon-specific placeholders in
// /// the soldier's base Animator Controller without replacing locomotion ownership.
// /// </summary>
// [CreateAssetMenu(
//     fileName = "WeaponAnimationProfile_",
//     menuName = "Scriptable Objects/Military/Animation/Weapon Animation Profile")]
// public class WeaponAnimationProfile : ScriptableObject
// {
//     [Header("Weapon Clip Overrides")]
//     [Tooltip("Weapon replacements are applied after soldier replacements, so weapon clips win when both target the same placeholder.")]
//     public List<AnimationClipReplacement> clipReplacements = new List<AnimationClipReplacement>();
//
//     [Header("Attack Variants")]
//     [Min(1)]
//     [Tooltip("Number of AttackVariant states authored in the base controller. SoldierAnimator chooses one before setting the Attack trigger.")]
//     public int attackVariantCount = 1;
//
//     [Header("Layer Behavior")]
//     [Tooltip("Keeps the current behavior where Attack temporarily disables the upper-body idle/walk layer so a full-body attack can play cleanly.")]
//     public bool disableUpperBodyLayerDuringAttack = true; // used?
//
//     void OnValidate()
//     {
//         attackVariantCount = Mathf.Max(1, attackVariantCount);
//     }
// }
//
//
//
// /// <summary>
// /// Maps one placeholder clip from the base Animator Controller to a replacement.
// /// References are used instead of names so renamed/localized display text does not
// /// affect runtime animation resolution.
// /// </summary>
// [System.Serializable]
// public struct AnimationClipReplacement
// {
//     public AnimationClip originalClip;
//     public AnimationClip replacementClip;
// }