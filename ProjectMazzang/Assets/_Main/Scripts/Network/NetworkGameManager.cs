using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public enum MatchState : byte
{
    Playing = 0,
    Ending = 1,
    Result = 2,
    Finished = 3
}

public sealed class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Header("Respawn")]
    [SerializeField]
    private float respawnHeight = 2f;

    [Header("Match")]
    [SerializeField]
    private float endingDuration = 2.1f;

    [SerializeField]
    private float resultDuration = 3f;

    private MapRuntime _currentMap;

    // =========================================================
    // Network State
    // =========================================================

    [Networked,
     OnChangedRender(nameof(OnMatchStateChanged))]
    public MatchState State { get; private set; }

    [Networked]
    public PlayerRef Winner { get; private set; }

    [Networked]
    private TickTimer PhaseTimer { get; set; }


    // =========================================================
    // Local Events
    // =========================================================

    // GameUIController 같은 씬 로컬 객체가
    // NetworkGameManager의 Spawn 시점을 몰라도
    // 안전하게 바인딩하기 위한 이벤트.
    public static event Action<NetworkGameManager> LocalSpawned;

    public static event Action<NetworkGameManager> LocalDespawned;

    // Network 상태가 각 로컬에 반영되었을 때 발생.
    // UI / Camera 등의 Presentation에서 사용한다.
    public event Action<MatchState> StateChanged;


    // =========================================================
    // Public State
    // =========================================================

    public bool IsPlaying =>
        State == MatchState.Playing;


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        Instance = this;

        BindPlayerLeaveEvent();

        if (HasStateAuthority)
        {
            Winner = PlayerRef.None;
            PhaseTimer = TickTimer.None;
            State = MatchState.Playing;

            InitializeGame();
        }

        LocalSpawned?.Invoke(this);
    }


    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        switch (State)
        {
            case MatchState.Playing:
                break;

            case MatchState.Ending:
                UpdateEnding();
                break;

            case MatchState.Result:
                UpdateResult();
                break;

            case MatchState.Finished:
                break;
        }
    }


    public override void Despawned(
    NetworkRunner runner,
    bool hasState)
    {
        UnbindPlayerLeaveEvent();

        LocalDespawned?.Invoke(this);

        if (Instance == this)
        {
            Instance = null;
        }
    }


    // =========================================================
    // Local Presentation Notification
    // =========================================================

    private void OnMatchStateChanged()
    {
        StateChanged?.Invoke(State);
    }


    // =========================================================
    // Match
    // =========================================================

    public void ReportPlayerEliminated(
        PlayerRef eliminatedPlayer,
        PlayerRef attacker)
    {
        if (!HasStateAuthority)
            return;

        if (State != MatchState.Playing)
            return;

        // eliminatedPlayer / attacker는
        // 이후 KillFeed, Score 등의 매치 기록에 활용 가능.
        CheckMatchResult();
    }


    private void CheckMatchResult()
    {
        int remainingCount = 0;

        PlayerRef remainingPlayer =
            PlayerRef.None;

        foreach (PlayerRef player
                 in Runner.ActivePlayers)
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

            // Respawn 대기 중인 플레이어도
            // 아직 매치에서 탈락한 것이 아니므로
            // IsAlive가 아닌 Lives를 사용한다.
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

    // =========================================================
    // Respawn
    // =========================================================

    public bool TryRespawnPlayer(
        PlayerRef player)
    {
        if (!HasStateAuthority)
            return false;

        if (!Runner.TryGetPlayerObject(
                player,
                out NetworkObject dataObject))
        {
            return false;
        }

        if (!dataObject.TryGetComponent(
                out NetworkPlayerData playerData))
        {
            return false;
        }

        NetworkObject character =
            playerData.CharacterObject;

        if (character == null)
            return false;

        Transform spawnPoint =
            _currentMap.GetRandomSpawnPoint();

        if (spawnPoint == null)
            return false;

        Vector2 respawnPosition =
            (Vector2)spawnPoint.position +
            Vector2.up * respawnHeight;

        if (!character.TryGetComponent(
                out PlayerMovement movement))
        {
            return false;
        }

        movement.ResetForRespawn(
            respawnPosition);

        return true;
    }

    // =========================================================
    // Ending
    // =========================================================

    private void BeginEnding(
        PlayerRef winner)
    {
        if (State != MatchState.Playing)
            return;

        // Presentation이 Ending을 감지했을 때
        // Winner가 이미 확정되어 있도록 먼저 설정.
        Winner = winner;

        PhaseTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                endingDuration);

        State = MatchState.Ending;
    }


    private void UpdateEnding()
    {
        if (!PhaseTimer.Expired(Runner))
            return;

        BeginResult();
    }


    // =========================================================
    // Result
    // =========================================================

    private void BeginResult()
    {
        if (State != MatchState.Ending)
            return;

        PhaseTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                resultDuration);

        State = MatchState.Result;
    }


    private void UpdateResult()
    {
        if (!PhaseTimer.Expired(Runner))
            return;

        FinishMatch();
    }


    // =========================================================
    // Finished
    // =========================================================

    private void FinishMatch()
    {
        if (State != MatchState.Result)
            return;

        State = MatchState.Finished;
        PhaseTimer = TickTimer.None;

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


    // =========================================================
    // Initialize
    // =========================================================

    private void InitializeGame()
    {
        NetworkGameSession gameSession =
            AppRoot.Instance.Network.GameSession;

        if (gameSession == null)
        {
            Debug.LogError(
                "[GM] NetworkGameSession을 찾을 수 없습니다.",
                this);

            return;
        }

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
            Runner.Spawn(
                mapData.MapPrefab);

        if (mapObject == null)
        {
            Debug.LogError(
                "[GM] Map Spawn에 실패했습니다.",
                this);

            return;
        }

        MapRuntime map =
            mapObject.GetComponent<MapRuntime>();

        if (map == null)
        {
            Debug.LogError(
                "[GM] Spawn된 Map에 MapRuntime이 없습니다.",
                mapObject);

            return;
        }

        SpawnPlayers(map);
    }

    private void BindPlayerLeaveEvent()
    {
        FusionSessionController network =
            AppRoot.Instance != null
                ? AppRoot.Instance.Network
                : null;

        if (network == null)
            return;

        network.PlayerLeaving +=
            OnPlayerLeaving;
    }

    private void UnbindPlayerLeaveEvent()
    {
        FusionSessionController network =
            AppRoot.Instance != null
                ? AppRoot.Instance.Network
                : null;

        if (network == null)
            return;

        network.PlayerLeaving -=
            OnPlayerLeaving;
    }

    private void OnPlayerLeaving(
    PlayerRef player,
    NetworkPlayerData playerData)
    {
        if (!HasStateAuthority)
            return;

        if (playerData != null)
        {
            NetworkObject character =
                playerData.CharacterObject;

            if (character != null &&
                Runner.Exists(character))
            {
                if (character.TryGetComponent(
                        out PlayerWeaponController weaponController))
                {
                    weaponController.TryDropWeapon();
                }

                Runner.Despawn(
                    character);
            }
        }

        if (State ==
            MatchState.Playing)
        {
            CheckMatchResult();
        }
    }

    // =========================================================
    // Player Spawn
    // =========================================================


    private void SpawnPlayers(
        MapRuntime map)
    {
        _currentMap = map;

        NetworkGameSession gameSession =
            AppRoot.Instance.Network.GameSession;

        if (gameSession == null ||
            gameSession.CharacterCatalog == null)
        {
            Debug.LogError(
                "[GM] CharacterCatalog을 찾을 수 없습니다.",
                this);

            return;
        }

        int index = 0;

        Transform lastSpawnPoint = null;

        foreach (PlayerRef player
                 in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(
                    player,
                    out NetworkObject dataObject) ||
                !dataObject.TryGetComponent(
                    out NetworkPlayerData playerData))
            {
                Debug.LogError(
                    $"[GM] PlayerData를 찾을 수 없습니다: {player}",
                    this);

                continue;
            }

            CharacterData characterData =
                gameSession.CharacterCatalog.GetById(
                    playerData.SelectedCharacterId);

            if (characterData == null)
            {
                Debug.LogError(
                    $"[GM] CharacterId " +
                    $"{playerData.SelectedCharacterId}를 찾을 수 없습니다.",
                    this);

                continue;
            }

            NetworkObject playerPrefab =
                characterData.PlayerPrefab;

            if (playerPrefab == null)
            {
                Debug.LogError(
                    $"[GM] {characterData.DisplayName}의 " +
                    "PlayerPrefab이 null입니다.",
                    this);

                continue;
            }

            Transform spawnPoint =
                _currentMap.GetSpawnPoint(
                    index);

            if (spawnPoint == null)
            {
                Debug.LogError(
                    $"SpawnPoint가 부족합니다. Index: {index}",
                    this);

                spawnPoint = lastSpawnPoint; 
                // break;
            }

            NetworkObject playerObject =
                Runner.Spawn(
                    playerPrefab,
                    spawnPoint.position,
                    spawnPoint.rotation,
                    player);

            lastSpawnPoint = spawnPoint;

            if (playerObject == null)
            {
                Debug.LogError(
                    $"[GM] Player Spawn에 실패했습니다: {player}",
                    this);

                continue;
            }

            playerData.SetPlayerCharacter(
                playerObject);

            index++;
        }
    }
}