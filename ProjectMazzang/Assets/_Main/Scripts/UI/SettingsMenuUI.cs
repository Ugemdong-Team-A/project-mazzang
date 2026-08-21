using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SettingsMenuUI : MonoBehaviour
{
    [SerializeField]
    private TMP_FontAsset fontAsset;

    private FusionSessionController _network;
    private GameObject _root;
    private TMP_Text _titleText;
    private TMP_Text _descriptionText;
    private TMP_Text _leaveButtonText;
    private Button _resumeButton;
    private Button _leaveButton;
    private Button _cancelButton;

    private bool _confirmingLeave;
    private bool _leavePending;
    private bool _sceneTransitioning;

    public static bool IsInputBlocked { get; private set; }

    private bool IsOpen =>
        _root != null &&
        _root.activeSelf;

    private void Awake()
    {
        BuildView();
        Hide();
    }

    private void Start()
    {
        Bind(
            AppRoot.Instance != null
                ? AppRoot.Instance.Network
                : null);

        SceneManager.activeSceneChanged +=
            OnActiveSceneChanged;
    }

    private void Update()
    {
        if (Keyboard.current == null ||
            !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (_leavePending ||
            _sceneTransitioning)
        {
            return;
        }

        if (IsEditingText())
            return;

        if (IsOpen)
        {
            if (_confirmingLeave)
            {
                ShowMainView();
            }
            else
            {
                Hide();
            }

            return;
        }

        if (CanOpen())
        {
            ShowMainView();
            _root.SetActive(true);
            IsInputBlocked = true;
        }
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -=
            OnActiveSceneChanged;

        Bind(null);

        if (IsOpen)
        {
            IsInputBlocked = false;
        }
    }

    private void Bind(
        FusionSessionController network)
    {
        if (_network == network)
            return;

        if (_network != null)
        {
            _network.StateChanged -=
                OnNetworkStateChanged;
            _network.SceneLoadStarted -=
                OnSceneLoadStarted;
            _network.SceneLoadCompleted -=
                OnSceneLoadCompleted;
        }

        _network = network;

        if (_network == null)
            return;

        _network.StateChanged +=
            OnNetworkStateChanged;
        _network.SceneLoadStarted +=
            OnSceneLoadStarted;
        _network.SceneLoadCompleted +=
            OnSceneLoadCompleted;
    }

    private bool CanOpen()
    {
        if (_network == null ||
            _network.State ==
                NetworkSessionState.ShuttingDown)
        {
            return false;
        }

        string sceneName =
            SceneManager.GetActiveScene().name;

        return sceneName == "Lobby" ||
               sceneName == "Gameplay";
    }

    private static bool IsEditingText()
    {
        GameObject selected =
            EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

        return selected != null &&
               selected.GetComponentInParent<
                   TMP_InputField>() != null;
    }

    private void ShowMainView()
    {
        _confirmingLeave = false;
        _titleText.text = "설정";
        _descriptionText.text =
            "경기는 일시 정지되지 않습니다.";

        _resumeButton.gameObject.SetActive(true);
        _cancelButton.gameObject.SetActive(false);

        bool showLeave =
            SceneManager.GetActiveScene().name ==
                "Gameplay" &&
            _network != null &&
            _network.State ==
                NetworkSessionState.InRoom;

        _leaveButton.gameObject.SetActive(
            showLeave);
        _leaveButton.interactable =
            showLeave;
        _leaveButtonText.text =
            "게임 나가기";
    }

    private void ShowLeaveConfirmation()
    {
        if (_leavePending)
            return;

        _confirmingLeave = true;
        _titleText.text =
            "게임에서 나갈까요?";
        _descriptionText.text =
            "현재 경기에서 나가고 로비로 돌아갑니다.";

        _resumeButton.gameObject.SetActive(false);
        _cancelButton.gameObject.SetActive(true);
        _leaveButtonText.text =
            "나가기";
    }

    private void Hide()
    {
        _confirmingLeave = false;

        if (_root != null)
        {
            _root.SetActive(false);
        }

        IsInputBlocked = false;
    }

    private async void OnLeaveClicked()
    {
        if (!_confirmingLeave)
        {
            ShowLeaveConfirmation();
            return;
        }

        await LeaveGameAsync();
    }

    private async Task LeaveGameAsync()
    {
        if (_leavePending ||
            _network == null ||
            _network.State !=
                NetworkSessionState.InRoom)
        {
            return;
        }

        _leavePending = true;
        _leaveButton.interactable = false;
        _cancelButton.interactable = false;
        _leaveButtonText.text =
            "나가는 중...";

        bool left =
            await _network.LeaveRoomAsync();

        if (!left)
        {
            _leavePending = false;
            _cancelButton.interactable = true;
            ShowLeaveConfirmation();
            return;
        }

        Hide();

        SceneManager.LoadScene(
            "Lobby",
            LoadSceneMode.Single);
    }

    private void OnNetworkStateChanged(
        NetworkSessionState state)
    {
        if (state ==
            NetworkSessionState.ShuttingDown)
        {
            _leaveButton.interactable = false;
            _cancelButton.interactable = false;
            return;
        }

        if (IsOpen && !_leavePending)
        {
            ShowMainView();
        }
    }

    private void OnSceneLoadStarted()
    {
        _sceneTransitioning = true;
        Hide();
    }

    private void OnSceneLoadCompleted()
    {
        _sceneTransitioning = false;
        Hide();
    }

    private void OnActiveSceneChanged(
        Scene previous,
        Scene next)
    {
        _leavePending = false;
        _sceneTransitioning = false;
        Hide();
    }

    private void BuildView()
    {
        Canvas canvas =
            GetComponentInChildren<
                Canvas>(true);

        if (canvas == null)
        {
            Debug.LogError(
                "[SettingsMenu] Global Canvas를 찾을 수 없습니다.",
                this);
            return;
        }

        _root = CreatePanel(
            "SettingsMenu",
            canvas.transform,
            new Color(0.015f, 0.025f, 0.04f, 0.88f));

        RectTransform rootRect =
            _root.GetComponent<RectTransform>();
        Stretch(rootRect);
        rootRect.SetAsFirstSibling();

        GameObject rail = CreatePanel(
            "MenuRail",
            _root.transform,
            new Color(0.035f, 0.055f, 0.085f, 0.98f));

        RectTransform railRect =
            rail.GetComponent<RectTransform>();
        railRect.anchorMin = new Vector2(0f, 0f);
        railRect.anchorMax = new Vector2(0.38f, 1f);
        railRect.offsetMin = Vector2.zero;
        railRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout =
            rail.AddComponent<VerticalLayoutGroup>();
        layout.padding =
            new RectOffset(72, 56, 86, 64);
        layout.spacing = 16f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        _titleText = CreateText(
            "Title",
            rail.transform,
            "설정",
            44f,
            FontStyles.Bold);
        AddLayoutHeight(
            _titleText.gameObject,
            74f);

        _descriptionText = CreateText(
            "Description",
            rail.transform,
            string.Empty,
            19f,
            FontStyles.Normal);
        _descriptionText.color =
            new Color(0.72f, 0.78f, 0.86f, 1f);
        AddLayoutHeight(
            _descriptionText.gameObject,
            72f);

        CreateSpacer(
            rail.transform);

        _resumeButton = CreateButton(
            "Resume",
            rail.transform,
            "계속",
            new Color(0.12f, 0.2f, 0.3f, 1f),
            Hide,
            out _);

        _leaveButton = CreateButton(
            "LeaveGame",
            rail.transform,
            "게임 나가기",
            new Color(0.55f, 0.12f, 0.12f, 1f),
            OnLeaveClicked,
            out _leaveButtonText);

        _cancelButton = CreateButton(
            "CancelLeave",
            rail.transform,
            "취소",
            new Color(0.12f, 0.2f, 0.3f, 1f),
            ShowMainView,
            out _);
    }

    private GameObject CreatePanel(
        string objectName,
        Transform parent,
        Color color)
    {
        GameObject panel =
            new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        panel.layer = 5;
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;

        return panel;
    }

    private TMP_Text CreateText(
        string objectName,
        Transform parent,
        string value,
        float size,
        FontStyles style)
    {
        GameObject textObject =
            new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

        textObject.layer = 5;
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text =
            textObject.GetComponent<
                TextMeshProUGUI>();
        text.text = value;
        text.font = fontAsset;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment =
            TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = true;

        return text;
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Color color,
        UnityEngine.Events.UnityAction action,
        out TMP_Text labelText)
    {
        GameObject buttonObject =
            CreatePanel(
                objectName,
                parent,
                color);

        Button button =
            buttonObject.AddComponent<Button>();
        button.targetGraphic =
            buttonObject.GetComponent<Image>();
        button.onClick.AddListener(action);

        labelText = CreateText(
            "Label",
            buttonObject.transform,
            label,
            24f,
            FontStyles.Bold);
        labelText.alignment =
            TextAlignmentOptions.Center;
        Stretch(
            labelText.GetComponent<RectTransform>());

        AddLayoutHeight(
            buttonObject,
            64f);

        return button;
    }

    private static void AddLayoutHeight(
        GameObject target,
        float height)
    {
        LayoutElement element =
            target.AddComponent<LayoutElement>();
        element.preferredHeight = height;
    }

    private static void CreateSpacer(
        Transform parent)
    {
        GameObject spacer =
            new(
                "FlexibleSpace",
                typeof(RectTransform),
                typeof(LayoutElement));
        spacer.layer = 5;
        spacer.transform.SetParent(parent, false);
        spacer.GetComponent<LayoutElement>().flexibleHeight = 1f;
    }

    private static void Stretch(
        RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
