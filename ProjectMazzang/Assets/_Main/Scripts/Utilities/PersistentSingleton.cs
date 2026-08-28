using UnityEngine;

/// <summary>
/// 씬이 전환되어도 유지되는 싱글턴 MonoBehaviour.
/// </summary>
public abstract class PersistentSingleton<T> : MonoBehaviour
    where T : PersistentSingleton<T>
{
    public static T Instance { get; private set; }

    public static bool HasInstance => Instance != null;

    private void Awake()
    {
        T currentInstance = (T)this;

        if (Instance != null && Instance != currentInstance)
        {
            Destroy(gameObject);
            return;
        }

        Instance = currentInstance;

        DontDestroyOnLoad(gameObject);

        OnSingletonAwake();
    }

    private void Start()
    {
        if (Instance == (T)this)
        {
            OnSingletonStart();
        }
    }

    private void OnDestroy()
    {
        if (Instance == (T)this)
        {
            Instance = null;
        }

        OnSingletonDestroyed();
    }

    protected virtual void OnSingletonAwake()
    {
    }

    protected virtual void OnSingletonStart()
    {
    }

    protected virtual void OnSingletonDestroyed()
    {
    }
}