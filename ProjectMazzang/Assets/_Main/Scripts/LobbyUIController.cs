using System.Collections.Generic;
using Fusion;
using UnityEngine;

public sealed class LobbyUIController :
    MonoBehaviour
{
    private enum LobbyPage
    {
        Title,
        SessionBrowser,
        Room
    }

    [SerializeField]
    private LobbyUI ui;

    private FusionSessionController network;

    private LobbyPage currentPage =
        LobbyPage.Title;

    // 타이틀에서 확정된 로컬 닉네임.
    // 방 입장 후 NetworkPlayerData로 제출한다.
    private string localNickname;

    private bool nicknameSubmittedForRoom;
    private bool readyRequestPending;

    // ==================================================
    // Unity
    // ==================================================

    private void Start()
    {
        network =
            AppRoot.Instance.Network;

        Bind();

        currentPage = LobbyPage.Title;

        ui.ShowTitle();

        RefreshFromCurrentState();

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
    // Bind
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

        // NetworkPlayerData
        NetworkPlayerData.LocalSpawned +=
            OnPlayerDataSpawned;

        NetworkPlayerData.LocalChanged +=
            OnPlayerDataChanged;

        NetworkPlayerData.LocalDespawned +=
            OnPlayerDataDespawned;

        // Title
        ui.Title.NicknameChanged +=
            OnNicknameChanged;

        ui.Title.EnterRequested +=
            OnTitleEnterRequested;

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

        NetworkPlayerData.LocalSpawned -=
            OnPlayerDataSpawned;

        NetworkPlayerData.LocalChanged -=
            OnPlayerDataChanged;

        NetworkPlayerData.LocalDespawned -=
            OnPlayerDataDespawned;

        if (ui == null)
            return;

        ui.Title.NicknameChanged -=
            OnNicknameChanged;

        ui.Title.EnterRequested -=
            OnTitleEnterRequested;

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
    }

    // ==================================================
    // Snapshot
    // ==================================================

    private void RefreshFromCurrentState()
    {
        ui.Browser.SetSessions(
            network.Sessions);

        ApplyNetworkState(
            network.State);

        RefreshNicknameValidation();

        if (network.State ==
            NetworkSessionState.InRoom)
        {
            RefreshRoomPlayers();
            TrySubmitLocalNickname();
        }
    }

    // ==================================================
    // Title
    // ==================================================

    private void OnNicknameChanged(
        string value)
    {
        RefreshNicknameValidation();
    }

    private void RefreshNicknameValidation()
    {
        string input =
            ui.Title.NicknameInput;

        bool nicknameValid =
            PlayerNicknamePolicy.TryNormalize(
                input,
                out _);

        if (!nicknameValid)
        {
            ui.Title.SetValidationMessage(
                "닉네임은 2~16자로 입력해 주세요.");
        }
        else
        {
            ui.Title.SetValidationMessage(
                string.Empty);
        }

        bool canEnter =
            nicknameValid &&
            network.State ==
            NetworkSessionState.LobbyReady;

        ui.Title.SetEnterInteractable(
            canEnter);
    }

    private void OnTitleEnterRequested(
        string nickname)
    {
        if (network.State !=
            NetworkSessionState.LobbyReady)
        {
            return;
        }

        if (!PlayerNicknamePolicy.TryNormalize(
                nickname,
                out string normalized))
        {
            RefreshNicknameValidation();
            return;
        }

        localNickname =
            normalized;

        currentPage =
            LobbyPage.SessionBrowser;

        ui.ShowBrowser();

        ui.Browser.SetSessions(
            network.Sessions);

        ApplyNetworkState(
            network.State);
    }

    // ==================================================
    // FSC State
    // ==================================================

    private void OnNetworkStateChanged(
        NetworkSessionState state)
    {
        ApplyNetworkState(state);
    }

    private void ApplyNetworkState(
        NetworkSessionState state)
    {
        switch (state)
        {
            case NetworkSessionState.Offline:
                {
                    HandleOfflineState();
                    break;
                }

            case NetworkSessionState.LobbyConnecting:
                {
                    HandleLobbyConnectingState();
                    break;
                }

            case NetworkSessionState.LobbyReady:
                {
                    HandleLobbyReadyState();
                    break;
                }

            case NetworkSessionState.RoomConnecting:
                {
                    HandleRoomConnectingState();
                    break;
                }

            case NetworkSessionState.InRoom:
                {
                    HandleInRoomState();
                    break;
                }

            case NetworkSessionState.ShuttingDown:
                {
                    HandleShuttingDownState();
                    break;
                }
        }

        RefreshNicknameValidation();
    }

    private void HandleOfflineState()
    {
        ui.HideLoading();

        readyRequestPending = false;
        nicknameSubmittedForRoom = false;

        ui.Title.SetConnectionState(
            "온라인 연결 없음");

        // 실제 Room 연결은 존재하지 않는데
        // UI만 Room에 남아 있는 상태를 허용하지 않는다.
        if (currentPage ==
            LobbyPage.Room)
        {
            ui.Room.ClearPlayers();

            currentPage =
                LobbyPage.SessionBrowser;

            ui.ShowBrowser();
        }

        ui.Browser.SetInteractable(false);

        ui.Room.SetReadyInteractable(false);
        ui.Room.SetStartInteractable(false);
        ui.Room.SetLeaveInteractable(false);
    }

    private void HandleLobbyConnectingState()
    {
        ui.Title.SetConnectionState(
            "온라인 로비 연결 중...");

        // 타이틀에서는 닉네임을 입력할 수 있으므로
        // 전체 Loading Overlay를 띄우지 않는다.
        if (currentPage ==
            LobbyPage.SessionBrowser)
        {
            ui.ShowLoading(
                "온라인 로비에 연결 중...");
        }

        ui.Browser.SetInteractable(false);
    }

    private void HandleLobbyReadyState()
    {
        ui.Title.SetConnectionState(
            "온라인 연결 완료");

        ui.HideLoading();

        if (currentPage ==
            LobbyPage.SessionBrowser)
        {
            ui.Browser.SetInteractable(true);

            ui.Browser.SetSessions(
                network.Sessions);
        }
    }

    private void HandleRoomConnectingState()
    {
        nicknameSubmittedForRoom = false;
        readyRequestPending = false;

        ui.Browser.SetInteractable(false);

        ui.ShowLoading(
            "방에 연결 중...");
    }

    private void HandleInRoomState()
    {
        currentPage =
            LobbyPage.Room;

        ui.HideLoading();

        ui.ShowRoom();

        ui.Room.SetRoomName(
            network.CurrentRoomName);

        // ui.Room.SetStartVisible(false);

        ui.Room.SetReadyInteractable(true);
        ui.Room.SetLeaveInteractable(true);

        nicknameSubmittedForRoom = false;
        readyRequestPending = false;

        RefreshRoomPlayers();

        TrySubmitLocalNickname();
    }

    private void HandleShuttingDownState()
    {
        ui.ShowLoading(
            "연결을 종료하는 중...");

        ui.Browser.SetInteractable(false);
        ui.Room.SetReadyInteractable(false);
        ui.Room.SetLeaveInteractable(false);
    }

    // ==================================================
    // Session List
    // ==================================================

    private void OnSessionListChanged(
        IReadOnlyList<SessionInfo> sessions)
    {
        ui.Browser.SetSessions(
            sessions);
    }

    // ==================================================
    // Browser
    // ==================================================

    private async void OnCreateRoomRequested(
        string roomName)
    {
        if (network.State !=
            NetworkSessionState.LobbyReady)
        {
            return;
        }

        await network.CreateRoomAsync(
            roomName);
    }

    private async void OnJoinRoomRequested(
        string sessionName)
    {
        if (network.State !=
            NetworkSessionState.LobbyReady)
        {
            return;
        }

        await network.JoinRoomAsync(
            sessionName);
    }

    // ==================================================
    // NetworkPlayerData
    // ==================================================

    private void OnPlayerDataSpawned(
        NetworkPlayerData playerData)
    {
        if (!BelongsToCurrentRunner(
                playerData))
        {
            return;
        }

        UpsertPlayerItem(playerData);

        if (playerData.IsLocalPlayer)
        {
            TrySubmitLocalNickname(
                playerData);
        }

        RefreshReadySummary();
    }

    private void OnPlayerDataChanged(
        NetworkPlayerData playerData)
    {
        if (!BelongsToCurrentRunner(
                playerData))
        {
            return;
        }

        UpsertPlayerItem(playerData);

        if (playerData.IsLocalPlayer)
        {
            readyRequestPending = false;

            ui.Room.SetLocalReadyState(
                playerData.Ready);

            if (network.State ==
                NetworkSessionState.InRoom)
            {
                ui.Room.SetReadyInteractable(
                    true);
            }
        }

        RefreshReadySummary();
    }

    private void OnPlayerDataDespawned(
        NetworkRunner sourceRunner,
        PlayerRef player)
    {
        if (network.Runner != sourceRunner)
            return;

        ui.Room.RemovePlayer(player);

        RefreshReadySummary();
    }

    private void UpsertPlayerItem(
        NetworkPlayerData playerData)
    {
        ui.Room.UpsertPlayer(
            playerData.PlayerRef,
            playerData.DisplayName,
            playerData.Ready,
            playerData.IsLocalPlayer);
    }

    // ==================================================
    // Room Snapshot
    // ==================================================

    private void RefreshRoomPlayers()
    {
        ui.Room.ClearPlayers();

        NetworkRunner runner =
            network.Runner;

        if (runner == null)
            return;

        foreach (PlayerRef player
                 in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(
                    player,
                    out NetworkObject playerObject))
            {
                continue;
            }

            NetworkPlayerData playerData =
                playerObject.GetComponent<
                    NetworkPlayerData>();

            if (playerData == null)
                continue;

            UpsertPlayerItem(playerData);

            if (playerData.IsLocalPlayer)
            {
                ui.Room.SetLocalReadyState(
                    playerData.Ready);
            }
        }

        RefreshReadySummary();
    }

    private void RefreshReadySummary()
    {
        NetworkRunner runner =
            network.Runner;

        if (runner == null)
        {
            ui.Room.SetReadySummary(0, 0);
            ui.Room.SetStartInteractable(false);
            return;
        }

        int playerCount = 0;
        int readyCount = 0;

        foreach (PlayerRef player
                 in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(
                    player,
                    out NetworkObject playerObject))
            {
                continue;
            }

            NetworkPlayerData playerData =
                playerObject.GetComponent<
                    NetworkPlayerData>();

            if (playerData == null)
                continue;

            playerCount++;

            if (playerData.Ready)
                readyCount++;
        }

        ui.Room.SetReadySummary(
            readyCount,
            playerCount);

        bool isHost =
            runner.IsServer;

        bool allReady =
            playerCount > 0 &&
            readyCount == playerCount;

        ui.Room.SetStartInteractable(
            isHost && allReady);
    }

    // ==================================================
    // Nickname Submit
    // ==================================================

    private void TrySubmitLocalNickname()
    {
        if (nicknameSubmittedForRoom)
            return;

        NetworkRunner runner =
            network.Runner;

        if (runner == null)
            return;

        if (!runner.TryGetPlayerObject(
                runner.LocalPlayer,
                out NetworkObject playerObject))
        {
            return;
        }

        NetworkPlayerData playerData =
            playerObject.GetComponent<
                NetworkPlayerData>();

        if (playerData == null)
            return;

        TrySubmitLocalNickname(
            playerData);
    }

    private void TrySubmitLocalNickname(
        NetworkPlayerData playerData)
    {
        if (nicknameSubmittedForRoom)
            return;

        if (!playerData.IsLocalPlayer)
            return;

        if (string.IsNullOrWhiteSpace(
                localNickname))
        {
            return;
        }

        bool requested =
            playerData.RequestNickname(
                localNickname);

        if (requested)
        {
            nicknameSubmittedForRoom = true;
        }
    }

    // ==================================================
    // Ready
    // ==================================================

    private void OnReadyRequested()
    {
        if (readyRequestPending)
            return;

        NetworkPlayerData localPlayer =
            GetLocalPlayerData();

        if (localPlayer == null)
            return;

        bool nextReady =
            !localPlayer.Ready;

        bool requested =
            localPlayer.RequestReady(
                nextReady);

        if (!requested)
            return;

        readyRequestPending = true;

        // 실제 확정 값이 복제되어 돌아오기 전까지
        // 재입력 방지.
        ui.Room.SetReadyInteractable(
            false);
    }

    private NetworkPlayerData
        GetLocalPlayerData()
    {
        NetworkRunner runner =
            network.Runner;

        if (runner == null)
            return null;

        if (!runner.TryGetPlayerObject(
                runner.LocalPlayer,
                out NetworkObject playerObject))
        {
            return null;
        }

        return playerObject.GetComponent<
            NetworkPlayerData>();
    }

    // ==================================================
    // Start
    // ==================================================

    private void OnStartRequested()
    {
        // 다음 단계:
        //
        // NetworkGameSession
        // → Host 최종 Ready 검증
        // → Starting
        // → Scene Load
    }

    // ==================================================
    // Leave
    // ==================================================

    private async void OnLeaveRequested()
    {
        readyRequestPending = false;
        nicknameSubmittedForRoom = false;

        bool result =
            await network.LeaveRoomAsync();

        if (!result)
            return;

        ui.Room.ClearPlayers();

        currentPage =
            LobbyPage.SessionBrowser;

        ui.ShowBrowser();

        await network.ConnectLobbyAsync();
    }

    // ==================================================
    // Failure
    // ==================================================

    private void ShowLobbyConnectionFailedPopup()
    {
        AppRoot.Instance.Popup.Show(
            "온라인 로비에 연결하지 못했습니다.\n" +
            "네트워크 상태를 확인한 뒤 다시 시도해 주세요.",
            "다시 시도",
            RetryLobbyConnection,
            allowClose: false);
    }

    private void RetryLobbyConnection()
    {
        if (network.State !=
            NetworkSessionState.Offline)
        {
            return;
        }

        _ = network.ConnectLobbyAsync();
    }

    private void ShowConnectionLostPopup()
    {
        AppRoot.Instance.Popup.Show(
            "방과의 연결이 종료되었습니다.",
            "로비로 돌아가기",
            ReconnectToLobby,
            allowClose: false);
    }

    private void ReconnectToLobby()
    {
        if (network.State !=
            NetworkSessionState.Offline)
        {
            return;
        }

        _ = network.ConnectLobbyAsync();
    }

    private void OnNetworkOperationFailed(
    NetworkOperationFailure failure)
    {
        readyRequestPending = false;

        ui.HideLoading();

        switch (failure.Operation)
        {
            case NetworkOperation.ConnectionLost:
                {
                    ShowConnectionLostPopup();
                    break;
                }

            case NetworkOperation.ConnectLobby:
                {
                    ShowLobbyConnectionFailedPopup();
                    break;
                }

            case NetworkOperation.CreateRoom:
                {
                    AppRoot.Instance.Popup.Show(
                        "방을 생성하지 못했습니다.",
                        "확인");

                    break;
                }

            case NetworkOperation.JoinRoom:
                {
                    AppRoot.Instance.Popup.Show(
                        "방에 참가하지 못했습니다.\n" +
                        "방이 가득 찼거나 종료되었을 수 있습니다.",
                        "확인");

                    break;
                }

            case NetworkOperation.LeaveRoom:
                {
                    AppRoot.Instance.Popup.Show(
                        "방에서 나가는 중 문제가 발생했습니다.",
                        "확인");

                    break;
                }

            default:
                {
                    AppRoot.Instance.Popup.Show(
                        "네트워크 작업 중 문제가 발생했습니다.",
                        "확인");

                    break;
                }
        }
    }

    private static string GetUserFriendlyError(
        NetworkOperationFailure failure)
    {
        return failure.Operation switch
        {
            NetworkOperation.ConnectLobby =>
                "온라인 로비에 연결하지 못했습니다.",

            NetworkOperation.CreateRoom =>
                "방을 생성하지 못했습니다.",

            NetworkOperation.JoinRoom =>
                "방에 참가하지 못했습니다.\n" +
                "방이 가득 찼거나 종료되었을 수 있습니다.",

            NetworkOperation.LeaveRoom =>
                "방에서 나가는 중 문제가 발생했습니다.",

            NetworkOperation.ConnectionLost =>
                "네트워크 연결이 끊어졌습니다.",

            _ =>
                "네트워크 작업 중 문제가 발생했습니다."
        };
    }

    // ==================================================
    // Utility
    // ==================================================

    private bool BelongsToCurrentRunner(
        NetworkPlayerData playerData)
    {
        if (playerData == null)
            return false;

        if (network.Runner == null)
            return false;

        return playerData.Runner ==
               network.Runner;
    }
}