using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum LobbySelectionPhase : byte
{
    CharacterSelect = 0,
    MapVote = 1,
    MapRoulette = 2,
    Starting = 3,
    Playing = 4,
    Returning = 5
}

public sealed class NetworkGameSession :
    NetworkBehaviour
{
    [Header("Scenes")]
    [SerializeField]
    private string lobbySceneName = "Lobby";

    [SerializeField]
    private string gameplaySceneName = "Gameplay";

    [Header("Selection Data")]
    [SerializeField]
    private CharacterCatalog characterCatalog;

    [SerializeField]
    private MapCatalog mapCatalog;

    [Header("Map Vote")]
    [Min(1f)]
    [SerializeField]
    private float mapVoteDuration = 10f;

    [Min(0f)]
    [SerializeField]
    private float mapRouletteDuration = 2.5f;

    private bool _gameplayLoadRequested;
    private bool _lobbyLoadRequested;

    // ==================================================
    // Network State
    // ==================================================

    [Networked,
     OnChangedRender(nameof(OnPhaseChanged))]
    public LobbySelectionPhase Phase
    {
        get;
        private set;
    }

    [Networked,
     OnChangedRender(nameof(OnSelectionStateChanged))]
    public int SelectedMapId
    {
        get;
        private set;
    }

    [Networked]
    private TickTimer PhaseTimer
    {
        get;
        set;
    }

    // ==================================================
    // Local Events
    // ==================================================

    // 씬 로컬 UI가 Spawn 순서를 몰라도 바인딩하기 위한 lifecycle event.
    // Instance 접근용 singleton은 두지 않습니다.
    public static event Action<
        NetworkGameSession> LocalSpawned;

    public static event Action<
        NetworkGameSession> LocalDespawned;

    public event Action<
        LobbySelectionPhase> PhaseChanged;

    public event Action SelectionStateChanged;

    // ==================================================
    // Public Data
    // ==================================================

    public string LobbySceneName =>
        lobbySceneName;

    public CharacterCatalog CharacterCatalog =>
        characterCatalog;

    public MapCatalog MapCatalog =>
        mapCatalog;

    public MapData SelectedMapData =>
        mapCatalog != null
            ? mapCatalog.GetById(
                SelectedMapId)
            : null;

    public float MapRouletteDuration =>
        mapRouletteDuration;

    public bool IsCharacterSelectionOpen =>
        Phase ==
        LobbySelectionPhase.CharacterSelect;

    public bool IsMapVoteOpen =>
        Phase ==
        LobbySelectionPhase.MapVote;

    // ==================================================
    // Fusion
    // ==================================================

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            InitializeLobbyState();
        }

        FusionSessionController network =
            AppRoot.Instance != null
                ? AppRoot.Instance.Network
                : null;

        if (network != null)
        {
            network.SceneLoadCompleted +=
                OnSceneLoadCompleted;

            network.PlayerLeaving +=
                HandlePlayerLeaving;
        }

        NetworkPlayerData.NicknameConfirmed +=
            HandleNicknameConfirmed;

        LocalSpawned?.Invoke(this);
    }

    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        FusionSessionController network =
            AppRoot.Instance != null
                ? AppRoot.Instance.Network
                : null;

        if (network != null)
        {
            network.SceneLoadCompleted -=
                OnSceneLoadCompleted;

            network.PlayerLeaving -=
                HandlePlayerLeaving;
        }

        NetworkPlayerData.NicknameConfirmed -=
            HandleNicknameConfirmed;

        LocalDespawned?.Invoke(this);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        switch (Phase)
        {
            case LobbySelectionPhase.CharacterSelect:
                break;

            case LobbySelectionPhase.MapVote:
                UpdateMapVote();
                break;

            case LobbySelectionPhase.MapRoulette:
                UpdateMapRoulette();
                break;

            case LobbySelectionPhase.Starting:
            case LobbySelectionPhase.Playing:
            case LobbySelectionPhase.Returning:
                break;
        }
    }

    // ==================================================
    // Validation
    // ==================================================

    public bool IsValidCharacterId(
        int characterId)
    {
        return characterCatalog != null &&
               characterCatalog.ContainsId(
                   characterId);
    }

    public bool IsValidMapId(
        int mapId)
    {
        return mapCatalog != null &&
               mapCatalog.ContainsId(
                   mapId);
    }

    // ==================================================
    // Character Select Request
    // ==================================================

    public bool RequestCharacterConfirm(
        int characterId)
    {
        if (!IsCharacterSelectionOpen)
            return false;

        if (!IsValidCharacterId(
                characterId))
        {
            return false;
        }

        if (HasStateAuthority)
        {
            return ApplyCharacterConfirm(
                Runner.LocalPlayer,
                characterId);
        }

        RPC_RequestCharacterConfirm(
            characterId);

        return true;
    }

    public bool RequestCharacterCancel()
    {
        if (!IsCharacterSelectionOpen)
            return false;

        if (HasStateAuthority)
        {
            return ApplyCharacterCancel(
                Runner.LocalPlayer);
        }

        RPC_RequestCharacterCancel();

        return true;
    }

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority)]
    private void RPC_RequestCharacterConfirm(
        int characterId,
        RpcInfo info = default)
    {
        ApplyCharacterConfirm(
            info.Source,
            characterId);
    }

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority)]
    private void RPC_RequestCharacterCancel(
        RpcInfo info = default)
    {
        ApplyCharacterCancel(
            info.Source);
    }

    private bool ApplyCharacterConfirm(
        PlayerRef player,
        int characterId)
    {
        if (!HasStateAuthority)
            return false;

        if (Phase !=
            LobbySelectionPhase.CharacterSelect)
        {
            return false;
        }

        if (!IsValidCharacterId(
                characterId))
        {
            return false;
        }

        if (!TryGetPlayerData(
                player,
                out NetworkPlayerData playerData))
        {
            return false;
        }

        if (playerData.CharacterConfirmed)
            return false;

        playerData.SetCharacterSelection(
            characterId,
            true);

        return true;
    }

    private bool ApplyCharacterCancel(
        PlayerRef player)
    {
        if (!HasStateAuthority ||
            Phase != LobbySelectionPhase.CharacterSelect)
        {
            return false;
        }

        if (!TryGetPlayerData(
                player,
                out NetworkPlayerData playerData) ||
            !playerData.CharacterConfirmed)
        {
            return false;
        }

        playerData.SetCharacterSelection(
            playerData.SelectedCharacterId,
            false);

        return true;
    }

    // ==================================================
    // Character Select Phase
    // ==================================================

    public bool RequestBeginMapVote()
    {
        if (!HasStateAuthority ||
            Phase != LobbySelectionPhase.CharacterSelect ||
            !AreAllCharactersConfirmed())
        {
            return false;
        }

        return BeginMapVote();
    }

    private bool AreAllCharactersConfirmed()
    {
        int playerCount = 0;

        foreach (PlayerRef player
                 in Runner.ActivePlayers)
        {
            if (!TryGetPlayerData(
                    player,
                    out NetworkPlayerData playerData))
            {
                return false;
            }

            if (!playerData.CharacterConfirmed)
                return false;

            if (!IsValidCharacterId(
                    playerData.SelectedCharacterId))
            {
                return false;
            }

            playerCount++;
        }

        return playerCount > 0;
    }

    private bool BeginMapVote()
    {
        if (Phase !=
            LobbySelectionPhase.CharacterSelect)
        {
            return false;
        }

        if (mapCatalog == null ||
            mapCatalog.Maps == null ||
            mapCatalog.Maps.Count == 0)
        {
            Debug.LogError(
                "[NGS] MapCatalog에 맵이 없습니다.",
                this);

            return false;
        }

        ClearPlayerVotes();

        SelectedMapId = -1;

        PhaseTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                mapVoteDuration);

        Phase =
            LobbySelectionPhase.MapVote;

        return true;
    }

    // ==================================================
    // Map Vote Request
    // ==================================================

    public bool RequestMapVote(
        int mapId)
    {
        if (!IsMapVoteOpen)
            return false;

        if (!IsValidMapId(
                mapId))
        {
            return false;
        }

        if (HasStateAuthority)
        {
            return ApplyMapVote(
                Runner.LocalPlayer,
                mapId);
        }

        RPC_RequestMapVote(
            mapId);

        return true;
    }

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority)]
    private void RPC_RequestMapVote(
        int mapId,
        RpcInfo info = default)
    {
        ApplyMapVote(
            info.Source,
            mapId);
    }

    private bool ApplyMapVote(
        PlayerRef player,
        int mapId)
    {
        if (!HasStateAuthority)
            return false;

        if (Phase !=
            LobbySelectionPhase.MapVote)
        {
            return false;
        }

        if (!IsValidMapId(
                mapId))
        {
            return false;
        }

        if (!TryGetPlayerData(
                player,
                out NetworkPlayerData playerData))
        {
            return false;
        }

        playerData.SetMapVote(
            mapId);

        return true;
    }

    // ==================================================
    // Map Vote Phase
    // ==================================================

    private void UpdateMapVote()
    {
        // 투표 도중 새 플레이어가 들어오면
        // 새 플레이어가 캐릭터를 선택할 수 있도록 선택 단계로 되돌립니다.
        if (!AreAllCharactersConfirmed())
        {
            ReturnToCharacterSelectForLateJoin();
            return;
        }

        // 모든 현재 플레이어가 유효한 투표를 마쳤다면
        // 남은 카운트다운을 기다리지 않고 즉시 결과를 확정한다.
        if (AreAllMapVotesSubmitted())
        {
            ResolveMapVote();
            return;
        }

        if (!PhaseTimer.Expired(Runner))
            return;

        ResolveMapVote();
    }

    private bool AreAllMapVotesSubmitted()
    {
        int playerCount = 0;

        foreach (PlayerRef player
                 in Runner.ActivePlayers)
        {
            if (!TryGetPlayerData(
                    player,
                    out NetworkPlayerData playerData))
            {
                return false;
            }

            if (!playerData.HasMapVote)
                return false;

            if (!IsValidMapId(
                    playerData.VotedMapId))
            {
                return false;
            }

            playerCount++;
        }

        return playerCount > 0;
    }

    private void ReturnToCharacterSelectForLateJoin()
    {
        ClearPlayerVotes();

        SelectedMapId = -1;
        PhaseTimer = TickTimer.None;

        Phase =
            LobbySelectionPhase.CharacterSelect;
    }

    private void ResolveMapVote()
    {
        if (Phase !=
            LobbySelectionPhase.MapVote)
        {
            return;
        }

        List<int> candidates =
            BuildTopVotedMapCandidates();

        if (candidates.Count == 0)
        {
            Debug.LogError(
                "[NGS] 선택 가능한 맵 후보가 없습니다.",
                this);

            return;
        }

        int randomIndex =
            UnityEngine.Random.Range(
                0,
                candidates.Count);

        SelectedMapId =
            candidates[randomIndex];

        PhaseTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                mapRouletteDuration);

        Phase =
            LobbySelectionPhase.MapRoulette;
    }

    private List<int>
        BuildTopVotedMapCandidates()
    {
        Dictionary<int, int> voteCounts =
            new();

        if (mapCatalog != null &&
            mapCatalog.Maps != null)
        {
            foreach (MapData map
                     in mapCatalog.Maps)
            {
                if (map == null)
                    continue;

                voteCounts[map.MapId] = 0;
            }
        }

        foreach (PlayerRef player
                 in Runner.ActivePlayers)
        {
            if (!TryGetPlayerData(
                    player,
                    out NetworkPlayerData playerData))
            {
                continue;
            }

            int voteId =
                playerData.VotedMapId;

            if (!voteCounts.ContainsKey(
                    voteId))
            {
                continue;
            }

            voteCounts[voteId]++;
        }

        int bestVoteCount = -1;

        foreach (int count
                 in voteCounts.Values)
        {
            if (count > bestVoteCount)
            {
                bestVoteCount = count;
            }
        }

        List<int> candidates =
            new();

        foreach (KeyValuePair<int, int> pair
                 in voteCounts)
        {
            if (pair.Value !=
                bestVoteCount)
            {
                continue;
            }

            candidates.Add(
                pair.Key);
        }

        return candidates;
    }

    public float GetMapVoteRemainingTime()
    {
        if (Phase !=
            LobbySelectionPhase.MapVote)
        {
            return 0f;
        }

        return PhaseTimer
                   .RemainingTime(Runner)
               ?? 0f;
    }

    // ==================================================
    // Roulette / Start
    // ==================================================

    private void UpdateMapRoulette()
    {
        // 룰렛 연출 중 새 플레이어가 들어온 경우에도
        // 아직 Gameplay 로드를 요청하기 전이라면 선택 단계로 되돌린다.
        if (!AreAllCharactersConfirmed())
        {
            ReturnToCharacterSelectForLateJoin();
            return;
        }

        if (!PhaseTimer.Expired(Runner))
            return;

        BeginGameplay();
    }

    private void BeginGameplay()
    {
        if (!HasStateAuthority)
            return;

        if (Phase !=
            LobbySelectionPhase.MapRoulette)
        {
            return;
        }

        MapData selectedMap =
            SelectedMapData;

        if (selectedMap == null ||
            selectedMap.MapPrefab == null)
        {
            Debug.LogError(
                "[NGS] 확정된 MapData가 올바르지 않습니다.",
                this);

            return;
        }

        if (string.IsNullOrWhiteSpace(
                gameplaySceneName))
        {
            Debug.LogError(
                "[NGS] Gameplay Scene 이름이 비어 있습니다.",
                this);

            return;
        }

        FusionSessionController network =
            AppRoot.Instance.Network;

        _gameplayLoadRequested = true;

        Phase =
            LobbySelectionPhase.Starting;

        bool loadStarted =
            network.TryLoadScene(
                gameplaySceneName,
                LoadSceneMode.Single,
                out _);

        if (loadStarted)
            return;

        _gameplayLoadRequested = false;

        Phase =
            LobbySelectionPhase.MapRoulette;

        PhaseTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                0.5f);
    }

    // ==================================================
    // Return To Lobby
    // ==================================================

    public bool RequestReturnToLobby()
    {
        if (!HasStateAuthority)
            return false;

        if (_lobbyLoadRequested)
            return false;

        if (string.IsNullOrWhiteSpace(
                lobbySceneName))
        {
            Debug.LogError(
                "[NGS] Lobby Scene 이름이 비어 있습니다.",
                this);

            return false;
        }

        FusionSessionController network =
            AppRoot.Instance.Network;

        _lobbyLoadRequested = true;

        Phase =
            LobbySelectionPhase.Returning;

        bool loadStarted =
            network.TryLoadScene(
                lobbySceneName,
                LoadSceneMode.Single,
                out _);

        if (loadStarted)
        {
            BroadcastSystemNotice(
                "게임이 종료되어 로비로 돌아갑니다.");

            return true;
        }

        _lobbyLoadRequested = false;

        Phase =
            LobbySelectionPhase.Playing;

        return false;
    }

    private void OnSceneLoadCompleted()
    {
        if (!HasStateAuthority)
            return;

        if (_gameplayLoadRequested)
        {
            _gameplayLoadRequested = false;

            PhaseTimer = TickTimer.None;
            Phase =
                LobbySelectionPhase.Playing;

            return;
        }

        if (!_lobbyLoadRequested)
            return;

        _lobbyLoadRequested = false;

        ResetPlayersForLobby();
        InitializeLobbyState();
    }

    // ==================================================
    // System Notice
    // ==================================================

    private void HandleNicknameConfirmed(
        NetworkPlayerData playerData)
    {
        if (!HasStateAuthority)
            return;

        if (playerData == null ||
            playerData.Runner != Runner)
        {
            return;
        }

        if (!CanShowPlayerPresenceNotice())
            return;

        BroadcastSystemNotice(
            $"{playerData.DisplayName}님이 참가했습니다.");
    }


    private bool CanShowPlayerPresenceNotice()
    {
        // Returning 중에는 씬 정리 과정의 입/퇴장 알림을 표시하지 않습니다.
        // Playing을 포함한 나머지 세션 단계에서는 표시합니다.
        return Phase !=
               LobbySelectionPhase.Returning;
    }


    private void HandlePlayerLeaving(
        PlayerRef player,
        NetworkPlayerData playerData)
    {
        if (!HasStateAuthority)
            return;

        if (!CanShowPlayerPresenceNotice())
            return;

        string playerName =
            ResolvePlayerName(
                player,
                playerData);

        BroadcastSystemNotice(
            $"{playerName}님이 나갔습니다.");
    }


    private void BroadcastSystemNotice(
        string message)
    {
        if (!HasStateAuthority)
            return;

        if (string.IsNullOrWhiteSpace(
                message))
        {
            return;
        }

        NetworkString<_64> networkMessage =
            message;

        RPC_ShowSystemNotice(
            networkMessage);
    }


    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All)]
    private void RPC_ShowSystemNotice(
        NetworkString<_64> message)
    {
        AppRoot appRoot =
            AppRoot.Instance;

        if (appRoot == null ||
            appRoot.SystemNotice == null)
        {
            return;
        }

        appRoot.SystemNotice.Show(
            message.ToString());
    }


    private static string ResolvePlayerName(
        PlayerRef player,
        NetworkPlayerData playerData)
    {
        if (playerData != null)
        {
            string displayName =
                playerData.DisplayName;

            if (!string.IsNullOrWhiteSpace(
                    displayName))
            {
                return displayName;
            }
        }

        return player.ToString();
    }


    // ==================================================
    // Reset
    // ==================================================

    private void InitializeLobbyState()
    {
        SelectedMapId = -1;
        PhaseTimer = TickTimer.None;

        Phase =
            LobbySelectionPhase.CharacterSelect;
    }

    private void ResetPlayersForLobby()
    {
        foreach (PlayerRef player
                 in Runner.ActivePlayers)
        {
            if (!TryGetPlayerData(
                    player,
                    out NetworkPlayerData playerData))
            {
                continue;
            }

            playerData.ResetForLobby();
        }
    }

    private void ClearPlayerVotes()
    {
        foreach (PlayerRef player
                 in Runner.ActivePlayers)
        {
            if (!TryGetPlayerData(
                    player,
                    out NetworkPlayerData playerData))
            {
                continue;
            }

            playerData.ResetMapVote();
        }
    }

    // ==================================================
    // Player Data
    // ==================================================

    private bool TryGetPlayerData(
        PlayerRef player,
        out NetworkPlayerData playerData)
    {
        playerData = null;

        if (!Runner.TryGetPlayerObject(
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

    // ==================================================
    // Presentation
    // ==================================================

    private void OnPhaseChanged()
    {
        PhaseChanged?.Invoke(
            Phase);

        SelectionStateChanged?.Invoke();
    }

    private void OnSelectionStateChanged()
    {
        SelectionStateChanged?.Invoke();
    }
}