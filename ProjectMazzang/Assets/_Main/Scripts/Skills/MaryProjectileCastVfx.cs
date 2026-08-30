using UnityEngine;

/// <summary>
/// 메이드 투사체의 시전 지점으로 작은 빛이 모이는 로컬 연출입니다.
/// 스킬 판정에는 관여하지 않으며 시전 진행도만 표현합니다.
/// </summary>
public sealed class MaryProjectileCastVfx :
    MonoBehaviour,
    IProjectileCastVfx
{
    private static Sprite _dotSprite;

    [Header("Gathering Dots")]
    [SerializeField]
    [Range(5, 16)]
    private int dotCount = 11;

    [SerializeField]
    [Min(0.1f)]
    private float outerRadius = 0.9f;

    [SerializeField]
    [Min(0f)]
    private float innerRadius = 0.045f;

    [SerializeField]
    [Min(0.001f)]
    private float minimumSize = 0.025f;

    [SerializeField]
    [Min(0.001f)]
    private float maximumSize = 0.075f;

    [SerializeField]
    private float rotationSpeed = 0.55f;

    [SerializeField]
    private int sortingOrder = 34;

    private Transform[] _dots;
    private SpriteRenderer[] _renderers;
    private float[] _angles;
    private float[] _radiusScales;
    private float[] _sizeScales;
    private Transform _gatherPoint;
    private SpriteRenderer _gatherPointRenderer;


    private void Awake()
    {
        Build();
    }


    public void SetProgress(float progress)
    {
        if (_dots == null)
            Build();

        progress = Mathf.Clamp01(progress);

        float time = Time.time;

        for (int i = 0; i < _dots.Length; i++)
        {
            float phase =
                Mathf.Repeat(
                    progress * 1.55f +
                    (float)i / _dots.Length,
                    1f);

            float eased =
                1f - Mathf.Pow(1f - phase, 2.2f);

            float radius =
                Mathf.Lerp(
                    outerRadius * _radiusScales[i],
                    innerRadius,
                    eased);

            float angle =
                _angles[i] +
                time * rotationSpeed +
                (1f - eased) * 0.65f;

            float drift =
                Mathf.Sin(
                    time * 2.1f + i * 1.73f) *
                0.025f *
                (1f - eased);

            _dots[i].localPosition =
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius + drift,
                    0f);

            float size =
                Mathf.Lerp(
                    maximumSize,
                    minimumSize,
                    eased) *
                _sizeScales[i];

            _dots[i].localScale =
                Vector3.one * size;

            float appear =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(phase / 0.12f));

            float disappear =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01((1f - phase) / 0.2f));

            Color color =
                i % 4 == 0
                    ? new Color(1f, 0.97f, 0.9f)
                    : new Color(0.93f, 0.98f, 1f);

            color.a =
                appear * disappear *
                Mathf.Lerp(0.48f, 0.9f, progress);

            _renderers[i].color = color;
        }

        UpdateGatherPoint(
            progress,
            time);
    }


    private void Build()
    {
        if (_dots != null)
            return;

        int count =
            Mathf.Clamp(dotCount, 5, 16);

        _dots = new Transform[count];
        _renderers = new SpriteRenderer[count];
        _angles = new float[count];
        _radiusScales = new float[count];
        _sizeScales = new float[count];

        for (int i = 0; i < count; i++)
        {
            GameObject dot =
                CreateDot(
                    $"Gathering Dot {i + 1}",
                    sortingOrder);

            _dots[i] = dot.transform;
            _renderers[i] =
                dot.GetComponent<SpriteRenderer>();

            float noise =
                Mathf.Abs(
                    Mathf.Sin((i + 1) * 12.9898f));

            _angles[i] =
                i * Mathf.PI * 2f / count +
                (noise - 0.5f) * 0.5f;

            _radiusScales[i] =
                Mathf.Lerp(0.72f, 1.12f, noise);

            _sizeScales[i] =
                Mathf.Lerp(0.72f, 1.18f, 1f - noise);
        }

        GameObject gatherPoint =
            CreateDot(
                "Gather Point",
                sortingOrder + 1);

        _gatherPoint = gatherPoint.transform;
        _gatherPointRenderer =
            gatherPoint.GetComponent<SpriteRenderer>();
    }


    private void UpdateGatherPoint(
        float progress,
        float time)
    {
        float completion =
            Mathf.InverseLerp(
                0.68f,
                1f,
                progress);

        float pulse =
            1f + Mathf.Sin(time * 16f) * 0.07f;

        _gatherPoint.localPosition = Vector3.zero;
        _gatherPoint.localScale =
            Vector3.one *
            Mathf.Lerp(0.01f, 0.14f, completion) *
            pulse;

        _gatherPointRenderer.color =
            new Color(
                0.96f,
                0.99f,
                1f,
                Mathf.Lerp(0.08f, 0.82f, completion));
    }


    private GameObject CreateDot(
        string objectName,
        int order)
    {
        GameObject dot =
            new(objectName);

        dot.transform.SetParent(
            transform,
            false);

        SpriteRenderer renderer =
            dot.AddComponent<SpriteRenderer>();

        renderer.sprite = GetDotSprite();
        renderer.sortingOrder = order;

        return dot;
    }


    private static Sprite GetDotSprite()
    {
        if (_dotSprite != null)
            return _dotSprite;

        const int size = 24;

        Texture2D texture =
            new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Mary Cast Dot",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

        Color[] pixels =
            new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point =
                    new(
                        (x + 0.5f) / size * 2f - 1f,
                        (y + 0.5f) / size * 2f - 1f);

                float alpha =
                    Mathf.Clamp01(
                        (1f - point.magnitude) * 3.2f);

                pixels[y * size + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        _dotSprite =
            Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);

        _dotSprite.name = "Mary Cast Dot";

        return _dotSprite;
    }
}
