using Fusion;
using UnityEngine;

[DefaultExecutionOrder(-75)]
public sealed class PlayerParry :
    PlayerTickModule,
    IParryVolume
{
    [SerializeField] private ParryData data;

    [Header("Use Policy")]
    [Min(0f)]
    [SerializeField] private float cooldown = 1.1f;

    [Header("Origin")]
    [Tooltip(
        "플레이어 루트 기준 패링 중심입니다. " +
        "X 양수는 바라보는 쪽의 앞, X 음수는 뒤이며 " +
        "Y 양수는 위, Y 음수는 아래입니다. 조준 회전의 영향은 받지 않습니다.")]
    [SerializeField]
    private Vector2 centerOffset =
        new(0.45f, 0.75f);

    [Header("Presentation")]
    [SerializeField] private CameraShakeProfile successShakeProfile;

    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked] private TickTimer ActiveTimer { get; set; }
    [Networked] private TickTimer CooldownTimer { get; set; }
    [Networked] private Vector2 Direction { get; set; }
    [Networked] private NetworkBool FacingRight { get; set; }
    [Networked] private Vector2 SuccessPoint { get; set; }
    [Networked] private byte SuccessSequence { get; set; }

    private byte _visibleSuccessSequence;
    private ParryPresentation _presentation;
    private IWeaponHandler _weaponHandler;
    // private IPlayerWeaponState _weaponState;

    public bool IsParryActive =>
        data != null && !ActiveTimer.ExpiredOrNotRunning(Runner);

    public NetworkObject ParryOwner => Object;

    public Vector2 ParryOrigin
    {
        get
        {
            return ParryGeometry.ResolveOrigin(
                transform.position,
                FacingRight,
                centerOffset);
        }
    }

    public Vector2 ParryDirection =>
        Direction.sqrMagnitude > 0.0001f
            ? Direction.normalized
            : (Vector2)transform.right;

    public float ParryRadius => data != null ? data.Radius : 0f;
    public float ParryHalfAngle => data != null ? data.HalfAngle : 0f;
    public float ParryAimInfluence => data != null ? data.AimInfluence : 0f;
    public ParryProjectileModifiers ProjectileModifiers =>
        data != null
            ? data.ProjectileModifiers
            : ParryProjectileModifiers.Identity;

    public override PlayerTickStage Stage => PlayerTickStage.DefenseIntent;

    public override void Spawned()
    {
        _weaponHandler =
            GetComponent<IWeaponHandler>();

        _visibleSuccessSequence = SuccessSequence;
        ParryRegistry.Register(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        ParryRegistry.Unregister(this);
        if (_presentation != null)
            Destroy(_presentation.gameObject);
    }

    public override void Simulate(in PlayerTick tick)
    {
        if (data == null || !GetInput(out PlayerInputData input))
            return;

        bool pressed = input.Buttons.WasPressed(
            PreviousButtons,
            PlayerButton.Parry);
        PreviousButtons = input.Buttons;

        bool isAlive =
            !tick.State.HasHealth ||
            tick.State.IsAlive;

        if (IsParryActive && isAlive)
        {
            UpdateTrackedDirection(
                in input,
                tick.State);
        }
        
        if (/*(_weaponState != null &&
             _weaponState.ConsumesParryInput) ||*/
            tick.State.HasEquippedWeapon ||
            !pressed ||
            !CooldownTimer.ExpiredOrNotRunning(Runner) ||
            !isAlive)
        {
            return;
        }

        UpdateTrackedDirection(
            in input,
            tick.State);

        ActiveTimer = TickTimer.CreateFromSeconds(
            Runner,
            data.ActiveDuration);
        CooldownTimer = TickTimer.CreateFromSeconds(
            Runner,
            cooldown);
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
        float cooldownProgress = cooldown <= 0f
            ? 1f
            : 1f - Mathf.Clamp01(cooldownRemaining / cooldown);

        _presentation.SetState(
            ResolvePresentationRoot(),
            ParryDirection,
            FacingRight,
            centerOffset,
            ParryRadius,
            ParryHalfAngle,
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

        Transform parent =
            ResolvePresentationRoot();

        GameObject presentationObject =
            new("Parry Presentation");

        presentationObject.transform.SetParent(
            parent != null
                ? parent
                : transform,
            false);

        _presentation =
            presentationObject.AddComponent<
                ParryPresentation>();
    }

    private Transform ResolvePresentationRoot()
    {
        Transform presentationRoot =
            _weaponHandler?.PresentationRoot;

        return presentationRoot != null
            ? presentationRoot
            : transform;
    }

    private void UpdateTrackedDirection(
        in PlayerInputData input,
        PlayerTickState state)
    {
        bool facingRight =
            !state.HasMovement ||
            state.FacingRight;

        Vector2 direction =
            state.ResolveAimDirectionTo(
                input.AimWorldPosition);

        direction =
            state.ResolveLimitedAimDirection(
                direction);

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = facingRight
                ? Vector2.right
                : Vector2.left;
        }

        Direction = direction.normalized;
        FacingRight = facingRight;
    }
}
