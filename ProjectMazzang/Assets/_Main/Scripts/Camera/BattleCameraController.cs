using Fusion;
using Unity.Cinemachine;
using UnityEngine;

public sealed class BattleCameraController : MonoBehaviour
{
    public static BattleCameraController Instance { get; private set; }

    [Header("Battle")]
    [SerializeField]
    private CinemachineCamera battleCamera;

    [Header("Winner")]
    [SerializeField]
    private CinemachineCamera winnerCamera;

    private NetworkGameManager _gameManager;
    private NetworkPlayerData _localPlayerData;
    private NetworkObject _localCharacter;

    private Transform _battleTarget;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        Instance = this;

        RestoreBattleView();
    }


    private void OnEnable()
    {
        if (Instance != this)
            return;

        NetworkGameManager.LocalSpawned +=
            HandleGameManagerSpawned;

        NetworkGameManager.LocalDespawned +=
            HandleGameManagerDespawned;

        NetworkPlayerData.LocalSpawned +=
            HandlePlayerDataSpawned;

        NetworkPlayerData.LocalChanged +=
            HandlePlayerDataChanged;

        NetworkPlayerData.LocalDespawned +=
            HandlePlayerDataDespawned;

        if (NetworkGameManager.Instance != null)
        {
            BindGameManager(
                NetworkGameManager.Instance);
        }

        TryBindLocalPlayerDataFromGameManager();
    }


    private void OnDisable()
    {
        if (Instance != this)
            return;

        NetworkGameManager.LocalSpawned -=
            HandleGameManagerSpawned;

        NetworkGameManager.LocalDespawned -=
            HandleGameManagerDespawned;

        NetworkPlayerData.LocalSpawned -=
            HandlePlayerDataSpawned;

        NetworkPlayerData.LocalChanged -=
            HandlePlayerDataChanged;

        NetworkPlayerData.LocalDespawned -=
            HandlePlayerDataDespawned;

        UnbindGameManager();
        UnbindLocalPlayerData();
    }


    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }


    // =========================================================
    // Game Manager Bind
    // =========================================================

    private void HandleGameManagerSpawned(
        NetworkGameManager gameManager)
    {
        BindGameManager(gameManager);
    }


    private void HandleGameManagerDespawned(
        NetworkGameManager gameManager)
    {
        if (_gameManager != gameManager)
            return;

        UnbindGameManager();
    }


    private void BindGameManager(
        NetworkGameManager gameManager)
    {
        if (_gameManager == gameManager)
        {
            RefreshCameraMode();
            TryBindLocalPlayerDataFromGameManager();
            return;
        }

        UnbindGameManager();

        _gameManager = gameManager;

        if (_gameManager == null)
            return;

        _gameManager.StateChanged +=
            HandleMatchStateChanged;

        RefreshCameraMode();
        TryBindLocalPlayerDataFromGameManager();
    }


    private void UnbindGameManager()
    {
        if (_gameManager != null)
        {
            _gameManager.StateChanged -=
                HandleMatchStateChanged;
        }

        _gameManager = null;

        RestoreBattleView();
    }


    // =========================================================
    // Local Player Bind
    // =========================================================

    private void HandlePlayerDataSpawned(
        NetworkPlayerData playerData)
    {
        TryBindLocalPlayerData(
            playerData);
    }


    private void HandlePlayerDataChanged(
        NetworkPlayerData playerData)
    {
        if (playerData == null ||
            !playerData.IsLocalPlayer)
        {
            return;
        }

        if (_localPlayerData !=
            playerData)
        {
            BindLocalPlayerData(
                playerData);

            return;
        }

        RefreshLocalCharacter();
        RefreshCameraMode();
    }


    private void HandlePlayerDataDespawned(
        NetworkRunner runner,
        PlayerRef player)
    {
        if (_localPlayerData == null ||
            _localPlayerData.PlayerRef !=
            player)
        {
            return;
        }

        UnbindLocalPlayerData();
    }


    private void TryBindLocalPlayerData(
        NetworkPlayerData playerData)
    {
        if (playerData == null ||
            !playerData.IsLocalPlayer)
        {
            return;
        }

        BindLocalPlayerData(
            playerData);
    }


    private void TryBindLocalPlayerDataFromGameManager()
    {
        if (_gameManager == null)
            return;

        NetworkRunner runner =
            _gameManager.Runner;

        if (runner == null)
            return;

        PlayerRef localPlayer =
            runner.LocalPlayer;

        if (localPlayer ==
            PlayerRef.None)
        {
            return;
        }

        if (!runner.TryGetPlayerObject(
                localPlayer,
                out NetworkObject dataObject))
        {
            return;
        }

        if (!dataObject.TryGetComponent(
                out NetworkPlayerData playerData))
        {
            return;
        }

        BindLocalPlayerData(
            playerData);
    }


    private void BindLocalPlayerData(
        NetworkPlayerData playerData)
    {
        if (_localPlayerData ==
            playerData)
        {
            RefreshLocalCharacter();
            return;
        }

        _localPlayerData =
            playerData;

        RefreshLocalCharacter();
    }


    private void RefreshLocalCharacter()
    {
        NetworkObject character =
            _localPlayerData != null
                ? _localPlayerData.CharacterObject
                : null;

        if (_localCharacter == character)
            return;

        _localCharacter =
            character;

        SetBattleTarget(
            ResolveCameraTarget(
                _localCharacter));
    }


    private void UnbindLocalPlayerData()
    {
        _localCharacter = null;
        _localPlayerData = null;

        SetBattleTarget(null);
    }


    private void SetBattleTarget(
        Transform target)
    {
        if (_battleTarget == target)
            return;

        _battleTarget = target;

        if (battleCamera == null)
            return;

        battleCamera.Follow = target;

        // 새 캐릭터에 이전 대상의 감쇠 상태를 이어 붙이지 않는다.
        battleCamera.PreviousStateIsValid = false;
    }


    private static Transform ResolveCameraTarget(
        NetworkObject character)
    {
        if (character == null)
            return null;

        if (character.TryGetComponent(
                out PlayerHealth health))
        {
            return health.CameraTarget;
        }

        return character.transform;
    }


    // =========================================================
    // Camera Mode
    // =========================================================

    private void HandleMatchStateChanged(
        MatchState state)
    {
        RefreshCameraMode();
    }


    private void RefreshCameraMode()
    {
        if (_gameManager == null ||
            _gameManager.State ==
            MatchState.Playing)
        {
            RestoreBattleView();
            return;
        }

        if (_gameManager.State ==
                MatchState.Ending ||
            _gameManager.State ==
                MatchState.Result)
        {
            TryFocusWinner();
        }
    }


    private void TryFocusWinner()
    {
        if (_gameManager == null ||
            _gameManager.Winner ==
            PlayerRef.None)
        {
            RestoreBattleView();
            return;
        }

        NetworkRunner runner =
            _gameManager.Runner;

        if (runner == null ||
            !runner.TryGetPlayerObject(
                _gameManager.Winner,
                out NetworkObject dataObject) ||
            !dataObject.TryGetComponent(
                out NetworkPlayerData winnerData))
        {
            return;
        }

        FocusWinner(
            ResolveCameraTarget(
                winnerData.CharacterObject));
    }


    private void FocusWinner(
        Transform winner)
    {
        if (winner == null ||
            winnerCamera == null)
        {
            return;
        }

        winnerCamera.Follow =
            winner;

        winnerCamera.LookAt =
            winner;

        winnerCamera.enabled =
            true;
    }


    private void RestoreBattleView()
    {
        if (winnerCamera == null)
            return;

        winnerCamera.enabled =
            false;

        winnerCamera.Follow =
            null;

        winnerCamera.LookAt =
            null;
    }
}
