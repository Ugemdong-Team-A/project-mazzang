using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class NetworkGameSession : NetworkBehaviour
{
    [Header("Scenes")]
    [SerializeField]
    private string lobbySceneName = "Lobby";

    [SerializeField]
    private string gameplaySceneName = "Gameplay";

    [Header("Temporary Map Selection")]
    [SerializeField]
    private MapData defaultMap;


    public MapData SelectedMapData =>
        _selectedMap;


    private MapData _selectedMap;

    private bool _startRequested;
    private bool _returnRequested;
    private bool _gameStarted;


    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            _selectedMap =
                defaultMap;
        }

        if (AppRoot.Instance != null &&
            AppRoot.Instance.Network != null)
        {
            AppRoot.Instance.Network.SceneLoadCompleted +=
                OnSceneLoadCompleted;
        }
    }


    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        if (AppRoot.Instance != null &&
            AppRoot.Instance.Network != null)
        {
            AppRoot.Instance.Network.SceneLoadCompleted -=
                OnSceneLoadCompleted;
        }
    }


    // ==================================================
    // Start Game
    // ==================================================

    public bool RequestStartGame()
    {
        if (!HasStateAuthority)
            return false;

        if (_startRequested ||
            _returnRequested ||
            _gameStarted)
        {
            return false;
        }

        if (!AreAllPlayersReady())
            return false;

        if (defaultMap == null ||
            defaultMap.MapPrefab == null)
        {
            Debug.LogError(
                "기본 MapData 또는 MapPrefab이 등록되지 않았습니다.",
                this);

            return false;
        }

        if (string.IsNullOrWhiteSpace(
                gameplaySceneName))
        {
            Debug.LogError(
                "Gameplay Scene 이름이 등록되지 않았습니다.",
                this);

            return false;
        }

        _selectedMap =
            defaultMap;

        FusionSessionController network =
            AppRoot.Instance.Network;

        _startRequested =
            true;

        bool loadStarted =
            network.TryLoadScene(
                gameplaySceneName,
                LoadSceneMode.Single,
                out _);

        if (!loadStarted)
        {
            _startRequested =
                false;

            return false;
        }

        return true;
    }


    // ==================================================
    // Return Lobby
    // ==================================================

    public bool RequestReturnToLobby()
    {
        if (!HasStateAuthority)
            return false;

        if (_startRequested ||
            _returnRequested ||
            !_gameStarted)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                lobbySceneName))
        {
            Debug.LogError(
                "Lobby Scene 이름이 등록되지 않았습니다.",
                this);

            return false;
        }

        FusionSessionController network =
            AppRoot.Instance.Network;

        _returnRequested =
            true;

        bool loadStarted =
            network.TryLoadScene(
                lobbySceneName,
                LoadSceneMode.Single,
                out _);

        if (!loadStarted)
        {
            _returnRequested =
                false;

            return false;
        }

        ResetPlayersForLobby();

        return true;
    }


    private void ResetPlayersForLobby()
    {
        foreach (PlayerRef player
                 in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(
                    player,
                    out NetworkObject playerObject))
            {
                continue;
            }

            if (!playerObject.TryGetComponent(
                    out NetworkPlayerData playerData))
            {
                continue;
            }

            playerData.ResetForLobby();
        }
    }


    // ==================================================
    // Ready
    // ==================================================

    private bool AreAllPlayersReady()
    {
        int playerCount = 0;

        foreach (PlayerRef player
                 in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(
                    player,
                    out NetworkObject playerObject))
            {
                return false;
            }

            NetworkPlayerData playerData =
                playerObject.GetComponent<
                    NetworkPlayerData>();

            if (playerData == null ||
                !playerData.Ready)
            {
                return false;
            }

            playerCount++;
        }

        return playerCount > 0;
    }


    // ==================================================
    // Scene
    // ==================================================

    private void OnSceneLoadCompleted()
    {
        if (!HasStateAuthority)
            return;

        if (_startRequested)
        {
            CompleteStartGame();
            return;
        }

        if (_returnRequested)
        {
            CompleteReturnToLobby();
        }
    }


    private void CompleteStartGame()
    {
        if (_selectedMap == null ||
            _selectedMap.MapPrefab == null)
        {
            Debug.LogError(
                "선택된 MapData가 올바르지 않습니다.",
                this);

            _startRequested =
                false;

            return;
        }

        _startRequested =
            false;

        _gameStarted =
            true;

        Debug.Log(
            $"[Game] Game Started: {_selectedMap.DisplayName}",
            this);
    }


    private void CompleteReturnToLobby()
    {
        _returnRequested =
            false;

        _gameStarted =
            false;

        _selectedMap =
            defaultMap;

        Debug.Log(
            "[Game] Returned To Lobby.",
            this);
    }
}