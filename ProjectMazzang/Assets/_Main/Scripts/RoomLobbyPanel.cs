using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RoomLobbyPanel : MonoBehaviour
{
    [Header("Room")]
    [SerializeField]
    private TMP_Text roomNameText;

    [Header("Players")]
    [SerializeField]
    private Transform playerListRoot;

    [Header("Actions")]
    [SerializeField]
    private Button readyButton;

    [SerializeField]
    private Button startButton;

    [SerializeField]
    private Button leaveButton;

    [SerializeField]
    private TMP_Text readyButtonText;

    public Transform PlayerListRoot =>
        playerListRoot;

    public event Action ReadyRequested;
    public event Action StartRequested;
    public event Action LeaveRequested;

    private void Awake()
    {
        readyButton.onClick.AddListener(
            OnReadyClicked);

        startButton.onClick.AddListener(
            OnStartClicked);

        leaveButton.onClick.AddListener(
            OnLeaveClicked);
    }

    private void OnDestroy()
    {
        readyButton.onClick.RemoveListener(
            OnReadyClicked);

        startButton.onClick.RemoveListener(
            OnStartClicked);

        leaveButton.onClick.RemoveListener(
            OnLeaveClicked);
    }

    public void SetRoomName(string roomName)
    {
        roomNameText.text =
            string.IsNullOrWhiteSpace(roomName)
                ? "-"
                : roomName;
    }

    public void SetReadyState(bool ready)
    {
        readyButtonText.text =
            ready ? "READY" : "READY?";
    }

    public void SetReadyInteractable(bool interactable)
    {
        readyButton.interactable = interactable;
    }

    public void SetStartVisible(bool visible)
    {
        startButton.gameObject.SetActive(visible);
    }

    public void SetStartInteractable(bool interactable)
    {
        startButton.interactable = interactable;
    }

    public void SetLeaveInteractable(bool interactable)
    {
        leaveButton.interactable = interactable;
    }

    private void OnReadyClicked()
    {
        ReadyRequested?.Invoke();
    }

    private void OnStartClicked()
    {
        StartRequested?.Invoke();
    }

    private void OnLeaveClicked()
    {
        LeaveRequested?.Invoke();
    }
}