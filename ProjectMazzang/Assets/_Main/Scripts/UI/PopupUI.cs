using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PopupUI : MonoBehaviour
{
    [Header("Message")]
    [SerializeField]
    private TMP_Text messageText;

    [Header("Primary Action")]
    [SerializeField]
    private Button primaryButton;

    [SerializeField]
    private TMP_Text primaryButtonText;

    [Header("Close")]
    [SerializeField]
    private Button closeButton;

    private Action primaryAction;

    private void Awake()
    {
        primaryButton.onClick.AddListener(
            OnPrimaryClicked);

        closeButton.onClick.AddListener(
            OnCloseClicked);

        Hide();
    }

    private void OnDestroy()
    {
        primaryButton.onClick.RemoveListener(
            OnPrimaryClicked);

        closeButton.onClick.RemoveListener(
            OnCloseClicked);
    }

    public void Show(
        string message,
        string primaryLabel = "»Æ¿Œ",
        Action onPrimary = null,
        bool allowClose = true)
    {
        messageText.text =
            message ?? string.Empty;

        primaryButtonText.text =
            primaryLabel;

        primaryAction =
            onPrimary;

        closeButton.gameObject.SetActive(
            allowClose);

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        primaryAction = null;

        gameObject.SetActive(false);
    }

    private void OnPrimaryClicked()
    {
        Action action =
            primaryAction;

        Hide();

        action?.Invoke();
    }

    private void OnCloseClicked()
    {
        Hide();
    }
}