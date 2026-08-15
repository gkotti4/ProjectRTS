using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class ContractMercenaryMenuButtonUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;

    private Button button;
    private Action onClick;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);

        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>();
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    public void Initialize(
        string buttonLabel,
        Action clickAction,
        bool interactable = true)
    {
        onClick = clickAction;

        if (label != null)
            label.text = buttonLabel ?? string.Empty;

        if (button != null)
            button.interactable = interactable;
    }

    void HandleClick()
    {
        onClick?.Invoke();
    }
}