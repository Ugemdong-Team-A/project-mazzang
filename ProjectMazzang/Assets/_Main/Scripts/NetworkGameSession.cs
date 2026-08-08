using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class NetworkGameSession : NetworkBehaviour
{
    [Header("Gameplay Scene")]
    [SerializeField]
    private string gameplaySceneName = "Gameplay";

    [Header("Temporary Map Selection")]
    [SerializeField]
    private MapData defaultMapData;

    public MapData SelectedMapData { get; private set; }

    private bool startRequested;
    private bool gameStarted;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            SelectedMapData = defaultMapData;
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

    public MapData GetSelectedMapData()
    {
        return SelectedMapData;
    }

    public bool RequestStartGame()
    {
        if (!HasStateAuthority)
            return false;

        if (startRequested || gameStarted)
            return false;

        if (!AreAllPlayersReady())
            return false;

        if (defaultMapData == null ||
            defaultMapData.MapPrefab == null)
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

        if(SelectedMapData == null)
            SelectedMapData = defaultMapData;

        FusionSessionController network =
            AppRoot.Instance.Network;

        startRequested = true;

        bool loadStarted =
            network.TryLoadScene(
                gameplaySceneName,
                LoadSceneMode.Single,
                out _);

        if (!loadStarted)
        {
            startRequested = false;
            return false;
        }

        return true;
    }

    private bool AreAllPlayersReady()
    {
        int playerCount = 0;

        foreach (PlayerRef player in Runner.ActivePlayers)
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

    private void OnSceneLoadCompleted()
    {
        if (!HasStateAuthority)
            return;

        if (!startRequested || gameStarted)
            return;

        startRequested = false;
        gameStarted = true;
    }
}