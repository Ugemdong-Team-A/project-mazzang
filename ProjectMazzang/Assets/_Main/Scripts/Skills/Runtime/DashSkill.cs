using Fusion;
using UnityEngine;

public sealed class DashSkill :
    Skill,
    IChargeSkill,
    ICastTimeSkill,
    IDurationSkill,
    IRecoverySkill
{
    private CapsuleCollider2D
        _movementCollider;

    private DashSkillData DashData =>
        (DashSkillData)Data;


    // =========================================================
    // Skill Pattern
    // =========================================================

    public int MaxCharges =>
        DashData.MaxCharges;

    public float RechargeDuration =>
        DashData.RechargeDuration;

    public float CastDuration =>
        DashData.StartupDuration;

    public float Duration =>
        DashData.DashDuration;

    public float RecoveryDuration =>
        DashData.RecoveryDuration;


    // =========================================================
    // Initialize
    // =========================================================

    protected override void OnInitialized()
    {
        _movementCollider = Controller.GetComponent<CapsuleCollider2D>();

        if (_movementCollider == null)
        {
            Debug.LogError(
                "[DashSkill] Player Root에 " +
                "이동용 CapsuleCollider2D가 없습니다.");
        }
    }


    // =========================================================
    // Use
    // =========================================================

    public override bool CanUse(
        in SkillUseContext useContext)
    {
        if (!base.CanUse(
                in useContext))
        {
            return false;
        }

        PlayerTickState state =
            Controller.TickState;

        if (state == null ||
            !state.HasMovement ||
            !state.HasAim)
        {
            return false;
        }

        if (state.IsMovementControlLocked)
            return false;

        if (state.HasCombat &&
            state.IsAttacking)
        {
            return false;
        }

        return true;
    }


    public override void Activate(
        in SkillUseContext useContext)
    {
        Vector2 direction =
            ResolveDashDirection(
                useContext.AimWorldPosition);


        // Dash가 끝날 때까지
        // 시전 순간 방향을 Networked PlayerAim에 고정합니다.

        PlayerAimOverride aimOverride =
            new(
                PlayerAimTrackingMode
                    .LockedDirection,

                PlayerAimFacingMode
                    .Locked,

                PlayerAimRigMode
                    .Procedural);


        Controller.TickCommands.RequestAimOverride(
            in aimOverride,
            direction);



        float controlLockDuration =
            DashData.StartupDuration +
            DashData.DashDuration +
            DashData.RecoveryDuration;

        Controller.TickCommands.RequestControlLock(
            PlayerControlLock.Movement |
            PlayerControlLock.Attack |
            PlayerControlLock.Skill,
            controlLockDuration);

        RequestMovementVelocity(
            Vector2.zero);
    }


    private void RequestMovementVelocity(
        Vector2 velocity)
    {
        Controller.TickCommands.RequestSetMovementVelocity(
            velocity);
    }


    // =========================================================
    // Update
    // =========================================================

    public override void FixedUpdateNetwork()
    {
        SkillUsePhase phase =
            Controller.GetUsePhase(
                Slot);


        switch (phase)
        {
            case SkillUsePhase.None:
                break;


            // 벽력일섬 준비 상태.
            case SkillUsePhase.Cast:

                RequestMovementVelocity(
                    Vector2.zero);

                break;


            case SkillUsePhase.Active:

                UpdateDash();

                break;


            // Dash가 끝난 후 아주 짧은 정지.
            case SkillUsePhase.Recovery:

                RequestMovementVelocity(
                    Vector2.zero);

                break;
        }
    }


    private void UpdateDash()
    {
        Vector2 direction =
            ResolveLockedDashDirection();


        if (TryHitPlayer(
                direction))
        {
            // Dash 자체는 즉시 끝내고 Recovery로 넘어감
            RequestMovementVelocity(
                Vector2.zero);


            Controller.EndActiveEarly(
                Slot);

            return;
        }

        RequestMovementVelocity(
            direction *
            DashData.DashSpeed);
    }


    // =========================================================
    // Player Collision
    // =========================================================

    private bool TryHitPlayer(
        Vector2 dashDirection)
    {
        if (_movementCollider == null)
            return false;

        AttackData attack =
            DashData.CollisionAttack;

        if (attack == null)
            return false;


        Transform colliderTransform =
            _movementCollider.transform;


        Vector3 scale =
            colliderTransform.lossyScale;


        Vector2 capsuleSize =
            new(
                Mathf.Abs(
                    _movementCollider.size.x *
                    scale.x),

                Mathf.Abs(
                    _movementCollider.size.y *
                    scale.y));


        Vector2 center =
            colliderTransform.TransformPoint(
                _movementCollider.offset);


        float angle =
            colliderTransform.eulerAngles.z;


        // 이번 Simulation Tick에 이동할 거리를 미리 검사합니다.
        // 단순 현재 위치 Overlap보다 빠른 Dash에서 관통할 가능성이 낮습니다.
        float distance =
            DashData.DashSpeed *
            Controller.Runner.DeltaTime;


        RaycastHit2D[] hits =
            Physics2D.CapsuleCastAll(
                center,
                capsuleSize,
                _movementCollider.direction,
                angle,
                dashDirection,
                distance,
                DashData.PlayerHurtboxLayer);


        IDamageable target =
            null;

        float nearestDistance =
            float.MaxValue;


        foreach (RaycastHit2D hit
                 in hits)
        {
            Collider2D collider =
                hit.collider;

            if (collider == null)
                continue;


            IDamageable receiver =
                collider.GetComponentInParent<
                    IDamageable>();

            if (receiver == null)
                continue;


            NetworkObject receiverObject =
                collider.GetComponentInParent<
                    NetworkObject>();

            if (receiverObject ==
                Controller.Object)
            {
                continue;
            }


            if (!receiver.IsAlive)
                continue;


            if (hit.distance >=
                nearestDistance)
            {
                continue;
            }


            target =
                receiver;

            nearestDistance =
                hit.distance;
        }


        if (target == null)
            return false;


        ApplyDashCollisionDamage(
            target,
            attack,
            dashDirection);

        return true;
    }


    private void ApplyDashCollisionDamage(
        IDamageable target,
        AttackData attack,
        Vector2 direction)
    {
        Vector2 knockback =
            direction *
                attack.KnockbackForward +
            Vector2.up *
                attack.KnockbackUp;


        DamageInfo info =
            new(
                attack.Damage,
                Controller.Object,
                knockback,
                attack.CrowdControl);


        CombatDamageService.ApplyDamage(
            target,
            in info);
    }


    // =========================================================
    // Direction
    // =========================================================

    private Vector2 ResolveDashDirection(
        Vector2 aimWorldPosition)
    {
        Vector2 direction =
            Controller.TickState.ResolveAimDirectionTo(
                aimWorldPosition);

        if (direction.sqrMagnitude >
            0.0001f)
        {
            return direction.normalized;
        }


        return
            Controller.TickState.FacingRight
                ? Vector2.right
                : Vector2.left;
    }


    private Vector2
        ResolveLockedDashDirection()
    {
        if (Controller.TickState.AimDirection.sqrMagnitude >
            0.0001f)
        {
            return
                Controller.TickState.AimDirection
                    .normalized;
        }


        return
            Controller.TickState.FacingRight
                ? Vector2.right
                : Vector2.left;
    }


    // =========================================================
    // End
    // =========================================================

    public override void OnUseEnded()
    {
        Controller.TickCommands?
            .RequestClearAimOverride();
    }


    public override void Cancel()
    {
        Controller.TickCommands?
            .RequestClearAimOverride();
    }
}
