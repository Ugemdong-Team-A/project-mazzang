using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(TrailRenderer))]
public sealed class ProjectileTrail : MonoBehaviour
{
    [Header("Trail")]
    [Min(0.01f)]
    [SerializeField]
    private float lifetime = 0.12f;

    [Min(0.001f)]
    [SerializeField]
    private float minVertexDistance = 0.04f;

    [Min(0f)]
    [SerializeField]
    private float startWidth = 0.09f;

    [Min(0f)]
    [SerializeField]
    private float endWidth;

    [SerializeField]
    private Gradient colorOverTrail;

    [Header("Rendering")]
    [SerializeField]
    private Material trailMaterial;

    [SerializeField]
    private string sortingLayerName = "Default";

    [SerializeField]
    private int sortingOrder;

    private TrailRenderer _sourceTrail;
    private TrailRenderer _visualTrail;
    private GameObject _visualObject;
    private Transform _followTarget;
    private bool _completed;

    private void Awake()
    {
        ResolveSourceTrail();

        if (_sourceTrail != null)
        {
            _sourceTrail.emitting = false;
            _sourceTrail.Clear();
            _sourceTrail.enabled = false;
        }
    }

    public void Begin(
        Vector3 presentationOrigin,
        Transform followTarget)
    {
        if (_visualTrail != null)
            return;

        _completed = false;
        _followTarget = followTarget;

        _visualObject =
            new GameObject(
                "[Local] Projectile Trail");
        _visualObject.layer =
            gameObject.layer;
        _visualObject.transform.position =
            presentationOrigin;

        _visualTrail =
            _visualObject.AddComponent<
                TrailRenderer>();

        ApplySettings(
            _visualTrail);

        _visualTrail.emitting = false;
        _visualTrail.Clear();
        _visualTrail.emitting = true;
    }

    private void LateUpdate()
    {
        if (_completed ||
            _visualObject == null)
        {
            return;
        }

        if (_followTarget == null)
        {
            Complete();
            return;
        }

        _visualObject.transform.SetPositionAndRotation(
            _followTarget.position,
            _followTarget.rotation);
    }

    public void Complete()
    {
        if (_completed)
            return;

        _completed = true;

        if (_visualObject != null &&
            _followTarget != null)
        {
            _visualObject.transform.SetPositionAndRotation(
                _followTarget.position,
                _followTarget.rotation);
        }

        _followTarget = null;

        if (_visualTrail != null)
        {
            _visualTrail.emitting = false;
        }

        if (_visualObject != null)
        {
            Destroy(
                _visualObject,
                Mathf.Max(0.01f, lifetime));
        }

        _visualTrail = null;
        _visualObject = null;
    }

    private void OnDestroy()
    {
        Complete();
    }

    private void ResolveSourceTrail()
    {
        if (_sourceTrail == null)
        {
            _sourceTrail =
                GetComponent<TrailRenderer>();
        }
    }

    private void ApplySettings(
        TrailRenderer trail)
    {
        if (trail == null)
            return;

        trail.time = lifetime;
        trail.minVertexDistance =
            minVertexDistance;
        trail.widthMultiplier = 1f;
        trail.widthCurve =
            new AnimationCurve(
                new Keyframe(0f, startWidth),
                new Keyframe(1f, endWidth));

        if (colorOverTrail != null)
        {
            trail.colorGradient =
                colorOverTrail;
        }

        if (trailMaterial != null)
        {
            trail.sharedMaterial =
                trailMaterial;
        }

        trail.alignment =
            LineAlignment.View;
        trail.textureMode =
            LineTextureMode.Stretch;
        trail.autodestruct = false;
        trail.generateLightingData = false;
        trail.shadowCastingMode =
            ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.sortingLayerName =
            sortingLayerName;
        trail.sortingOrder =
            sortingOrder;
    }

#if UNITY_EDITOR

    private void Reset()
    {
        colorOverTrail =
            new Gradient();

        colorOverTrail.SetKeys(
            new[]
            {
                new GradientColorKey(
                    Color.white,
                    0f),
                new GradientColorKey(
                    Color.white,
                    1f)
            },
            new[]
            {
                new GradientAlphaKey(
                    1f,
                    0f),
                new GradientAlphaKey(
                    0f,
                    1f)
            });

        ResolveSourceTrail();
        ApplySettings(
            _sourceTrail);
    }

    private void OnValidate()
    {
        ResolveSourceTrail();
        ApplySettings(
            _sourceTrail);
    }

#endif
}
