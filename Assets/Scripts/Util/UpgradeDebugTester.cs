using UnityEngine;

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporary runtime harness for testing UpgradeData assets.
///
/// Setup:
/// - Add this component to any active scene object.
/// - Populate testUpgrades in the Inspector.
/// - Press the configured keys during Play Mode.
///
/// This uses the normal GameManager/FactionInstance upgrade path, so existing
/// squads should receive the upgrade event and refresh their runtime stats.
/// </summary>
public class UpgradeDebugTester : MonoBehaviour
{
    #region Upgrade Setup

    [Header("Upgrade Setup")]
    [SerializeField] private List<UpgradeData> testUpgrades =
        new List<UpgradeData>();

    [Min(0)]
    [SerializeField] private int selectedUpgradeIndex = 0;

    [Tooltip("When enabled, upgrades are applied to the enemy faction instead of the player faction.")]
    [SerializeField] private bool targetEnemyFaction = false;

    #endregion

    #region Debug Hotkeys

    [Header("Debug Hotkeys")]
    [SerializeField] private KeyCode applySelectedUpgradeKey = KeyCode.U;
    [SerializeField] private KeyCode selectPreviousUpgradeKey = KeyCode.Minus;
    [SerializeField] private KeyCode selectNextUpgradeKey = KeyCode.Plus;
    [SerializeField] private KeyCode printUpgradeStateKey = KeyCode.P;

    #endregion

    #region Startup

    [Header("Startup")]
    [Tooltip("Applies the currently selected upgrade when the scene starts.")]
    [SerializeField] private bool applySelectedUpgradeOnStart = false;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        ClampSelectedUpgradeIndex();

        if (applySelectedUpgradeOnStart)
            ApplySelectedUpgrade();
        else
            PrintSelectedUpgrade();
    }

    void Update()
    {
        if (Input.GetKeyDown(selectPreviousUpgradeKey))
            SelectPreviousUpgrade();

        if (Input.GetKeyDown(selectNextUpgradeKey))
            SelectNextUpgrade();

        if (Input.GetKeyDown(applySelectedUpgradeKey))
            ApplySelectedUpgrade();

        if (Input.GetKeyDown(printUpgradeStateKey))
            PrintFactionUpgradeState();
    }

    void OnValidate()
    {
        ClampSelectedUpgradeIndex();
    }

    #endregion

    #region Upgrade Selection

    void SelectPreviousUpgrade()
    {
        if (testUpgrades == null || testUpgrades.Count == 0)
        {
            Debug.LogWarning(
                $"{name}: No UpgradeData assets are assigned.",
                this);

            return;
        }

        selectedUpgradeIndex--;

        if (selectedUpgradeIndex < 0)
            selectedUpgradeIndex = testUpgrades.Count - 1;

        PrintSelectedUpgrade();
    }

    void SelectNextUpgrade()
    {
        if (testUpgrades == null || testUpgrades.Count == 0)
        {
            Debug.LogWarning(
                $"{name}: No UpgradeData assets are assigned.",
                this);

            return;
        }

        selectedUpgradeIndex++;

        if (selectedUpgradeIndex >= testUpgrades.Count)
            selectedUpgradeIndex = 0;

        PrintSelectedUpgrade();
    }

    void ClampSelectedUpgradeIndex()
    {
        if (testUpgrades == null || testUpgrades.Count == 0)
        {
            selectedUpgradeIndex = 0;
            return;
        }

        selectedUpgradeIndex = Mathf.Clamp(
            selectedUpgradeIndex,
            0,
            testUpgrades.Count - 1);
    }

    UpgradeData GetSelectedUpgrade()
    {
        ClampSelectedUpgradeIndex();

        if (testUpgrades == null || testUpgrades.Count == 0)
            return null;

        return testUpgrades[selectedUpgradeIndex];
    }

    #endregion

    #region Upgrade Application

    public void ApplySelectedUpgrade()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError(
                $"{name}: GameManager.Instance is missing.",
                this);

            return;
        }

        UpgradeData selectedUpgrade = GetSelectedUpgrade();

        if (selectedUpgrade == null)
        {
            Debug.LogWarning(
                $"{name}: Selected upgrade is null or no upgrades are assigned.",
                this);

            return;
        }

        FactionInstance targetFaction = GetTargetFaction();

        if (targetFaction == null)
        {
            Debug.LogError(
                $"{name}: Target faction could not be resolved.",
                this);

            return;
        }

        int previousStackCount =
            targetFaction.GetUpgradeStackCount(selectedUpgrade);

        bool applied = GameManager.Instance.TryApplyFactionUpgrade(
            selectedUpgrade,
            targetFaction,
            UpgradeGrantSource.Debug);

        int newStackCount =
            targetFaction.GetUpgradeStackCount(selectedUpgrade);

        string factionName = targetFaction.baseData != null
            ? targetFaction.baseData.factionName
            : $"Faction {targetFaction.factionId}";

        if (applied)
        {
            Debug.Log(
                $"{name}: Applied upgrade '{GetUpgradeDisplayName(selectedUpgrade)}' " +
                $"to {factionName}. Stack {previousStackCount} -> {newStackCount}.",
                this);

            return;
        }

        Debug.LogWarning(
            $"{name}: Could not apply upgrade " +
            $"'{GetUpgradeDisplayName(selectedUpgrade)}' to {factionName}. " +
            $"Current stacks: {previousStackCount}. " +
            $"Check scope, prerequisites, blocked upgrades, repeatable, and maximum stacks.",
            this);
    }

    FactionInstance GetTargetFaction()
    {
        if (GameManager.Instance == null)
            return null;

        return targetEnemyFaction
            ? GameManager.Instance.EnemyFaction
            : GameManager.Instance.PlayerFaction;
    }

    #endregion

    #region Debug Output

    void PrintSelectedUpgrade()
    {
        UpgradeData selectedUpgrade = GetSelectedUpgrade();

        if (selectedUpgrade == null)
        {
            Debug.Log(
                $"{name}: No valid upgrade selected.",
                this);

            return;
        }

        Debug.Log(
            $"{name}: Selected upgrade [{selectedUpgradeIndex + 1}/{testUpgrades.Count}] " +
            $"'{GetUpgradeDisplayName(selectedUpgrade)}'. " +
            $"Press {applySelectedUpgradeKey} to apply.",
            this);
    }

    void PrintFactionUpgradeState()
    {
        FactionInstance targetFaction = GetTargetFaction();

        if (targetFaction == null)
        {
            Debug.LogWarning(
                $"{name}: No target faction is available.",
                this);

            return;
        }

        string factionName = targetFaction.baseData != null
            ? targetFaction.baseData.factionName
            : $"Faction {targetFaction.factionId}";

        Debug.Log(
            $"{name}: Applied upgrades for {factionName}: " +
            $"{targetFaction.AppliedUpgradeStacks.Count}",
            this);

        foreach (KeyValuePair<UpgradeData, int> pair
                 in targetFaction.AppliedUpgradeStacks)
        {
            UpgradeData upgrade = pair.Key;
            int stackCount = pair.Value;

            Debug.Log(
                $"Upgrade: {GetUpgradeDisplayName(upgrade)} | Stacks: {stackCount}",
                this);
        }
    }

    string GetUpgradeDisplayName(UpgradeData upgrade)
    {
        if (upgrade == null)
            return "Null Upgrade";

        if (!string.IsNullOrWhiteSpace(upgrade.upgradeName))
            return upgrade.upgradeName;

        return upgrade.name;
    }

    #endregion
}
