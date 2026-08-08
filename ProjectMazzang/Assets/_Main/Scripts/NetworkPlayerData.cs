using System;
using Fusion;
using UnityEngine;

public sealed class NetworkPlayerData : NetworkBehaviour
{
    [Networked,
     OnChangedRender(nameof(OnPlayerDataChanged))]
    public NetworkString<_16> Nickname { get; private set; }

    [Networked,
     OnChangedRender(nameof(OnPlayerDataChanged))]
    public NetworkBool IsReady { get; private set; }

    [Networked,
     OnChangedRender(nameof(OnPlayerDataChanged))]
    public NetworkObject CharacterObject { get; private set; }

    public PlayerRef PlayerRef =>
        Object.InputAuthority;

    public string DisplayName =>
        Nickname.ToString();

    public bool Ready =>
        IsReady;

    public bool IsLocalPlayer =>
        HasInputAuthority;

    // 로컬 프로세스의 UI 등에 알려주기 위한 이벤트.
    // Network 상태 자체의 원본은 아님.
    public static event Action<NetworkPlayerData> LocalSpawned;
    public static event Action<NetworkPlayerData> LocalChanged;

    public static event Action<
        NetworkRunner,
        PlayerRef> LocalDespawned;

    // ==================================================
    // Spawn / Despawn
    // ==================================================

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            Nickname =
                PlayerNicknamePolicy.CreateFallback(
                    PlayerRef);

            IsReady = false;
        }

        LocalSpawned?.Invoke(this);
    }

    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        PlayerRef player =
            Object.InputAuthority;

        LocalDespawned?.Invoke(
            runner,
            player);
    }

    public void SetPlayerCharacter(NetworkObject player)
    {
        if (!HasStateAuthority)
            return;

        if (player != null &&
        player.InputAuthority != Object.InputAuthority)
        {
            Debug.LogWarning(
                "[NPD] Character InputAuthority mismatch.",
                this);

            return;
        }

        CharacterObject = player;
    }

    // ==================================================
    // Nickname
    // ==================================================

    public bool RequestNickname(string nickname)
    {
        if (!HasInputAuthority)
            return false;

        if (!PlayerNicknamePolicy.TryNormalize(
                nickname,
                out string normalized))
        {
            return false;
        }

        NetworkString<_16> networkName =
            normalized;

        // Host 자신은 Input + State Authority일 수 있으므로
        // 굳이 자기 자신에게 RPC를 보낼 필요가 없다.
        if (HasStateAuthority)
        {
            ApplyNickname(networkName);
        }
        else
        {
            RPC_RequestNickname(networkName);
        }

        return true;
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority)]
    private void RPC_RequestNickname(
        NetworkString<_16> requestedNickname)
    {
        ApplyNickname(requestedNickname);
    }

    private void ApplyNickname(
        NetworkString<_16> requestedNickname)
    {
        if (!HasStateAuthority)
            return;

        string requested =
            requestedNickname.ToString();

        if (!PlayerNicknamePolicy.TryNormalize(
                requested,
                out string normalized))
        {
            normalized =
                PlayerNicknamePolicy.CreateFallback(
                    PlayerRef);
        }

        normalized =
            ResolveUniqueNickname(normalized);

        Nickname = normalized;
    }

    // ==================================================
    // Ready
    // ==================================================

    public bool RequestReady(bool ready)
    {
        if (!HasInputAuthority)
            return false;

        if (HasStateAuthority)
        {
            ApplyReady(ready);
        }
        else
        {
            RPC_RequestReady(ready);
        }

        return true;
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority)]
    private void RPC_RequestReady(bool ready)
    {
        ApplyReady(ready);
    }

    private void ApplyReady(bool ready)
    {
        if (!HasStateAuthority)
            return;

        IsReady = ready;
    }

    // ==================================================
    // Nickname Validation
    // ==================================================

    private string ResolveUniqueNickname(
        string requested)
    {
        string candidate = requested;

        if (!NicknameExists(candidate))
            return candidate;

        int number = 2;

        while (number < 100)
        {
            string suffix = $"#{number}";

            string baseName =
                PlayerNicknamePolicy.ClampForSuffix(
                    requested,
                    suffix);

            candidate =
                baseName + suffix;

            if (!NicknameExists(candidate))
                return candidate;

            number++;
        }

        return PlayerNicknamePolicy.CreateFallback(
            PlayerRef);
    }

    private bool NicknameExists(
        string nickname)
    {
        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (player == PlayerRef)
                continue;

            if (!Runner.TryGetPlayerObject(
                    player,
                    out NetworkObject playerObject))
            {
                continue;
            }

            NetworkPlayerData other =
                playerObject.GetComponent<
                    NetworkPlayerData>();

            if (other == null)
                continue;

            if (string.Equals(
                    other.DisplayName,
                    nickname,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // ==================================================
    // Change Notification
    // ==================================================

    private void OnPlayerDataChanged()
    {
        LocalChanged?.Invoke(this);
    }

    // ==================================================
    // Match Reset
    // ==================================================

    public void ResetForLobby()
    {
        if (!HasStateAuthority)
            return;

        IsReady = false;
        CharacterObject = null;
    }
}