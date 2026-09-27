using UnityEngine;

public sealed class ParryPresentation : MonoBehaviour
{
    private const float ArcDegreesPerSegment = 6f;
    private LineRenderer _shieldGlow;
    private LineRenderer _shieldCore;
    private LineRenderer _cooldownBack;
    private LineRenderer _cooldownFill;
    private LineRenderer _success;
    private Material _material;
    private float _successTime;
    private Vector2 _successPoint;
    private Transform _holderRoot;
    private Vector2 _direction;
    private bool _facingRight;
    private Vector2 _centerOffset;
    private float _radius;
    private float _halfAngle;
    private float _cooldownProgress;
    private bool _active;
    private bool _displayCooldown;

    public void SetState(
        Transform holderRoot,
        Vector2 direction,
        bool facingRight,
        Vector2 centerOffset,
        float radius,
        float halfAngle,
        bool active,
        bool coolingDown,
        float cooldownProgress,
        bool showCooldown)
    {
        EnsureCreated();

        _holderRoot = holderRoot;
        _direction = direction;
        _facingRight = facingRight;
        _centerOffset = centerOffset;
        _radius = radius;
        _halfAngle = halfAngle;
        _active = active;
        _displayCooldown =
            showCooldown && coolingDown;
        _cooldownProgress =
            Mathf.Clamp01(cooldownProgress);
    }

    public void PlaySuccess(Vector2 point)
    {
        EnsureCreated();
        _successPoint = point;
        _successTime = 0.16f;
        _success.enabled = true;
    }

    private void LateUpdate()
    {
        if (_shieldGlow == null)
            return;

        UpdateParryArc();
        UpdateCooldown();
        UpdateSuccess();
    }

    private void UpdateParryArc()
    {
        _shieldGlow.enabled = _active;
        _shieldCore.enabled = _active;

        if (!_active)
            return;

        Vector2 origin =
            ParryGeometry.ResolveOrigin(
                ResolveHolderPosition(),
                _facingRight,
                _centerOffset);

        float pulse =
            0.92f +
            Mathf.Sin(Time.time * 38f) * 0.08f;

        _shieldGlow.widthMultiplier =
            0.16f * pulse;

        _shieldCore.widthMultiplier =
            0.06f * pulse;

        SetArc(
            _shieldGlow,
            origin,
            _direction,
            _radius,
            _halfAngle,
            1f);

        SetArc(
            _shieldCore,
            origin,
            _direction,
            _radius,
            _halfAngle,
            1f);
    }

    private void UpdateCooldown()
    {
        _cooldownBack.enabled =
            _displayCooldown;
        _cooldownFill.enabled =
            _displayCooldown;

        if (!_displayCooldown)
            return;

        Vector2 meterOrigin =
            ResolveHolderPosition() +
            Vector2.down * 0.85f;

        SetArc(
            _cooldownBack,
            meterOrigin,
            Vector2.up,
            0.24f,
            75f,
            1f);

        SetArc(
            _cooldownFill,
            meterOrigin,
            Vector2.up,
            0.24f,
            75f,
            _cooldownProgress);
    }

    private Vector2 ResolveHolderPosition()
    {
        return _holderRoot != null
            ? (Vector2)_holderRoot.position
            : (Vector2)transform.position;
    }

    private void UpdateSuccess()
    {
        if (_successTime <= 0f)
        {
            _success.enabled = false;
            return;
        }

        _successTime -= Time.deltaTime;
        float normalized = Mathf.Clamp01(_successTime / 0.16f);
        float radius = Mathf.Lerp(0.7f, 0.12f, normalized);
        _success.startWidth = Mathf.Lerp(0.01f, 0.12f, normalized);
        _success.endWidth = 0.01f;
        Color color = new(0.85f, 1f, 1f, normalized);
        _success.startColor = color;
        _success.endColor = new Color(0.15f, 0.8f, 1f, 0f);
        SetArc(_success, _successPoint, Vector2.right, radius, 180f, 1f);
    }

    private void EnsureCreated()
    {
        if (_shieldGlow != null)
            return;

        Shader shader = Shader.Find("Sprites/Default");
        _material = new Material(shader);
        _shieldGlow = CreateLine("Parry Shield Glow", 32);
        _shieldCore = CreateLine("Parry Shield Core", 33);
        _cooldownBack = CreateLine("Parry Cooldown Back", 30);
        _cooldownFill = CreateLine("Parry Cooldown Fill", 31);
        _success = CreateLine("Parry Success", 34);

        ConfigureSymmetricArc(
            _shieldGlow,
            new Color(0.05f, 0.55f, 1f),
            new Color(0.35f, 0.95f, 1f),
            0.04f,
            0.48f);

        ConfigureSymmetricArc(
            _shieldCore,
            new Color(0.08f, 0.72f, 1f),
            new Color(0.9f, 1f, 1f),
            0.2f,
            1f);

        _cooldownBack.startWidth = _cooldownBack.endWidth = 0.045f;
        _cooldownBack.startColor = _cooldownBack.endColor =
            new Color(0.08f, 0.14f, 0.18f, 0.65f);
        _cooldownFill.startWidth = _cooldownFill.endWidth = 0.055f;
        _cooldownFill.startColor = _cooldownFill.endColor =
            new Color(0.2f, 0.9f, 1f, 0.95f);
    }

    private LineRenderer CreateLine(string lineName, int sortingOrder)
    {
        GameObject child = new(lineName);
        child.transform.SetParent(transform, false);
        LineRenderer line = child.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.numCapVertices = 3;
        line.numCornerVertices = 2;
        line.material = _material;
        line.sortingOrder = sortingOrder;
        line.enabled = false;
        return line;
    }


    private static void ConfigureSymmetricArc(
        LineRenderer line,
        Color edgeColor,
        Color centerColor,
        float edgeAlpha,
        float centerAlpha)
    {
        line.widthCurve =
            new AnimationCurve(
                new Keyframe(0f, 0.25f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0.25f));

        Gradient gradient =
            new();

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(edgeColor, 0f),
                new GradientColorKey(centerColor, 0.5f),
                new GradientColorKey(edgeColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(edgeAlpha, 0f),
                new GradientAlphaKey(centerAlpha, 0.5f),
                new GradientAlphaKey(edgeAlpha, 1f)
            });

        line.colorGradient =
            gradient;
    }

    private static void SetArc(
        LineRenderer line,
        Vector2 origin,
        Vector2 direction,
        float radius,
        float halfAngle,
        float progress)
    {
        progress =
            Mathf.Clamp01(progress);

        float resolvedHalfAngle =
            Mathf.Max(
                0f,
                halfAngle);

        float span =
            resolvedHalfAngle *
            2f *
            progress;

        int count =
            Mathf.Max(
                2,
                Mathf.CeilToInt(
                    span /
                    ArcDegreesPerSegment) +
                1);

        line.positionCount = count;
        float center = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float start = center - resolvedHalfAngle;

        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0f : i / (float)(count - 1);
            float angle = (start + span * t) * Mathf.Deg2Rad;
            line.SetPosition(
                i,
                origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }

    private void OnDestroy()
    {
        if (_material != null)
            Destroy(_material);
    }
}
