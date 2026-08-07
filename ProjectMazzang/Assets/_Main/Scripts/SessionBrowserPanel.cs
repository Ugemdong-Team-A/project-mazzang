using System;
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SessionBrowserPanel : MonoBehaviour
{
    [Header("Create Room")]
    [SerializeField]
    private TMP_InputField roomNameInput;

    [SerializeField]
    private Button createRoomButton;

    [Header("Session List")]
    [SerializeField]
    private Transform sessionListRoot;

    [SerializeField]
    private SessionRow sessionRowPrefab;

    [SerializeField]
    private GameObject emptyListMessage;

    private readonly List<SessionRow> rows = new();

    public event Action<string> CreateRoomRequested;
    public event Action<string> JoinRoomRequested;

    private void Awake()
    {
        createRoomButton.onClick.AddListener(
            OnCreateRoomClicked);
    }

    private void OnDestroy()
    {
        createRoomButton.onClick.RemoveListener(
            OnCreateRoomClicked);
    }

    public void SetSessions(
        IReadOnlyList<SessionInfo> sessions)
    {
        ClearRows();

        if (sessions == null ||
            sessions.Count == 0)
        {
            emptyListMessage.SetActive(true);
            return;
        }

        emptyListMessage.SetActive(false);

        foreach (SessionInfo session in sessions)
        {
            if (session == null ||
                !session.IsValid)
            {
                continue;
            }

            SessionRow row = Instantiate(
                sessionRowPrefab,
                sessionListRoot);

            row.Bind(
                session,
                OnJoinRoomClicked);

            rows.Add(row);
        }

        emptyListMessage.SetActive(
            rows.Count == 0);
    }

    public void SetInteractable(bool interactable)
    {
        createRoomButton.interactable = interactable;
        roomNameInput.interactable = interactable;

        foreach (SessionRow row in rows)
        {
            row.SetInteractable(interactable);
        }
    }

    private void OnCreateRoomClicked()
    {
        string roomName =
            roomNameInput.text?.Trim();

        CreateRoomRequested?.Invoke(roomName);
    }

    private void OnJoinRoomClicked(
        string sessionName)
    {
        JoinRoomRequested?.Invoke(sessionName);
    }

    private void ClearRows()
    {
        foreach (SessionRow row in rows)
        {
            if (row != null)
                Destroy(row.gameObject);
        }

        rows.Clear();
    }
}