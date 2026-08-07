using System;
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RoomLobbyPanel :
    MonoBehaviour
{
    [Header("Room")]
    [SerializeField]
    private TMP_Text roomNameText;

    [Header("Players")]
    [SerializeField]
    private Transform playerListRoot;

    [SerializeField]
    private PlayerListItem playerListItemPrefab;

    [SerializeField]
    private TMP_Text readySummaryText;

    [Header("Actions")]
    [SerializeField]
    private Button readyButton;

    [SerializeField]
    private TMP_Text readyButtonText;

    [SerializeField]
    private Button startButton;

    [SerializeField]
    private Button leaveButton;

    private readonly Dictionary<
        PlayerRef,
        PlayerListItem> playerItems = new();

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

    // ==================================================
    // Room
    // ==================================================

    public void SetRoomName(
        string roomName)
    {
        roomNameText.text =
            string.IsNullOrWhiteSpace(roomName)
                ? "-"
                : roomName;
    }

    // ==================================================
    // Players
    // ==================================================

    public void UpsertPlayer(
        PlayerRef player,
        string nickname,
        bool ready,
        bool isLocal)
    {
        if (!playerItems.TryGetValue(
                player,
                out PlayerListItem item))
        {
            item = Instantiate(
                playerListItemPrefab,
                playerListRoot);

            playerItems.Add(
                player,
                item);
        }

        item.SetView(
            nickname,
            ready,
            isLocal);
    }

    public void RemovePlayer(
        PlayerRef player)
    {
        if (!playerItems.TryGetValue(
                player,
                out PlayerListItem item))
        {
            return;
        }

        playerItems.Remove(player);

        if (item != null)
            Destroy(item.gameObject);
    }

    public void ClearPlayers()
    {
        foreach (PlayerListItem item
                 in playerItems.Values)
        {
            if (item != null)
                Destroy(item.gameObject);
        }

        playerItems.Clear();

        SetReadySummary(0, 0);
    }

    public void SetReadySummary(
        int readyCount,
        int playerCount)
    {
        readySummaryText.text =
            $"{readyCount} / {playerCount} READY";
    }

    // ==================================================
    // Ready
    // ==================================================

    public void SetLocalReadyState(
        bool ready)
    {
        readyButtonText.text =
            ready
                ? "준비 해제"
                : "준비";
    }

    public void SetReadyInteractable(
        bool interactable)
    {
        readyButton.interactable =
            interactable;
    }

    // ==================================================
    // Start
    // ==================================================

    public void SetStartVisible(
        bool visible)
    {
        startButton.gameObject.SetActive(
            visible);
    }

    public void SetStartInteractable(
        bool interactable)
    {
        startButton.interactable =
            interactable;
    }

    // ==================================================
    // Leave
    // ==================================================

    public void SetLeaveInteractable(
        bool interactable)
    {
        leaveButton.interactable =
            interactable;
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