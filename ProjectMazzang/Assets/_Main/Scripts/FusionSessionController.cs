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

    // --------------------------------------------------
    // Public State
    // --------------------------------------------------

    public NetworkRunner Runner => runner;

    public NetworkGameSession GameSession =>
        gameSession;

    public NetworkSessionState State => state;

    public IReadOnlyList<SessionInfo> Sessions => sessions;

    public string CurrentRoomName { get; private set; }

    public bool IsBusy =>
        state == NetworkSessionState.LobbyConnecting ||
        state == NetworkSessionState.RoomConnecting ||
        state == NetworkSessionState.ShuttingDown;

    // --------------------------------------------------
    // Events
    // --------------------------------------------------

    public event Action<NetworkSessionState> StateChanged;

    public event Action<IReadOnlyList<SessionInfo>>
        SessionListChanged;

    public event Action<NetworkOperationFailure>
        OperationFailed;

    public event Action SceneLoadStarted;
    public event Action SceneLoadCompleted;

    // ==================================================
    // Lobby
    // ==================================================

    public async Task<bool> ConnectLobbyAsync()
    {
        // 중복 요청 방지.
        if (state != NetworkSessionState.Offline)
            return false;

        if (activeOperation != NetworkOperation.None)
            return false;

        activeOperation = NetworkOperation.ConnectLobby;

        NetworkRunner newRunner = null;

        try
        {
            newRunner = CreateRunner();

            SetState(NetworkSessionState.LobbyConnecting);

            StartGameResult result =
                await newRunner.JoinSessionLobby(
                    SessionLobby.ClientServer);

            if (!result.Ok)
            {
                await DisposeFailedRunnerAsync(newRunner);

                SetState(NetworkSessionState.Offline);

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

            SetState(NetworkSessionState.LobbyReady);

            return true;
        }
        catch (Exception e)
        {
            if (newRunner != null)
            {
                await DisposeFailedRunnerAsync(newRunner);
            }

            SetState(NetworkSessionState.Offline);

            RaiseFailure(
                NetworkOperation.ConnectLobby,
                e.Message);

            return false;
        }
        finally
        {
            activeOperation = NetworkOperation.None;
        }
    }

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
            flags: NetworkSpawnFlags.DontDestroyOnLoad);

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

        activeOperation = NetworkOperation.CreateRoom;

        NetworkRunner currentRunner = runner;

        if (currentRunner.IsServer)
        {
            EnsurePlayerDataForActivePlayers(
                currentRunner);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                roomName =
                    $"Room_{Guid.NewGuid():N}"
                    .Substring(0, 13);
            }

            roomName = roomName.Trim();

            SetState(NetworkSessionState.RoomConnecting);

            StartGameResult result =
                await currentRunner.StartGame(
                    new StartGameArgs
                    {
                        GameMode = GameMode.Host,

                        SessionName = roomName,

                        PlayerCount = defaultMaxPlayers,

                        IsOpen = true,
                        IsVisible = true,

                        SceneManager = sceneManager
                    });

            if (!result.Ok)
            {
                await DisposeFailedRunnerAsync(
                    currentRunner);

                SetState(NetworkSessionState.Offline);

                RaiseFailure(
                    NetworkOperation.CreateRoom,
                    result.ErrorMessage,
                    result.ShutdownReason);

                return false;
            }

            if (runner != currentRunner)
                return false;

            CurrentRoomName = roomName;

            EnsureGameSession(
                currentRunner);

            ClearSessionList();

            SetState(NetworkSessionState.InRoom);

            return true;
        }
        catch (Exception e)
        {
            await DisposeFailedRunnerAsync(
                currentRunner);

            SetState(NetworkSessionState.Offline);

            RaiseFailure(
                NetworkOperation.CreateRoom,
                e.Message);

            return false;
        }
        finally
        {
            activeOperation = NetworkOperation.None;
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

        if (string.IsNullOrWhiteSpace(sessionName))
            return false;

        activeOperation = NetworkOperation.JoinRoom;

        NetworkRunner currentRunner = runner;

        try
        {
            SetState(NetworkSessionState.RoomConnecting);

            StartGameResult result =
                await currentRunner.StartGame(
                    new StartGameArgs
                    {
                        GameMode = GameMode.Client,

                        SessionName = sessionName,

                        // Client가 대상 Room을 못 찾았다고
                        // 새 Session을 만들어 버리는 의도가 아님.
                        EnableClientSessionCreation = false,

                        SceneManager = sceneManager
                    });

            if (!result.Ok)
            {
                await DisposeFailedRunnerAsync(
                    currentRunner);

                SetState(NetworkSessionState.Offline);

                RaiseFailure(
                    NetworkOperation.JoinRoom,
                    result.ErrorMessage,
                    result.ShutdownReason);

                return false;
            }

            if (runner != currentRunner)
                return false;

            CurrentRoomName = sessionName;

            ClearSessionList();

            SetState(NetworkSessionState.InRoom);

            return true;
        }
        catch (Exception e)
        {
            await DisposeFailedRunnerAsync(
                currentRunner);

            SetState(NetworkSessionState.Offline);

            RaiseFailure(
                NetworkOperation.JoinRoom,
                e.Message);

            return false;
        }
        finally
        {
            activeOperation = NetworkOperation.None;
        }
    }

    // ==================================================
    // Leave
    // ==================================================

    public async Task<bool> LeaveRoomAsync()
    {
        if (runner == null)
            return false;

        if (state == NetworkSessionState.ShuttingDown)
            return false;

        if (activeOperation != NetworkOperation.None)
            return false;

        activeOperation = NetworkOperation.LeaveRoom;

        NetworkRunner currentRunner = runner;

        try
        {
            SetState(NetworkSessionState.ShuttingDown);

            await currentRunner.Shutdown();

            // NetworkRunner는 종료 후 재사용하지 않는다.
            if (runner == currentRunner)
                runner = null;

            if (currentRunner != null)
            {
                Destroy(currentRunner.gameObject);
            }

            sceneManager = null;
            gameSession = null;

            CurrentRoomName = null;

            ClearSessionList();

            SetState(NetworkSessionState.Offline);

            return true;
        }
        catch (Exception e)
        {
            ForceDestroyRunner(currentRunner);

            SetState(NetworkSessionState.Offline);

            RaiseFailure(
                NetworkOperation.LeaveRoom,
                e.Message);

            return false;
        }
        finally
        {
            activeOperation = NetworkOperation.None;
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

        runner = Instantiate(
            runnerPrefab,
            transform);

        runner.name = "NetworkRunner";

        sceneManager =
            runner.GetComponentInChildren<
                NetworkSceneManagerDefault>();

        if (sceneManager == null)
        {
            sceneManager =
                runner.gameObject.AddComponent<
                    NetworkSceneManagerDefault>();
        }

        runner.AddCallbacks(this);

        return runner;
    }

    public bool TryLoadScene(
        string sceneName,
        LoadSceneMode loadMode,
        out NetworkSceneAsyncOp operation)
    {
        operation = default;

        if (runner == null ||
            !runner.IsRunning)
        {
            return false;
        }

        if (!runner.IsSceneAuthority)
            return false;

        if (runner.IsSceneManagerBusy)
            return false;

        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        try
        {
            operation = runner.LoadScene(
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

        gameSession = targetRunner.Spawn(
            networkGameSessionPrefab,
            flags: NetworkSpawnFlags.DontDestroyOnLoad);

        if (gameSession == null)
        {
            Debug.LogError(
                "NetworkGameSession Spawn에 실패했습니다.",
                this);
        }
    }

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

        if (runner == target)
            runner = null;

        sceneManager = null;
        gameSession = null;

        if (target != null)
        {
            Destroy(target.gameObject);
        }

        CurrentRoomName = null;

        ClearSessionList();
    }

    private void ForceDestroyRunner(
        NetworkRunner target)
    {
        if (runner == target)
            runner = null;

        sceneManager = null;
        gameSession = null;

        if (target != null)
            Destroy(target.gameObject);

        CurrentRoomName = null;

        ClearSessionList();
    }

    // ==================================================
    // State
    // ==================================================

    private bool CanStartRoomOperation()
    {
        if (state != NetworkSessionState.LobbyReady)
            return false;

        if (runner == null)
            return false;

        if (activeOperation != NetworkOperation.None)
            return false;

        return true;
    }

    private void SetState(
        NetworkSessionState newState)
    {
        if (state == newState)
            return;

        state = newState;

        Debug.Log(
            $"[Fusion] Session State -> {state}",
            this);

        StateChanged?.Invoke(state);
    }

    private void RaiseFailure(
        NetworkOperation operation,
        string message,
        ShutdownReason? shutdownReason = null)
    {
        if (string.IsNullOrWhiteSpace(message))
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

        SessionListChanged?.Invoke(sessions);
    }

    // ==================================================
    // Fusion Callbacks
    // ==================================================

    public void OnSessionListUpdated(
        NetworkRunner callbackRunner,
        List<SessionInfo> sessionList)
    {
        if (callbackRunner != runner)
            return;

        sessions.Clear();

        if (sessionList != null)
        {
            sessions.AddRange(sessionList);
        }

        SessionListChanged?.Invoke(sessions);
    }

    public void OnShutdown(
        NetworkRunner callbackRunner,
        ShutdownReason shutdownReason)
    {
        if (callbackRunner != runner)
            return;

        bool wasUnexpected =
            activeOperation == NetworkOperation.None &&
            state != NetworkSessionState.ShuttingDown &&
            shutdownReason != ShutdownReason.Ok;

        runner = null;
        sceneManager = null;
        gameSession = null;

        CurrentRoomName = null;

        ClearSessionList();

        SetState(NetworkSessionState.Offline);

        if (wasUnexpected)
        {
            RaiseFailure(
                NetworkOperation.ConnectionLost,
                $"네트워크 연결이 종료되었습니다. ({shutdownReason})",
                shutdownReason);
        }
    }

    public void OnDisconnectedFromServer(
        NetworkRunner callbackRunner,
        NetDisconnectReason reason)
    {
        if (callbackRunner != runner)
            return;

        Debug.LogWarning(
            $"[Fusion] Disconnected: {reason}",
            this);
    }

    public void OnConnectFailed(
        NetworkRunner callbackRunner,
        NetAddress remoteAddress,
        NetConnectFailedReason reason)
    {
        if (callbackRunner != runner)
            return;

        Debug.LogWarning(
            $"[Fusion] Connect Failed: {reason}",
            this);
    }

    // --------------------------------------------------
    // 현재 단계에서는 사용하지 않는 Callback
    // --------------------------------------------------

    public void OnConnectedToServer(
        NetworkRunner runner)
    {
    }

    public void OnPlayerJoined(
    NetworkRunner callbackRunner,
    PlayerRef player)
    {
        if (callbackRunner != runner)
            return;

        // Host / Server만 NetworkObject를 생성한다.
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
        if (callbackRunner != runner)
            return;

        if (!callbackRunner.IsServer)
            return;

        if (!callbackRunner.TryGetPlayerObject(
                player,
                out NetworkObject playerObject))
        {
            return;
        }

        callbackRunner.Despawn(
            playerObject);
    }

    public void OnInput(
        NetworkRunner runner,
        NetworkInput input)
    {
    }

    public void OnInputMissing(
        NetworkRunner runner,
        PlayerRef player,
        NetworkInput input)
    {
    }

    public void OnConnectRequest(
        NetworkRunner runner,
        NetworkRunnerCallbackArgs.ConnectRequest request,
        byte[] token)
    {
    }

    public void OnCustomAuthenticationResponse(
        NetworkRunner runner,
        Dictionary<string, object> data)
    {
    }

    public void OnHostMigration(
        NetworkRunner runner,
        HostMigrationToken hostMigrationToken)
    {
    }

    public void OnSceneLoadStart(
        NetworkRunner callbackRunner)
    {
        if (callbackRunner != runner)
            return;

        SceneLoadStarted?.Invoke();
    }

    public void OnSceneLoadDone(
        NetworkRunner callbackRunner)
    {
        if (callbackRunner != runner)
            return;

        SceneLoadCompleted?.Invoke();
    }

    public void OnObjectEnterAOI(
        NetworkRunner runner,
        NetworkObject obj,
        PlayerRef player)
    {
    }

    public void OnObjectExitAOI(
        NetworkRunner runner,
        NetworkObject obj,
        PlayerRef player)
    {
    }

    public void OnReliableDataReceived(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        ReadOnlySpan<byte> data)
    {
    }

    public void OnReliableDataProgress(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        float progress)
    {
    }

    public void OnUserSimulationMessage(
        NetworkRunner runner,
        SimulationMessagePtr message)
    {
    }
}