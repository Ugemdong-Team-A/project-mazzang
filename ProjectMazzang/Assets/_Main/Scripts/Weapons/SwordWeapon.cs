using System.Collections.Generic;
using Fusion;
using UnityEngine;

public sealed class SwordWeapon :
    Weapon
{
    [Header("Primary Attack")]
    [SerializeField]
    private WeaponAttackSlotData defaultAttack =
        new();

    [SerializeField]
    private WeaponAttackSlotData noDirectionAttack =
        new(false);

    [SerializeField]
    private WeaponAttackSlotData sideAttack =
        new(false);

    [SerializeField]
    private WeaponAttackSlotData downAttack =
        new(false);

    [Header("Target")]
    [SerializeField]
    private LayerMask hurtboxLayer;

    [Networked]
    private TickTimer HitDelayTimer
    {
        get;
        set;
    }

    [Networked]
    private TickTimer CooldownTimer
    {
        get;
        set;
    }

    [Networked]
    private Vector2 AttackDirection
    {
        get;
        set;
    }

    [Networked]
    private WeaponAttackSlot ActiveAttackSlot
    {
        get;
        set;
    }

    [Networked]
    private float AttackDamageMultiplier
    {
        get;
        set;
    }

    private readonly HashSet<IDamageable>
        _hitTargets = new();

    public override void Spawned()
    {
        base.Spawned();

        if (!HasStateAuthority)
            return;

        HitDelayTimer = TickTimer.None;
        CooldownTimer = TickTimer.None;
        AttackDirection = Vector2.right;
        ActiveAttackSlot =
            WeaponAttackSlot.Default;
        AttackDamageMultiplier = 1f;
    }

    public override WeaponActionData GetAction(
        WeaponButton button,
        WeaponAttackSlot slot)
    {
        if (button != WeaponButton.Primary)
            return null;

        return GetAttack(slot);
    }

    public override float GetActionDuration(
        WeaponButton button,
        WeaponAttackSlot slot)
    {
        return button == WeaponButton.Primary
            ? GetAttack(slot)?.Duration ?? 0f
            : 0f;
    }

    public override bool TryUse(
        Vector2 origin,
        Vector2 direction,
        WeaponAttackSlot slot,
        bool mirrored,
        float attackDamageMultiplier)
    {
        WeaponAttackSlotData action =
            GetAttack(slot);

        if (!CanAttack(action))
            return false;

        AttackDirection =
            NormalizeDirection(direction);
        ActiveAttackSlot = slot;
        AttackDamageMultiplier =
            attackDamageMultiplier;

        StartCooldown(action.Cooldown);

        if (action.HitDelay <= 0f)
        {
            ExecuteAttack(action);
        }
        else
        {
            HitDelayTimer =
                TickTimer.CreateFromSeconds(
                    Runner,
                    action.HitDelay);
        }

        return true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority ||
            !HitDelayTimer.IsRunning ||
            !HitDelayTimer.Expired(Runner))
        {
            return;
        }

        HitDelayTimer = TickTimer.None;

        WeaponAttackSlotData action =
            GetAttack(
                ActiveAttackSlot);

        if (action == null ||
            !action.Enabled)
        {
            return;
        }

        ExecuteAttack(action);
    }

    private WeaponAttackSlotData GetAttack(
        WeaponAttackSlot slot)
    {
        return slot switch
        {
            WeaponAttackSlot.NoDirection =>
                noDirectionAttack,
            WeaponAttackSlot.Side =>
                sideAttack,
            WeaponAttackSlot.Down =>
                downAttack,
            _ => defaultAttack
        };
    }

    private bool CanAttack(
        WeaponAttackSlotData action)
    {
        return action != null &&
               action.Enabled &&
               HasStateAuthority &&
               IsEquipped &&
               Holder != null &&
               CooldownTimer
                   .ExpiredOrNotRunning(
                       Runner);
    }

    private void StartCooldown(
        float cooldown)
    {
        CooldownTimer =
            cooldown > 0f
                ? TickTimer.CreateFromSeconds(
                    Runner,
                    cooldown)
                : TickTimer.None;
    }

    private void ExecuteAttack(
        WeaponAttackSlotData action)
    {
        if (Holder == null ||
            !Holder.TryGetComponent(
                out IWeaponHandler handler))
        {
            return;
        }

        Vector2 origin =
            handler.WeaponSocket != null
                ? handler.WeaponSocket.position
                : (Vector2)transform.position;

        Vector2 direction =
            NormalizeDirection(
                AttackDirection);

        ApplyDash(
            action.Dash,
            direction);

        PerformAttack(
            action,
            origin,
            direction);
    }

    private void ApplyDash(
        DashData dash,
        Vector2 direction)
    {
        if (dash == null ||
            dash.Speed <= 0f ||
            Holder == null ||
            !Holder.TryGetComponent(
                out IPlayerTickCommandDispatcher dispatcher))
        {
            return;
        }

        dispatcher.TickCommands
            .RequestSetMovementVelocity(
                direction *
                dash.Speed);

        dispatcher.TickCommands
            .RequestControlLock(
                PlayerControlLock.Movement |
                PlayerControlLock.Attack,
                dash.Duration);
    }

    private void PerformAttack(
        WeaponAttackSlotData action,
        Vector2 origin,
        Vector2 direction)
    {
        if (action.Attack == null)
            return;

        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;

        Vector2 center =
            origin +
            direction *
            action.HitboxForwardOffset;

        Collider2D[] hits =
            Physics2D.OverlapBoxAll(
                center,
                action.HitboxSize,
                angle,
                hurtboxLayer);

        _hitTargets.Clear();

        foreach (Collider2D hit in hits)
        {
            IDamageable damageable =
                hit.GetComponentInParent<
                    IDamageable>();

            if (damageable == null ||
                !damageable.IsAlive ||
                !_hitTargets.Add(
                    damageable))
            {
                continue;
            }

            NetworkObject target =
                hit.GetComponentInParent<
                    NetworkObject>();

            if (target == Holder)
                continue;

            Vector2 knockback =
                direction *
                    action.Attack.KnockbackForward +
                Vector2.up *
                    action.Attack.KnockbackUp;

            DamageInfo info =
                new(
                    action.Attack.Damage,
                    AttackDamageMultiplier,
                    Holder,
                    knockback,
                    action.Attack.CrowdControl);

            CombatDamageService.ApplyDamage(
                damageable,
                in info);
        }
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
        WeaponAttackSlotData action =
            defaultAttack;

        if (action == null)
            return;

        Vector2 center =
            (Vector2)transform.position +
            Vector2.right *
            action.HitboxForwardOffset;

        Gizmos.color = Color.white;
        Gizmos.matrix =
            Matrix4x4.TRS(
                center,
                Quaternion.identity,
                Vector3.one);
        Gizmos.DrawWireCube(
            Vector3.zero,
            action.HitboxSize);
    }

#endif
}
