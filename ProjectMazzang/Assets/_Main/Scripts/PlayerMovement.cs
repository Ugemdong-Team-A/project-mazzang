using Fusion;
using UnityEngine;

public enum JumpType : byte
{
    Ground = 0,
    Air = 1,
    Wall = 2
}

public sealed class PlayerMovement : NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private Rigidbody2D rb;

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
    private float airAcceleration = 20f;

    [SerializeField]
    private float airDeceleration = 8f;


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
    private float wallCheckRadius = 0.15f;

    [SerializeField]
    private float wallSlideSpeed = 3f;

    [SerializeField]
    private float wallJumpHorizontalSpeed = 6f;

    [SerializeField]
    private float wallJumpVerticalSpeed = 10f;

    // 벽점프 후 잠깐 동안 수평 입력을 막아
    // 벽 반대쪽으로 확실히 튕겨나가게 한다.
    [SerializeField]
    private float wallJumpControlDelay = 0.1f;

    // WallSlide에 진입한 뒤
    // 이 시간이 지나야 벽점프를 허용한다.
    [SerializeField]
    private float wallJumpReadyDelay = 0.08f;


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

    public bool IsGrounded { get; private set; }

    public bool IsTouchingWallLeft { get; private set; }

    public bool IsTouchingWallRight { get; private set; }

    public bool IsTouchingWall =>
        IsTouchingWallLeft ||
        IsTouchingWallRight;

    public Vector2 Velocity =>
        rb.linearVelocity;


    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void FixedUpdateNetwork()
    {
        UpdateGrounded();
        UpdateWallState();

        if (!GetInput(out PlayerInputData input))
            return;

        Vector2 moveInput =
            Vector2.ClampMagnitude(
                input.Move,
                1f);

        MoveHorizontal(moveInput.x);

        HandleWallSlide(moveInput.x);

        HandleFastFall(moveInput.y);

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
    // Horizontal
    // =========================================================

    private void MoveHorizontal(float inputX)
    {
        // 벽 점프 직후에는 현재 입력보다
        // 벽점프의 초기 X 속도를 우선한다.
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

        UpdateFacing(inputX);

        float targetSpeed =
            inputX * maxMoveSpeed;

        bool hasInput =
            Mathf.Abs(inputX) > 0.01f;

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


    private void UpdateFacing(float inputX)
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

    private void HandleFastFall(float inputY)
    {
        if (IsGrounded)
            return;

        // WallSlide의 낙하 제한을
        // FastFall이 덮어쓰지 않도록 한다.
        if (IsWallSliding)
            return;

        if (inputY > fastFallInputThreshold)
            return;

        Vector2 velocity =
            rb.linearVelocity;

        // 아직 상승 중이면 Fast Fall을 시작하지 않는다.
        // 정점을 지나 실제 하강이 시작됐을 때만 적용.
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
        // 1. Ground Jump
        if (IsGrounded)
        {
            ApplyJump(
                jumpSpeed,
                JumpType.Ground);

            return;
        }

        // 2. Wall
        //
        // WallSliding 상태라면
        // WallJump 준비 시간이 끝나기 전에는
        // AirJump로 빠져나가지 않고 입력을 무시한다.
        if (IsWallSliding)
        {
            if (CanWallJump())
            {
                ApplyWallJump();
            }

            return;
        }

        // 3. Air Jump
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

        NotifyJump(jumpType);
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

        IsWallSliding = false;

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
            Physics2D.OverlapCircle(
                wallCheckLeft.position,
                wallCheckRadius,
                groundLayer) != null;

        IsTouchingWallRight =
            Physics2D.OverlapCircle(
                wallCheckRight.position,
                wallCheckRadius,
                groundLayer) != null;
    }


    private void HandleWallSlide(
        float inputX)
    {
        bool wallSliding =
            CanWallSlide(inputX);

        // WallSlide에 처음 진입한 Tick.
        if (wallSliding &&
            !WasWallSliding)
        {
            WallJumpReadyTimer =
                TickTimer.CreateFromSeconds(
                    Runner,
                    wallJumpReadyDelay);
        }

        // 완전히 WallSlide에서 빠져나갔다면
        // 다시 벽을 잡을 때 새로 시간을 재도록 초기화.
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
            IsPressingTowardWall(inputX) &&
            rb.linearVelocity.y < 0f;
    }


    private bool CanWallJump()
    {
        return
            IsWallSliding &&
            WallJumpReadyTimer
                .Expired(Runner);
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
            Gizmos.DrawWireSphere(
                wallCheckLeft.position,
                wallCheckRadius);
        }

        if (wallCheckRight != null)
        {
            Gizmos.DrawWireSphere(
                wallCheckRight.position,
                wallCheckRadius);
        }
    }

#endif
}