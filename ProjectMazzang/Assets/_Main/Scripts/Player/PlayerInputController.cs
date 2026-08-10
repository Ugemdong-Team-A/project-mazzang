using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerInputController :
    NetworkBehaviour,
    INetworkRunnerCallbacks
{
    private InputSystem_Actions
        _inputActions;

    private Vector2 _move;

    private NetworkButtons _buttons;

    private bool _callbacksRegistered;


    // =========================================================
    // Input Lifecycle
    // =========================================================

    private void CreateInput()
    {
        if (_inputActions != null)
            return;

        _inputActions =
            new InputSystem_Actions();

        _inputActions.Player.Move.performed +=
            OnMove;

        _inputActions.Player.Move.canceled +=
            OnMove;

        _inputActions.Player.Jump.performed +=
            OnJump;

        _inputActions.Player.Jump.canceled +=
            OnJump;

        _inputActions.Player.Attack.performed +=
            OnAttack;

        _inputActions.Player.Attack.canceled +=
            OnAttack;

        _inputActions.Enable();
    }


    private void DestroyInput()
    {
        if (_inputActions == null)
            return;

        _inputActions.Player.Move.performed -=
            OnMove;

        _inputActions.Player.Move.canceled -=
            OnMove;

        _inputActions.Player.Jump.performed -=
            OnJump;

        _inputActions.Player.Jump.canceled -=
            OnJump;

        _inputActions.Player.Attack.performed -=
            OnAttack;

        _inputActions.Player.Attack.canceled -=
            OnAttack;

        _inputActions.Disable();
        _inputActions.Dispose();

        _inputActions =
            null;

        _move =
            Vector2.zero;

        _buttons =
            default;
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        if (!HasInputAuthority)
            return;

        CreateInput();

        Runner.AddCallbacks(
            this);

        _callbacksRegistered =
            true;
    }


    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        if (_callbacksRegistered)
        {
            runner.RemoveCallbacks(
                this);

            _callbacksRegistered =
                false;
        }

        DestroyInput();
    }


    // =========================================================
    // Unity Input
    // =========================================================

    private void OnMove(
        InputAction.CallbackContext context)
    {
        _move =
            context.ReadValue<
                Vector2>();
    }


    private void OnJump(
        InputAction.CallbackContext context)
    {
        bool jumped =
            context.ReadValueAsButton();

        _buttons.Set(
            PlayerButton.Jump,
            jumped);
    }


    private void OnAttack(
        InputAction.CallbackContext context)
    {
        bool attacked =
            context.ReadValueAsButton();

        _buttons.Set(
            PlayerButton.Attack,
            attacked);
    }


    // =========================================================
    // Runner Input
    // =========================================================

    public void OnInput(
        NetworkRunner runner,
        NetworkInput input)
    {
        PlayerInputData data =
            new();

        data.Move =
            _move;

        data.Buttons =
            _buttons;

        input.Set(
            data);
    }


    // =========================================================
    // Unused INetworkRunnerCallbacks
    // =========================================================

    public void OnObjectExitAOI(
        NetworkRunner runner,
        NetworkObject obj,
        PlayerRef player)
    {
    }

    public void OnObjectEnterAOI(
        NetworkRunner runner,
        NetworkObject obj,
        PlayerRef player)
    {
    }

    public void OnPlayerJoined(
        NetworkRunner runner,
        PlayerRef player)
    {
    }

    public void OnPlayerLeft(
        NetworkRunner runner,
        PlayerRef player)
    {
    }

    public void OnShutdown(
        NetworkRunner runner,
        ShutdownReason shutdownReason)
    {
    }

    public void OnDisconnectedFromServer(
        NetworkRunner runner,
        NetDisconnectReason reason)
    {
    }

    public void OnConnectRequest(
        NetworkRunner runner,
        NetworkRunnerCallbackArgs.ConnectRequest request,
        byte[] token)
    {
    }

    public void OnConnectFailed(
        NetworkRunner runner,
        NetAddress remoteAddress,
        NetConnectFailedReason reason)
    {
    }

    public void OnReliableDataReceived(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        ReadOnlySpan<byte> data)
    {
    }

    public void OnReliableDataProgress(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        float progress)
    {
    }

    public void OnInputMissing(
        NetworkRunner runner,
        PlayerRef player,
        NetworkInput input)
    {
    }

    public void OnConnectedToServer(
        NetworkRunner runner)
    {
    }

    public void OnSessionListUpdated(
        NetworkRunner runner,
        List<SessionInfo> sessionList)
    {
    }

    public void OnCustomAuthenticationResponse(
        NetworkRunner runner,
        Dictionary<string, object> data)
    {
    }

    public void OnHostMigration(
        NetworkRunner runner,
        HostMigrationToken hostMigrationToken)
    {
    }

    public void OnSceneLoadDone(
        NetworkRunner runner)
    {
    }

    public void OnSceneLoadStart(
        NetworkRunner runner)
    {
    }
}
