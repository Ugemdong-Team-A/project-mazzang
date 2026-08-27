using UnityEngine;

public sealed class ShieldWeaponPresentation :
    MonoBehaviour
{
    private const int ArcSegments = 24;
    private const float FlashDuration = 0.18f;
    private const float CooldownVerticalOffset = 0.05f;

    private Material _material;
    private LineRenderer _guardOuter;
    private LineRenderer _guardInner;
    private LineRenderer _cooldownBack;
    private LineRenderer _cooldownFill;
    private LineRenderer _flash;
    private LineRenderer _rays;
    private float _flashTime;
    private Vector2 _flashOrigin;
    private Vector2 _flashDirection;
    private bool _successFlash;

    public void SetState(
        Vector2 holderPosition,
        Vector2 parryOrigin,
        Vector2 direction,
        float radius,
        float halfAngle,
        bool parryActive,
        bool coolingDown,
        float cooldownProgress,
        bool showLocalCooldown)
    {
        EnsureCreated();

        _guardOuter.enabled = parryActive;
        _guardInner.enabled = parryActive;

        if (parryActive)
        {
            float pulse =
                0.88f +
                Mathf.Sin(Time.time * 42f) * 0.12f;

            _guardOuter.startWidth =
                _guardOuter.endWidth = 0.13f * pulse;
            _guardInner.startWidth =
                _guardInner.endWidth = 0.035f;

            Color outer =
                new(1f, 1f, 1f, 0.9f);
            Color inner =
                new(0.88f, 0.98f, 1f, 0.82f);

            _guardOuter.startColor =
                _guardOuter.endColor = outer;
            _guardInner.startColor =
                _guardInner.endColor = inner;

            SetArc(
                _guardOuter,
                parryOrigin,
                direction,
                radius,
                halfAngle,
                1f);
            SetArc(
                _guardInner,
                parryOrigin,
                direction,
                radius - 0.13f,
                halfAngle - 5f,
                1f);
        }

        bool showCooldown =
            showLocalCooldown &&
            coolingDown;

        _cooldownBack.enabled = showCooldown;
        _cooldownFill.enabled = showCooldown;

        if (showCooldown)
        {
            Vector2 meterOrigin =
                holderPosition +
                Vector2.up * CooldownVerticalOffset;

            SetArc(
                _cooldownBack,
                meterOrigin,
                Vector2.up,
                0.27f,
                82f,
                1f);
            SetArc(
                _cooldownFill,
                meterOrigin,
                Vector2.up,
                0.27f,
                82f,
                Mathf.Clamp01(cooldownProgress));
        }

        UpdateFlash();
    }

    public void PlayBash(
        Vector2 origin,
        Vector2 direction)
    {
        StartFlash(origin, direction, false, 0.12f);
    }

    public void PlayParryStart(
        Vector2 origin,
        Vector2 direction)
    {
        StartFlash(origin, direction, false, FlashDuration);
    }

    public void PlaySuccess(Vector2 point)
    {
        StartFlash(point, Vector2.right, true, 0.22f);
    }

    private void StartFlash(
        Vector2 origin,
        Vector2 direction,
        bool success,
        float duration)
    {
        EnsureCreated();
        _flashOrigin = origin;
        _flashDirection =
            direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector2.right;
        _successFlash = success;
        _flashTime = duration;
        _flash.enabled = true;
        _rays.enabled = true;
    }

    private void UpdateFlash()
    {
        if (_flashTime <= 0f)
        {
            _flash.enabled = false;
            _rays.enabled = false;
            return;
        }

        _flashTime -= Time.deltaTime;

        float duration =
            _successFlash ? 0.22f : FlashDuration;
        float remaining =
            Mathf.Clamp01(_flashTime / duration);
        float expansion = 1f - remaining;
        float radius = _successFlash
            ? Mathf.Lerp(0.12f, 0.82f, expansion)
            : Mathf.Lerp(0.35f, 1.25f, expansion);

        Color white =
            new(1f, 1f, 1f, remaining);
        Color clear =
            new(0.8f, 0.95f, 1f, 0f);

        _flash.startWidth =
            Mathf.Lerp(0.15f, 0.015f, expansion);
        _flash.endWidth = 0.01f;
        _flash.startColor = white;
        _flash.endColor = clear;

        SetArc(
            _flash,
            _flashOrigin,
            _flashDirection,
            radius,
            _successFlash ? 180f : 72f,
            1f);

        SetRays(
            _rays,
            _flashOrigin,
            radius,
            remaining,
            _successFlash ? 8 : 5);
    }

    private void EnsureCreated()
    {
        if (_guardOuter != null)
            return;

        Shader shader =
            Shader.Find("Sprites/Default");
        _material = new Material(shader);

        _guardOuter = CreateLine("Shield Guard Outer", 36);
        _guardInner = CreateLine("Shield Guard Inner", 37);
        _cooldownBack = CreateLine("Shield Cooldown Back", 34);
        _cooldownFill = CreateLine("Shield Cooldown Fill", 35);
        _flash = CreateLine("Shield White Flash", 39);
        _rays = CreateLine("Shield White Rays", 40);

        _cooldownBack.startWidth =
            _cooldownBack.endWidth = 0.05f;
        _cooldownBack.startColor =
            _cooldownBack.endColor =
                new Color(0.08f, 0.09f, 0.11f, 0.7f);

        _cooldownFill.startWidth =
            _cooldownFill.endWidth = 0.06f;
        _cooldownFill.startColor =
            _cooldownFill.endColor =
                new Color(1f, 1f, 1f, 0.98f);
    }

    private LineRenderer CreateLine(
        string lineName,
        int sortingOrder)
    {
        GameObject child = new(lineName);
        child.transform.SetParent(transform, false);

        LineRenderer line =
            child.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.numCapVertices = 4;
        line.numCornerVertices = 3;
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
        progress = Mathf.Clamp01(progress);
        int count = Mathf.Max(
            2,
            Mathf.CeilToInt(
                ArcSegments * progress) + 1);
        line.positionCount = count;

        float center = Mathf.Atan2(
            direction.y,
            direction.x) * Mathf.Rad2Deg;
        float start = center - halfAngle;
        float span = halfAngle * 2f * progress;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            float angle =
                (start + span * t) *
                Mathf.Deg2Rad;
            line.SetPosition(
                i,
                origin +
                new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)) * radius);
        }
    }

    private static void SetRays(
        LineRenderer line,
        Vector2 origin,
        float radius,
        float alpha,
        int rayCount)
    {
        line.positionCount = rayCount * 3;
        line.startWidth = 0.035f;
        line.endWidth = 0.008f;
        line.startColor =
            new Color(1f, 1f, 1f, alpha);
        line.endColor =
            new Color(1f, 1f, 1f, 0f);

        for (int i = 0; i < rayCount; i++)
        {
            float angle =
                i / (float)rayCount *
                Mathf.PI * 2f;
            Vector2 direction =
                new(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle));
            int index = i * 3;
            line.SetPosition(
                index,
                origin + direction * radius * 0.25f);
            line.SetPosition(
                index + 1,
                origin + direction * radius);
            line.SetPosition(
                index + 2,
                origin + direction * radius * 0.25f);
        }
    }

    private void OnDestroy()
    {
        if (_material != null)
            Destroy(_material);
    }
}
