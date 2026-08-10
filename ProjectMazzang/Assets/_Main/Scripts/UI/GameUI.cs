using TMPro;
using UnityEngine;

public sealed class GameUI : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField]
    private GameObject hudRoot;

    [Header("Result")]
    [SerializeField]
    private GameObject resultRoot;

    [SerializeField]
    private TMP_Text resultTitleText;

    [SerializeField]
    private TMP_Text winnerNameText;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        ShowPlaying();
    }


    // =========================================================
    // Match State
    // =========================================================

    public void ShowPlaying()
    {
        SetActive(
            hudRoot,
            true);

        SetActive(
            resultRoot,
            false);
    }


    public void ShowEnding()
    {
        // 마지막 KO와 승자 카메라 연출을
        // 방해하지 않도록 HUD는 숨긴다.
        SetActive(
            hudRoot,
            false);

        SetActive(
            resultRoot,
            false);
    }


    public void ShowWinner(
        string winnerName)
    {
        SetActive(
            hudRoot,
            false);

        SetActive(
            resultRoot,
            true);

        if (resultTitleText != null)
        {
            resultTitleText.text =
                "승자는..!";
        }

        if (winnerNameText != null)
        {
            winnerNameText.text =
                winnerName;
        }
    }


    public void ShowDraw()
    {
        SetActive(
            hudRoot,
            false);

        SetActive(
            resultRoot,
            true);

        if (resultTitleText != null)
        {
            resultTitleText.text =
                "DRAW";
        }

        if (winnerNameText != null)
        {
            winnerNameText.text =
                string.Empty;
        }
    }


    public void HideAll()
    {
        SetActive(
            hudRoot,
            false);

        SetActive(
            resultRoot,
            false);
    }


    // =========================================================
    // Utility
    // =========================================================

    private static void SetActive(
        GameObject target,
        bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }
}