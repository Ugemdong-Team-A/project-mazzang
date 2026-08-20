using TMPro;
using UnityEngine;

public sealed class LobbyUI : MonoBehaviour
{
    [Header("Main Panels")]
    [SerializeField]
    private LobbyTitlePanel titlePanel;

    [SerializeField]
    private SessionBrowserPanel browserPanel;

    [SerializeField]
    private RoomLobbyPanel roomPanel;

    [Header("Loading Overlay")]
    [SerializeField]
    private GameObject loadingOverlay;

    [SerializeField]
    private TMP_Text loadingText;

    public LobbyTitlePanel Title =>
        titlePanel;

    public SessionBrowserPanel Browser =>
        browserPanel;

    public RoomLobbyPanel Room =>
        roomPanel;

    private void Awake()
    {
        loadingOverlay.SetActive(false);
    }

    public void ShowTitle()
    {
        titlePanel.gameObject.SetActive(true);
        browserPanel.gameObject.SetActive(false);
        roomPanel.gameObject.SetActive(false);
    }

    public void ShowBrowser()
    {
        titlePanel.gameObject.SetActive(false);
        browserPanel.gameObject.SetActive(true);
        roomPanel.gameObject.SetActive(false);
    }

    public void ShowRoom()
    {
        titlePanel.gameObject.SetActive(false);
        browserPanel.gameObject.SetActive(false);
        roomPanel.gameObject.SetActive(true);
    }

    public void ShowLoading(
        string message)
    {
        loadingText.text = message;
        loadingOverlay.SetActive(true);
    }

    public void HideLoading()
    {
        loadingOverlay.SetActive(false);
    }
}