using UnityEngine;
using Fusion;

public enum MatchState : byte
{
    Playing,
    Ending,
    Finished
}

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [SerializeField]
    private NetworkObject defaultPlayerPrefab;

    [Header("Match")]
    [SerializeField]
    private float resultDuration = 3f;

    private MapRuntime _currentMap;


    [Networked,
        OnChangedRender(nameof(OnMatchStateChanged))]
    public MatchState State { get; private set; }

    [Networked]
    public PlayerRef Winner { get; private set; }

    [Networked]
    private TickTimer ResultTimer { get; set; }


    public override void Spawned()
    {
        Instance = this;

        if (!HasStateAuthority)
            return;

        State = MatchState.Playing;
        Winner = PlayerRef.None;

        InitializeGame();
    }


    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (State != MatchState.Ending)
            return;

        if (!ResultTimer.Expired(Runner))
            return;

        FinishMatch();
    }


    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }


    // =========================================================
    // Match
    // =========================================================

    private void OnMatchStateChanged()
    {
        if (State == MatchState.Ending)
        {
            PlayEndingPresentation();
        }
    }

    private void PlayEndingPresentation()
    {
        if (!Runner.TryGetPlayerObject(
                    Winner,
                    out NetworkObject dataObject))
        {
            return;
        }

        if (!dataObject.TryGetComponent(
                out NetworkPlayerData playerData))
        {
            return;
        }

        Transform winnerTransform = playerData.CharacterObject.transform;

        Debug.Log
                (winnerTransform);

        if (winnerTransform != null)
        {
            BattleCameraController.Instance?
                .FocusWinner(winnerTransform);
        }

        // GameUI.Instance?.ShowWinner(...);
        // Audio...
        // Fade...
    }

    public void ReportPlayerEliminated(
        PlayerRef eliminatedPlayer,
        PlayerRef attacker)
    {
        if (!HasStateAuthority)
            return;

        if (State != MatchState.Playing)
            return;

        CheckMatchResult();
    }


    private void CheckMatchResult()
    {
        int remainingCount = 0;
        PlayerRef remainingPlayer = PlayerRef.None;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(
                    player,
                    out NetworkObject dataObject))
            {
                continue;
            }

            if (!dataObject.TryGetComponent(
                    out NetworkPlayerData playerData))
            {
                continue;
            }

            NetworkObject character =
                playerData.CharacterObject;

            if (character == null)
                continue;

            if (!character.TryGetComponent(
                    out PlayerHealth health))
            {
                continue;
            }

            // IsAlive를 쓰면 안 됨.
            // Respawn 대기 중인 플레이어도 매치에서는 생존자이기 때문.
            if (health.Lives <= 0)
                continue;

            remainingCount++;
            remainingPlayer = player;
        }

        if (remainingCount == 1)
        {
            BeginEnding(
                remainingPlayer);

            return;
        }

        if (remainingCount == 0)
        {
            BeginEnding(
                PlayerRef.None);
        }
    }


    private void BeginEnding(
        PlayerRef winner)
    {
        if (State != MatchState.Playing)
            return;

        Winner = winner;
        State = MatchState.Ending;

        ResultTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                resultDuration);
    }


    private void FinishMatch()
    {
        if (State != MatchState.Ending)
            return;

        State =
            MatchState.Finished;

        ResultTimer =
            TickTimer.None;

        NetworkGameSession gameSession =
            AppRoot.Instance.Network.GameSession;

        if (gameSession == null)
        {
            Debug.LogError(
                "[GM] NetworkGameSession을 찾을 수 없습니다.",
                this);

            return;
        }

        if (!gameSession.RequestReturnToLobby())
        {
            Debug.LogError(
                "[GM] Lobby 복귀 요청에 실패했습니다.",
                this);
        }
    }

    private void InitializeGame()
    {
        NetworkGameSession gameSession =
            AppRoot.Instance.Network.GameSession;

        MapData mapData =
            gameSession.SelectedMapData;

        if (mapData == null ||
            mapData.MapPrefab == null)
        {
            Debug.LogError(
                "선택된 MapData가 올바르지 않습니다.",
                this);

            return;
        }

        NetworkObject mapObject =
            Runner.Spawn(mapData.MapPrefab);

        MapRuntime map =
            mapObject.GetComponent<MapRuntime>();

        SpawnPlayers(map);
    }

    private void SpawnPlayers(MapRuntime map)
    {
        _currentMap = map;

        if (defaultPlayerPrefab == null)
        {
            Debug.LogError("[GM] 플레이어 프리팹이 null 입니다.");
            return;
        }

        int index = 0;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            Transform spawnPoint =
                _currentMap.GetSpawnPoint(index);

            if (spawnPoint == null)
            {
                Debug.LogError(
                    $"SpawnPoint가 부족합니다. Index: {index}",
                    this);

                break;
            }

            NetworkObject playerObj = Runner.Spawn(
                defaultPlayerPrefab,
                spawnPoint.position,
                spawnPoint.rotation,
                player);

            if (Runner.TryGetPlayerObject(player, out NetworkObject dataObj))
                if (dataObj.TryGetComponent(out NetworkPlayerData data))
                    data.SetPlayerCharacter(playerObj);

            index++;
        }
    }
}
