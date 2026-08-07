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

    public FusionSessionController Network =>
        network;

    public PopupUI Popup =>
        popup;

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
    }
}