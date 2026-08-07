using TMPro;
using UnityEngine;

public sealed class PlayerListItem :
    MonoBehaviour
{
    [SerializeField]
    private TMP_Text nicknameText;

    [SerializeField]
    private TMP_Text readyText;

    [SerializeField]
    private GameObject localPlayerMarker;

    public void SetView(
        string nickname,
        bool ready,
        bool isLocal)
    {
        nicknameText.text =
            string.IsNullOrWhiteSpace(nickname)
                ? "Player"
                : nickname;

        readyText.text =
            ready
                ? "READY"
                : "WAIT";

        if (localPlayerMarker != null)
        {
            localPlayerMarker.SetActive(
                isLocal);
        }
    }
}