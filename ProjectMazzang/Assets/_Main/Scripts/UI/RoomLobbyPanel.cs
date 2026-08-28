using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class RoomLobbyPanel :
    MonoBehaviour
{
    [Header("Room")]
    [SerializeField]
    private TMP_Text roomNameText;

    [Header("Players")]
    [SerializeField]
    private Transform playerListRoot;

    [SerializeField]
    private PlayerListItem playerListItemPrefab;

    [FormerlySerializedAs("readySummaryText")]
    [SerializeField]
    private TMP_Text characterSummaryText;

    [Header("Character Select")]
    [SerializeField]
    private GameObject characterSelectPanel;

    [SerializeField]
    private Transform characterListRoot;

    [SerializeField]
    private CharacterSelectItem characterItemPrefab;

    [SerializeField]
    private Image characterPortrait;


    [SerializeField]
    private TMP_Text characterNameText;

    [FormerlySerializedAs("readyButton")]
    [SerializeField]
    private Button characterConfirmButton;

    [FormerlySerializedAs("readyButtonText")]
    [SerializeField]
    private TMP_Text characterConfirmButtonText;

    [Header("Map Vote")]
    [SerializeField]
    private GameObject mapVotePanel;

    [SerializeField]
    private Transform mapListRoot;

    [SerializeField]
    private MapVoteItem mapVoteItemPrefab;

    [SerializeField]
    private TMP_Text mapVoteTimerText;

    [SerializeField]
    private TMP_Text mapVoteStatusText;

    [Header("Actions")]
    [SerializeField]
    private Button leaveButton;

    // 기존 Start 버튼 Serialized 참조를 잃지 않기 위한 마이그레이션 슬롯.
    // 자동 진행 구조에서는 사용하지 않고 항상 숨깁니다.
    [FormerlySerializedAs("startButton")]
    [SerializeField]
    private Button legacyStartButton;

    private TMP_Text _startButtonText;

    private readonly Dictionary<
        PlayerRef,
        PlayerListItem> _playerItems = new();

    private readonly Dictionary<
        int,
        CharacterSelectItem> _characterItems = new();

    private readonly Dictionary<
        int,
        MapVoteItem> _mapItems = new();

    private CharacterCatalog _characterCatalog;
    private MapCatalog _mapCatalog;

    private int _previewCharacterId = -1;
    private Coroutine _rouletteRoutine;

    public event Action<int>
        CharacterConfirmRequested;

    public event Action
        MapVoteStartRequested;

    public event Action<int>
        MapVoteRequested;

    public event Action
        LeaveRequested;

    // ==================================================
    // Unity
    // ==================================================

    private void Awake()
    {
        if (characterConfirmButton != null)
        {
            characterConfirmButton.onClick.AddListener(
                OnCharacterConfirmClicked);
        }

        if (leaveButton != null)
        {
            leaveButton.onClick.AddListener(
                OnLeaveClicked);
        }

        if (legacyStartButton != null)
        {
            legacyStartButton.onClick.AddListener(
                OnMapVoteStartClicked);

            _startButtonText =
                legacyStartButton.GetComponentInChildren<
                    TMP_Text>(true);
        }
    }

    private void OnDestroy()
    {
        if (characterConfirmButton != null)
        {
            characterConfirmButton.onClick.RemoveListener(
                OnCharacterConfirmClicked);
        }

        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveListener(
                OnLeaveClicked);
        }

        if (legacyStartButton != null)
        {
            legacyStartButton.onClick.RemoveListener(
                OnMapVoteStartClicked);
        }

        StopMapRoulette();
        ClearCharacterItems();
        ClearMapItems();
    }

    // ==================================================
    // Room
    // ==================================================

    public void SetRoomName(
        string roomName)
    {
        if (roomNameText == null)
            return;

        roomNameText.text =
            string.IsNullOrWhiteSpace(
                roomName)
                ? "-"
                : roomName;
    }

    public void SetCatalogs(
        CharacterCatalog characterCatalog,
        MapCatalog mapCatalog)
    {
        bool characterChanged =
            _characterCatalog !=
            characterCatalog;

        bool mapChanged =
            _mapCatalog !=
            mapCatalog;

        _characterCatalog =
            characterCatalog;

        _mapCatalog =
            mapCatalog;

        if (characterChanged)
        {
            BuildCharacterItems();
        }

        if (mapChanged)
        {
            BuildMapItems();
        }
    }

    public void ShowPhase(
        LobbySelectionPhase phase)
    {
        bool showCharacter =
            phase ==
            LobbySelectionPhase.CharacterSelect;

        bool showMap =
            phase ==
                LobbySelectionPhase.MapVote ||
            phase ==
                LobbySelectionPhase.MapRoulette;

        if (characterSelectPanel != null)
        {
            characterSelectPanel.SetActive(
                showCharacter);
        }

        if (mapVotePanel != null)
        {
            mapVotePanel.SetActive(
                showMap);
        }

        if (mapVoteTimerText != null)
        {
            mapVoteTimerText.gameObject.SetActive(
                phase ==
                LobbySelectionPhase.MapVote);
        }

        if (phase !=
            LobbySelectionPhase.MapRoulette)
        {
            StopMapRoulette();
        }
    }

    // ==================================================
    // Players
    // ==================================================

    public void UpsertPlayer(
        PlayerRef player,
        string nickname,
        bool characterConfirmed,
        bool isLocal)
    {
        if (!_playerItems.TryGetValue(
                player,
                out PlayerListItem item))
        {
            item = Instantiate(
                playerListItemPrefab,
                playerListRoot);

            _playerItems.Add(
                player,
                item);
        }

        // 기존 PlayerListItem의 Ready 표시는
        // 이제 캐릭터 확정 상태를 의미합니다.
        item.SetView(
            nickname,
            characterConfirmed,
            isLocal);
    }

    public void RemovePlayer(
        PlayerRef player)
    {
        if (!_playerItems.TryGetValue(
                player,
                out PlayerListItem item))
        {
            return;
        }

        _playerItems.Remove(
            player);

        if (item != null)
        {
            Destroy(
                item.gameObject);
        }
    }

    public void ClearPlayers()
    {
        foreach (PlayerListItem item
                 in _playerItems.Values)
        {
            if (item != null)
            {
                Destroy(
                    item.gameObject);
            }
        }

        _playerItems.Clear();

        SetCharacterSummary(
            0,
            0);
    }

    public void SetCharacterSummary(
        int confirmedCount,
        int playerCount)
    {
        if (characterSummaryText == null)
            return;

        characterSummaryText.text =
            $"{confirmedCount} / {playerCount} LOCKED";
    }

    // ==================================================
    // Character Select
    // ==================================================

    private void BuildCharacterItems()
    {
        ClearCharacterItems();

        if (_characterCatalog == null ||
            _characterCatalog.Characters == null ||
            characterItemPrefab == null ||
            characterListRoot == null)
        {
            return;
        }

        foreach (CharacterData character
                 in _characterCatalog.Characters)
        {
            if (character == null)
                continue;

            CharacterSelectItem item =
                Instantiate(
                    characterItemPrefab,
                    characterListRoot);

            item.Setup(
                character,
                OnCharacterPreviewRequested);

            _characterItems.Add(
                character.CharacterId,
                item);
        }
    }

    private void ClearCharacterItems()
    {
        foreach (CharacterSelectItem item
                 in _characterItems.Values)
        {
            if (item != null)
            {
                Destroy(
                    item.gameObject);
            }
        }

        _characterItems.Clear();
    }

    private void OnCharacterPreviewRequested(
        int characterId)
    {
        CharacterData character =
            _characterCatalog != null
                ? _characterCatalog.GetById(
                    characterId)
                : null;

        if (character == null)
            return;

        _previewCharacterId =
            characterId;

        foreach (KeyValuePair<
                     int,
                     CharacterSelectItem> pair
                 in _characterItems)
        {
            pair.Value.SetSelected(
                pair.Key ==
                characterId);
        }

        ShowCharacterPreview(
            character);

        if (characterConfirmButton != null)
        {
            characterConfirmButton.interactable =
                true;
        }
    }

    private void ShowCharacterPreview(
        CharacterData character)
    {
        if (characterNameText != null)
        {
            characterNameText.text =
                character.DisplayName;
        }

        if (characterPortrait != null)
        {
            characterPortrait.sprite =
                character.Portrait;

            characterPortrait.enabled =
                character.Portrait != null;
        }
    }


    public void SetLocalCharacterState(
        int characterId,
        bool confirmed)
    {
        if (characterId >= 0 &&
            characterId !=
            _previewCharacterId)
        {
            OnCharacterPreviewRequested(
                characterId);
        }

        foreach (CharacterSelectItem item
                 in _characterItems.Values)
        {
            item.SetInteractable(
                !confirmed);
        }

        if (characterConfirmButton != null)
        {
            characterConfirmButton.interactable =
                _previewCharacterId >= 0;
        }

        if (characterConfirmButtonText != null)
        {
            characterConfirmButtonText.text =
                confirmed
                    ? "확정 취소"
                    : "캐릭터 확정";
        }
    }

    public void SetCharacterConfirmPending(
        bool pending)
    {
        if (characterConfirmButton == null)
            return;

        characterConfirmButton.interactable =
            !pending &&
            _previewCharacterId >= 0;
    }

    private void OnCharacterConfirmClicked()
    {
        if (_previewCharacterId < 0)
            return;

        CharacterConfirmRequested?.Invoke(
            _previewCharacterId);
    }

    public void SetMapVoteStartState(
        bool visible,
        bool isHost,
        bool allCharactersConfirmed,
        int confirmedCount,
        int playerCount)
    {
        if (legacyStartButton == null)
            return;

        legacyStartButton.gameObject.SetActive(
            visible);

        if (!visible)
            return;

        legacyStartButton.interactable =
            isHost && allCharactersConfirmed;

        if (_startButtonText == null)
            return;

        if (!allCharactersConfirmed)
        {
            _startButtonText.text =
                $"캐릭터 선택 대기 중 ({confirmedCount}/{playerCount})";
            return;
        }

        _startButtonText.text =
            isHost
                ? "맵 투표 시작"
                : "방장이 맵 투표를 시작하기를 기다리는 중...";
    }

    private void OnMapVoteStartClicked()
    {
        MapVoteStartRequested?.Invoke();
    }

    // ==================================================
    // Map Vote
    // ==================================================

    private void BuildMapItems()
    {
        ClearMapItems();

        if (_mapCatalog == null ||
            _mapCatalog.Maps == null ||
            mapVoteItemPrefab == null ||
            mapListRoot == null)
        {
            return;
        }

        foreach (MapData map
                 in _mapCatalog.Maps)
        {
            if (map == null)
                continue;

            MapVoteItem item =
                Instantiate(
                    mapVoteItemPrefab,
                    mapListRoot);

            item.Setup(
                map,
                OnMapVoteClicked);

            _mapItems.Add(
                map.MapId,
                item);
        }
    }

    private void ClearMapItems()
    {
        foreach (MapVoteItem item
                 in _mapItems.Values)
        {
            if (item != null)
            {
                Destroy(
                    item.gameObject);
            }
        }

        _mapItems.Clear();
    }

    private void OnMapVoteClicked(
        int mapId)
    {
        SetLocalMapVote(
            mapId);

        MapVoteRequested?.Invoke(
            mapId);
    }

    public void SetMapVoteCount(
        int mapId,
        int count)
    {
        if (!_mapItems.TryGetValue(
                mapId,
                out MapVoteItem item))
        {
            return;
        }

        item.SetVoteCount(
            count);
    }

    public void SetLocalMapVote(
        int mapId)
    {
        foreach (KeyValuePair<
                     int,
                     MapVoteItem> pair
                 in _mapItems)
        {
            pair.Value.SetLocalVote(
                pair.Key ==
                mapId);
        }
    }

    public void SetMapVoteInteractable(
        bool interactable)
    {
        foreach (MapVoteItem item
                 in _mapItems.Values)
        {
            item.SetInteractable(
                interactable);
        }
    }

    public void SetMapVoteTimer(
        float seconds)
    {
        if (mapVoteTimerText == null)
            return;

        int displaySeconds =
            Mathf.CeilToInt(
                Mathf.Max(
                    0f,
                    seconds));

        mapVoteTimerText.text =
            displaySeconds.ToString();
    }

    public void SetMapVoteStatus(
        string text)
    {
        if (mapVoteStatusText != null)
        {
            mapVoteStatusText.text =
                text ?? string.Empty;
        }
    }

    // ==================================================
    // Roulette Presentation
    // ==================================================

    public void PlayMapRoulette(
        IReadOnlyList<int> candidateIds,
        int winnerMapId,
        float duration)
    {
        StopMapRoulette();

        SetMapVoteInteractable(
            false);

        _rouletteRoutine =
            StartCoroutine(
                MapRouletteRoutine(
                    candidateIds,
                    winnerMapId,
                    duration));
    }

    public void StopMapRoulette()
    {
        if (_rouletteRoutine != null)
        {
            StopCoroutine(
                _rouletteRoutine);

            _rouletteRoutine = null;
        }

        foreach (MapVoteItem item
                 in _mapItems.Values)
        {
            item.SetRouletteHighlight(
                false);

            item.SetWinner(
                false);
        }
    }

    private IEnumerator MapRouletteRoutine(
        IReadOnlyList<int> candidateIds,
        int winnerMapId,
        float duration)
    {
        List<int> candidates =
            new();

        if (candidateIds != null)
        {
            foreach (int id
                     in candidateIds)
            {
                if (_mapItems.ContainsKey(
                        id))
                {
                    candidates.Add(
                        id);
                }
            }
        }

        if (candidates.Count == 0)
        {
            foreach (int id
                     in _mapItems.Keys)
            {
                candidates.Add(
                    id);
            }
        }

        if (candidates.Count == 0)
        {
            yield break;
        }

        SetMapVoteStatus(
            candidates.Count > 1
                ? "동률 후보 중 맵을 선택합니다..."
                : "선택된 맵");

        float safeDuration =
            Mathf.Max(
                0.1f,
                duration);

        float elapsed = 0f;
        float interval = 0.06f;
        int index = 0;

        while (elapsed <
               safeDuration - 0.2f)
        {
            int currentId =
                candidates[
                    index %
                    candidates.Count];

            SetRouletteHighlightOnly(
                currentId);

            yield return
                new WaitForSecondsRealtime(
                    interval);

            elapsed +=
                interval;

            interval =
                Mathf.Min(
                    interval * 1.13f,
                    0.24f);

            index++;
        }

        SetRouletteHighlightOnly(
            winnerMapId);

        if (_mapItems.TryGetValue(
                winnerMapId,
                out MapVoteItem winner))
        {
            winner.SetWinner(
                true);
        }

        MapData winnerData =
            _mapCatalog != null
                ? _mapCatalog.GetById(
                    winnerMapId)
                : null;

        SetMapVoteStatus(
            winnerData != null
                ? $"{winnerData.DisplayName} 선택!"
                : "맵 선택 완료");

        _rouletteRoutine = null;
    }

    private void SetRouletteHighlightOnly(
        int mapId)
    {
        foreach (KeyValuePair<
                     int,
                     MapVoteItem> pair
                 in _mapItems)
        {
            pair.Value.SetRouletteHighlight(
                pair.Key ==
                mapId);
        }
    }

    // ==================================================
    // Leave
    // ==================================================

    public void SetLeaveInteractable(
        bool interactable)
    {
        if (leaveButton != null)
        {
            leaveButton.interactable =
                interactable;
        }
    }

    private void OnLeaveClicked()
    {
        LeaveRequested?.Invoke();
    }
}