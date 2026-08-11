using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public enum JumpType : byte
{
    Ground = 0,
    Air = 1,
    Wall = 2
}

[DefaultExecutionOrder(-100)]
public sealed class PlayerMovement :
    PlayerModule,
    IPlayerMovementState,
    IPlayerKnockbackReceiver
{
    [Header("References")]
    [SerializeField]
    private Rigidbody2D rb;

    [SerializeField]
    private NetworkRigidbody networkRigidbody;

    [SerializeField]
    private Transform groundCheck;

    [SerializeField]
    private Transform wallCheckLeft;

    [SerializeField]
    private Transform wallCheckRight;

    [SerializeField]
    private LayerMask groundLayer;


    [Header("Horizontal")]
    [SerializeField]
    private float maxMoveSpeed = 7f;

    [SerializeField]
    private float groundAcceleration = 45f;

    [SerializeField]
    private float groundDeceleration = 60f;

    [SerializeField]
    private float airAcceleration = 30f;

    [SerializeField]
    private float airDeceleration = 40f;


    [Header("Jump")]
    [SerializeField]
    private float jumpSpeed = 11f;

    [SerializeField]
    private byte maxAirJumps = 1;

    [SerializeField]
    private float groundCheckRadius = 0.15f;


    [Header("Fast Fall")]
    [SerializeField]
    private float fastFallSpeed = 14f;

    [SerializeField]
    private float fastFallAcceleration = 40f;

    [SerializeField]
    [Range(-1f, 0f)]
    private float fastFallInputThreshold = -0.5f;


    [Header("Wall")]
    [SerializeField]
    private Vector2 wallCheckSize =
        new Vector2(0.2f, 0.8f);

    [SerializeField]
    private float wallSlideSpeed = 3f;

    [SerializeField]
    private float wallJumpHorizontalSpeed = 6f;

    [SerializeField]
    private float wallJumpVerticalSpeed = 10f;

    [SerializeField]
    private float wallJumpControlDelay = 0.1f;

    [SerializeField]
    private float wallJumpReadyDelay = 0.08f;


    private IPlayerHealthState _healthState;

    private IPlayerCombatState _combatState;


    // =========================================================
    // Network State
    // =========================================================

    [Networked]
    private byte RemainingAirJumps { get; set; }

    [Networked]
    private NetworkBool WasGrounded { get; set; }

    [Networked]
    private NetworkButtons PreviousButtons { get; set; }

    [Networked]
    private TickTimer WallJumpControlTimer { get; set; }

    [Networked]
    private TickTimer WallJumpReadyTimer { get; set; }

    [Networked]
    private NetworkBool WasWallSliding { get; set; }

    [Networked]
    private TickTimer KnockbackControlTimer { get; set; }


    [Networked]
    public NetworkBool IsGrounded { get; private set; }

    [Networked]
    public NetworkBool FacingRight { get; private set; }

    [Networked]
    public NetworkBool IsWallSliding { get; private set; }

    [Networked]
    public byte JumpSequence { get; private set; }

    [Networked]
    public JumpType LastJumpType { get; private set; }


    // =========================================================
    // Runtime State
    // =========================================================

    public bool IsTouchingWallLeft { get; private set; }

    public bool IsTouchingWallRight { get; private set; }

    public bool IsTouchingWall =>
        IsTouchingWallLeft ||
        IsTouchingWallRight;

    public Vector2 Velocity =>
        rb.linearVelocity;


    bool IPlayerMovementState.IsGrounded =>
        IsGrounded;

    bool IPlayerMovementState.FacingRight =>
        FacingRight;

    bool IPlayerMovementState.IsWallSliding =>
        IsWallSliding;


    public bool IsControlLocked =>
        !KnockbackControlTimer
            .ExpiredOrNotRunning(Runner);


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
        if (networkRigidbody == null)
        {
            networkRigidbody = GetComponent<NetworkRigidbody>();
        }
    }


    // =========================================================
    // Context
    // =========================================================

    protected override void RegisterContextUnits()
    {
        Context.Register<
            IPlayerMovementState>(
            this);

        Context.Register<
            IPlayerKnockbackReceiver>(
            this);
    }


    protected override void OnContextReady()
    {
        _healthState =
            Context.Get<
                IPlayerHealthState>();

        _combatState =
            Context.Get<
                IPlayerCombatState>();
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {

    }

    public override void FixedUpdateNetwork()
    {
        UpdateGrounded();
        UpdateWallState();

        if (!GetInput(
                out PlayerInputData input))
        {
            return;
        }

        Vector2 moveInput =
            Vector2.ClampMagnitude(
                input.Move,
                1f);

        // ��� �߿��� ���� ��ư ���´� �Һ���
        // ������ ���� ���� �Է��� �� �Է�ó�� Ƣ����� �ʰ� �Ѵ�.
        if (_healthState == null ||
            !_healthState.IsAlive)
        {
            PreviousButtons =
                input.Buttons;

            ClearControlDrivenStates();

            return;
        }


        // ==========================================
        // Knockback Control Lock
        // ==========================================

        if (IsControlLocked)
        {
            PreviousButtons =
                input.Buttons;

            return;
        }


        // ==========================================
        // Attack Control Lock
        // ==========================================

        if (_combatState != null &&
            _combatState.IsAttacking)
        {
            PreviousButtons =
                input.Buttons;

            LockMovementForAttack();

            return;
        }

        MoveHorizontal(
            moveInput.x);

        HandleWallSlide(
            moveInput.x);

        HandleFastFall(
            moveInput.y);

        bool jumpPressed =
            input.Buttons.WasPressed(
                PreviousButtons,
                PlayerButton.Jump);

        PreviousButtons =
            input.Buttons;

        if (jumpPressed)
        {
            TryJump();
        }
    }


    // =========================================================
    // Attack Control Lock
    // =========================================================

    private void LockMovementForAttack()
    {
        ClearControlDrivenStates();

        Vector2 velocity =
            rb.linearVelocity;

        // ���� �߿��� �Է� ��� ���� �̵��� �����.
        // ���� �ӵ��� ������ ���߿����� �߷�/���ϰ� ��� ����ȴ�.
        velocity.x =
            0f;

        rb.linearVelocity =
            velocity;
    }


    // =========================================================
    // Horizontal
    // =========================================================

    private void MoveHorizontal(
        float inputX)
    {
        if (!WallJumpControlTimer
                .ExpiredOrNotRunning(Runner))
        {
            return;
        }

        inputX =
            Mathf.Clamp(
                inputX,
                -1f,
                1f);

        UpdateFacing(
            inputX);

        float targetSpeed =
            inputX *
            maxMoveSpeed;

        bool hasInput =
            Mathf.Abs(inputX) >
            0.01f;

        float acceleration;

        if (IsGrounded)
        {
            acceleration =
                hasInput
                    ? groundAcceleration
                    : groundDeceleration;
        }
        else
        {
            acceleration =
                hasInput
                    ? airAcceleration
                    : airDeceleration;
        }

        Vector2 velocity =
            rb.linearVelocity;

        velocity.x =
            Mathf.MoveTowards(
                velocity.x,
                targetSpeed,
                acceleration *
                Runner.DeltaTime);

        rb.linearVelocity =
            velocity;
    }


    private void UpdateFacing(
        float inputX)
    {
        if (inputX > 0.01f)
        {
            FacingRight = true;
        }
        else if (inputX < -0.01f)
        {
            FacingRight = false;
        }
    }


    // =========================================================
    // Fast Fall
    // =========================================================

    private void HandleFastFall(
        float inputY)
    {
        if (IsGrounded)
            return;

        if (IsWallSliding)
            return;

        if (inputY >
            fastFallInputThreshold)
        {
            return;
        }

        Vector2 velocity =
            rb.linearVelocity;

        if (velocity.y >= 0f)
            return;

        velocity.y =
            Mathf.MoveTowards(
                velocity.y,
                -fastFallSpeed,
                fastFallAcceleration *
                Runner.DeltaTime);

        rb.linearVelocity =
            velocity;
    }


    // =========================================================
    // Jump
    // =========================================================

    private void TryJump()
    {
        if (IsGrounded)
        {
            ApplyJump(
                jumpSpeed,
                JumpType.Ground);

            return;
        }

        if (IsWallSliding)
        {
            if (CanWallJump())
            {
                ApplyWallJump();
            }

            return;
        }

        if (RemainingAirJumps == 0)
            return;

        RemainingAirJumps--;

        ApplyJump(
            jumpSpeed,
            JumpType.Air);
    }


    private void ApplyJump(
        float verticalSpeed,
        JumpType jumpType)
    {
        Vector2 velocity =
            rb.linearVelocity;

        velocity.y =
            verticalSpeed;

        rb.linearVelocity =
            velocity;

        NotifyJump(
            jumpType);
    }


    private void ApplyWallJump()
    {
        float direction =
            IsTouchingWallLeft
                ? 1f
                : -1f;

        rb.linearVelocity =
            new Vector2(
                direction *
                wallJumpHorizontalSpeed,
                wallJumpVerticalSpeed);

        WallJumpControlTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                wallJumpControlDelay);

        IsWallSliding =
            false;

        WasWallSliding =
            false;

        WallJumpReadyTimer =
            TickTimer.None;

        NotifyJump(
            JumpType.Wall);
    }


    private void NotifyJump(
        JumpType jumpType)
    {
        LastJumpType =
            jumpType;

        JumpSequence++;
    }


    // =========================================================
    // Ground
    // =========================================================

    private void UpdateGrounded()
    {
        bool grounded =
            Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundLayer) != null;

        if (grounded &&
            !WasGrounded)
        {
            RemainingAirJumps =
                maxAirJumps;
        }

        IsGrounded =
            grounded;

        WasGrounded =
            grounded;
    }


    // =========================================================
    // Wall
    // =========================================================

    private void UpdateWallState()
    {
        IsTouchingWallLeft =
            Physics2D.OverlapBox(
                wallCheckLeft.position,
                wallCheckSize,
                0f,
                groundLayer) != null;

        IsTouchingWallRight =
            Physics2D.OverlapBox(
                wallCheckRight.position,
                wallCheckSize,
                0f,
                groundLayer) != null;
    }


    private void HandleWallSlide(
        float inputX)
    {
        bool wallSliding =
            CanWallSlide(
                inputX);

        if (wallSliding &&
            !WasWallSliding)
        {
            WallJumpReadyTimer =
                TickTimer.CreateFromSeconds(
                    Runner,
                    wallJumpReadyDelay);
        }

        if (!wallSliding)
        {
            WallJumpReadyTimer =
                TickTimer.None;
        }

        IsWallSliding =
            wallSliding;

        WasWallSliding =
            wallSliding;

        if (!IsWallSliding)
            return;

        // Presentation�� ���� Wall Check�� ���� �ʾƵ� �ǵ���
        // ���� Ÿ�� ���� ���� �ٶ󺸴� ������ Networked ���·� Ȯ���Ѵ�.
        if (IsTouchingWallLeft)
        {
            FacingRight =
                true;
        }
        else if (IsTouchingWallRight)
        {
            FacingRight =
                false;
        }

        Vector2 velocity =
            rb.linearVelocity;

        velocity.y =
            Mathf.Max(
                velocity.y,
                -wallSlideSpeed);

        rb.linearVelocity =
            velocity;
    }


    private bool IsPressingTowardWall(
        float inputX)
    {
        return
            (IsTouchingWallLeft &&
             inputX < -0.01f)
            ||
            (IsTouchingWallRight &&
             inputX > 0.01f);
    }


    private bool CanWallSlide(
        float inputX)
    {
        return
            !IsGrounded &&
            IsTouchingWall &&
            IsPressingTowardWall(
                inputX) &&
            rb.linearVelocity.y < 0f;
    }


    private bool CanWallJump()
    {
        return
            IsWallSliding &&
            WallJumpReadyTimer
                .Expired(Runner);
    }


    private void ClearControlDrivenStates()
    {
        IsWallSliding =
            false;

        WasWallSliding =
            false;

        WallJumpReadyTimer =
            TickTimer.None;
    }


    // =========================================================
    // External Movement Commands
    // =========================================================

    public void ApplyKnockback(
        Vector2 velocity,
        float controlLockDuration)
    {
        if (!HasStateAuthority)
            return;

        rb.linearVelocity =
            velocity;

        KnockbackControlTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                controlLockDuration);

        IsWallSliding =
            false;

        WasWallSliding =
            false;

        WallJumpReadyTimer =
            TickTimer.None;
    }


    // =========================================================
    // Respawn
    // =========================================================

    public void ResetForRespawn(
        Vector2 position)
    {
        if (!HasStateAuthority)
            return;

        rb.position =
            position;

        rb.linearVelocity =
            Vector2.zero;

        RemainingAirJumps =
            maxAirJumps;

        WasGrounded =
            false;

        PreviousButtons =
            default;

        WallJumpControlTimer =
            TickTimer.None;

        WallJumpReadyTimer =
            TickTimer.None;

        KnockbackControlTimer =
            TickTimer.None;

        WasWallSliding =
            false;

        IsWallSliding =
            false;

        IsGrounded =
            false;

        IsTouchingWallLeft =
            false;

        IsTouchingWallRight =
            false;
    }


#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.DrawWireSphere(
                groundCheck.position,
                groundCheckRadius);
        }

        if (wallCheckLeft != null)
        {
            Gizmos.DrawWireCube(
                wallCheckLeft.position,
                wallCheckSize);
        }

        if (wallCheckRight != null)
        {
            Gizmos.DrawWireCube(
                wallCheckRight.position,
                wallCheckSize);
        }
    }

#endif
}