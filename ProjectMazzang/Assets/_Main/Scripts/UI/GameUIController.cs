using Fusion;
using UnityEngine;

public sealed class GameUIController : MonoBehaviour
{
    [SerializeField]
    private GameUI ui;

    private NetworkGameManager _gameManager;

    private NetworkPlayerData _localPlayerData;
    private NetworkObject _localCharacter;


    // =========================================================
    // Unity
    // =========================================================

    private void OnEnable()
    {
        NetworkGameManager.LocalSpawned +=
            HandleGameManagerSpawned;

        NetworkGameManager.LocalDespawned +=
            HandleGameManagerDespawned;

        PlayerHealth.LocalDeathOccurred +=
            HandlePlayerDeathOccurred;

        NetworkPlayerData.LocalSpawned +=
            HandlePlayerDataSpawned;

        NetworkPlayerData.LocalChanged +=
            HandlePlayerDataChanged;

        NetworkPlayerData.LocalDespawned +=
            HandlePlayerDataDespawned;

        if (NetworkGameManager.Instance != null)
        {
            Bind(
                NetworkGameManager.Instance);
        }

        TryBindLocalPlayerDataFromGameManager();
    }


    private void OnDisable()
    {
        NetworkGameManager.LocalSpawned -=
            HandleGameManagerSpawned;

        NetworkGameManager.LocalDespawned -=
            HandleGameManagerDespawned;

        PlayerHealth.LocalDeathOccurred -=
            HandlePlayerDeathOccurred;

        NetworkPlayerData.LocalSpawned -=
            HandlePlayerDataSpawned;

        NetworkPlayerData.LocalChanged -=
            HandlePlayerDataChanged;

        NetworkPlayerData.LocalDespawned -=
            HandlePlayerDataDespawned;

        UnbindLocalPlayerData();
        Unbind();
    }


    // =========================================================
    // Bind
    // =========================================================

    private void HandleGameManagerSpawned(
        NetworkGameManager gameManager)
    {
        Bind(gameManager);
    }


    private void HandleGameManagerDespawned(
        NetworkGameManager gameManager)
    {
        if (_gameManager != gameManager)
            return;

        Unbind();
    }


    private void Bind(
        NetworkGameManager gameManager)
    {
        if (_gameManager == gameManager)
            return;

        Unbind();

        _gameManager =
            gameManager;

        _gameManager.StateChanged +=
            HandleMatchStateChanged;

        RefreshFromCurrentState();
        TryBindLocalPlayerDataFromGameManager();
    }


    private void Unbind()
    {
        if (_gameManager != null)
        {
            _gameManager.StateChanged -=
                HandleMatchStateChanged;
        }

        _gameManager = null;
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
    }


    private void HandlePlayerDataDespawned(
        NetworkRunner runner,
        PlayerRef player)
    {
        if (_localPlayerData == null)
            return;

        if (_localPlayerData.PlayerRef !=
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

        if (_localCharacter == null)
            return;

        PlayerSkillController skillController =
            _localCharacter.GetComponent<
                PlayerSkillController>();

        /*PlayerWeaponController weaponController =
            _localCharacter.GetComponent<
                PlayerWeaponController>();*/

        ui.BindPlayerHUD(
            skillController);
    }


    private void UnbindLocalPlayerData()
    {
        _localCharacter =
            null;

        _localPlayerData =
            null;
    }


    // =========================================================
    // Snapshot
    // =========================================================

    private void RefreshFromCurrentState()
    {
        if (_gameManager == null)
            return;

        ApplyMatchState(
            _gameManager.State);
    }


    // =========================================================
    // Match State
    // =========================================================

    private void HandleMatchStateChanged(
        MatchState state)
    {
        ApplyMatchState(state);
    }


    private void ApplyMatchState(
        MatchState state)
    {
        switch (state)
        {
            case MatchState.Playing:
                HandlePlaying();
                break;

            case MatchState.Ending:
                HandleEnding();
                break;

            case MatchState.Result:
                HandleResult();
                break;

            case MatchState.Finished:
                HandleFinished();
                break;
        }
    }


    // =========================================================
    // Playing
    // =========================================================

    private void HandlePlaying()
    {
        ui.ShowPlaying();

        BattleCameraController.Instance?
            .RestoreBattleView();
    }


    // =========================================================
    // Ending
    // =========================================================

    private void HandleEnding()
    {
        ui.ShowEnding();

        if (!TryGetWinnerData(
                out NetworkPlayerData winnerData))
        {
            return;
        }

        NetworkObject character =
            winnerData.CharacterObject;

        if (character == null)
            return;

        BattleCameraController.Instance?
            .FocusWinner(
                character.transform);
    }


    // =========================================================
    // Result
    // =========================================================

    private void HandleResult()
    {
        if (_gameManager == null)
            return;

        if (_gameManager.Winner ==
            PlayerRef.None)
        {
            ui.ShowDraw();
            return;
        }

        if (!TryGetWinnerData(
                out NetworkPlayerData winnerData))
        {
            ui.ShowDraw();
            return;
        }

        ui.ShowWinner(
            winnerData.DisplayName);
    }


    // =========================================================
    // Finished
    // =========================================================

    private void HandleFinished()
    {
        // Scene Loading UI는 AppRoot 쪽에서
        // 실제 SceneLoadStarted를 받아 표시하므로
        // 여기서는 Match UI만 정리.
        ui.HideAll();
    }


    // =========================================================
    // Combat Notice
    // =========================================================

    private void HandlePlayerDeathOccurred(
        PlayerHealth health)
    {
        if (health == null ||
            ui == null)
        {
            return;
        }

        NetworkRunner runner =
            health.Runner;

        if (runner == null)
            return;

        string victimName =
            ResolvePlayerName(
                runner,
                health.Object.InputAuthority);

        string attackerName =
            ResolvePlayerName(
                runner,
                health.LastDeathAttacker);

        if (health.Lives <= 0)
        {
            ui.ShowEliminatedNotice(
                attackerName,
                victimName);

            return;
        }

        ui.ShowKillNotice(
            attackerName,
            victimName);
    }


    private static string ResolvePlayerName(
        NetworkRunner runner,
        PlayerRef player)
    {
        if (runner == null ||
            player == PlayerRef.None)
        {
            return string.Empty;
        }

        if (!runner.TryGetPlayerObject(
                player,
                out NetworkObject dataObject))
        {
            return player.ToString();
        }

        if (!dataObject.TryGetComponent(
                out NetworkPlayerData playerData))
        {
            return player.ToString();
        }

        string displayName =
            playerData.DisplayName;

        return string.IsNullOrWhiteSpace(
                displayName)
            ? player.ToString()
            : displayName;
    }


    // =========================================================
    // Player Lookup
    // =========================================================

    private bool TryGetWinnerData(
        out NetworkPlayerData winnerData)
    {
        winnerData = null;

        if (_gameManager == null)
            return false;

        if (_gameManager.Winner ==
            PlayerRef.None)
        {
            return false;
        }

        NetworkRunner runner =
            _gameManager.Runner;

        if (runner == null)
            return false;

        if (!runner.TryGetPlayerObject(
                _gameManager.Winner,
                out NetworkObject dataObject))
        {
            return false;
        }

        return dataObject.TryGetComponent(
            out winnerData);
    }
}