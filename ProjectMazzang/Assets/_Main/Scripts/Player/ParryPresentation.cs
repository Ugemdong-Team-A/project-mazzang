using UnityEngine;

public sealed class ParryPresentation : MonoBehaviour
{
    private const int ArcSegments = 18;
    private LineRenderer _shield;
    private LineRenderer _cooldownBack;
    private LineRenderer _cooldownFill;
    private LineRenderer _success;
    private Material _material;
    private float _successTime;
    private Vector2 _successPoint;
    private Transform _holderRoot;
    private Transform _parryAnchor;
    private Vector2 _direction;
    private float _forwardOffset;
    private float _radius;
    private float _halfAngle;
    private float _cooldownProgress;
    private bool _active;
    private bool _displayCooldown;

    public void SetState(
        Transform holderRoot,
        Transform parryAnchor,
        Vector2 direction,
        float forwardOffset,
        float radius,
        float halfAngle,
        bool active,
        bool coolingDown,
        float cooldownProgress,
        bool showCooldown)
    {
        EnsureCreated();

        _holderRoot = holderRoot;
        _parryAnchor = parryAnchor;
        _direction = direction;
        _forwardOffset = forwardOffset;
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
        if (_shield == null)
            return;

        UpdateParryArc();
        UpdateCooldown();
        UpdateSuccess();
    }

    private void UpdateParryArc()
    {
        _shield.enabled = _active;

        if (!_active)
            return;

        Vector2 anchorPosition =
            _parryAnchor != null
                ? _parryAnchor.position
                : ResolveHolderPosition();

        Vector2 origin =
            anchorPosition +
            _direction * _forwardOffset;

        float pulse =
            0.9f +
            Mathf.Sin(Time.time * 38f) * 0.1f;

        _shield.startWidth = 0.09f * pulse;
        _shield.endWidth = 0.035f;
        _shield.startColor =
            new Color(0.85f, 1f, 1f, 0.98f);
        _shield.endColor =
            new Color(0.1f, 0.75f, 1f, 0.32f);

        SetArc(
            _shield,
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
        if (_shield != null)
            return;

        Shader shader = Shader.Find("Sprites/Default");
        _material = new Material(shader);
        _shield = CreateLine("Parry Shield", 32);
        _cooldownBack = CreateLine("Parry Cooldown Back", 30);
        _cooldownFill = CreateLine("Parry Cooldown Fill", 31);
        _success = CreateLine("Parry Success", 33);

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

    private static void SetArc(
        LineRenderer line,
        Vector2 origin,
        Vector2 direction,
        float radius,
        float halfAngle,
        float progress)
    {
        int count = Mathf.Max(2, Mathf.CeilToInt(ArcSegments * progress) + 1);
        line.positionCount = count;
        float center = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float span = halfAngle * 2f * progress;
        float start = center - halfAngle;

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
