using Fusion;
using UnityEngine;

[DefaultExecutionOrder(-75)]
public sealed class PlayerParry :
    PlayerTickModule,
    IParryVolume
{
    [SerializeField] private ParryData data;
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private CameraShakeProfile successShakeProfile;

    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked] private TickTimer ActiveTimer { get; set; }
    [Networked] private TickTimer CooldownTimer { get; set; }
    [Networked] private Vector2 Direction { get; set; }
    [Networked] private Vector2 SuccessPoint { get; set; }
    [Networked] private byte SuccessSequence { get; set; }

    private byte _visibleSuccessSequence;
    private ParryPresentation _presentation;
    // private IPlayerWeaponState _weaponState;

    public bool IsParryActive =>
        data != null && !ActiveTimer.ExpiredOrNotRunning(Runner);

    public NetworkObject ParryOwner => Object;

    public Vector2 ParryOrigin
    {
        get
        {
            Vector2 anchor = weaponSocket != null
                ? weaponSocket.position
                : transform.position;
            return anchor + ParryDirection * data.AnchorForwardOffset;
        }
    }

    public Vector2 ParryDirection =>
        Direction.sqrMagnitude > 0.0001f
            ? Direction.normalized
            : (Vector2)transform.right;

    public float ParryRadius => data != null ? data.Radius : 0f;
    public float ParryHalfAngle => data != null ? data.HalfAngle : 0f;
    public float ParryAimInfluence => data != null ? data.AimInfluence : 0f;
    public float ParrySpeedMultiplier => data != null ? data.SpeedMultiplier : 1f;

    public override PlayerTickStage Stage => PlayerTickStage.DefenseIntent;

    public override void Spawned()
    {
        _visibleSuccessSequence = SuccessSequence;
        ParryRegistry.Register(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        ParryRegistry.Unregister(this);
        if (_presentation != null)
            Destroy(_presentation);
    }

    public override void Simulate(in PlayerTick tick)
    {
        if (data == null || !GetInput(out PlayerInputData input))
            return;

        bool pressed = input.Buttons.WasPressed(
            PreviousButtons,
            PlayerButton.Parry);
        PreviousButtons = input.Buttons;

        /*if ((_weaponState != null &&
             _weaponState.ConsumesParryInput) ||
            !pressed ||
            !CooldownTimer.ExpiredOrNotRunning(Runner) ||
            (tick.State.HasHealth && !tick.State.IsAlive))
        {
            return;
        }*/

        Direction = ClampDirectionToBody(
            tick.State.AimDirection,
            !tick.State.HasMovement || tick.State.FacingRight);

        ActiveTimer = TickTimer.CreateFromSeconds(
            Runner,
            data.ActiveDuration);
        CooldownTimer = TickTimer.CreateFromSeconds(
            Runner,
            data.Cooldown);
    }

    public void OnParrySuccess(Vector2 point)
    {
        if (!HasStateAuthority)
            return;

        SuccessPoint = point;
        SuccessSequence++;
    }

    public override void Present(in PlayerTickState tickState)
    {
        if (data == null)
            return;

        EnsurePresentation();

        float cooldownRemaining = CooldownTimer.RemainingTime(Runner) ?? 0f;
        float cooldownProgress = data.Cooldown <= 0f
            ? 1f
            : 1f - Mathf.Clamp01(cooldownRemaining / data.Cooldown);

        _presentation.SetState(
            ParryOrigin,
            ParryDirection,
            data.Radius,
            data.HalfAngle,
            IsParryActive,
            cooldownRemaining > 0f,
            cooldownProgress,
            HasInputAuthority);

        if (_visibleSuccessSequence == SuccessSequence)
            return;

        _visibleSuccessSequence = SuccessSequence;
        _presentation.PlaySuccess(SuccessPoint);
        CameraShakeService.Play(successShakeProfile, SuccessPoint);
    }

    private void EnsurePresentation()
    {
        if (_presentation != null)
            return;

        _presentation = gameObject.AddComponent<ParryPresentation>();
    }

    private static Vector2 ClampDirectionToBody(
        Vector2 direction,
        bool facingRight)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return facingRight ? Vector2.right : Vector2.left;

        Vector2 local = facingRight
            ? direction.normalized
            : new Vector2(-direction.x, direction.y).normalized;
        float angle = Mathf.Clamp(
            Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg,
            -80f,
            80f);
        Vector2 clamped = new(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad));
        if (!facingRight)
            clamped.x *= -1f;
        return clamped.normalized;
    }
}
