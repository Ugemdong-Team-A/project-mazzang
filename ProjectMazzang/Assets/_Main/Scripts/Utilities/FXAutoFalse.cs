using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EffectPoolUnit))]
public sealed class FXAutoFalse : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float _fallbackLifetime = 2f;

    private EffectPoolUnit _poolUnit;
    private Coroutine _returnRoutine;

    private void Awake()
    {
        _poolUnit = GetComponent<EffectPoolUnit>();
    }

    private void OnEnable()
    {
        if (_returnRoutine != null)
            StopCoroutine(_returnRoutine);

        _returnRoutine = StartCoroutine(ReturnRoutine());
    }

    private void OnDisable()
    {
        if (_returnRoutine == null)
            return;

        StopCoroutine(_returnRoutine);
        _returnRoutine = null;
    }

    private IEnumerator ReturnRoutine()
    {
        yield return new WaitForSeconds(GetLifetime());
        _returnRoutine = null;
        _poolUnit.ReturnToPool();
    }

    private float GetLifetime()
    {
        ParticleSystem[] particles =
            GetComponentsInChildren<ParticleSystem>(true);

        float lifetime = 0f;

        foreach (ParticleSystem particle in particles)
        {
            ParticleSystem.MainModule main = particle.main;
            float maxLifetime = main.startLifetime.constantMax;
            lifetime = Mathf.Max(lifetime, main.duration + maxLifetime);
        }

        return lifetime > 0f ? lifetime : _fallbackLifetime;
    }
}
