using System.Collections.Generic;
using UnityEngine;

public sealed class EffectPool : SingletonMonoBehaviour<EffectPool>
{
    [SerializeField] private GameObject[] _effectPrefabs;
    [SerializeField, Min(0)] private int _prewarmCount = 2;
    [SerializeField] private Transform _effectRoot;

    private readonly Dictionary<string, GameObject> _prefabTable = new();
    private readonly Dictionary<string, Queue<EffectPoolUnit>> _effectPools = new();

    protected override void OnAwake()
    {
        base.OnAwake();

        if (_effectRoot == null)
            _effectRoot = transform;

        if (_effectPrefabs == null || _effectPrefabs.Length == 0)
            _effectPrefabs = Resources.LoadAll<GameObject>("Effects");

        BuildPools();
    }

    public EffectPoolUnit GetEffect(string effectName)
    {
        if (string.IsNullOrWhiteSpace(effectName))
            return null;

        if (!_prefabTable.TryGetValue(effectName, out GameObject prefab))
            return null;

        Queue<EffectPoolUnit> pool = _effectPools[effectName];

        while (pool.Count > 0)
        {
            EffectPoolUnit unit = pool.Dequeue();

            if (unit != null)
                return unit;
        }

        return CreateUnit(effectName, prefab);
    }

    public void InsertEffect(string effectName, EffectPoolUnit effect)
    {
        if (effect == null || string.IsNullOrWhiteSpace(effectName))
            return;

        if (!_effectPools.TryGetValue(effectName, out Queue<EffectPoolUnit> pool))
            return;

        pool.Enqueue(effect);
    }

    public EffectPoolUnit CreateEffect(string effectName, Vector3 position)
    {
        return CreateEffect(effectName, position, Quaternion.identity);
    }

    public EffectPoolUnit CreateEffect(
        string effectName,
        Vector3 position,
        Quaternion rotation)
    {
        EffectPoolUnit effect = GetEffect(effectName);

        if (effect == null)
            return null;

        effect.transform.SetPositionAndRotation(position, rotation);
        effect.gameObject.SetActive(true);
        return effect;
    }

    private void BuildPools()
    {
        _prefabTable.Clear();
        _effectPools.Clear();

        foreach (GameObject prefab in _effectPrefabs)
        {
            if (prefab == null)
                continue;

            string key = GetEffectKey(prefab);

            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (_prefabTable.ContainsKey(key))
            {
                Debug.LogWarning($"중복 Effect 이름입니다: {key}", prefab);
                continue;
            }

            _prefabTable.Add(key, prefab);
            Queue<EffectPoolUnit> pool = new();
            _effectPools.Add(key, pool);

            for (int i = 0; i < _prewarmCount; i++)
                pool.Enqueue(CreateUnit(key, prefab));
        }
    }

    private EffectPoolUnit CreateUnit(string key, GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, _effectRoot);

        if (!instance.TryGetComponent(out EffectPoolUnit unit))
            unit = instance.AddComponent<EffectPoolUnit>();

        if (!instance.TryGetComponent<FXAutoFalse>(out _))
            instance.AddComponent<FXAutoFalse>();

        unit.SetEffectPool(key);
        instance.SetActive(false);
        return unit;
    }

    private static string GetEffectKey(GameObject prefab)
    {
        if (prefab.TryGetComponent(out EffectPoolUnit unit) &&
            !string.IsNullOrWhiteSpace(unit.EffectName))
        {
            return unit.EffectName;
        }

        return prefab.name;
    }
}
