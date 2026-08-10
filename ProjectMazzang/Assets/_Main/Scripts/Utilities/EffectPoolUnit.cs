using UnityEngine;

public sealed class EffectPoolUnit : MonoBehaviour
{
    [SerializeField] private string _effectName;

    private string _poolKey;
    private bool _isReturned;

    public string EffectName => _effectName;
    public bool IsReady => !gameObject.activeSelf;

    public void SetEffectPool(string poolKey)
    {
        _poolKey = poolKey;
    }

    public void ReturnToPool()
    {
        if (_isReturned)
            return;

        _isReturned = true;
        gameObject.SetActive(false);

        EffectPool.Instance.InsertEffect(_poolKey, this);
    }

    private void OnEnable()
    {
        _isReturned = false;
    }
}
