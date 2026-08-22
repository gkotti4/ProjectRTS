using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Contracts content page.
/// Current prototype uses a left selectable list plus a right details panel.
/// Selection and details are intentionally separated so the list can later become
/// a bulletin-board selection view and the details can become a separate zoomed-in
/// contract-info view without changing Contract Mercenary gameplay rules.
/// </summary>
[DisallowMultipleComponent]
public class ContractMercenaryContractsPageUI : MonoBehaviour
{
    private ContractMercenaryController contractController;

    [Header("Contract Selection")]
    [SerializeField] private Transform contractListContainer;
    [SerializeField] private ContractMercenaryMenuButtonUI contractButtonPrefab;

    [Header("Selected Contract - Details")]
    [SerializeField] private TextMeshProUGUI contractNameText;
    [SerializeField] private TextMeshProUGUI contractMetaText;
    [SerializeField] private TextMeshProUGUI contractDescriptionText;
    [SerializeField] private TextMeshProUGUI contractRequirementsText;
    [SerializeField] private TextMeshProUGUI contractRewardsText;
    [SerializeField] private Button acceptContractButton;

    private readonly List<ContractMercenaryMenuButtonUI> spawnedButtons =
        new List<ContractMercenaryMenuButtonUI>();

    private ContractData selectedContract;

    void Awake()
    {
        acceptContractButton?.onClick.AddListener(HandleAcceptContract);
    }

    void OnDestroy()
    {
        acceptContractButton?.onClick.RemoveListener(HandleAcceptContract);
    }

    public void RefreshPage()
    {
        RebuildContractList();
        ResolveSelection();
        RefreshSelectedContract();
    }

    public void SelectContract(ContractData contract)
    {
        selectedContract = contract;
        RefreshSelectedContract();
    }

    /// <summary>
    /// Bound by ContractMercenaryMenuUI, the CompanyHub root.
    /// This page intentionally does not resolve the CM controller on its own.
    /// </summary>
    public void Initialize(ContractMercenaryController controller)
    {
        contractController = controller;
    }

    void ResolveSelection()
    {
        IReadOnlyList<ContractData> contracts =
            contractController != null
                ? contractController.AvailableContracts
                : null;

        if (contracts == null || contracts.Count == 0)
        {
            selectedContract = null;
            return;
        }

        if (selectedContract != null && ContainsContract(contracts, selectedContract))
            return;

        selectedContract = FirstValidContract(contracts);
    }

    void RebuildContractList()
    {
        ClearButtons();

        if (contractController == null ||
            contractListContainer == null ||
            contractButtonPrefab == null)
        {
            return;
        }

        IReadOnlyList<ContractData> contracts = contractController.AvailableContracts;
        ContractMercenaryRunState runState = contractController.RunState;

        for (int index = 0; index < contracts.Count; index++)
        {
            ContractData contract = contracts[index];

            if (contract == null)
                continue;

            ContractData capturedContract = contract;
            string label = BuildContractListLabel(contract, runState);

            ContractMercenaryMenuButtonUI button = Instantiate(
                contractButtonPrefab,
                contractListContainer);

            // Locked contracts remain selectable so the player can inspect why they
            // are locked. Only the Accept button is disabled.
            button.Initialize(
                label,
                () => SelectContract(capturedContract),
                interactable: true);

            spawnedButtons.Add(button);
        }
    }

    void RefreshSelectedContract()
    {
        ContractMercenaryRunState runState =
            contractController != null
                ? contractController.RunState
                : null;

        if (selectedContract == null)
        {
            SetText(contractNameText, "No Contract Selected");
            SetText(contractMetaText, string.Empty);
            SetText(contractDescriptionText, string.Empty);
            SetText(contractRequirementsText, string.Empty);
            SetText(contractRewardsText, string.Empty);

            if (acceptContractButton != null)
                acceptContractButton.interactable = false;

            return;
        }

        SetText(contractNameText, selectedContract.contractName);
        SetText(
            contractMetaText,
            $"Threat: {Mathf.Clamp(selectedContract.threatRating, 1, 5)}/5");

        SetText(
            contractDescriptionText,
            string.IsNullOrWhiteSpace(selectedContract.contractDescription)
                ? "No contract description authored yet."
                : selectedContract.contractDescription);

        SetText(
            contractRequirementsText,
            BuildRequirementsText(selectedContract, runState));

        SetText(
            contractRewardsText,
            BuildRewardsText(selectedContract));

        if (acceptContractButton != null)
        {
            acceptContractButton.interactable =
                contractController != null &&
                contractController.CanStartContract(selectedContract);
        }
    }

    void HandleAcceptContract()
    {
        if (contractController == null || selectedContract == null)
            return;

        contractController.StartContract(selectedContract);
    }

    static string BuildContractListLabel(
        ContractData contract,
        ContractMercenaryRunState runState)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(contract.contractName);
        builder.Append($"  |  Threat {Mathf.Clamp(contract.threatRating, 1, 5)}/5");

        if (runState != null && runState.IsContractCompleted(contract) && !contract.repeatable)
            builder.Append("  |  Completed");
        else if (runState != null && !runState.MeetsContractProgressionRequirements(contract))
            builder.Append("  |  Locked");

        return builder.ToString();
    }

    static string BuildRequirementsText(
        ContractData contract,
        ContractMercenaryRunState runState)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Requirements");

        bool hasRequirement = false;

        if (contract.minimumPrestige > 0)
        {
            builder.AppendLine($"Prestige: {contract.minimumPrestige}");
            hasRequirement = true;
        }

        if (contract.requiredContracts != null)
        {
            for (int index = 0; index < contract.requiredContracts.Count; index++)
            {
                ContractData required = contract.requiredContracts[index];
                if (required == null) continue;

                bool completed = runState != null && runState.IsContractCompleted(required);
                builder.AppendLine($"{(completed ? "Complete" : "Required")}: {required.contractName}");
                hasRequirement = true;
            }
        }

        if (!contract.repeatable && runState != null && runState.IsContractCompleted(contract))
        {
            builder.AppendLine("Already completed");
            hasRequirement = true;
        }

        if (!hasRequirement)
            builder.Append("None");

        return builder.ToString().TrimEnd();
    }

    static string BuildRewardsText(ContractData contract)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Rewards");

        bool hasReward = false;

        if (contract.rewards != null)
        {
            for (int index = 0; index < contract.rewards.Count; index++)
            {
                ContractMercenaryResourceAmount reward = contract.rewards[index];
                if (reward == null || reward.amount <= 0) continue;

                builder.AppendLine($"+{reward.amount} {reward.resourceType}");
                hasReward = true;
            }
        }

        if (contract.prestigeReward > 0)
        {
            builder.AppendLine($"+{contract.prestigeReward} Prestige");
            hasReward = true;
        }

        if (!hasReward)
            builder.Append("None");

        return builder.ToString().TrimEnd();
    }

    static bool ContainsContract(
        IReadOnlyList<ContractData> contracts,
        ContractData target)
    {
        for (int index = 0; index < contracts.Count; index++)
        {
            if (contracts[index] == target)
                return true;
        }

        return false;
    }

    static ContractData FirstValidContract(IReadOnlyList<ContractData> contracts)
    {
        for (int index = 0; index < contracts.Count; index++)
        {
            if (contracts[index] != null)
                return contracts[index];
        }

        return null;
    }

    void ClearButtons()
    {
        for (int index = 0; index < spawnedButtons.Count; index++)
        {
            if (spawnedButtons[index] != null)
                Destroy(spawnedButtons[index].gameObject);
        }

        spawnedButtons.Clear();
    }

    static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }
}
