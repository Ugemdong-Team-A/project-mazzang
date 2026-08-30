using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class MagicCliffsArenaVisual :
    MonoBehaviour
{
    [Header("Background")]
    [SerializeField]
    private Sprite skySprite;

    [FormerlySerializedAs("cloudsSprite")]
    [SerializeField]
    private Sprite ground1Sprite;

    [FormerlySerializedAs("farGroundsSprite")]
    [SerializeField]
    private Sprite ground2Sprite;

    [FormerlySerializedAs("seaSprite")]
    [SerializeField]
    private Sprite ground3Sprite;

    [SerializeField]
    private Vector2 backgroundWorldSize =
        new(84f, 34f);

    [SerializeField, Min(0.01f)]
    private float backgroundScale =
        0.6666667f;

    [FormerlySerializedAs("backgroundCenter")]
    [SerializeField]
    private Vector2 backgroundOffset =
        new(0f, 2f);

    [Header("Terrain")]
    [SerializeField]
    private Sprite platformSprite;

    [SerializeField]
    private Sprite treeSprite;

    private readonly List<ParallaxLayer>
        _parallaxLayers = new();

    private Camera _camera;


    private sealed class ParallaxLayer
    {
        public Transform Transform;
        public Vector3 Origin;
        public float Follow;
    }


    private void Awake()
    {
        BuildBackground();
        SkinPlatforms();
        AddForegroundDecoration();
    }


    private void LateUpdate()
    {
        if (_camera == null)
        {
            _camera = Camera.main;
        }

        if (_camera == null)
            return;

        Vector3 cameraPosition =
            _camera.transform.position;

        foreach (ParallaxLayer layer
                 in _parallaxLayers)
        {
            layer.Transform.position =
                layer.Origin +
                new Vector3(
                    cameraPosition.x *
                    layer.Follow,
                    cameraPosition.y *
                    layer.Follow,
                    0f);
        }
    }


    private void BuildBackground()
    {
        CreateSingleLayer(
            "Sky",
            skySprite,
            -100,
            1f);

        CreateSingleLayer(
            "Ground 1",
            ground1Sprite,
            -90,
            0.9f);

        CreateSingleLayer(
            "Ground 2",
            ground2Sprite,
            -80,
            0.75f);

        CreateSingleLayer(
            "Ground 3",
            ground3Sprite,
            -70,
            0.55f);
    }


    private void CreateSingleLayer(
        string layerName,
        Sprite sprite,
        int sortingOrder,
        float follow)
    {
        if (sprite == null)
            return;

        Transform layer =
            CreateLayerRoot(
                layerName,
                follow);

        SpriteRenderer renderer =
            CreateRenderer(
                layer,
                layerName,
                sprite,
                sortingOrder,
                Color.white);

        Vector2 spriteSize =
            sprite.bounds.size;

        float scale =
            Mathf.Max(
                backgroundWorldSize.x /
                Mathf.Max(spriteSize.x, 0.01f),
                backgroundWorldSize.y /
                Mathf.Max(spriteSize.y, 0.01f)) *
            Mathf.Max(backgroundScale, 0.01f);

        renderer.transform.localScale =
            Vector3.one * scale;

        MoveRendererCenter(
            renderer,
            transform.TransformPoint(
                backgroundOffset));
    }


    private Transform CreateLayerRoot(
        string layerName,
        float follow)
    {
        GameObject layerObject =
            new(layerName);

        Transform layer =
            layerObject.transform;

        layer.SetParent(
            transform,
            false);

        Vector3 origin =
            transform.position;

        _parallaxLayers.Add(
            new ParallaxLayer
            {
                Transform = layer,
                Origin = origin,
                Follow = follow
            });

        return layer;
    }


    private void SkinPlatforms()
    {
        if (platformSprite == null)
            return;

        BoxCollider2D[] colliders =
            GetComponentsInChildren<
                BoxCollider2D>(
                true);

        foreach (BoxCollider2D collider
                 in colliders)
        {
            if (!collider.enabled ||
                collider.isTrigger)
            {
                continue;
            }

            SpriteRenderer original =
                collider.GetComponent<
                    SpriteRenderer>();

            if (original != null)
            {
                original.enabled =
                    true;

                original.color =
                    new Color(
                        0.055f,
                        0.12f,
                        0.13f,
                        1f);

                original.sortingOrder =
                    -2;
            }

            GameObject visualObject =
                new("Magic Cliffs Surface");

            Transform visual =
                visualObject.transform;

            visual.SetParent(
                collider.transform,
                false);

            Vector3 parentScale =
                collider.transform
                    .lossyScale;

            visual.localScale =
                new Vector3(
                    SafeInverse(parentScale.x),
                    SafeInverse(parentScale.y),
                    1f);

            SpriteRenderer renderer =
                visualObject.AddComponent<
                    SpriteRenderer>();

            renderer.sprite =
                platformSprite;

            renderer.drawMode =
                SpriteDrawMode.Tiled;

            renderer.tileMode =
                SpriteTileMode.Continuous;

            renderer.size =
                new Vector2(
                    collider.bounds.size.x,
                    platformSprite.bounds.size.y);

            renderer.sortingOrder =
                0;

            MoveRendererTopCenter(
                renderer,
                new Vector2(
                    collider.bounds.center.x,
                    collider.bounds.max.y));
        }
    }


    private void AddForegroundDecoration()
    {
        if (treeSprite == null)
            return;

        BoxCollider2D widestGround =
            null;

        foreach (BoxCollider2D collider in
                 GetComponentsInChildren<
                     BoxCollider2D>(
                     true))
        {
            if (!collider.enabled ||
                collider.isTrigger)
            {
                continue;
            }

            if (widestGround == null ||
                collider.bounds.size.x >
                widestGround.bounds.size.x)
            {
                widestGround =
                    collider;
            }
        }

        if (widestGround == null)
            return;

        GameObject treeObject =
            new("Cliff Tree");

        treeObject.transform.SetParent(
            transform,
            false);

        SpriteRenderer renderer =
            treeObject.AddComponent<
                SpriteRenderer>();

        renderer.sprite =
            treeSprite;

        renderer.sortingOrder =
            -1;

        treeObject.transform.localScale =
            Vector3.one *
            2.35f;

        Bounds groundBounds =
            widestGround.bounds;

        MoveRendererBottomCenter(
            renderer,
            new Vector2(
                groundBounds.center.x -
                groundBounds.extents.x *
                0.28f,
                groundBounds.max.y));
    }


    private static SpriteRenderer CreateRenderer(
        Transform parent,
        string objectName,
        Sprite sprite,
        int sortingOrder,
        Color color)
    {
        GameObject rendererObject =
            new(objectName);

        rendererObject.transform.SetParent(
            parent,
            false);

        SpriteRenderer renderer =
            rendererObject.AddComponent<
                SpriteRenderer>();

        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        renderer.color = color;

        return renderer;
    }


    private static void MoveRendererCenter(
        SpriteRenderer renderer,
        Vector2 targetCenter)
    {
        Vector3 offset =
            (Vector3)targetCenter -
            renderer.bounds.center;

        renderer.transform.position +=
            offset;
    }


    private static void MoveRendererBottomCenter(
        SpriteRenderer renderer,
        Vector2 target)
    {
        Bounds bounds =
            renderer.bounds;

        Vector3 bottomCenter =
            new(
                bounds.center.x,
                bounds.min.y,
                renderer.transform.position.z);

        renderer.transform.position +=
            (Vector3)target -
            bottomCenter;
    }


    private static void MoveRendererTopCenter(
        SpriteRenderer renderer,
        Vector2 target)
    {
        Bounds bounds =
            renderer.bounds;

        Vector3 topCenter =
            new(
                bounds.center.x,
                bounds.max.y,
                renderer.transform.position.z);

        renderer.transform.position +=
            (Vector3)target -
            topCenter;
    }


    private static float SafeInverse(
        float value)
    {
        return Mathf.Abs(value) > 0.0001f
            ? 1f / value
            : 1f;
    }
}
