using TMPro;
using UnityEngine;

public sealed class GameUI :
    MonoBehaviour
{
    [Header("HUD")]
    [SerializeField]
    private PlayerHUD playerHUD;


    [Header("Combat Notice")]
    [SerializeField]
    private CombatNoticeUI combatNotice;


    [Header("Result")]
    [SerializeField]
    private GameObject resultRoot;

    [SerializeField]
    private TMP_Text resultTitleText;

    [SerializeField]
    private TMP_Text winnerNameText;


    private void Awake()
    {
        HideAll();
    }


    // =========================================================
    // Player HUD
    // =========================================================

    public void BindPlayerHUD(
        PlayerSkillController skillController)
    {
        playerHUD?.Bind(
            skillController);
    }


    public void UnbindPlayerHUD()
    {
        playerHUD?.Unbind();
    }


    // =========================================================
    // Match State
    // =========================================================

    public void ShowPlaying()
    {
        playerHUD?.Show();

        SetActive(
            resultRoot,
            false);
    }


    public void ShowEnding()
    {
        playerHUD?.Hide();

        SetActive(
            resultRoot,
            false);
    }


    public void ShowWinner(
        string winnerName)
    {
        playerHUD?.Hide();

        SetActive(
            resultRoot,
            true);

        SetText(
            resultTitleText,
            "WINNER");

        SetText(
            winnerNameText,
            winnerName);

        combatNotice?
            .HideImmediate();
    }


    public void ShowDraw()
    {
        playerHUD?.Hide();

        SetActive(
            resultRoot,
            true);

        SetText(
            resultTitleText,
            "DRAW");

        SetText(
            winnerNameText,
            string.Empty);

        combatNotice?
            .HideImmediate();
    }


    public void HideAll()
    {
        playerHUD?.Hide();

        SetActive(
            resultRoot,
            false);

        combatNotice?
            .HideImmediate();
    }


    // =========================================================
    // Combat Notice
    // =========================================================

    public void ShowKillNotice(
        string attackerName,
        string victimName)
    {
        combatNotice?
            .ShowKill(
                attackerName,
                victimName);
    }


    public void ShowEliminatedNotice(
        string attackerName,
        string victimName)
    {
        combatNotice?
            .ShowEliminated(
                attackerName,
                victimName);
    }


    // =========================================================
    // Utility
    // =========================================================

    private static void SetActive(
        GameObject target,
        bool active)
    {
        if (target == null)
            return;

        target.SetActive(
            active);
    }


    private static void SetText(
        TMP_Text target,
        string value)
    {
        if (target == null)
            return;

        target.text =
            value ?? string.Empty;
    }
}