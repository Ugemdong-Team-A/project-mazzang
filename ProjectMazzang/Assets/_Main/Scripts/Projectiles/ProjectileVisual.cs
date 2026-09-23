using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ProjectileVisual : MonoBehaviour
{
    [SerializeField]
    private Transform visualRoot;

    [SerializeField]
    private SpriteRenderer[] spriteRenderers;

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private ParticleSystem[] particleSystems;

    [SerializeField]
    private ProjectileTrail projectileTrail;

    private Vector3 _baseScale;
    private Color[] _baseSpriteColors;
    private bool[] _baseSpriteEnabled;
    private float _baseAnimatorSpeed = 1f;
    private bool _initialized;


    public static ProjectileVisual
        CreatePredictionCopy(
            GameObject sourceRoot,
            GameObject targetRoot)
    {
        if (sourceRoot == null)
        {
            throw new System.ArgumentNullException(
                nameof(sourceRoot));
        }

        if (targetRoot == null)
        {
            throw new System.ArgumentNullException(
                nameof(targetRoot));
        }

        targetRoot.layer =
            sourceRoot.layer;

        targetRoot.transform.localScale =
            sourceRoot.transform.localScale;

        ProjectileVisual sourceVisual =
            sourceRoot.GetComponent<
                ProjectileVisual>();

        if (sourceVisual != null &&
            sourceVisual.visualRoot != null &&
            sourceVisual.visualRoot !=
            sourceRoot.transform)
        {
            GameObject clonedVisual =
                Instantiate(
                    sourceVisual.visualRoot.gameObject,
                    targetRoot.transform,
                    false);

            ProjectileVisual clonedController =
                clonedVisual.GetComponent<
                    ProjectileVisual>();

            if (clonedController == null)
            {
                clonedController =
                    clonedVisual.AddComponent<
                        ProjectileVisual>();
            }

            clonedController.Initialize();

            return clonedController;
        }

        Dictionary<Transform, Transform>
            transformCopies =
                new();

        transformCopies.Add(
            sourceRoot.transform,
            targetRoot.transform);

        SpriteRenderer[] sources =
            sourceRoot.GetComponentsInChildren<
                SpriteRenderer>(
                    true);

        for (int i = 0;
             i < sources.Length;
             i++)
        {
            SpriteRenderer source =
                sources[i];

            Transform targetTransform =
                GetOrCreatePredictionTransform(
                    sourceRoot.transform,
                    targetRoot.transform,
                    source.transform,
                    transformCopies);

            SpriteRenderer target =
                targetTransform.gameObject
                    .AddComponent<
                        SpriteRenderer>();

            CopySpriteRenderer(
                source,
                target);
        }

        ProjectileTrail sourceTrail =
            sourceRoot.GetComponentInChildren<
                ProjectileTrail>(
                    true);

        if (sourceTrail != null)
        {
            Transform targetTransform =
                GetOrCreatePredictionTransform(
                    sourceRoot.transform,
                    targetRoot.transform,
                    sourceTrail.transform,
                    transformCopies);

            if (!targetTransform.TryGetComponent(
                    out TrailRenderer _))
            {
                targetTransform.gameObject
                    .AddComponent<TrailRenderer>();
            }

            ProjectileTrail targetTrail =
                targetTransform.gameObject
                    .AddComponent<
                        ProjectileTrail>();

            targetTrail.CopySettingsFrom(
                sourceTrail);
        }

        ProjectileVisual result =
            targetRoot.AddComponent<
                ProjectileVisual>();

        result.Initialize();

        return result;
    }


    private void Awake()
    {
        Initialize();
    }


    public void Initialize()
    {
        if (_initialized)
            return;

        ResolveReferences();

        _baseScale =
            visualRoot.localScale;

        _baseSpriteColors =
            new Color[spriteRenderers.Length];

        _baseSpriteEnabled =
            new bool[spriteRenderers.Length];

        for (int i = 0;
             i < spriteRenderers.Length;
             i++)
        {
            SpriteRenderer renderer =
                spriteRenderers[i];

            if (renderer == null)
                continue;

            _baseSpriteColors[i] =
                renderer.color;

            _baseSpriteEnabled[i] =
                renderer.enabled;
        }

        if (animator != null)
        {
            _baseAnimatorSpeed =
                animator.speed;
        }


        _initialized = true;
    }


    public void Apply(
        in ProjectileStatSnapshot stats)
    {
        Initialize();

        ResetVisual();

        visualRoot.localScale =
            _baseScale *
            stats.ScaleMultiplier;

        if (projectileTrail != null)
        {
            projectileTrail
                .SetWidthMultiplier(
                    stats.ScaleMultiplier);
        }
    }


    public void Show(
        Vector3 presentationOrigin,
        Transform followTarget)
    {
        Initialize();

        for (int i = 0;
             i < spriteRenderers.Length;
             i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].enabled =
                    _baseSpriteEnabled[i];
            }
        }

        if (animator != null)
        {
            animator.enabled =
                true;
        }

        for (int i = 0;
             i < particleSystems.Length;
             i++)
        {
            ParticleSystem particles =
                particleSystems[i];

            if (particles != null &&
                !particles.isPlaying)
            {
                particles.Play(
                    true);
            }
        }

        if (projectileTrail != null)
        {
            projectileTrail.Begin(
                presentationOrigin,
                followTarget);
        }
    }


    public void Hide()
    {
        Initialize();

        for (int i = 0;
             i < spriteRenderers.Length;
             i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].enabled =
                    false;
            }
        }

        if (animator != null)
        {
            animator.enabled =
                false;
        }

        for (int i = 0;
             i < particleSystems.Length;
             i++)
        {
            ParticleSystem particles =
                particleSystems[i];

            if (particles != null)
            {
                particles.Stop(
                    true,
                    ParticleSystemStopBehavior
                        .StopEmittingAndClear);
            }
        }

        if (projectileTrail != null)
        {
            projectileTrail.Complete();
        }
    }


    public void Complete()
    {
        if (projectileTrail != null)
        {
            projectileTrail.Complete();
        }
    }


    public void ResetVisual()
    {
        if (!_initialized)
            return;

        visualRoot.localScale =
            _baseScale;

        for (int i = 0;
             i < spriteRenderers.Length;
             i++)
        {
            SpriteRenderer renderer =
                spriteRenderers[i];

            if (renderer == null)
                continue;

            renderer.color =
                _baseSpriteColors[i];

            renderer.enabled =
                _baseSpriteEnabled[i];
        }

        if (animator != null)
        {
            animator.speed =
                _baseAnimatorSpeed;
        }

        if (projectileTrail != null)
        {
            projectileTrail
                .SetWidthMultiplier(
                    1f);
        }
    }


    private void ResolveReferences()
    {
        if (visualRoot == null)
        {
            visualRoot =
                transform;
        }

        if (spriteRenderers == null ||
            spriteRenderers.Length == 0)
        {
            spriteRenderers =
                visualRoot.GetComponentsInChildren<
                    SpriteRenderer>(
                        true);
        }

        if (particleSystems == null ||
            particleSystems.Length == 0)
        {
            particleSystems =
                visualRoot.GetComponentsInChildren<
                    ParticleSystem>(
                        true);
        }

        if (animator == null)
        {
            animator =
                visualRoot.GetComponentInChildren<
                    Animator>(
                        true);
        }

        if (projectileTrail == null)
        {
            projectileTrail =
                visualRoot.GetComponentInChildren<
                    ProjectileTrail>(
                        true);
        }
    }


    private static Transform
        GetOrCreatePredictionTransform(
            Transform sourceRoot,
            Transform targetRoot,
            Transform source,
            Dictionary<Transform, Transform> copies)
    {
        if (copies.TryGetValue(
                source,
                out Transform existing))
        {
            return existing;
        }

        Transform targetParent =
            GetOrCreatePredictionTransform(
                sourceRoot,
                targetRoot,
                source.parent,
                copies);

        GameObject targetObject =
            new GameObject(
                source.name);

        targetObject.layer =
            source.gameObject.layer;

        Transform target =
            targetObject.transform;

        target.SetParent(
            targetParent,
            false);

        target.localPosition =
            source.localPosition;

        target.localRotation =
            source.localRotation;

        target.localScale =
            source.localScale;

        targetObject.SetActive(
            source.gameObject.activeSelf);

        copies.Add(
            source,
            target);

        return target;
    }


    private static void CopySpriteRenderer(
        SpriteRenderer source,
        SpriteRenderer target)
    {
        target.sprite =
            source.sprite;

        target.sharedMaterials =
            source.sharedMaterials;

        target.color =
            source.color;

        target.flipX =
            source.flipX;

        target.flipY =
            source.flipY;

        target.drawMode =
            source.drawMode;

        target.size =
            source.size;

        target.maskInteraction =
            source.maskInteraction;

        target.spriteSortPoint =
            source.spriteSortPoint;

        target.sortingLayerID =
            source.sortingLayerID;

        target.sortingOrder =
            source.sortingOrder;

        target.enabled =
            source.enabled;
    }
}
