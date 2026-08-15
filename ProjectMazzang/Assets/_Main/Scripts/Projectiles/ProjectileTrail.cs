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
    private float endWidth = 0f;

    [SerializeField]
    private Gradient colorOverTrail;


    [Header("Rendering")]
    [SerializeField]
    private Material trailMaterial;

    [SerializeField]
    private string sortingLayerName = "Default";

    [SerializeField]
    private int sortingOrder;


    private TrailRenderer _trail;


    private void Awake()
    {
        ResolveTrail();
        ApplySettings();
    }


    private void OnEnable()
    {
        ResolveTrail();

        _trail.emitting =
            false;

        _trail.Clear();
    }


    public void Begin()
    {
        ResolveTrail();

        _trail.emitting =
            false;

        _trail.Clear();

        _trail.emitting =
            true;
    }


    private void OnDisable()
    {
        if (_trail == null)
            return;

        _trail.emitting =
            false;

        _trail.Clear();
    }

    private void Start()
    {
        Begin();
    }

    public void Clear()
    {
        ResolveTrail();

        _trail.Clear();
    }


    public void SetEmitting(
        bool emitting)
    {
        ResolveTrail();

        _trail.emitting =
            emitting;
    }


    private void ResolveTrail()
    {
        if (_trail != null)
            return;

        _trail =
            GetComponent<TrailRenderer>();
    }


    private void ApplySettings()
    {
        if (_trail == null)
            return;

        _trail.time =
            lifetime;

        _trail.minVertexDistance =
            minVertexDistance;

        _trail.widthMultiplier =
            1f;

        _trail.widthCurve =
            new AnimationCurve(
                new Keyframe(
                    0f,
                    startWidth),
                new Keyframe(
                    1f,
                    endWidth));

        if (colorOverTrail != null)
        {
            _trail.colorGradient =
                colorOverTrail;
        }

        if (trailMaterial != null)
        {
            _trail.sharedMaterial =
                trailMaterial;
        }

        _trail.alignment =
            LineAlignment.View;

        _trail.textureMode =
            LineTextureMode.Stretch;

        _trail.autodestruct =
            false;

        _trail.generateLightingData =
            false;

        _trail.shadowCastingMode =
            ShadowCastingMode.Off;

        _trail.receiveShadows =
            false;

        _trail.sortingLayerName =
            sortingLayerName;

        _trail.sortingOrder =
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

        ResolveTrail();
        ApplySettings();
    }


    private void OnValidate()
    {
        ResolveTrail();
        ApplySettings();
    }

#endif
}