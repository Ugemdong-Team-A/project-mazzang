using System.Collections.Generic;
using Fusion;
using UnityEngine;

public sealed class ShieldWeapon :
    Weapon,
    IParryVolume
{
    [Header("Shared Cooldown")]
    [Min(0f)]
    [SerializeField]
    private float sharedCooldown = 1.35f;

    [Header("Shield Bash")]
    [SerializeField]
    private AttackData bashAttack;

    [SerializeField]
    private Vector2 hitboxSize =
        new(1.25f, 1.5f);

    [Min(0f)]
    [SerializeField]
    private float hitboxForwardOffset = 0.58f;

    [SerializeField]
    private LayerMask hurtboxLayer;

    [Min(0f)]
    [SerializeField]
    private float dashSpeed = 7.5f;

    [Min(0f)]
    [SerializeField]
    private float dashControlLock = 0.1f;

    [Header("Shield Parry")]
    [Min(0.01f)]
    [SerializeField]
    private float parryDuration = 0.22f;

    [Min(0.1f)]
    [SerializeField]
    private float parryRadius = 1.15f;

    [Range(10f, 180f)]
    [SerializeField]
    private float parryArcAngle = 92f;

    [Range(0f, 1f)]
    [SerializeField]
    private float parryAimInfluence = 0.95f;

    [Min(0f)]
    [SerializeField]
    private float parrySpeedMultiplier = 1.25f;

    [SerializeField]
    private float parryForwardOffset = 0.52f;

    [Header("Presentation")]
    [Min(0f)]
    [SerializeField]
    private float bashEffectForwardOffset = 0.68f;

    [SerializeField]
    private CameraShakeProfile bashShakeProfile;

    [SerializeField]
    private CameraShakeProfile parrySuccessShakeProfile;

    [Networked]
    private TickTimer SharedCooldownTimer { get; set; }

    [Networked]
    private TickTimer ParryActiveTimer { get; set; }

    [Networked]
    private Vector2 ActionDirection { get; set; }

    [Networked]
    private Vector2 ActionOrigin { get; set; }

    [Networked]
    private Vector2 SuccessPoint { get; set; }

    [Networked]
    private byte BashSequence { get; set; }

    [Networked]
    private byte ParrySequence { get; set; }

    [Networked]
    private byte SuccessSequence { get; set; }

    private readonly HashSet<IDamageable> _hitTargets = new();
    private ShieldWeaponPresentation _presentation;
    private byte _visibleBashSequence;
    private byte _visibleParrySequence;
    private byte _visibleSuccessSequence;

    public override bool ConsumesParryInput => true;

    public bool IsParryActive =>
        IsEquipped &&
        !ParryActiveTimer.ExpiredOrNotRunning(Runner);

    public NetworkObject ParryOwner => Holder;

    public Vector2 ParryOrigin =>
        ResolveHolderAnchor() +
        ParryDirection * parryForwardOffset;

    public Vector2 ParryDirection =>
        NormalizeDirection(ActionDirection);

    public float ParryRadius => parryRadius;
    public float ParryHalfAngle => parryArcAngle * 0.5f;
    public float ParryAimInfluence => parryAimInfluence;
    public float ParrySpeedMultiplier => parrySpeedMultiplier;

    public override void Spawned()
    {
        base.Spawned();

        _visibleBashSequence = BashSequence;
        _visibleParrySequence = ParrySequence;
        _visibleSuccessSequence = SuccessSequence;

        ParryRegistry.Register(this);

        if (!HasStateAuthority)
            return;

        SharedCooldownTimer = TickTimer.None;
        ParryActiveTimer = TickTimer.None;
    }

    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        ParryRegistry.Unregister(this);

        if (_presentation != null)
        {
            Destroy(_presentation);
            _presentation = null;
        }

        base.Despawned(runner, hasState);
    }

    public override bool TryUse(
        Vector2 origin,
        Vector2 direction,
        bool mirrored,
        float attackDamageMultiplier)
    {
        if (!CanStartAction())
            return false;

        direction = NormalizeDirection(direction);
        ActionOrigin = origin;
        ActionDirection = direction;

        ApplyDash(direction);
        PerformBash(
            origin,
            direction,
            attackDamageMultiplier);
        StartSharedCooldown();
        BashSequence++;

        return true;
    }

    public override bool TryUseSecondary(
        Vector2 origin,
        Vector2 direction,
        bool mirrored)
    {
        if (!CanStartAction())
            return false;

        ActionOrigin = origin;
        ActionDirection = NormalizeDirection(direction);
        ParryActiveTimer = TickTimer.CreateFromSeconds(
            Runner,
            parryDuration);

        StartSharedCooldown();
        ParrySequence++;

        return true;
    }

    public void OnParrySuccess(Vector2 point)
    {
        if (!HasStateAuthority)
            return;

        SuccessPoint = point;
        SuccessSequence++;
    }

    public override void Render()
    {
        EnsurePresentation();

        float remaining =
            SharedCooldownTimer.RemainingTime(Runner) ?? 0f;

        float progress =
            sharedCooldown <= 0f
                ? 1f
                : 1f - Mathf.Clamp01(
                    remaining / sharedCooldown);

        bool localOwner =
            Holder != null &&
            Runner != null &&
            Holder.InputAuthority == Runner.LocalPlayer;

        _presentation.SetState(
            ResolveStableHolderPosition(),
            ParryOrigin,
            ParryDirection,
            parryRadius,
            ParryHalfAngle,
            IsParryActive,
            remaining > 0f,
            progress,
            localOwner);

        if (_visibleBashSequence != BashSequence)
        {
            _visibleBashSequence = BashSequence;
            _presentation.PlayBash(
                ActionOrigin +
                ActionDirection *
                bashEffectForwardOffset,
                ActionDirection);
            CameraShakeService.Play(
                bashShakeProfile,
                ActionOrigin);
        }

        if (_visibleParrySequence != ParrySequence)
        {
            _visibleParrySequence = ParrySequence;
            _presentation.PlayParryStart(
                ParryOrigin,
                ParryDirection);
        }

        if (_visibleSuccessSequence != SuccessSequence)
        {
            _visibleSuccessSequence = SuccessSequence;
            _presentation.PlaySuccess(SuccessPoint);
            CameraShakeService.Play(
                parrySuccessShakeProfile,
                SuccessPoint);
        }
    }

    private void EnsurePresentation()
    {
        if (_presentation != null)
            return;

        _presentation =
            gameObject.AddComponent<
                ShieldWeaponPresentation>();
    }

    private bool CanStartAction()
    {
        return HasStateAuthority &&
               IsEquipped &&
               Holder != null &&
               SharedCooldownTimer
                   .ExpiredOrNotRunning(Runner);
    }

    private void StartSharedCooldown()
    {
        SharedCooldownTimer =
            sharedCooldown > 0f
                ? TickTimer.CreateFromSeconds(
                    Runner,
                    sharedCooldown)
                : TickTimer.None;
    }

    private void ApplyDash(Vector2 direction)
    {
        if (Holder == null ||
            !Holder.TryGetComponent(
                out IPlayerTickCommandDispatcher playerTickCommander))
        {
            return;
        }

        Vector2 velocity = playerTickCommander.TickState.MovementVelocity;
        velocity.x = direction.x * dashSpeed;
        velocity.y = Mathf.Max(
            velocity.y,
            direction.y * dashSpeed * 0.45f);

        playerTickCommander.TickCommands.RequestSetMovementVelocity(
            velocity);

        playerTickCommander.TickCommands.RequestControlLock(
            PlayerControlLock.Movement |
            PlayerControlLock.Attack,
            dashControlLock);
    }

    private void PerformBash(
        Vector2 origin,
        Vector2 direction,
        float attackDamageMultiplier)
    {
        if (bashAttack == null)
            return;

        float angle = Mathf.Atan2(
            direction.y,
            direction.x) * Mathf.Rad2Deg;

        Vector2 center =
            origin +
            direction * hitboxForwardOffset;

        Collider2D[] hits =
            Physics2D.OverlapBoxAll(
                center,
                hitboxSize,
                angle,
                hurtboxLayer);

        _hitTargets.Clear();

        foreach (Collider2D hit in hits)
        {
            IDamageable damageable =
                hit.GetComponentInParent<IDamageable>();

            if (damageable == null ||
                !damageable.IsAlive ||
                !_hitTargets.Add(damageable))
            {
                continue;
            }

            NetworkObject target =
                hit.GetComponentInParent<NetworkObject>();

            if (target == Holder)
                continue;

            Vector2 knockback =
                direction * bashAttack.KnockbackForward +
                Vector2.up * bashAttack.KnockbackUp;

            DamageInfo info = new(
                bashAttack.Damage,
                attackDamageMultiplier,
                Holder,
                knockback,
                bashAttack.CrowdControl);

            CombatDamageService.ApplyDamage(
                damageable,
                in info);
        }
    }

    private Vector2 ResolveHolderAnchor()
    {
        if (Holder != null &&
            Holder.TryGetComponent(
                out PlayerWeaponController controller) &&
            controller.WeaponSocket != null)
        {
            return controller.WeaponSocket.position;
        }

        return ActionOrigin.sqrMagnitude > 0.0001f
            ? ActionOrigin
            : (Vector2)transform.position;
    }

    private Vector2 ResolveStableHolderPosition()
    {
        return Holder != null
            ? (Vector2)Holder.transform.position
            : (Vector2)transform.position;
    }

    private static Vector2 NormalizeDirection(
        Vector2 direction)
    {
        return direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.right;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector2 direction =
            Application.isPlaying
                ? ParryDirection
                : Vector2.right;

        Vector2 origin =
            Application.isPlaying
                ? ResolveHolderAnchor()
                : transform.position;

        float angle = Mathf.Atan2(
            direction.y,
            direction.x) * Mathf.Rad2Deg;

        Gizmos.color = Color.white;
        Gizmos.matrix = Matrix4x4.TRS(
            origin + direction * hitboxForwardOffset,
            Quaternion.Euler(0f, 0f, angle),
            Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, hitboxSize);
    }
#endif
}
