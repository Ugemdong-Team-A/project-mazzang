using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerStatusUI :
    NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private PlayerHealth health;

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


    private NetworkPlayerData playerData;


    private void Awake()
    {
        if (health == null)
        {
            health =
                GetComponent<PlayerHealth>();
        }
    }


    public override void Spawned()
    {
        TryResolvePlayerData();
        Refresh();

        nicknameText.color = HasInputAuthority ?
           Color.yellow :
           Color.white;

        healthFill.color = HasInputAuthority ?
            Color.greenYellow :
            Color.softRed;
    }


    public override void Render()
    {
        if (playerData == null)
        {
            TryResolvePlayerData();
        }

        Refresh();
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

        playerData =
            playerObject.GetComponent<
                NetworkPlayerData>();
    }


    // =========================================================
    // UI
    // =========================================================

    private void Refresh()
    {
        RefreshNickname();
        RefreshHealth();
        RefreshLives();
    }


    private void RefreshNickname()
    {
        if (nicknameText == null)
            return;

        if (playerData == null)
        {
            nicknameText.text =
                string.Empty;

            return;
        }

        nicknameText.text =
            playerData.DisplayName;
    }


    private void RefreshHealth()
    {
        if (health == null)
            return;

        if (healthBar != null)
        {
            float ratio =
                health.MaxHealth > 0
                    ? (float)health.Health /
                      health.MaxHealth
                    : 0f;

            healthBar.value =
                Mathf.Clamp01(ratio);
        }

        if (healthText != null)
        {
            healthText.text =
                $"{health.Health}/{health.MaxHealth}";
        }
    }


    private void RefreshLives()
    {
        if (livesText == null ||
            health == null)
        {
            return;
        }

        livesText.text =
            $"x{health.Lives}";
    }
}