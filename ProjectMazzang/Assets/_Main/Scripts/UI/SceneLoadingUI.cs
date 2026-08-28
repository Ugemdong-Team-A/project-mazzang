using TMPro;
using UnityEngine;

public sealed class SceneLoadingUI : MonoBehaviour
{
    [SerializeField]
    private GameObject root;

    [SerializeField]
    private TMP_Text messageText;

    private FusionSessionController network;

    private void Awake()
    {
        Hide();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    public void Bind(FusionSessionController source)
    {
        if (network == source)
            return;

        Unbind();

        network = source;

        if (network == null)
            return;

        network.SceneLoadStarted +=
            OnSceneLoadStarted;

        network.SceneLoadCompleted +=
            OnSceneLoadCompleted;
    }

    public void Unbind()
    {
        if (network == null)
            return;

        network.SceneLoadStarted -=
            OnSceneLoadStarted;

        network.SceneLoadCompleted -=
            OnSceneLoadCompleted;

        network = null;
    }

    public void Show(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }

        if (root != null)
        {
            root.SetActive(true);
        }
    }

    public void Hide()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    private void OnSceneLoadStarted()
    {
        Show("게임을 불러오는 중...");
    }

    private void OnSceneLoadCompleted()
    {
        Hide();
    }
}