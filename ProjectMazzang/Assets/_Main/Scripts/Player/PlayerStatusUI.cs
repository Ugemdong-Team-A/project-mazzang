using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerStatusUI :
    PlayerTickModule
{
    [Header("Root")]
    [SerializeField]
    private GameObject statusRoot;

    [Header("Nickname")]
    [SerializeField]
    private TMP_Text nicknameText;


    [Header("Health")]
    [SerializeField]
    private Slider healthBar;

    [SerializeField]
    private Image healthFill;

    [SerializeField]
    private TMP_Text healthText;


    [Header("Lives")]
    [SerializeField]
    private TMP_Text livesText;

    private NetworkPlayerData
        _playerData;

    public override PlayerTickStage Stage => PlayerTickStage.Finalize;

    public override int Order => 100;


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        TryResolvePlayerData();

        RefreshNickname();

        if (nicknameText != null)
        {
            nicknameText.color =
                HasInputAuthority
                    ? Color.yellow
                    : Color.white;
        }

        if (healthFill != null)
        {
            healthFill.color =
                HasInputAuthority
                    ? Color.greenYellow
                    : Color.softRed;
        }
    }


    public override void Present(in PlayerTickState tickState)
    {
        if (_playerData == null)
        {
            TryResolvePlayerData();
        }

        Refresh(
            in tickState);
    }


    private void RefreshVisibility(
        in PlayerTickState tickState)
    {
        if (statusRoot == null ||
            !tickState.HasHealth)
        {
            return;
        }

        bool visible =
            tickState.IsAlive;

        if (statusRoot.activeSelf ==
            visible)
        {
            return;
        }

        statusRoot.SetActive(
            visible);
    }

    // =========================================================
    // Player Data
    // =========================================================

    private void TryResolvePlayerData()
    {
        PlayerRef player =
            Object.InputAuthority;

        if (!Runner.TryGetPlayerObject(
                player,
                out NetworkObject playerObject))
        {
            return;
        }

        _playerData =
            playerObject.GetComponent<
                NetworkPlayerData>();
    }


    // =========================================================
    // UI
    // =========================================================

    private void Refresh(
        in PlayerTickState tickState)
    {
        RefreshVisibility(
            in tickState);

        RefreshNickname();

        RefreshHealth(
            in tickState);

        RefreshLives(
            in tickState);
    }


    private void RefreshNickname()
    {
        if (nicknameText == null)
            return;

        if (_playerData == null)
        {
            nicknameText.text =
                string.Empty;

            return;
        }

        nicknameText.text =
            _playerData.DisplayName;
    }


    private void RefreshHealth(
        in PlayerTickState tickState)
    {
        if (!tickState.HasHealth)
            return;

        if (healthBar != null)
        {
            float ratio =
                tickState.MaxHealth > 0
                    ? (float)tickState.Health /
                      tickState.MaxHealth
                    : 0f;

            healthBar.value =
                Mathf.Clamp01(
                    ratio);
        }

        if (healthText != null)
        {
            healthText.text =
                $"{tickState.Health}/" +
                $"{tickState.MaxHealth}";
        }
    }


    private void RefreshLives(
        in PlayerTickState tickState)
    {
        if (livesText == null ||
            !tickState.HasHealth)
        {
            return;
        }

        livesText.text =
            $"x{tickState.Lives}";
    }

    public override void Simulate(in PlayerTick tick)
    {

    }
}
