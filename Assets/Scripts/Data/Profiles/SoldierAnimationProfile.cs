// using UnityEngine;
//
// /// <summary>
// /// Defines the base Animator Controller used by a compatible soldier body/rig.
// /// Active weapons may replace its clips through a pre-authored
// /// AnimatorOverrideController stored on WeaponProfile.
// /// </summary>
// [CreateAssetMenu(
//     fileName = "SoldierAnimationProfile_",
//     menuName = "Scriptable Objects/Military/Animation/Soldier Animation Profile")]
// public class SoldierAnimationProfile : ScriptableObject
// {
//     [Header("Controller")]
//     [Tooltip("Compatible base controller for this soldier body/rig. When null, SoldierAnimator uses the controller authored on the prefab.")]
//     public RuntimeAnimatorController baseController;
// }