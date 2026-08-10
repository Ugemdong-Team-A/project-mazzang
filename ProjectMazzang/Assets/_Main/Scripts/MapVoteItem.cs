using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MapVoteItem :
    MonoBehaviour
{
    [SerializeField]
    private Button button;

    [SerializeField]
    private Image previewImage;

    [SerializeField]
    private TMP_Text nameText;

    [SerializeField]
    private TMP_Text voteCountText;

    [SerializeField]
    private GameObject localVoteIndicator;

    [SerializeField]
    private GameObject rouletteHighlight;

    [SerializeField]
    private GameObject winnerIndicator;

    private int _mapId;
    private Action<int> _clicked;

    public int MapId =>
        _mapId;

    public void Setup(
        MapData data,
        Action<int> clicked)
    {
        _mapId =
            data.MapId;

        _clicked =
            clicked;

        if (nameText != null)
        {
            nameText.text =
                data.DisplayName;
        }

        if (previewImage != null)
        {
            previewImage.sprite =
                data.PreviewImage;

            previewImage.enabled =
                data.PreviewImage != null;
        }

        button.onClick.AddListener(
            OnClicked);

        SetVoteCount(0);
        SetLocalVote(false);
        SetRouletteHighlight(false);
        SetWinner(false);
    }

    public void SetVoteCount(
        int count)
    {
        if (voteCountText != null)
        {
            voteCountText.text =
                count.ToString();
        }
    }

    public void SetLocalVote(
        bool selected)
    {
        if (localVoteIndicator != null)
        {
            localVoteIndicator.SetActive(
                selected);
        }
    }

    public void SetRouletteHighlight(
        bool highlighted)
    {
        if (rouletteHighlight != null)
        {
            rouletteHighlight.SetActive(
                highlighted);
        }
    }

    public void SetWinner(
        bool winner)
    {
        if (winnerIndicator != null)
        {
            winnerIndicator.SetActive(
                winner);
        }
    }

    public void SetInteractable(
        bool interactable)
    {
        button.interactable =
            interactable;
    }

    private void OnClicked()
    {
        _clicked?.Invoke(
            _mapId);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(
                OnClicked);
        }
    }
}
