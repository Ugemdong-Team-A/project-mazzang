using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SingletonMonoBehaviour<T> : MonoBehaviour where T : SingletonMonoBehaviour<T>
{
    private static T instance;
    public static T Instance {  get { return instance; } }

    protected virtual void OnAwake() { }
    protected virtual void OnStart() { }
    protected void DestroyInstance()
    {
        if(instance)
        {
            Destroy(instance.gameObject);
            instance = null;
        }
    }

    private void Awake()
    {
        if(instance == null)
        {
            instance = (T)this;
        }
        OnAwake();
    }
    private void Start()
    {
        OnStart();
    }
    protected virtual void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
