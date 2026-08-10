using System.Collections.Generic;
using System.Threading.Tasks;
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

    private FusionSessionController _network;
    private NetworkGameSession _gameSession;

    private LobbyPage _currentPage =
        LobbyPage.Title;

    private string _localNickname;

    private bool _nicknameSubmittedForRoom;
    private bool _characterConfirmRequestPending;
    private bool _mapVoteRequestPending;

    // ==================================================
    // Unity
    // ==================================================

    private void Start()
    {
        _network =
            AppRoot.Instance.Network;

        Bind();

        _currentPage =
            LobbyPage.Title;

        ui.ShowTitle();

        RefreshFromCurrentState();

        if (_network.State ==
            NetworkSessionState.Offline)
        {
            _ =
                _network.ConnectLobbyAsync();
        }
    }

    private void Update()
    {
        if (_currentPage !=
            LobbyPage.Room)
        {
            return;
        }

        if (_gameSession == null)
        {
            TryBindGameSession();
            return;
        }

        if (_gameSession.Phase !=
            LobbySelectionPhase.MapVote)
        {
            return;
        }

        ui.Room.SetMapVoteTimer(
            _gameSession
                .GetMapVoteRemainingTime());
    }

    private void OnDestroy()
    {
        Unbind();
        UnbindGameSession();
    }

    // ==================================================
    // Bind
    // ==================================================

    private void Bind()
    {
        _network.StateChanged +=
            OnNetworkStateChanged;

        _network.SessionListChanged +=
            OnSessionListChanged;

        _network.OperationFailed +=
            OnNetworkOperationFailed;

        _network.GameSessionChanged +=
            OnGameSessionChanged;

        NetworkPlayerData.LocalSpawned +=
            OnPlayerDataSpawned;

        NetworkPlayerData.LocalChanged +=
            OnPlayerDataChanged;

        NetworkPlayerData.LocalDespawned +=
            OnPlayerDataDespawned;

        ui.Title.NicknameChanged +=
            OnNicknameChanged;

        ui.Title.EnterRequested +=
            OnTitleEnterRequested;

        ui.Browser.CreateRoomRequested +=
            OnCreateRoomRequested;

        ui.Browser.JoinRoomRequested +=
            OnJoinRoomRequested;

        ui.Room.CharacterConfirmRequested +=
            OnCharacterConfirmRequested;

        ui.Room.MapVoteRequested +=
            OnMapVoteRequested;

        ui.Room.LeaveRequested +=
            OnLeaveRequested;
    }

    private void Unbind()
    {
        if (_network != null)
        {
            _network.StateChanged -=
                OnNetworkStateChanged;

            _network.SessionListChanged -=
                OnSessionListChanged;

            _network.OperationFailed -=
                OnNetworkOperationFailed;

            _network.GameSessionChanged -=
                OnGameSessionChanged;
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

        ui.Room.CharacterConfirmRequested -=
            OnCharacterConfirmRequested;

        ui.Room.MapVoteRequested -=
            OnMapVoteRequested;

        ui.Room.LeaveRequested -=
            OnLeaveRequested;
    }

    // ==================================================
    // Game Session Bind
    // ==================================================

    private void OnGameSessionChanged(
        NetworkGameSession session)
    {
        if (session == null)
        {
            UnbindGameSession();
            return;
        }

        if (!BelongsToCurrentRunner(
                session))
        {
            return;
        }

        BindGameSession(
            session);
    }

    private void TryBindGameSession()
    {
        NetworkGameSession session =
            _network != null
                ? _network.GameSession
                : null;

        if (session == null ||
            !BelongsToCurrentRunner(
                session))
        {
            return;
        }

        BindGameSession(
            session);
    }

    private void BindGameSession(
        NetworkGameSession session)
    {
        if (_gameSession == session)
        {
            RefreshSelectionUI();
            return;
        }

        UnbindGameSession();

        _gameSession =
            session;

        _gameSession.PhaseChanged +=
            OnSelectionPhaseChanged;

        _gameSession.SelectionStateChanged +=
            OnSelectionStateChanged;

        ui.Room.SetCatalogs(
            _gameSession.CharacterCatalog,
            _gameSession.MapCatalog);

        RefreshSelectionUI();
    }

    private void UnbindGameSession()
    {
        if (_gameSession == null)
            return;

        _gameSession.PhaseChanged -=
            OnSelectionPhaseChanged;

        _gameSession.SelectionStateChanged -=
            OnSelectionStateChanged;

        _gameSession =
            null;
    }

    // ==================================================
    // Snapshot
    // ==================================================

    private void RefreshFromCurrentState()
    {
        ui.Browser.SetSessions(
            _network.Sessions);

        ApplyNetworkState(
            _network.State);

        RefreshNicknameValidation();

        if (_network.State ==
            NetworkSessionState.InRoom)
        {
            TryBindGameSession();
            RefreshRoomPlayers();
            TrySubmitLocalNickname();
            RefreshSelectionUI();
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
            _network.State ==
            NetworkSessionState.LobbyReady;

        ui.Title.SetEnterInteractable(
            canEnter);
    }

    private void OnTitleEnterRequested(
        string nickname)
    {
        if (_network.State !=
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

        _localNickname =
            normalized;

        _currentPage =
            LobbyPage.SessionBrowser;

        ui.ShowBrowser();

        ui.Browser.SetSessions(
            _network.Sessions);

        ApplyNetworkState(
            _network.State);
    }

    // ==================================================
    // FSC State
    // ==================================================

    private void OnNetworkStateChanged(
        NetworkSessionState state)
    {
        ApplyNetworkState(
            state);
    }

    private void ApplyNetworkState(
        NetworkSessionState state)
    {
        switch (state)
        {
            case NetworkSessionState.Offline:
                HandleOfflineState();
                break;

            case NetworkSessionState.LobbyConnecting:
                HandleLobbyConnectingState();
                break;

            case NetworkSessionState.LobbyReady:
                HandleLobbyReadyState();
                break;

            case NetworkSessionState.RoomConnecting:
                HandleRoomConnectingState();
                break;

            case NetworkSessionState.InRoom:
                HandleInRoomState();
                break;

            case NetworkSessionState.ShuttingDown:
                HandleShuttingDownState();
                break;
        }

        RefreshNicknameValidation();
    }

    private void HandleOfflineState()
    {
        ui.HideLoading();

        _characterConfirmRequestPending =
            false;

        _mapVoteRequestPending =
            false;

        _nicknameSubmittedForRoom =
            false;

        UnbindGameSession();

        ui.Title.SetConnectionState(
            "온라인 연결 없음");

        if (_currentPage ==
            LobbyPage.Room)
        {
            ui.Room.ClearPlayers();

            _currentPage =
                LobbyPage.SessionBrowser;

            ui.ShowBrowser();
        }

        ui.Browser.SetInteractable(
            false);

        ui.Room.SetLeaveInteractable(
            false);
    }

    private void HandleLobbyConnectingState()
    {
        ui.Title.SetConnectionState(
            "온라인 로비 연결 중...");

        if (_currentPage ==
            LobbyPage.SessionBrowser)
        {
            ui.ShowLoading(
                "온라인 로비에 연결 중...");
        }

        ui.Browser.SetInteractable(
            false);
    }

    private void HandleLobbyReadyState()
    {
        ui.Title.SetConnectionState(
            "온라인 연결 완료");

        ui.HideLoading();

        if (_currentPage ==
            LobbyPage.SessionBrowser)
        {
            ui.Browser.SetInteractable(
                true);

            ui.Browser.SetSessions(
                _network.Sessions);
        }
    }

    private void HandleRoomConnectingState()
    {
        _nicknameSubmittedForRoom =
            false;

        _characterConfirmRequestPending =
            false;

        _mapVoteRequestPending =
            false;

        ui.Browser.SetInteractable(
            false);

        ui.ShowLoading(
            "방에 연결 중...");
    }

    private void HandleInRoomState()
    {
        _currentPage =
            LobbyPage.Room;

        ui.HideLoading();
        ui.ShowRoom();

        ui.Room.SetRoomName(
            _network.CurrentRoomName);

        ui.Room.SetLeaveInteractable(
            true);

        _nicknameSubmittedForRoom =
            false;

        _characterConfirmRequestPending =
            false;

        _mapVoteRequestPending =
            false;

        TryBindGameSession();
        RefreshRoomPlayers();
        TrySubmitLocalNickname();
        RefreshSelectionUI();
    }

    private void HandleShuttingDownState()
    {
        ui.ShowLoading(
            "연결을 종료하는 중...");

        ui.Browser.SetInteractable(
            false);

        ui.Room.SetLeaveInteractable(
            false);
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
        if (_network.State !=
            NetworkSessionState.LobbyReady)
        {
            return;
        }

        await _network.CreateRoomAsync(
            roomName);
    }

    private async void OnJoinRoomRequested(
        string sessionName)
    {
        if (_network.State !=
            NetworkSessionState.LobbyReady)
        {
            return;
        }

        await _network.JoinRoomAsync(
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

        UpsertPlayerItem(
            playerData);

        if (playerData.IsLocalPlayer)
        {
            TrySubmitLocalNickname(
                playerData);
        }

        RefreshCharacterSummary();
        RefreshMapVoteCounts();
        RefreshLocalSelectionState();
    }

    private void OnPlayerDataChanged(
        NetworkPlayerData playerData)
    {
        if (!BelongsToCurrentRunner(
                playerData))
        {
            return;
        }

        UpsertPlayerItem(
            playerData);

        if (playerData.IsLocalPlayer)
        {
            _characterConfirmRequestPending =
                false;

            _mapVoteRequestPending =
                false;
        }

        RefreshCharacterSummary();
        RefreshMapVoteCounts();
        RefreshLocalSelectionState();
    }

    private void OnPlayerDataDespawned(
        NetworkRunner sourceRunner,
        PlayerRef player)
    {
        if (_network.Runner !=
            sourceRunner)
        {
            return;
        }

        ui.Room.RemovePlayer(
            player);

        RefreshCharacterSummary();
        RefreshMapVoteCounts();
    }

    private void UpsertPlayerItem(
        NetworkPlayerData playerData)
    {
        ui.Room.UpsertPlayer(
            playerData.PlayerRef,
            playerData.DisplayName,
            playerData.CharacterConfirmed,
            playerData.IsLocalPlayer);
    }

    // ==================================================
    // Room Snapshot
    // ==================================================

    private void RefreshRoomPlayers()
    {
        ui.Room.ClearPlayers();

        NetworkRunner runner =
            _network.Runner;

        if (runner == null)
            return;

        foreach (PlayerRef player
                 in runner.ActivePlayers)
        {
            if (!TryGetPlayerData(
                    player,
                    out NetworkPlayerData playerData))
            {
                continue;
            }

            UpsertPlayerItem(
                playerData);
        }

        RefreshCharacterSummary();
        RefreshMapVoteCounts();
        RefreshLocalSelectionState();
    }

    private void RefreshCharacterSummary()
    {
        NetworkRunner runner =
            _network.Runner;

        if (runner == null)
        {
            ui.Room.SetCharacterSummary(
                0,
                0);

            return;
        }

        int playerCount = 0;
        int confirmedCount = 0;

        foreach (PlayerRef player
                 in runner.ActivePlayers)
        {
            if (!TryGetPlayerData(
                    player,
                    out NetworkPlayerData playerData))
            {
                continue;
            }

            playerCount++;

            if (playerData.CharacterConfirmed)
            {
                confirmedCount++;
            }
        }

        ui.Room.SetCharacterSummary(
            confirmedCount,
            playerCount);
    }

    // ==================================================
    // Nickname Submit
    // ==================================================

    private void TrySubmitLocalNickname()
    {
        if (_nicknameSubmittedForRoom)
            return;

        NetworkPlayerData playerData =
            GetLocalPlayerData();

        if (playerData == null)
            return;

        TrySubmitLocalNickname(
            playerData);
    }

    private void TrySubmitLocalNickname(
        NetworkPlayerData playerData)
    {
        if (_nicknameSubmittedForRoom)
            return;

        if (!playerData.IsLocalPlayer)
            return;

        if (string.IsNullOrWhiteSpace(
                _localNickname))
        {
            return;
        }

        bool requested =
            playerData.RequestNickname(
                _localNickname);

        if (requested)
        {
            _nicknameSubmittedForRoom =
                true;
        }
    }

    // ==================================================
    // Character Select
    // ==================================================

    private void OnCharacterConfirmRequested(
        int characterId)
    {
        if (_characterConfirmRequestPending)
            return;

        if (_gameSession == null ||
            _gameSession.Phase !=
            LobbySelectionPhase.CharacterSelect)
        {
            return;
        }

        NetworkPlayerData localPlayer =
            GetLocalPlayerData();

        if (localPlayer == null ||
            localPlayer.CharacterConfirmed)
        {
            return;
        }

        bool requested =
            _gameSession.RequestCharacterConfirm(
                characterId);

        if (!requested)
            return;

        _characterConfirmRequestPending =
            true;

        ui.Room.SetCharacterConfirmPending(
            true);
    }

    // ==================================================
    // Map Vote
    // ==================================================

    private void OnMapVoteRequested(
        int mapId)
    {
        if (_mapVoteRequestPending)
            return;

        if (_gameSession == null ||
            _gameSession.Phase !=
            LobbySelectionPhase.MapVote)
        {
            return;
        }

        if (GetLocalPlayerData() == null)
            return;

        bool requested =
            _gameSession.RequestMapVote(
                mapId);

        if (!requested)
        {
            RefreshLocalSelectionState();
            return;
        }

        _mapVoteRequestPending =
            true;

        // 클릭 체감은 즉시 유지하고,
        // 확정 값은 NetworkPlayerData.LocalChanged에서 다시 맞춘다.
        ui.Room.SetLocalMapVote(
            mapId);

        ui.Room.SetMapVoteInteractable(
            false);
    }

    private void RefreshMapVoteCounts()
    {
        if (_gameSession == null ||
            _gameSession.MapCatalog == null ||
            _gameSession.MapCatalog.Maps == null)
        {
            return;
        }

        Dictionary<int, int> counts =
            new();

        foreach (MapData map
                 in _gameSession.MapCatalog.Maps)
        {
            if (map == null)
                continue;

            counts[map.MapId] = 0;
        }

        NetworkRunner runner =
            _network.Runner;

        if (runner != null)
        {
            foreach (PlayerRef player
                     in runner.ActivePlayers)
            {
                if (!TryGetPlayerData(
                        player,
                        out NetworkPlayerData playerData))
                {
                    continue;
                }

                int mapId =
                    playerData.VotedMapId;

                if (!counts.ContainsKey(
                        mapId))
                {
                    continue;
                }

                counts[mapId]++;
            }
        }

        foreach (KeyValuePair<int, int> pair
                 in counts)
        {
            ui.Room.SetMapVoteCount(
                pair.Key,
                pair.Value);
        }
    }

    private List<int>
        BuildTopVotedMapIds()
    {
        List<int> result =
            new();

        if (_gameSession == null ||
            _gameSession.MapCatalog == null ||
            _gameSession.MapCatalog.Maps == null)
        {
            return result;
        }

        Dictionary<int, int> counts =
            new();

        foreach (MapData map
                 in _gameSession.MapCatalog.Maps)
        {
            if (map == null)
                continue;

            counts[map.MapId] = 0;
        }

        NetworkRunner runner =
            _network.Runner;

        if (runner != null)
        {
            foreach (PlayerRef player
                     in runner.ActivePlayers)
            {
                if (!TryGetPlayerData(
                        player,
                        out NetworkPlayerData playerData))
                {
                    continue;
                }

                if (counts.ContainsKey(
                        playerData.VotedMapId))
                {
                    counts[
                        playerData.VotedMapId]++;
                }
            }
        }

        int best = -1;

        foreach (int count
                 in counts.Values)
        {
            if (count > best)
            {
                best = count;
            }
        }

        foreach (KeyValuePair<int, int> pair
                 in counts)
        {
            if (pair.Value == best)
            {
                result.Add(
                    pair.Key);
            }
        }

        return result;
    }

    // ==================================================
    // Selection Phase
    // ==================================================

    private void OnSelectionPhaseChanged(
        LobbySelectionPhase phase)
    {
        RefreshSelectionUI();

        if (phase !=
            LobbySelectionPhase.MapRoulette)
        {
            return;
        }

        List<int> candidates =
            BuildTopVotedMapIds();

        ui.Room.PlayMapRoulette(
            candidates,
            _gameSession.SelectedMapId,
            _gameSession.MapRouletteDuration);
    }

    private void OnSelectionStateChanged()
    {
        RefreshSelectionUI();
    }

    private void RefreshSelectionUI()
    {
        if (_gameSession == null)
            return;

        ui.Room.SetCatalogs(
            _gameSession.CharacterCatalog,
            _gameSession.MapCatalog);

        ui.Room.ShowPhase(
            _gameSession.Phase);

        switch (_gameSession.Phase)
        {
            case LobbySelectionPhase.CharacterSelect:
                ui.Room.SetMapVoteStatus(
                    string.Empty);

                RefreshLocalSelectionState();
                break;

            case LobbySelectionPhase.MapVote:
                ui.Room.SetMapVoteStatus(
                    "맵에 투표하세요");

                ui.Room.SetMapVoteInteractable(
                    !_mapVoteRequestPending);

                ui.Room.SetMapVoteTimer(
                    _gameSession
                        .GetMapVoteRemainingTime());

                RefreshMapVoteCounts();
                RefreshLocalSelectionState();
                break;

            case LobbySelectionPhase.MapRoulette:
                ui.Room.SetMapVoteInteractable(
                    false);

                RefreshMapVoteCounts();
                break;

            case LobbySelectionPhase.Starting:
                ui.Room.SetLeaveInteractable(
                    false);
                break;

            case LobbySelectionPhase.Playing:
            case LobbySelectionPhase.Returning:
                break;
        }
    }

    private void RefreshLocalSelectionState()
    {
        NetworkPlayerData localPlayer =
            GetLocalPlayerData();

        if (localPlayer == null)
            return;

        ui.Room.SetLocalCharacterState(
            localPlayer.SelectedCharacterId,
            localPlayer.CharacterConfirmed);

        ui.Room.SetLocalMapVote(
            localPlayer.VotedMapId);

        if (_gameSession == null)
            return;

        bool mapVoteInteractable =
            _gameSession.Phase ==
                LobbySelectionPhase.MapVote &&
            !_mapVoteRequestPending;

        ui.Room.SetMapVoteInteractable(
            mapVoteInteractable);
    }

    // ==================================================
    // Leave
    // ==================================================

    private async void OnLeaveRequested()
    {
        _characterConfirmRequestPending =
            false;

        _mapVoteRequestPending =
            false;

        _nicknameSubmittedForRoom =
            false;

        bool result =
            await _network.LeaveRoomAsync();

        if (!result)
            return;

        UnbindGameSession();

        ui.Room.ClearPlayers();

        _currentPage =
            LobbyPage.SessionBrowser;

        ui.ShowBrowser();

        await _network.ConnectLobbyAsync();
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
        if (_network.State !=
            NetworkSessionState.Offline)
        {
            return;
        }

        _ =
            _network.ConnectLobbyAsync();
    }

    private async Task RecoverFromConnectionLostAsync()
    {
        // OnShutdown에서 FSC가 Offline까지 정리한 뒤
        // ConnectionLost가 발생하므로 여기서 새 Runner로
        // Session Lobby에 다시 연결한다.
        if (_network.State !=
            NetworkSessionState.Offline)
        {
            return;
        }

        bool connected =
            await _network.ConnectLobbyAsync();

        if (!connected)
        {
            // ConnectLobbyAsync 자체가 실패 원인을
            // OperationFailed로 올리므로 여기서 중복 팝업은 띄우지 않는다.
            return;
        }

        AppRoot.Instance.Popup.Show(
            "방과의 연결이 종료되어 온라인 로비로 돌아왔습니다.",
            "확인");
    }

    private void OnNetworkOperationFailed(
        NetworkOperationFailure failure)
    {
        _characterConfirmRequestPending =
            false;

        _mapVoteRequestPending =
            false;

        ui.HideLoading();

        switch (failure.Operation)
        {
            case NetworkOperation.ConnectionLost:
                _ =
                    RecoverFromConnectionLostAsync();

                break;

            case NetworkOperation.ConnectLobby:
                ShowLobbyConnectionFailedPopup();
                break;

            case NetworkOperation.CreateRoom:
                AppRoot.Instance.Popup.Show(
                    "방을 생성하지 못했습니다.",
                    "확인");
                break;

            case NetworkOperation.JoinRoom:
                AppRoot.Instance.Popup.Show(
                    "방에 참가하지 못했습니다.\n" +
                    "방이 가득 찼거나 종료되었을 수 있습니다.",
                    "확인");
                break;

            case NetworkOperation.LeaveRoom:
                AppRoot.Instance.Popup.Show(
                    "방에서 나가는 중 문제가 발생했습니다.",
                    "확인");
                break;

            default:
                AppRoot.Instance.Popup.Show(
                    "네트워크 작업 중 문제가 발생했습니다.",
                    "확인");
                break;
        }
    }

    // ==================================================
    // Utility
    // ==================================================

    private NetworkPlayerData
        GetLocalPlayerData()
    {
        NetworkRunner runner =
            _network.Runner;

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

    private bool TryGetPlayerData(
        PlayerRef player,
        out NetworkPlayerData playerData)
    {
        playerData = null;

        NetworkRunner runner =
            _network.Runner;

        if (runner == null)
            return false;

        if (!runner.TryGetPlayerObject(
                player,
                out NetworkObject playerObject))
        {
            return false;
        }

        playerData =
            playerObject.GetComponent<
                NetworkPlayerData>();

        return playerData != null;
    }

    private bool BelongsToCurrentRunner(
        NetworkPlayerData playerData)
    {
        if (playerData == null ||
            _network.Runner == null)
        {
            return false;
        }

        return playerData.Runner ==
               _network.Runner;
    }

    private bool BelongsToCurrentRunner(
        NetworkGameSession session)
    {
        if (session == null ||
            _network.Runner == null)
        {
            return false;
        }

        return session.Runner ==
               _network.Runner;
    }
}