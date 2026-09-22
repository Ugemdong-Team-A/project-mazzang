using UnityEngine;

public class ProjectileVisual : MonoBehaviour
{
    bool _initialized;

    bool _trailStarted;

    public void Initialize()
    {
        if (_initialized) return;

        // movement.OnExpired += () => Destroy(gameObject);

        _initialized = true;
    }

    /*void Update() { }

    private void LateUpdate() { }*/
}
