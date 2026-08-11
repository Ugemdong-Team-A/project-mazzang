using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerStatusUI :
    PlayerModule
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


    private IPlayerHealthState
        _healthState;

    private NetworkPlayerData
        _playerData;


    // =========================================================
    // Context
    // =========================================================

    protected override void OnContextReady()
    {
        _healthState =
            Context.Get<
                IPlayerHealthState>();
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        TryResolvePlayerData();

        Refresh();

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


    public override void Render()
    {
        if (_playerData == null)
        {
            TryResolvePlayerData();
        }

        Refresh();
    }


    private void RefreshVisibility()
    {
        if (statusRoot == null/* ||
            _healthState == null*/)
        {
            return;
        }

        bool visible =
            !_healthState.IsDead;

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

    private void Refresh()
    {
        RefreshVisibility();
        RefreshNickname();
        RefreshHealth();
        RefreshLives();
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


    private void RefreshHealth()
    {
        if (_healthState == null)
            return;

        if (healthBar != null)
        {
            float ratio =
                _healthState.MaxHealth > 0
                    ? (float)_healthState.Health /
                      _healthState.MaxHealth
                    : 0f;

            healthBar.value =
                Mathf.Clamp01(
                    ratio);
        }

        if (healthText != null)
        {
            healthText.text =
                $"{_healthState.Health}/" +
                $"{_healthState.MaxHealth}";
        }
    }


    private void RefreshLives()
    {
        if (livesText == null ||
            _healthState == null)
        {
            return;
        }

        livesText.text =
            $"x{_healthState.Lives}";
    }
}
