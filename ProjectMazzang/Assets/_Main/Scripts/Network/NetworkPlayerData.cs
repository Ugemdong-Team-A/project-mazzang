using System;
using Fusion;
using UnityEngine;

public sealed class NetworkPlayerData :
    NetworkBehaviour
{
    private static string _localNickname;


    // ==================================================
    // Local Nickname
    // ==================================================

    public static string LocalNickname =>
        _localNickname;

    public static bool HasLocalNickname =>
        !string.IsNullOrWhiteSpace(
            _localNickname);


    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLocalState()
    {
        _localNickname =
            null;
    }


    public static bool TrySetLocalNickname(
        string nickname)
    {
        if (!PlayerNicknamePolicy.TryNormalize(
                nickname,
                out string normalized))
        {
            return false;
        }

        _localNickname =
            normalized;

        return true;
    }


    [Networked,
     OnChangedRender(nameof(OnPlayerDataChanged))]
    public NetworkString<_16> Nickname
    {
        get;
        private set;
    }

    [Networked]
    public NetworkBool HasConfirmedNickname
    {
        get;
        private set;
    }


    [Networked,
     OnChangedRender(nameof(OnPlayerDataChanged))]
    public int SelectedCharacterId
    {
        get;
        private set;
    }

    [Networked,
     OnChangedRender(nameof(OnPlayerDataChanged))]
    public NetworkBool IsCharacterConfirmed
    {
        get;
        private set;
    }

    [Networked,
     OnChangedRender(nameof(OnPlayerDataChanged))]
    public int VotedMapId
    {
        get;
        private set;
    }

    [Networked,
     OnChangedRender(nameof(OnPlayerDataChanged))]
    public NetworkObject CharacterObject
    {
        get;
        private set;
    }

    public PlayerRef PlayerRef =>
        Object.InputAuthority;

    public string DisplayName =>
        Nickname.ToString();

    public bool CharacterConfirmed =>
        IsCharacterConfirmed;

    public bool HasMapVote =>
        VotedMapId >= 0;

    public bool IsLocalPlayer =>
        HasInputAuthority;

    public static event Action<
        NetworkPlayerData> LocalSpawned;

    public static event Action<
        NetworkPlayerData> LocalChanged;

    /// <summary>
    /// StateAuthority에서 실제 닉네임이 최초 확정된 순간 한 번 발생합니다.
    /// NPD는 UI를 모르고, 확정 사실만 외부에 알립니다.
    /// </summary>
    public static event Action<
        NetworkPlayerData> NicknameConfirmed;

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

            HasConfirmedNickname =
                false;

            SelectedCharacterId = -1;
            IsCharacterConfirmed = false;
            VotedMapId = -1;
            CharacterObject = null;
        }

        LocalSpawned?.Invoke(this);

        TrySubmitLocalNickname();
    }

    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        // 여기서는 다른 NetworkObject를 Despawn하지 않는다.
        //
        // 서버의 Player 이탈 순서는:
        // 1. FSC.OnPlayerLeft
        // 2. NetworkGameManager가 Character Despawn
        // 3. FSC가 이 PlayerData Despawn
        //
        // Despawned는 각 로컬의 UI/Presentation에게
        // 데이터가 사라졌다는 사실만 알리는 역할로 유지한다.
        LocalDespawned?.Invoke(
            runner,
            Object.InputAuthority);
    }

    // ==================================================
    // Character Object
    // ==================================================

    public void SetPlayerCharacter(
        NetworkObject player)
    {
        if (!HasStateAuthority)
            return;

        if (player != null &&
            player.InputAuthority !=
            Object.InputAuthority)
        {
            Debug.LogWarning(
                "[NPD] Character InputAuthority mismatch.",
                this);

            return;
        }

        CharacterObject = player;
    }

    // ==================================================
    // Lobby Selection State
    // ==================================================

    /// <summary>
    /// 방 전체 선택 규칙은 NetworkGameSession이 검사하고,
    /// NetworkPlayerData는 확정된 개인 상태만 저장합니다.
    /// </summary>
    public void SetCharacterSelection(
        int characterId,
        bool confirmed)
    {
        if (!HasStateAuthority)
            return;

        SelectedCharacterId =
            characterId;

        IsCharacterConfirmed =
            confirmed;
    }

    public void SetMapVote(
        int mapId)
    {
        if (!HasStateAuthority)
            return;

        VotedMapId =
            mapId;
    }

    public void ResetMapVote()
    {
        if (!HasStateAuthority)
            return;

        VotedMapId = -1;
    }

    // ==================================================
    // Nickname
    // ==================================================

    public bool RequestNickname(
        string nickname)
    {
        if (!HasInputAuthority)
            return false;

        if (!PlayerNicknamePolicy.TryNormalize(
                nickname,
                out string normalized))
        {
            return false;
        }

        _localNickname =
            normalized;

        NetworkString<_16> networkName =
            normalized;

        if (HasStateAuthority)
        {
            ApplyNickname(
                networkName);
        }
        else
        {
            RPC_RequestNickname(
                networkName);
        }

        return true;
    }

    private void TrySubmitLocalNickname()
    {
        if (!HasInputAuthority)
            return;

        if (!HasLocalNickname)
            return;

        RequestNickname(
            _localNickname);
    }


    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority)]
    private void RPC_RequestNickname(
        NetworkString<_16> requestedNickname)
    {
        ApplyNickname(
            requestedNickname);
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
            ResolveUniqueNickname(
                normalized);

        bool firstConfirmation =
            !HasConfirmedNickname;

        Nickname =
            normalized;

        HasConfirmedNickname =
            true;

        if (firstConfirmation)
        {
            NicknameConfirmed?
                .Invoke(this);
        }
    }

    // ==================================================
    // Nickname Validation
    // ==================================================

    private string ResolveUniqueNickname(
        string requested)
    {
        string candidate =
            requested;

        if (!NicknameExists(
                candidate))
        {
            return candidate;
        }

        int number = 2;

        while (number < 100)
        {
            string suffix =
                $"#{number}";

            string baseName =
                PlayerNicknamePolicy.ClampForSuffix(
                    requested,
                    suffix);

            candidate =
                baseName +
                suffix;

            if (!NicknameExists(
                    candidate))
            {
                return candidate;
            }

            number++;
        }

        return PlayerNicknamePolicy.CreateFallback(
            PlayerRef);
    }

    private bool NicknameExists(
        string nickname)
    {
        foreach (PlayerRef player
                 in Runner.ActivePlayers)
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

        SelectedCharacterId = -1;
        IsCharacterConfirmed = false;
        VotedMapId = -1;
        CharacterObject = null;
    }
}