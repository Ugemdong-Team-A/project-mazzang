using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LobbyTitlePanel :
    MonoBehaviour
{
    [SerializeField]
    private TMP_InputField nicknameInput;

    [SerializeField]
    private TMP_Text validationText;

    [SerializeField]
    private TMP_Text connectionStateText;

    [SerializeField]
    private Button enterButton;

    public event Action<string> NicknameChanged;
    public event Action<string> EnterRequested;

    private void Awake()
    {
        nicknameInput.onValueChanged.AddListener(
            OnNicknameChanged);

        enterButton.onClick.AddListener(
            OnEnterClicked);
    }

    private void OnDestroy()
    {
        nicknameInput.onValueChanged.RemoveListener(
            OnNicknameChanged);

        enterButton.onClick.RemoveListener(
            OnEnterClicked);
    }

    public string NicknameInput =>
        nicknameInput.text;

    public void SetNickname(string nickname)
    {
        nicknameInput.SetTextWithoutNotify(
            nickname);
    }

    public void SetEnterInteractable(
        bool interactable)
    {
        enterButton.interactable =
            interactable;
    }

    public void SetValidationMessage(
        string message)
    {
        validationText.text =
            message ?? string.Empty;
    }

    public void SetConnectionState(
        string message)
    {
        connectionStateText.text =
            message ?? string.Empty;
    }

    private void OnNicknameChanged(
        string value)
    {
        NicknameChanged?.Invoke(value);
    }

    private void OnEnterClicked()
    {
        EnterRequested?.Invoke(
            nicknameInput.text);
    }
}