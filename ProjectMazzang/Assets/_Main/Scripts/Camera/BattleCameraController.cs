using Fusion;
using Unity.Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class BattleCameraController : MonoBehaviour
{
    public static BattleCameraController Instance { get; private set; }

    [Header("Battle")]
    [SerializeField]
    private CinemachineCamera battleCamera;

    [Header("Winner")]
    [SerializeField]
    private CinemachineCamera winnerCamera;

    [Header("Movement Composition")]
    [Min(0.01f)]
    [SerializeField]
    private float velocityResponse = 12f;

    [Min(0f)]
    [SerializeField]
    private float horizontalStopSpeed = 0.25f;

    [Min(0f)]
    [SerializeField]
    private float horizontalReverseSpeed = 1f;

    [Min(0.01f)]
    [SerializeField]
    private float horizontalSpeedForMaxLead = 8f;

    [Min(0f)]
    [SerializeField]
    private float directionChangeDelay = 0.15f;

    [Min(0f)]
    [SerializeField]
    private float maximumHorizontalLead = 1.5f;

    [Min(0.01f)]
    [SerializeField]
    private float horizontalLeadSmoothTime = 0.35f;

    [Header("Fall Composition")]
    [Min(0f)]
    [SerializeField]
    private float fallSpeedThreshold = 3.5f;

    [Min(0.01f)]
    [SerializeField]
    private float fallSpeedForMaxLead = 12f;

    [Min(0f)]
    [SerializeField]
    private float fallLeadDelay = 0.12f;

    [Min(0f)]
    [SerializeField]
    private float maximumFallLead = 1f;

    [Min(0.01f)]
    [SerializeField]
    private float verticalLeadSmoothTime = 0.3f;

    [Header("Warp Recovery")]
    [Min(0.01f)]
    [SerializeField]
    private float targetWarpDistance = 4f;

    private CinemachinePositionComposer
        _positionComposer;

    private CinemachineConfiner2D
        _cameraConfiner;

    private BoxCollider2D
        _cameraConfinerBounds;

    private NetworkGameManager _gameManager;
    private NetworkPlayerData _localPlayerData;
    private NetworkObject _localCharacter;
    private PlayerHealth _localHealth;

    private Transform _battleTarget;

    private Vector3 _baseTargetOffset;
    private Vector3 _previousTargetPosition;
    private Vector2 _smoothedVelocity;

    private float _horizontalLead;
    private float _horizontalLeadVelocity;
    private float _verticalLead;
    private float _verticalLeadVelocity;
    private float _directionChangeTime;
    private float _fallLeadTime;

    private int _horizontalLeadDirection;
    private int _pendingLeadDirection;
    private bool _hasPreviousTargetPosition;
    private bool _wasLocalCharacterDead;


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

        ResolveComposition();
        ResetMovementComposition(null);
        RestoreBattleView();
    }


    private void LateUpdate()
    {
        if (HandleRespawnTransition())
            return;

        UpdateMovementComposition();
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


#if UNITY_EDITOR

    private void OnValidate()
    {
        velocityResponse =
            Mathf.Max(
                0.01f,
                velocityResponse);

        horizontalStopSpeed =
            Mathf.Max(
                0f,
                horizontalStopSpeed);

        horizontalReverseSpeed =
            Mathf.Max(
                horizontalStopSpeed,
                horizontalReverseSpeed);

        horizontalSpeedForMaxLead =
            Mathf.Max(
                horizontalStopSpeed + 0.01f,
                horizontalSpeedForMaxLead);

        directionChangeDelay =
            Mathf.Max(
                0f,
                directionChangeDelay);

        maximumHorizontalLead =
            Mathf.Max(
                0f,
                maximumHorizontalLead);

        horizontalLeadSmoothTime =
            Mathf.Max(
                0.01f,
                horizontalLeadSmoothTime);

        fallSpeedThreshold =
            Mathf.Max(
                0f,
                fallSpeedThreshold);

        fallSpeedForMaxLead =
            Mathf.Max(
                fallSpeedThreshold + 0.01f,
                fallSpeedForMaxLead);

        fallLeadDelay =
            Mathf.Max(
                0f,
                fallLeadDelay);

        maximumFallLead =
            Mathf.Max(
                0f,
                maximumFallLead);

        verticalLeadSmoothTime =
            Mathf.Max(
                0.01f,
                verticalLeadSmoothTime);

        targetWarpDistance =
            Mathf.Max(
                0.01f,
                targetWarpDistance);
    }

#endif


    // =========================================================
    // Movement Composition
    // =========================================================

    private void ResolveComposition()
    {
        if (battleCamera == null)
            return;

        if (_positionComposer == null)
        {
            _positionComposer =
                battleCamera.GetComponent<
                    CinemachinePositionComposer>();

            if (_positionComposer != null)
            {
                _baseTargetOffset =
                    _positionComposer.TargetOffset;
            }
        }

        if (_cameraConfiner == null)
        {
            _cameraConfiner =
                battleCamera.GetComponent<
                    CinemachineConfiner2D>();
        }

        if (_cameraConfinerBounds == null &&
            _cameraConfiner != null)
        {
            _cameraConfinerBounds =
                _cameraConfiner
                    .BoundingShape2D as
                    BoxCollider2D;
        }
    }


    private void UpdateMovementComposition()
    {
        if (_positionComposer == null ||
            _battleTarget == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        if (deltaTime <= Mathf.Epsilon)
            return;

        Vector3 targetPosition =
            _battleTarget.position;

        if (!_hasPreviousTargetPosition)
        {
            _previousTargetPosition =
                targetPosition;

            _hasPreviousTargetPosition = true;
            return;
        }

        Vector3 frameDelta =
            targetPosition -
            _previousTargetPosition;

        if (frameDelta.sqrMagnitude >=
            targetWarpDistance *
            targetWarpDistance)
        {
            NotifyTargetWarp(frameDelta);
            ResetMovementComposition(
                _battleTarget);

            return;
        }

        Vector2 frameVelocity =
            frameDelta /
            deltaTime;

        _previousTargetPosition =
            targetPosition;

        float velocityBlend =
            1f - Mathf.Exp(
                -velocityResponse *
                deltaTime);

        _smoothedVelocity =
            Vector2.Lerp(
                _smoothedVelocity,
                frameVelocity,
                velocityBlend);

        float desiredHorizontalLead =
            CalculateHorizontalLead(
                _smoothedVelocity.x,
                deltaTime);

        float desiredVerticalLead =
            CalculateVerticalLead(
                _smoothedVelocity.y,
                deltaTime);

        _horizontalLead =
            Mathf.SmoothDamp(
                _horizontalLead,
                desiredHorizontalLead,
                ref _horizontalLeadVelocity,
                horizontalLeadSmoothTime,
                Mathf.Infinity,
                deltaTime);

        _verticalLead =
            Mathf.SmoothDamp(
                _verticalLead,
                desiredVerticalLead,
                ref _verticalLeadVelocity,
                verticalLeadSmoothTime,
                Mathf.Infinity,
                deltaTime);

        _horizontalLead =
            Mathf.Clamp(
                _horizontalLead,
                -maximumHorizontalLead,
                maximumHorizontalLead);

        _verticalLead =
            Mathf.Clamp(
                _verticalLead,
                -maximumFallLead,
                0f);

        _positionComposer.TargetOffset =
            _baseTargetOffset +
            new Vector3(
                _horizontalLead,
                _verticalLead,
                0f);
    }


    private float CalculateHorizontalLead(
        float horizontalVelocity,
        float deltaTime)
    {
        float speed =
            Mathf.Abs(
                horizontalVelocity);

        if (speed < horizontalStopSpeed)
        {
            ClearPendingDirection();
            return 0f;
        }

        int requestedDirection =
            horizontalVelocity > 0f
                ? 1
                : -1;

        if (_horizontalLeadDirection == 0)
        {
            _horizontalLeadDirection =
                requestedDirection;
        }
        else if (requestedDirection !=
                 _horizontalLeadDirection)
        {
            if (speed < horizontalReverseSpeed)
            {
                ClearPendingDirection();
                return 0f;
            }

            if (_pendingLeadDirection !=
                requestedDirection)
            {
                _pendingLeadDirection =
                    requestedDirection;

                _directionChangeTime = 0f;
            }

            _directionChangeTime +=
                deltaTime;

            if (_directionChangeTime <
                directionChangeDelay)
            {
                return 0f;
            }

            _horizontalLeadDirection =
                requestedDirection;

            ClearPendingDirection();
        }
        else
        {
            ClearPendingDirection();
        }

        float speedRatio =
            Mathf.InverseLerp(
                horizontalStopSpeed,
                horizontalSpeedForMaxLead,
                speed);

        return _horizontalLeadDirection *
               maximumHorizontalLead *
               speedRatio;
    }


    private float CalculateVerticalLead(
        float verticalVelocity,
        float deltaTime)
    {
        float fallSpeed =
            -verticalVelocity;

        if (fallSpeed < fallSpeedThreshold)
        {
            _fallLeadTime = 0f;
            return 0f;
        }

        _fallLeadTime +=
            deltaTime;

        if (_fallLeadTime < fallLeadDelay)
            return 0f;

        float fallRatio =
            Mathf.InverseLerp(
                fallSpeedThreshold,
                fallSpeedForMaxLead,
                fallSpeed);

        return -maximumFallLead *
               fallRatio;
    }


    private void ClearPendingDirection()
    {
        _pendingLeadDirection = 0;
        _directionChangeTime = 0f;
    }


    private void ResetMovementComposition(
        Transform target)
    {
        _hasPreviousTargetPosition =
            target != null;

        _previousTargetPosition =
            target != null
                ? target.position
                : Vector3.zero;

        _smoothedVelocity =
            Vector2.zero;

        _horizontalLead = 0f;
        _horizontalLeadVelocity = 0f;
        _verticalLead = 0f;
        _verticalLeadVelocity = 0f;
        _horizontalLeadDirection = 0;
        _fallLeadTime = 0f;

        ClearPendingDirection();

        if (_positionComposer != null)
        {
            _positionComposer.TargetOffset =
                _baseTargetOffset;
        }
    }


    private bool HandleRespawnTransition()
    {
        if (_localHealth == null)
            return false;

        bool isDead =
            _localHealth.IsDead;

        bool respawned =
            _wasLocalCharacterDead &&
            !isDead;

        _wasLocalCharacterDead =
            isDead;

        if (!respawned ||
            _battleTarget == null)
        {
            return false;
        }

        if (_hasPreviousTargetPosition)
        {
            Vector3 positionDelta =
                _battleTarget.position -
                _previousTargetPosition;

            NotifyTargetWarp(
                positionDelta);
        }

        ResetMovementComposition(
            _battleTarget);

        if (battleCamera != null)
        {
            battleCamera.PreviousStateIsValid =
                false;
        }

        return true;
    }


    private void NotifyTargetWarp(
        Vector3 positionDelta)
    {
        if (battleCamera == null ||
            positionDelta.sqrMagnitude <=
            Mathf.Epsilon)
        {
            return;
        }

        battleCamera.OnTargetObjectWarped(
            _battleTarget,
            positionDelta);
    }


    // =========================================================
    // Map Bounds
    // =========================================================

    public void ApplyMapBounds(
        MapRuntime map)
    {
        if (map == null)
            return;

        ResolveComposition();

        if (_cameraConfiner == null ||
            _cameraConfinerBounds == null)
        {
            return;
        }

        Rect localBounds =
            map.OutZoneBounds;

        Transform mapTransform =
            map.transform;

        Transform boundsTransform =
            _cameraConfinerBounds.transform;

        boundsTransform.position =
            mapTransform.TransformPoint(
                new Vector3(
                    localBounds.center.x,
                    localBounds.center.y,
                    0f));

        boundsTransform.rotation =
            mapTransform.rotation;

        boundsTransform.localScale =
            ResolveLocalScale(
                boundsTransform.parent,
                mapTransform.lossyScale);

        _cameraConfinerBounds.offset =
            Vector2.zero;

        _cameraConfinerBounds.size =
            localBounds.size;

        _cameraConfiner
            .InvalidateBoundingShapeCache();

        if (battleCamera != null)
        {
            battleCamera.PreviousStateIsValid =
                false;
        }
    }


    private static Vector3 ResolveLocalScale(
        Transform parent,
        Vector3 worldScale)
    {
        if (parent == null)
            return worldScale;

        Vector3 parentScale =
            parent.lossyScale;

        return new Vector3(
            DivideScale(
                worldScale.x,
                parentScale.x),
            DivideScale(
                worldScale.y,
                parentScale.y),
            DivideScale(
                worldScale.z,
                parentScale.z));
    }


    private static float DivideScale(
        float value,
        float divisor)
    {
        return Mathf.Abs(divisor) >
               Mathf.Epsilon
            ? value / divisor
            : value;
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

        _localHealth = null;

        if (_localCharacter != null)
        {
            _localCharacter.TryGetComponent(
                out _localHealth);
        }

        _wasLocalCharacterDead =
            _localHealth != null &&
            _localHealth.IsDead;

        SetBattleTarget(
            ResolveCameraTarget(
                _localCharacter));
    }


    private void UnbindLocalPlayerData()
    {
        _localCharacter = null;
        _localPlayerData = null;
        _localHealth = null;
        _wasLocalCharacterDead = false;

        SetBattleTarget(null);
    }


    private void SetBattleTarget(
        Transform target)
    {
        if (_battleTarget == target)
            return;

        _battleTarget = target;

        ResetMovementComposition(target);

        if (battleCamera == null)
            return;

        battleCamera.Follow = target;

        // 대상이 사라지면 마지막으로 유효했던 위치가
        // 안전한 대기 화면으로 남는다.
        // 새 캐릭터에는 이전 대상의 감쇠 상태를 이어 붙이지 않는다.
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
