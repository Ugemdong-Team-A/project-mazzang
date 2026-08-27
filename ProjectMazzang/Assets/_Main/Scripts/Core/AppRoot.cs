using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class AppRoot : MonoBehaviour
{
    public static AppRoot Instance { get; private set; }

    [Header("Services")]
    [SerializeField]
    private FusionSessionController network;

    [Header("Global UI")]
    [SerializeField]
    private PopupUI popup;

    [SerializeField]
    private SceneLoadingUI sceneLoading;

    [SerializeField]
    private SystemNoticeUI systemNotice;

    public FusionSessionController Network =>
        network;

    public PopupUI Popup =>
        popup;

    public SceneLoadingUI SceneLoading =>
        sceneLoading;

    public SystemNoticeUI SystemNotice =>
        systemNotice;

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        if (network == null)
        {
            Debug.LogError(
                $"{nameof(FusionSessionController)}가 등록되지 않았습니다.",
                this);
        }

        if (popup == null)
        {
            Debug.LogError(
                $"{nameof(PopupUI)}가 등록되지 않았습니다.",
                this);
        }

        if (sceneLoading == null)
        {
            Debug.LogError(
                $"{nameof(SceneLoadingUI)}가 등록되지 않았습니다.",
                this);
        }

        if (systemNotice == null)
        {
            Debug.LogError(
                $"{nameof(SystemNoticeUI)}가 등록되지 않았습니다.",
                this);
        }

        if (network != null &&
            sceneLoading != null)
        {
            sceneLoading.Bind(network);
        }

        if (network != null &&
            systemNotice != null)
        {
            systemNotice.Bind(network);
        }
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        if (sceneLoading != null)
        {
            sceneLoading.Unbind();
        }

        if (systemNotice != null)
        {
            systemNotice.Unbind();
        }

        Instance = null;
    }
}