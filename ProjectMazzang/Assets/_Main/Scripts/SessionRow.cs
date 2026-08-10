using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SessionRow : MonoBehaviour
{
    [SerializeField]
    private TMP_Text roomNameText;

    [SerializeField]
    private TMP_Text playerCountText;

    [SerializeField]
    private Button joinButton;

    private string sessionName;

    private Action<string> joinRequested;

    private void Awake()
    {
        joinButton.onClick.AddListener(OnJoinClicked);
    }

    private void OnDestroy()
    {
        joinButton.onClick.RemoveListener(OnJoinClicked);
    }

    public void Bind(
        SessionInfo session,
        Action<string> onJoinRequested)
    {
        sessionName = session.Name;
        joinRequested = onJoinRequested;

        roomNameText.text = session.Name;

        playerCountText.text =
            $"{session.PlayerCount} / {session.MaxPlayers}";
    }

    public void SetInteractable(bool interactable)
    {
        joinButton.interactable = interactable;
    }

    private void OnJoinClicked()
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            return;

        joinRequested?.Invoke(sessionName);
    }
}