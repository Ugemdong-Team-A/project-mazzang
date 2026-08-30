using UnityEngine;

public interface IProjectileCastVfx
{
    void SetProgress(float progress);
}

public sealed class FireballCastPresentation : MonoBehaviour
{
    private static Sprite _fallbackSprite;

    private Transform _core;
    private readonly Transform[] _orbiters = new Transform[3];
    private bool _usesExternalPrefab;
    private IProjectileCastVfx _externalVfx;

    public static FireballCastPresentation Create(
        GameObject externalPrefab)
    {
        GameObject root;

        if (externalPrefab != null)
        {
            root = Instantiate(externalPrefab);
        }
        else
        {
            root = new GameObject("Fireball Cast Preview");
        }

        FireballCastPresentation presentation =
            root.GetComponent<FireballCastPresentation>();

        if (presentation == null)
        {
            presentation =
                root.AddComponent<FireballCastPresentation>();
        }

        presentation._usesExternalPrefab =
            externalPrefab != null;

        presentation._externalVfx =
            root.GetComponent<IProjectileCastVfx>();

        if (!presentation._usesExternalPrefab)
        {
            presentation.BuildFallback();
        }

        return presentation;
    }

    public void SetPose(
        Vector2 position,
        float progress)
    {
        transform.position = position;

        if (_usesExternalPrefab)
        {
            _externalVfx?.SetProgress(progress);
            return;
        }

        float pulse =
            1f + Mathf.Sin(Time.time * 18f) * 0.08f;

        _core.localScale =
            Vector3.one *
            Mathf.Lerp(0.12f, 0.55f, progress) *
            pulse;

        float radius =
            Mathf.Lerp(0.42f, 0.18f, progress);

        for (int i = 0; i < _orbiters.Length; i++)
        {
            float angle =
                Time.time * 5f +
                i * Mathf.PI * 2f / _orbiters.Length;

            _orbiters[i].localPosition =
                new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle),
                    0f) * radius;
        }
    }

    public void Release()
    {
        Destroy(gameObject);
    }

    private void BuildFallback()
    {
        _core = CreateDot(
            "Core",
            new Color(1f, 0.23f, 0.02f, 0.9f),
            31);

        for (int i = 0; i < _orbiters.Length; i++)
        {
            _orbiters[i] = CreateDot(
                $"Spark {i + 1}",
                new Color(1f, 0.78f, 0.08f, 0.85f),
                32);

            _orbiters[i].localScale =
                Vector3.one * 0.1f;
        }
    }

    private Transform CreateDot(
        string objectName,
        Color color,
        int sortingOrder)
    {
        GameObject dot =
            new GameObject(objectName);

        dot.transform.SetParent(
            transform,
            false);

        SpriteRenderer renderer =
            dot.AddComponent<SpriteRenderer>();

        renderer.sprite = GetFallbackSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        return dot.transform;
    }

    private static Sprite GetFallbackSprite()
    {
        if (_fallbackSprite != null)
            return _fallbackSprite;

        const int size = 32;
        Texture2D texture =
            new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Fireball Placeholder",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

        Color[] pixels = new Color[size * size];

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
                        (1f - point.magnitude) * 4f);

                pixels[y * size + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        _fallbackSprite =
            Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);

        _fallbackSprite.name =
            "Fireball Placeholder";

        return _fallbackSprite;
    }
}
