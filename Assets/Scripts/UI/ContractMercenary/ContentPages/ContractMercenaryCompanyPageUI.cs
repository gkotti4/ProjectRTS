using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Company-wide progression page. For the current prototype it presents only state
/// that really exists: Prestige, completed contracts, company army size, and the
/// authored contract unlocks currently gated by Prestige/progression requirements.
/// Employer/reputation systems can expand this page later without moving squad-level
/// progression out of Army.
/// </summary>
[DisallowMultipleComponent]
public class ContractMercenaryCompanyPageUI : MonoBehaviour
{
    private ContractMercenaryController contractController;

    [Header("Company Summary")]
    [SerializeField] private TextMeshProUGUI companyNameText;
    [SerializeField] private TextMeshProUGUI companyStatsText;

    [Header("Progression / Unlocks")]
    [SerializeField] private TextMeshProUGUI prestigeProgressText;
    [SerializeField] private TextMeshProUGUI contractUnlocksText;

    [Header("Presentation")]
    [SerializeField] private string defaultCompanyName = "Mercenary Company";

    public void RefreshPage()
    {

        ContractMercenaryRunState runState =
            contractController != null
                ? contractController.RunState
                : null;

        SetText(companyNameText, defaultCompanyName);

        if (runState == null)
        {
            SetText(companyStatsText, "No active company run.");
            SetText(prestigeProgressText, string.Empty);
            SetText(contractUnlocksText, string.Empty);
            return;
        }

        SetText(
            companyStatsText,
            $"Prestige: {runState.Prestige}\n" +
            $"Contracts Completed: {runState.CompletedContractCount}\n" +
            $"Owned Squads: {runState.Army.Count}\n" +
            $"Current Manpower: {GetCurrentManpower(runState)}");

        SetText(
            prestigeProgressText,
            BuildPrestigeProgressText(runState));

        SetText(
            contractUnlocksText,
            BuildContractUnlockText(runState));
    }

    /// <summary>
    /// Bound by ContractMercenaryMenuUI, the CompanyHub root.
    /// This page intentionally does not resolve the CM controller on its own.
    /// </summary>
    public void Initialize(ContractMercenaryController controller)
    {
        contractController = controller;
    }

    string BuildPrestigeProgressText(ContractMercenaryRunState runState)
    {
        int nextPrestige = int.MaxValue;
        string nextUnlockName = null;

        if (contractController != null)
        {
            for (int index = 0; index < contractController.AvailableContracts.Count; index++)
            {
                ContractData contract = contractController.AvailableContracts[index];
                if (contract == null) continue;

                int requiredPrestige = Mathf.Max(0, contract.minimumPrestige);

                if (requiredPrestige <= runState.Prestige || requiredPrestige >= nextPrestige)
                    continue;

                nextPrestige = requiredPrestige;
                nextUnlockName = contract.contractName;
            }
        }

        if (nextUnlockName == null)
            return $"Prestige: {runState.Prestige}\nNo higher Prestige-gated contract is currently authored.";

        return
            $"Prestige: {runState.Prestige}\n" +
            $"Next authored Prestige unlock: {nextUnlockName} at {nextPrestige}";
    }

    string BuildContractUnlockText(ContractMercenaryRunState runState)
    {
        if (contractController == null || contractController.AvailableContracts.Count == 0)
            return "No contract unlocks authored yet.";

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Contract Access");

        for (int index = 0; index < contractController.AvailableContracts.Count; index++)
        {
            ContractData contract = contractController.AvailableContracts[index];
            if (contract == null) continue;

            bool completed = runState.IsContractCompleted(contract);
            bool unlocked = runState.MeetsContractProgressionRequirements(contract);

            string state = completed
                ? "Completed"
                : unlocked
                    ? "Available"
                    : "Locked";

            builder.Append(contract.contractName);
            builder.Append(" - ");
            builder.Append(state);

            if (contract.minimumPrestige > 0)
                builder.Append($" (Prestige {contract.minimumPrestige})");

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    static int GetCurrentManpower(ContractMercenaryRunState runState)
    {
        int total = 0;

        for (int index = 0; index < runState.Army.Count; index++)
        {
            ContractMercenarySquadState squad = runState.Army[index];
            if (squad != null)
                total += Mathf.Max(0, squad.currentSoldierCount);
        }

        return total;
    }

    static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }
}
