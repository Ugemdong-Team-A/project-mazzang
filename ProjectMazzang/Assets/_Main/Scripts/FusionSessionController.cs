using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class FusionSessionController :
    MonoBehaviour,
    INetworkRunnerCallbacks
{
    [Header("Runner")]
    [SerializeField]
    private NetworkRunner runnerPrefab;

    [Header("Room")]
    [Min(1)]
    [SerializeField]
    private int defaultMaxPlayers = 4;

    [Header("Player")]
    [SerializeField]
    private NetworkPlayerData networkPlayerDataPrefab;

    [Header("Game Session")]
    [SerializeField]
    private NetworkGameSession networkGameSessionPrefab;

    private readonly List<SessionInfo> sessions = new();

    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;
    private NetworkGameSession gameSession;

    private NetworkSessionState state =
        NetworkSessionState.Offline;

    private NetworkOperation activeOperation =
        NetworkOperation.None;

    // ==================================================
    // Public State
    // ==================================================

    public NetworkRunner Runner =>
        runner;

    public NetworkGameSession GameSession =>
        gameSession;

    public NetworkSessionState State =>
        state;

    public IReadOnlyList<SessionInfo> Sessions =>
        sessions;

    public string CurrentRoomName
    {
        get;
        private set;
    }

    public bool IsBusy =>
        state == NetworkSessionState.LobbyConnecting ||
        state == NetworkSessionState.RoomConnecting ||
        state == NetworkSessionState.ShuttingDown;

    // ==================================================
    // Events
    // ==================================================

    public event Action<NetworkSessionState>
        StateChanged;

    public event Action<IReadOnlyList<SessionInfo>>
        SessionListChanged;

    public event Action<NetworkOperationFailure>
        OperationFailed;

    /// <summary>
    /// Host뿐 아니라 Client에서도 복제된 NetworkGameSession을
    /// FSC가 확보했을 때 발생합니다.
    ///
    /// LobbyUIController는 이 이벤트를 통해
    /// 늦게 복제된 GameSession에도 안전하게 바인딩할 수 있습니다.
    /// </summary>
    public event Action<NetworkGameSession>
        GameSessionChanged;

    public event Action SceneLoadStarted;
    public event Action SceneLoadCompleted;

    // ==================================================
    // Unity
    // ==================================================

    private void Awake()
    {
        NetworkGameSession.LocalSpawned +=
            OnGameSessionSpawned;

        NetworkGameSession.LocalDespawned +=
            OnGameSessionDespawned;
    }

    private void OnDestroy()
    {
        NetworkGameSession.LocalSpawned -=
            OnGameSessionSpawned;

        NetworkGameSession.LocalDespawned -=
            OnGameSessionDespawned;
    }

    // ==================================================
    // Lobby
    // ==================================================

    public async Task<bool> ConnectLobbyAsync()
    {
        if (state !=
            NetworkSessionState.Offline)
        {
            return false;
        }

        if (activeOperation !=
            NetworkOperation.None)
        {
            return false;
        }

        activeOperation =
            NetworkOperation.ConnectLobby;

        NetworkRunner newRunner =
            null;

        try
        {
            newRunner =
                CreateRunner();

            SetState(
                NetworkSessionState.LobbyConnecting);

            StartGameResult result =
                await newRunner.JoinSessionLobby(
                    SessionLobby.ClientServer);

            if (!result.Ok)
            {
                await DisposeFailedRunnerAsync(
                    newRunner);

                SetState(
                    NetworkSessionState.Offline);

                RaiseFailure(
                    NetworkOperation.ConnectLobby,
                    result.ErrorMessage,
                    result.ShutdownReason);

                return false;
            }

            // await 도중 다른 이유로 현재 Runner가 교체됐다면
            // 이 결과를 사용하지 않는다.
            if (runner != newRunner)
                return false;

            SetState(
                NetworkSessionState.LobbyReady);

            return true;
        }
        catch (Exception e)
        {
            if (newRunner != null)
            {
                await DisposeFailedRunnerAsync(
                    newRunner);
            }

            SetState(
                NetworkSessionState.Offline);

            RaiseFailure(
                NetworkOperation.ConnectLobby,
                e.Message);

            return false;
        }
        finally
        {
            activeOperation =
                NetworkOperation.None;
        }
    }

    // ==================================================
    // Player Data
    // ==================================================

    private void EnsurePlayerData(
        NetworkRunner targetRunner,
        PlayerRef player)
    {
        if (networkPlayerDataPrefab == null)
        {
            Debug.LogError(
                "NetworkPlayerData Prefab이 등록되지 않았습니다.",
                this);

            return;
        }

        if (targetRunner.TryGetPlayerObject(
                player,
                out _))
        {
            return;
        }

        NetworkPlayerData playerData =
            targetRunner.Spawn(
                networkPlayerDataPrefab,
                inputAuthority: player,
                flags:
                    NetworkSpawnFlags.DontDestroyOnLoad);

        if (playerData == null)
        {
            Debug.LogError(
                $"PlayerData Spawn 실패: {player}",
                this);

            return;
        }

        targetRunner.SetPlayerObject(
            player,
            playerData.Object);

        Debug.Log(
            $"[Fusion] PlayerData Created: {player}",
            this);
    }

    private void EnsurePlayerDataForActivePlayers(
        NetworkRunner targetRunner)
    {
        foreach (PlayerRef player
                 in targetRunner.ActivePlayers)
        {
            EnsurePlayerData(
                targetRunner,
                player);
        }
    }

    // ==================================================
    // Room Create
    // ==================================================

    public async Task<bool> CreateRoomAsync(
        string roomName)
    {
        if (!CanStartRoomOperation())
            return false;

        activeOperation =
            NetworkOperation.CreateRoom;

        NetworkRunner currentRunner =
            runner;

        try
        {
            if (string.IsNullOrWhiteSpace(
                    roomName))
            {
                roomName =
                    $"Room_{Guid.NewGuid():N}"
                    .Substring(
                        0,
                        13);
            }

            roomName =
                roomName.Trim();

            SetState(
                NetworkSessionState.RoomConnecting);

            StartGameResult result =
                await currentRunner.StartGame(
                    new StartGameArgs
                    {
                        GameMode =
                            GameMode.Host,

                        SessionName =
                            roomName,

                        PlayerCount =
                            defaultMaxPlayers,

                        IsOpen =
                            true,

                        IsVisible =
                            true,

                        SceneManager =
                            sceneManager
                    });

            if (!result.Ok)
            {
                await DisposeFailedRunnerAsync(
                    currentRunner);

                SetState(
                    NetworkSessionState.Offline);

                RaiseFailure(
                    NetworkOperation.CreateRoom,
                    result.ErrorMessage,
                    result.ShutdownReason);

                return false;
            }

            if (runner !=
                currentRunner)
            {
                return false;
            }

            CurrentRoomName =
                roomName;

            // StartGame 이후 ActivePlayers가 유효해진 시점에서
            // Host 자신의 PlayerData까지 한 번 더 보장한다.
            EnsurePlayerDataForActivePlayers(
                currentRunner);

            EnsureGameSession(
                currentRunner);

            ClearSessionList();

            SetState(
                NetworkSessionState.InRoom);

            return true;
        }
        catch (Exception e)
        {
            await DisposeFailedRunnerAsync(
                currentRunner);

            SetState(
                NetworkSessionState.Offline);

            RaiseFailure(
                NetworkOperation.CreateRoom,
                e.Message);

            return false;
        }
        finally
        {
            activeOperation =
                NetworkOperation.None;
        }
    }

    // ==================================================
    // Room Join
    // ==================================================

    public async Task<bool> JoinRoomAsync(
        string sessionName)
    {
        if (!CanStartRoomOperation())
            return false;

        if (string.IsNullOrWhiteSpace(
                sessionName))
        {
            return false;
        }

        activeOperation =
            NetworkOperation.JoinRoom;

        NetworkRunner currentRunner =
            runner;

        try
        {
            SetState(
                NetworkSessionState.RoomConnecting);

            StartGameResult result =
                await currentRunner.StartGame(
                    new StartGameArgs
                    {
                        GameMode =
                            GameMode.Client,

                        SessionName =
                            sessionName,

                        // Client가 대상 Room을 못 찾았다고
                        // 새 Session을 만들어 버리는 의도가 아님.
                        EnableClientSessionCreation =
                            false,

                        SceneManager =
                            sceneManager
                    });

            if (!result.Ok)
            {
                await DisposeFailedRunnerAsync(
                    currentRunner);

                SetState(
                    NetworkSessionState.Offline);

                RaiseFailure(
                    NetworkOperation.JoinRoom,
                    result.ErrorMessage,
                    result.ShutdownReason);

                return false;
            }

            if (runner !=
                currentRunner)
            {
                return false;
            }

            CurrentRoomName =
                sessionName;

            ClearSessionList();

            SetState(
                NetworkSessionState.InRoom);

            return true;
        }
        catch (Exception e)
        {
            await DisposeFailedRunnerAsync(
                currentRunner);

            SetState(
                NetworkSessionState.Offline);

            RaiseFailure(
                NetworkOperation.JoinRoom,
                e.Message);

            return false;
        }
        finally
        {
            activeOperation =
                NetworkOperation.None;
        }
    }

    // ==================================================
    // Leave
    // ==================================================

    public async Task<bool> LeaveRoomAsync()
    {
        if (runner == null)
            return false;

        if (state ==
            NetworkSessionState.ShuttingDown)
        {
            return false;
        }

        if (activeOperation !=
            NetworkOperation.None)
        {
            return false;
        }

        activeOperation =
            NetworkOperation.LeaveRoom;

        NetworkRunner currentRunner =
            runner;

        try
        {
            SetState(
                NetworkSessionState.ShuttingDown);

            await currentRunner.Shutdown();

            // 정상적으로는 Shutdown 과정에서 OnShutdown이 호출되어
            // Runner 정리가 끝난다.
            // 혹시 callback을 타지 못한 경우만 여기서 보정한다.
            if (runner ==
                currentRunner)
            {
                ForceDestroyRunner(
                    currentRunner);

                SetState(
                    NetworkSessionState.Offline);
            }

            return true;
        }
        catch (Exception e)
        {
            ForceDestroyRunner(
                currentRunner);

            SetState(
                NetworkSessionState.Offline);

            RaiseFailure(
                NetworkOperation.LeaveRoom,
                e.Message);

            return false;
        }
        finally
        {
            activeOperation =
                NetworkOperation.None;
        }
    }

    // ==================================================
    // Runner
    // ==================================================

    private NetworkRunner CreateRunner()
    {
        if (runner != null)
        {
            throw new InvalidOperationException(
                "이미 NetworkRunner가 존재합니다.");
        }

        if (runnerPrefab == null)
        {
            throw new InvalidOperationException(
                "NetworkRunner Prefab이 등록되지 않았습니다.");
        }

        runner =
            Instantiate(
                runnerPrefab,
                transform);

        runner.name =
            "NetworkRunner";

        sceneManager =
            runner.GetComponentInChildren<
                NetworkSceneManagerDefault>();

        if (sceneManager == null)
        {
            sceneManager =
                runner.gameObject.AddComponent<
                    NetworkSceneManagerDefault>();
        }

        runner.AddCallbacks(
            this);

        return runner;
    }

    public bool TryLoadScene(
        string sceneName,
        LoadSceneMode loadMode,
        out NetworkSceneAsyncOp operation)
    {
        operation =
            default;

        if (runner == null ||
            !runner.IsRunning)
        {
            return false;
        }

        if (!runner.IsSceneAuthority)
            return false;

        if (runner.IsSceneManagerBusy)
            return false;

        if (string.IsNullOrWhiteSpace(
                sceneName))
        {
            return false;
        }

        try
        {
            operation =
                runner.LoadScene(
                    sceneName,
                    loadMode);

            return operation.IsValid;
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[Fusion] Scene Load 요청 실패: {e.Message}",
                this);

            return false;
        }
    }

    // ==================================================
    // Game Session
    // ==================================================

    private void EnsureGameSession(
        NetworkRunner targetRunner)
    {
        if (!targetRunner.IsServer)
            return;

        if (gameSession != null)
            return;

        if (networkGameSessionPrefab == null)
        {
            Debug.LogError(
                "NetworkGameSession Prefab이 등록되지 않았습니다.",
                this);

            return;
        }

        NetworkGameSession spawnedSession =
            targetRunner.Spawn(
                networkGameSessionPrefab,
                flags:
                    NetworkSpawnFlags.DontDestroyOnLoad);

        if (spawnedSession == null)
        {
            Debug.LogError(
                "NetworkGameSession Spawn에 실패했습니다.",
                this);

            return;
        }

        SetGameSession(
            spawnedSession);
    }

    private void OnGameSessionSpawned(
        NetworkGameSession session)
    {
        if (session == null)
            return;

        // Host/Client 공통.
        // 현재 FSC가 관리 중인 Runner의 NGS만 저장한다.
        if (runner == null ||
            session.Runner != runner)
        {
            return;
        }

        SetGameSession(
            session);
    }

    private void OnGameSessionDespawned(
        NetworkGameSession session)
    {
        if (gameSession !=
            session)
        {
            return;
        }

        SetGameSession(
            null);
    }

    private void SetGameSession(
        NetworkGameSession session)
    {
        if (gameSession ==
            session)
        {
            return;
        }

        gameSession =
            session;

        GameSessionChanged?.Invoke(
            gameSession);
    }

    // ==================================================
    // Runner Cleanup
    // ==================================================

    private async Task DisposeFailedRunnerAsync(
        NetworkRunner target)
    {
        if (target == null)
            return;

        try
        {
            await target.Shutdown();
        }
        catch
        {
            // 실패 Runner는 어차피 재사용하지 않는다.
        }

        // 정상 Shutdown callback이 이미 정리했다면
        // 현재 runner는 target이 아니다.
        if (runner ==
            target)
        {
            ForceDestroyRunner(
                target);
        }
    }

    private void ForceDestroyRunner(
        NetworkRunner target)
    {
        if (target == null)
            return;

        if (runner ==
            target)
        {
            runner =
                null;

            sceneManager =
                null;

            SetGameSession(
                null);

            CurrentRoomName =
                null;

            ClearSessionList();
        }

        if (target != null)
        {
            Destroy(
                target.gameObject);
        }
    }

    // ==================================================
    // State
    // ==================================================

    private bool CanStartRoomOperation()
    {
        if (state !=
            NetworkSessionState.LobbyReady)
        {
            return false;
        }

        if (runner == null)
            return false;

        if (activeOperation !=
            NetworkOperation.None)
        {
            return false;
        }

        return true;
    }

    private void SetState(
        NetworkSessionState newState)
    {
        if (state ==
            newState)
        {
            return;
        }

        state =
            newState;

        Debug.Log(
            $"[Fusion] Session State -> {state}",
            this);

        StateChanged?.Invoke(
            state);
    }

    private void RaiseFailure(
        NetworkOperation operation,
        string message,
        ShutdownReason? shutdownReason = null)
    {
        if (string.IsNullOrWhiteSpace(
                message))
        {
            message =
                shutdownReason?.ToString()
                ?? "Unknown network error";
        }

        Debug.LogWarning(
            $"[Fusion] {operation} Failed: {message}",
            this);

        OperationFailed?.Invoke(
            new NetworkOperationFailure(
                operation,
                message,
                shutdownReason));
    }

    private void ClearSessionList()
    {
        if (sessions.Count == 0)
            return;

        sessions.Clear();

        SessionListChanged?.Invoke(
            sessions);
    }

    // ==================================================
    // Fusion Callbacks - Players
    // ==================================================

    public void OnPlayerJoined(
        NetworkRunner callbackRunner,
        PlayerRef player)
    {
        if (callbackRunner !=
            runner)
        {
            return;
        }

        if (!callbackRunner.IsServer)
            return;

        EnsurePlayerData(
            callbackRunner,
            player);
    }

    public void OnPlayerLeft(
        NetworkRunner callbackRunner,
        PlayerRef player)
    {
    }

    // ==================================================
    // Fusion Callbacks - Lobby / Connection
    // ==================================================

    public void OnSessionListUpdated(
        NetworkRunner callbackRunner,
        List<SessionInfo> sessionList)
    {
        if (callbackRunner !=
            runner)
        {
            return;
        }

        sessions.Clear();

        if (sessionList != null)
        {
            sessions.AddRange(
                sessionList);
        }

        SessionListChanged?.Invoke(
            sessions);
    }

    public void OnConnectedToServer(
        NetworkRunner callbackRunner)
    {
    }

    public void OnDisconnectedFromServer(
        NetworkRunner callbackRunner,
        NetDisconnectReason reason)
    {
        if (callbackRunner !=
            runner)
        {
            return;
        }

        // 실제 상태 정리와 ConnectionLost 발생은
        // OnShutdown에서 일괄 처리한다.
        Debug.LogWarning(
            $"[Fusion] Disconnected: {reason}",
            this);
    }

    public void OnConnectRequest(
        NetworkRunner callbackRunner,
        NetworkRunnerCallbackArgs.ConnectRequest request,
        byte[] token)
    {
        if (callbackRunner !=
            runner)
        {
            request.Refuse();
            return;
        }

        // 현재는 별도 입장 정책이 없으므로 허용한다.
        // Session의 PlayerCount 제한은 Fusion Session 설정이 담당한다.
        request.Accept();
    }

    public void OnConnectFailed(
        NetworkRunner callbackRunner,
        NetAddress remoteAddress,
        NetConnectFailedReason reason)
    {
        if (callbackRunner !=
            runner)
        {
            return;
        }

        Debug.LogWarning(
            $"[Fusion] Connect Failed: {reason}",
            this);
    }

    public void OnCustomAuthenticationResponse(
        NetworkRunner callbackRunner,
        Dictionary<string, object> data)
    {
    }

    // ==================================================
    // Fusion Callbacks - Shutdown
    // ==================================================

    public void OnShutdown(
        NetworkRunner callbackRunner,
        ShutdownReason shutdownReason)
    {
        if (callbackRunner !=
            runner)
        {
            return;
        }

        bool wasUnexpected =
            activeOperation ==
                NetworkOperation.None &&
            state !=
                NetworkSessionState.ShuttingDown &&
            shutdownReason !=
                ShutdownReason.Ok;

        NetworkRunner stoppedRunner =
            callbackRunner;

        // 먼저 FSC의 현재 네트워크 상태를 정리한다.
        runner =
            null;

        sceneManager =
            null;

        SetGameSession(
            null);

        CurrentRoomName =
            null;

        ClearSessionList();

        SetState(
            NetworkSessionState.Offline);

        // NetworkRunner는 Shutdown 후 재사용하지 않는다.
        if (stoppedRunner != null)
        {
            Destroy(
                stoppedRunner.gameObject);
        }

        // 정상 LeaveRoomAsync에서는 activeOperation이 LeaveRoom이고
        // state도 ShuttingDown이므로 여기로 들어오지 않는다.
        //
        // Host가 종료되거나 연결이 끊긴 Client만
        // ConnectionLost를 UI에 알린다.
        if (wasUnexpected)
        {
            RaiseFailure(
                NetworkOperation.ConnectionLost,
                $"네트워크 연결이 종료되었습니다. ({shutdownReason})",
                shutdownReason);
        }
    }

    // ==================================================
    // Fusion Callbacks - Scene
    // ==================================================

    public void OnSceneLoadStart(
        NetworkRunner callbackRunner)
    {
        if (callbackRunner !=
            runner)
        {
            return;
        }

        SceneLoadStarted?.Invoke();
    }

    public void OnSceneLoadDone(
        NetworkRunner callbackRunner)
    {
        if (callbackRunner !=
            runner)
        {
            return;
        }

        SceneLoadCompleted?.Invoke();
    }

    // ==================================================
    // Fusion Callbacks - Input
    // ==================================================

    public void OnInput(
        NetworkRunner callbackRunner,
        NetworkInput input)
    {
    }

    public void OnInputMissing(
        NetworkRunner callbackRunner,
        PlayerRef player,
        NetworkInput input)
    {
    }

    // ==================================================
    // Fusion Callbacks - Other
    // ==================================================

    public void OnObjectEnterAOI(
        NetworkRunner callbackRunner,
        NetworkObject obj,
        PlayerRef player)
    {
    }

    public void OnObjectExitAOI(
        NetworkRunner callbackRunner,
        NetworkObject obj,
        PlayerRef player)
    {
    }

    public void OnUserSimulationMessage(
        NetworkRunner callbackRunner,
        SimulationMessagePtr message)
    {
    }

    public void OnHostMigration(
        NetworkRunner callbackRunner,
        HostMigrationToken hostMigrationToken)
    {
        // 현재 프로젝트에서는 Host Migration 미구현.
    }

    public void OnReliableDataReceived(
        NetworkRunner callbackRunner,
        PlayerRef player,
        ReliableKey key,
        ArraySegment<byte> data)
    {
    }

    public void OnReliableDataProgress(
        NetworkRunner callbackRunner,
        PlayerRef player,
        ReliableKey key,
        float progress)
    {
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
    {
        throw new NotImplementedException();
    }
}