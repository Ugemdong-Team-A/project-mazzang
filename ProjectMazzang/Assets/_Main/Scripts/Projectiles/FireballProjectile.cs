using Fusion;
using UnityEngine;

public sealed class FireballProjectile :
    Projectile
{
    [Header("Impact Presentation")]
    [SerializeField]
    private CameraShakeProfile impactShakeProfile;

    [Min(0.05f)]
    [SerializeField]
    private float impactPresentationDuration = 0.15f;

    [Networked]
    private NetworkBool HasImpacted
    {
        get;
        set;
    }

    [Networked]
    private Vector2 LastImpactPosition
    {
        get;
        set;
    }

    [Networked]
    private int ImpactSequence
    {
        get;
        set;
    }

    [Networked]
    private TickTimer ImpactDespawnTimer
    {
        get;
        set;
    }

    private int _visibleImpactSequence;


    public override void Spawned()
    {
        base.Spawned();

        _visibleImpactSequence =
            ImpactSequence;
    }


    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority &&
            HasImpacted)
        {
            if (ImpactDespawnTimer.Expired(
                    Runner))
            {
                DespawnProjectile();
            }

            return;
        }

        base.FixedUpdateNetwork();
    }


    public override void Render()
    {
        base.Render();

        if (_visibleImpactSequence ==
            ImpactSequence)
        {
            return;
        }

        _visibleImpactSequence =
            ImpactSequence;

        CameraShakeService.Play(
            impactShakeProfile,
            LastImpactPosition);
    }


    protected override void OnImpact(
        RaycastHit2D hit)
    {
        TryApplyDamage(
            hit.collider);

        LastImpactPosition =
            transform.position;

        HasImpacted =
            true;

        ImpactSequence++;

        StopProjectile();

        ImpactDespawnTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                impactPresentationDuration);
    }
}
