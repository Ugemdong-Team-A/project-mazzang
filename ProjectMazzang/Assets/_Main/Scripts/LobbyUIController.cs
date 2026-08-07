using System.Collections.Generic;
using Fusion;
using UnityEngine;

public sealed class LobbyUIController : MonoBehaviour
{
    [SerializeField]
    private LobbyUI ui;

    private FusionSessionController network;

    private void Start()
    {
        network = AppRoot.Instance.Network;

        Bind();

        // 이벤트를 먼저 연결한 뒤
        // 현재 Snapshot을 복원한다.
        RefreshFromCurrentState();

        // 처음 로비 씬에 왔다면
        // 자동으로 Photon Session Lobby에 접속.
        if (network.State ==
            NetworkSessionState.Offline)
        {
            _ = network.ConnectLobbyAsync();
        }
    }

    private void OnDestroy()
    {
        Unbind();
    }

    // ==================================================
    // Binding
    // ==================================================

    private void Bind()
    {
        // FSC
        network.StateChanged +=
            OnNetworkStateChanged;

        network.SessionListChanged +=
            OnSessionListChanged;

        network.OperationFailed +=
            OnNetworkOperationFailed;

        // Browser
        ui.Browser.CreateRoomRequested +=
            OnCreateRoomRequested;

        ui.Browser.JoinRoomRequested +=
            OnJoinRoomRequested;

        // Room
        ui.Room.ReadyRequested +=
            OnReadyRequested;

        ui.Room.StartRequested +=
            OnStartRequested;

        ui.Room.LeaveRequested +=
            OnLeaveRequested;

        // Error
        ui.RetryRequested +=
            OnRetryRequested;
    }

    private void Unbind()
    {
        if (network != null)
        {
            network.StateChanged -=
                OnNetworkStateChanged;

            network.SessionListChanged -=
                OnSessionListChanged;

            network.OperationFailed -=
                OnNetworkOperationFailed;
        }

        if (ui == null)
            return;

        ui.Browser.CreateRoomRequested -=
            OnCreateRoomRequested;

        ui.Browser.JoinRoomRequested -=
            OnJoinRoomRequested;

        ui.Room.ReadyRequested -=
            OnReadyRequested;

        ui.Room.StartRequested -=
            OnStartRequested;

        ui.Room.LeaveRequested -=
            OnLeaveRequested;

        ui.RetryRequested -=
            OnRetryRequested;
    }

    // ==================================================
    // Snapshot
    // ==================================================

    private void RefreshFromCurrentState()
    {
        ApplyNetworkState(network.State);

        ui.Browser.SetSessions(
            network.Sessions);
    }

    // ==================================================
    // FSC Events
    // ==================================================

    private void OnNetworkStateChanged(
        NetworkSessionState state)
    {
        ApplyNetworkState(state);
    }

    private void OnSessionListChanged(
        IReadOnlyList<SessionInfo> sessions)
    {
        ui.Browser.SetSessions(sessions);
    }

    private void OnNetworkOperationFailed(
        NetworkOperationFailure failure)
    {
        ui.HideLoading();

        string message =
            GetUserFriendlyError(failure);

        // Runner가 실패했다면 현재 Offline으로
        // 돌아왔으므로 다시 Lobby 연결 가능.
        bool allowRetry =
            network.State ==
            NetworkSessionState.Offline;

        ui.ShowError(
            message,
            allowRetry);
    }

    // ==================================================
    // State -> UI
    // ==================================================

    private void ApplyNetworkState(
        NetworkSessionState state)
    {
        switch (state)
        {
            case NetworkSessionState.Offline:
                {
                    ui.ShowBrowser();

                    ui.HideLoading();

                    ui.Browser.SetInteractable(false);

                    break;
                }

            case NetworkSessionState.LobbyConnecting:
                {
                    ui.ShowBrowser();

                    ui.Browser.SetInteractable(false);

                    ui.ShowLoading(
                        "온라인 로비에 연결 중...");

                    break;
                }

            case NetworkSessionState.LobbyReady:
                {
                    ui.ShowBrowser();

                    ui.HideLoading();

                    ui.Browser.SetInteractable(true);

                    break;
                }

            case NetworkSessionState.RoomConnecting:
                {
                    ui.Browser.SetInteractable(false);

                    ui.ShowLoading(
                        "방에 연결 중...");

                    break;
                }

            case NetworkSessionState.InRoom:
                {
                    ui.HideLoading();

                    ui.ShowRoom();

                    ui.Room.SetRoomName(
                        network.CurrentRoomName);

                    // NetworkGameSession / LobbyPlayer
                    // 연결 전까지는 잠시 비활성화.
                    ui.Room.SetReadyInteractable(false);
                    ui.Room.SetStartVisible(false);

                    ui.Room.SetLeaveInteractable(true);

                    break;
                }

            case NetworkSessionState.ShuttingDown:
                {
                    ui.ShowLoading(
                        "연결을 종료하는 중...");

                    ui.Browser.SetInteractable(false);
                    ui.Room.SetLeaveInteractable(false);

                    break;
                }
        }
    }

    // ==================================================
    // Browser Input
    // ==================================================

    private async void OnCreateRoomRequested(
        string roomName)
    {
        await network.CreateRoomAsync(
            roomName);
    }

    private async void OnJoinRoomRequested(
        string sessionName)
    {
        await network.JoinRoomAsync(
            sessionName);
    }

    // ==================================================
    // Room Input
    // ==================================================

    private void OnReadyRequested()
    {
        // 다음 구현:
        //
        // Local LobbyPlayer
        //     .RequestToggleReady();
    }

    private void OnStartRequested()
    {
        // 다음 구현:
        //
        // NetworkGameSession
        //     .RequestStartGame();
    }

    private async void OnLeaveRequested()
    {
        bool left =
            await network.LeaveRoomAsync();

        if (!left)
            return;

        // Room에서 나왔으므로
        // 새 Runner로 Photon Lobby 재진입.
        await network.ConnectLobbyAsync();
    }

    // ==================================================
    // Error
    // ==================================================

    private async void OnRetryRequested()
    {
        if (network.State !=
            NetworkSessionState.Offline)
        {
            return;
        }

        await network.ConnectLobbyAsync();
    }

    private static string GetUserFriendlyError(
        NetworkOperationFailure failure)
    {
        return failure.Operation switch
        {
            NetworkOperation.ConnectLobby =>
                "온라인 로비에 연결하지 못했습니다.\n" +
                "네트워크 상태를 확인한 뒤 다시 시도해 주세요.",

            NetworkOperation.CreateRoom =>
                "방을 생성하지 못했습니다.\n" +
                "다시 시도해 주세요.",

            NetworkOperation.JoinRoom =>
                "방에 참가하지 못했습니다.\n" +
                "방이 가득 찼거나 이미 종료되었을 수 있습니다.",

            NetworkOperation.LeaveRoom =>
                "방에서 나가는 중 문제가 발생했습니다.",

            NetworkOperation.ConnectionLost =>
                "네트워크 연결이 끊어졌습니다.",

            _ =>
                "네트워크 작업 중 문제가 발생했습니다."
        };
    }
}