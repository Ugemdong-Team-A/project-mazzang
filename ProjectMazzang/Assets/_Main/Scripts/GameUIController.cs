using Fusion;
using UnityEngine;

public sealed class GameUIController : MonoBehaviour
{
    [SerializeField]
    private GameUI ui;

    private NetworkGameManager _gameManager;


    // =========================================================
    // Unity
    // =========================================================

    private void OnEnable()
    {
        NetworkGameManager.LocalSpawned +=
            OnGameManagerSpawned;

        NetworkGameManager.LocalDespawned +=
            OnGameManagerDespawned;

        if (NetworkGameManager.Instance != null)
        {
            Bind(
                NetworkGameManager.Instance);
        }
    }


    private void OnDisable()
    {
        NetworkGameManager.LocalSpawned -=
            OnGameManagerSpawned;

        NetworkGameManager.LocalDespawned -=
            OnGameManagerDespawned;

        Unbind();
    }


    // =========================================================
    // Bind
    // =========================================================

    private void OnGameManagerSpawned(
        NetworkGameManager gameManager)
    {
        Bind(gameManager);
    }


    private void OnGameManagerDespawned(
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
            OnMatchStateChanged;

        RefreshFromCurrentState();
    }


    private void Unbind()
    {
        if (_gameManager != null)
        {
            _gameManager.StateChanged -=
                OnMatchStateChanged;
        }

        _gameManager = null;
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

    private void OnMatchStateChanged(
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