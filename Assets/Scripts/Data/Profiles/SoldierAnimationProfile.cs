using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Soldier-owned animation package. The base controller and locomotion/general
/// overrides stay with the soldier, while the resolved weapon contributes its own
/// upper-body/combat overrides through WeaponAnimationProfile.
/// </summary>
[CreateAssetMenu(
    fileName = "SoldierAnimationProfile_",
    menuName = "Scriptable Objects/Military/Animation/Soldier Animation Profile")]
public class SoldierAnimationProfile : ScriptableObject
{
    [Header("Controller")]
    [Tooltip("Optional base controller. When null, SoldierAnimator keeps the controller authored on the prefab.")]
    public RuntimeAnimatorController baseController;

    [Header("Soldier / Locomotion Overrides")]
    [Tooltip("Replaces placeholder clips in the base controller. Use this for idle, walk, run, hit, death, and other soldier-owned animation sets.")]
    public List<AnimationClipReplacement> clipReplacements = new List<AnimationClipReplacement>();
}