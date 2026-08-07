using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LobbyUI : MonoBehaviour
{
    [Header("Main Panels")]
    [SerializeField]
    private SessionBrowserPanel browserPanel;

    [SerializeField]
    private RoomLobbyPanel roomPanel;

    [Header("Loading Overlay")]
    [SerializeField]
    private GameObject loadingOverlay;

    [SerializeField]
    private TMP_Text loadingText;

    [Header("Error Popup")]
    [SerializeField]
    private GameObject errorPopup;

    [SerializeField]
    private TMP_Text errorText;

    [SerializeField]
    private Button retryButton;

    [SerializeField]
    private Button closeErrorButton;

    public SessionBrowserPanel Browser =>
        browserPanel;

    public RoomLobbyPanel Room =>
        roomPanel;

    public event Action RetryRequested;

    private void Awake()
    {
        retryButton.onClick.AddListener(
            OnRetryClicked);

        closeErrorButton.onClick.AddListener(
            HideError);

        loadingOverlay.SetActive(false);
        errorPopup.SetActive(false);
    }

    private void OnDestroy()
    {
        retryButton.onClick.RemoveListener(
            OnRetryClicked);

        closeErrorButton.onClick.RemoveListener(
            HideError);
    }

    public void ShowBrowser()
    {
        browserPanel.gameObject.SetActive(true);
        roomPanel.gameObject.SetActive(false);
    }

    public void ShowRoom()
    {
        browserPanel.gameObject.SetActive(false);
        roomPanel.gameObject.SetActive(true);
    }

    public void ShowLoading(string message)
    {
        loadingText.text = message;
        loadingOverlay.SetActive(true);
    }

    public void HideLoading()
    {
        loadingOverlay.SetActive(false);
    }

    public void ShowError(
        string message,
        bool allowRetry)
    {
        errorText.text = message;

        retryButton.gameObject.SetActive(
            allowRetry);

        errorPopup.SetActive(true);
    }

    public void HideError()
    {
        errorPopup.SetActive(false);
    }

    private void OnRetryClicked()
    {
        HideError();

        RetryRequested?.Invoke();
    }
}