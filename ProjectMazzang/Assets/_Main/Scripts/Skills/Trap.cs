using Fusion;
using UnityEngine;

public class Trap : Deployable
{
    [Header("Detection")]
    [SerializeField]
    private Collider2D triggerCollider;

    [SerializeField]
    private LayerMask targetLayers;


    [Header("Effect")]
    [SerializeField]
    private AttackData attack;


    [Header("Lifetime")]
    [Tooltip("켜면 발동 직후 사라집니다. 끄면 AttackData의 CC 지연과 지속 시간이 끝난 뒤 사라집니다.")]
    [SerializeField]
    private bool despawnImmediatelyOnTrigger;


    [Header("Presentation")]
    [SerializeField]
    private Animator animator;


    private static readonly int TriggeredState =
        Animator.StringToHash(
            "HunterTrap_Triggered");


    [Networked,
     OnChangedRender(nameof(OnTriggeredChanged))]
    public NetworkBool HasTriggered { get; private set; }

    [Networked]
    private TickTimer TriggeredDespawnTimer { get; set; }


    public override void Spawned()
    {
        RefreshTriggerCollider();

        if (HasTriggered)
        {
            PlayTriggeredAnimation();
        }
    }


    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (!HasTriggered)
        {
            base.FixedUpdateNetwork();
            return;
        }

        if (!TriggeredDespawnTimer
                .ExpiredOrNotRunning(Runner))
        {
            return;
        }

        Runner.Despawn(Object);
    }


    private void OnTriggerEnter2D(
        Collider2D other)
    {
        if (!HasStateAuthority ||
            HasTriggered ||
            attack == null)
        {
            return;
        }

        if ((targetLayers.value &
             1 << other.gameObject.layer) == 0)
        {
            return;
        }

        IDamageable damageable =
            other.GetComponentInParent<IDamageable>();

        if (damageable == null ||
            !damageable.IsAlive)
        {
            return;
        }

        Component targetComponent =
            damageable as Component;

        NetworkObject targetObject =
            targetComponent != null
                ? targetComponent
                    .GetComponentInParent<NetworkObject>()
                : null;

        if (targetObject == null ||
            targetObject == Owner)
        {
            return;
        }

        NetworkObject source =
            Owner != null
                ? Owner
                : Object;

        Vector2 toTarget =
            targetComponent.transform.position -
            transform.position;

        float direction =
            Mathf.Approximately(
                toTarget.x,
                0f)
                ? 1f
                : Mathf.Sign(toTarget.x);

        Vector2 knockback =
            Vector2.right *
            direction *
            attack.KnockbackForward +
            Vector2.up *
            attack.KnockbackUp;

        DamageResult result =
            damageable.ApplyDamage(
                new DamageInfo(
                    attack.Damage,
                    source,
                    knockback,
                    attack.CrowdControl));

        if (!result.WasProcessed)
            return;

        Trigger();
    }


    private void Trigger()
    {
        HasTriggered = true;
        RefreshTriggerCollider();

        float effectLifetime =
            attack.CrowdControl.ActivationDelay +
            attack.CrowdControl.Duration;

        if (despawnImmediatelyOnTrigger ||
            effectLifetime <= 0f)
        {
            Runner.Despawn(Object);
            return;
        }

        TriggeredDespawnTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                effectLifetime);
    }


    private void OnTriggeredChanged()
    {
        RefreshTriggerCollider();

        if (!HasTriggered ||
            animator == null)
        {
            return;
        }

        PlayTriggeredAnimation();
    }


    private void PlayTriggeredAnimation()
    {
        if (animator == null)
            return;

        animator.Play(
            TriggeredState,
            0,
            0f);
    }


    private void RefreshTriggerCollider()
    {
        if (triggerCollider != null)
        {
            triggerCollider.enabled =
                !HasTriggered;
        }
    }
}
