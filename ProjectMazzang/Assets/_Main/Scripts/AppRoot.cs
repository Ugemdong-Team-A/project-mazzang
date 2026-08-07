using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class AppRoot : MonoBehaviour
{
    public static AppRoot Instance { get; private set; }

    [Header("Services")]
    [SerializeField]
    private FusionSessionController network;

    public FusionSessionController Network => network;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        if (network == null)
        {
            Debug.LogError(
                $"{nameof(AppRoot)}에 " +
                $"{nameof(FusionSessionController)}가 등록되지 않았습니다.",
                this);
        }
    }
}