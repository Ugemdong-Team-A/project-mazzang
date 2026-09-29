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


    private void Awake()
    {
        Initialize();

        if (Application.isPlaying)
        {
            Hide();
        }
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


}
