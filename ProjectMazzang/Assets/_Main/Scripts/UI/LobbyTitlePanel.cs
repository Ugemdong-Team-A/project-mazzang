using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LobbyTitlePanel :
    MonoBehaviour
{
    [Header("Nickname")]
    [SerializeField]
    private TMP_InputField nicknameInput;

    [SerializeField]
    private TMP_Text validationText;

    [Header("State")]
    [SerializeField]
    private TMP_Text connectionStateText;

    [SerializeField]
    private TMP_Text versionText;

    [Header("Action")]
    [SerializeField]
    private Button enterButton;

    public event Action<string> NicknameChanged;
    public event Action<string> EnterRequested;

    private void Awake()
    {
        nicknameInput.onValueChanged.AddListener(
            HandleNicknameInputChanged);

        enterButton.onClick.AddListener(
            HandleEnterClicked);
    }

    private void OnDestroy()
    {
        nicknameInput.onValueChanged.RemoveListener(
            HandleNicknameInputChanged);

        enterButton.onClick.RemoveListener(
            HandleEnterClicked);
    }

    public string NicknameInput =>
        nicknameInput.text;

    public void SetNickname(
        string nickname)
    {
        nicknameInput.SetTextWithoutNotify(
            nickname);
    }

    public void SetNicknameInteractable(
        bool interactable)
    {
        nicknameInput.interactable =
            interactable;
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

    public void SetVersion(
        string version)
    {
        if (versionText == null)
            return;

        versionText.text =
            string.IsNullOrWhiteSpace(
                version)
                ? string.Empty
                : $"v{version}";
    }

    private void HandleNicknameInputChanged(
        string value)
    {
        NicknameChanged?.Invoke(
            value);
    }

    private void HandleEnterClicked()
    {
        EnterRequested?.Invoke(
            nicknameInput.text);
    }
}