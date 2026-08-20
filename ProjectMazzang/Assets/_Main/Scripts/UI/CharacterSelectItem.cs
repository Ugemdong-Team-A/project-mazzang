using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterSelectItem :
    MonoBehaviour
{
    [SerializeField]
    private Button button;

    [SerializeField]
    private Image portraitImage;

    [SerializeField]
    private TMP_Text nameText;

    [SerializeField]
    private GameObject selectedIndicator;

    private int _characterId;
    private Action<int> _clicked;

    public int CharacterId =>
        _characterId;

    public void Setup(
        CharacterData data,
        Action<int> clicked)
    {
        _characterId =
            data.CharacterId;

        _clicked =
            clicked;

        if (nameText != null)
        {
            nameText.text =
                data.DisplayName;
        }

        if (portraitImage != null)
        {
            portraitImage.sprite =
                data.Portrait;

            portraitImage.enabled =
                data.Portrait != null;
        }

        button.onClick.AddListener(
            OnClicked);

        SetSelected(false);
    }

    public void SetSelected(
        bool selected)
    {
        if (selectedIndicator != null)
        {
            selectedIndicator.SetActive(
                selected);
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
            _characterId);
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
