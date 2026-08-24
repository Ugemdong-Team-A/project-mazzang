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
    PlayerTickModule,
    IPlayerTickStateSource,
    IPlayerTickCommandSink
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


    [Header("Wall Debug")]
    [SerializeField]
    private bool enableWallDebug;

    [SerializeField]
    private bool verboseWallDebug;

    private PlayerSkillController _skillController;

    private bool _debugPreviousTouchingWallLeft;
    private bool _debugPreviousTouchingWallRight;
    private bool _debugPreviousWallSliding;
    private string _debugPreviousBlockReason;


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
    private TickTimer MovementControlLockTimer { get; set; }

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

    public bool IsMovementControlLocked =>
        !MovementControlLockTimer
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
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        _skillController =
            GetComponent<PlayerSkillController>();

        if (!HasStateAuthority)
            return;

        MovementControlLockTimer =
            TickTimer.None;
    }

    public override PlayerTickStage Stage =>
        PlayerTickStage.Motion;


    public override void Simulate(
        in PlayerTick tick)
    {
        TickMotion(
            tick.State.HasHealth &&
            tick.State.IsAlive,
            tick.State.HasCombat &&
            tick.State.IsCombatMovementLocked);
    }


    void IPlayerTickStateSource.CaptureTickState(
        PlayerTickState state)
    {
        state.HasMovement = true;
        state.IsGrounded = IsGrounded;
        state.FacingRight = FacingRight;
        state.IsWallSliding = IsWallSliding;
        state.IsMovementControlLocked =
            IsMovementControlLocked;
        state.MovementVelocity = Velocity;
        state.JumpSequence = JumpSequence;
        state.LastJumpType = LastJumpType;
    }


    bool IPlayerTickCommandSink.ResolveTickCommands(
        PlayerTickCommands commands,
        PlayerTickState state)
    {
        bool resolved = false;

        if (commands.TryConsumeMovementControlLock(
                out float controlLockDuration))
        {
            LockMovementControl(
                controlLockDuration);

            resolved = true;
        }

        if (commands.TryConsumeSetMovementVelocity(
                out Vector2 movementVelocity))
        {
            SetVelocity(
                movementVelocity);

            resolved = true;
        }

        if (commands.TryConsumeKnockback(
                out Vector2 velocity))
        {
            ApplyKnockback(
                velocity);

            resolved = true;
        }

        if (commands.TryConsumeFacing(
                out bool facingRight))
        {
            SetFacing(
                facingRight);

            resolved = true;
        }

        return resolved;
    }


    private void TickMotion(
        bool isAlive,
        bool isCombatMovementLocked)
    {
        /*Debug.Log("[Movement] isAlive: " + isAlive + "" +
            " isCombatMovementLocked: " + isCombatMovementLocked);*/
       

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

        if (!isAlive)
        {
            MovementControlLockTimer =
                TickTimer.None;

            PreviousButtons =
                input.Buttons;

            ClearControlDrivenStates();

            return;
        }


        // ==========================================
        // Control Lock
        // ==========================================

        if (IsMovementControlLocked)
        {
            PreviousButtons =
                input.Buttons;

            return;
        }


        // ==========================================
        // Attack Control Lock
        // ==========================================

        if (isCombatMovementLocked)
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

        DebugWallState(
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

        // 공격 중에는 입력으로 인한 수평 이동을 잠근다.
        // 기존 수직 속도는 유지해 공중에서도 중력과 낙하는 계속 적용된다.
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

        float targetSpeed =
            inputX *
            maxMoveSpeed *
            ResolveMoveSpeedMultiplier();

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


    private float ResolveMoveSpeedMultiplier()
    {
        return _skillController != null
            ? _skillController
                .GetActiveStatModifiers()
                .MoveSpeed
            : 1f;
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

            if (RemainingAirJumps <= maxAirJumps)
                RemainingAirJumps = maxAirJumps;
        }

        if (!wallSliding)
        {
            WallJumpReadyTimer =
                TickTimer.None;
        }       

        WasWallSliding =
            IsWallSliding;

        IsWallSliding =
            wallSliding;

        if (!IsWallSliding)
            return;

        // Presentation은 별도의 Wall Check를 하지 않아도 되도록
        // 현재 닿은 벽을 바라보는 방향을 Networked 상태로 확정한다.
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

        // 이거 Max일수도
        velocity.y =
            Mathf.Min(
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
            rb.linearVelocity.y <= 0f;
    }


    private bool CanWallJump()
    {
        return
            IsWallSliding &&
            WallJumpReadyTimer
                .Expired(Runner);
    }


    // =========================================================
    // Wall Debug
    // =========================================================

    private void DebugWallState(
        float inputX)
    {
        if (!enableWallDebug)
            return;

        bool touchingLeftChanged =
            _debugPreviousTouchingWallLeft !=
            IsTouchingWallLeft;

        bool touchingRightChanged =
            _debugPreviousTouchingWallRight !=
            IsTouchingWallRight;

        bool slidingChanged =
            _debugPreviousWallSliding !=
            IsWallSliding;

        if (touchingLeftChanged ||
            touchingRightChanged)
        {
            Debug.Log(
                $"[WallDebug] Touch Changed | " +
                $"Left={IsTouchingWallLeft}, " +
                $"Right={IsTouchingWallRight}, " +
                $"Grounded={IsGrounded}, " +
                $"Pos={rb.position}",
                this);
        }

        if (slidingChanged)
        {
            Debug.Log(
                $"[WallDebug] Sliding -> {IsWallSliding} | " +
                $"InputX={inputX:F2}, " +
                $"VelocityY={rb.linearVelocity.y:F2}, " +
                $"Left={IsTouchingWallLeft}, " +
                $"Right={IsTouchingWallRight}, " +
                $"Toward={IsPressingTowardWall(inputX)}, " +
                $"Grounded={IsGrounded}",
                this);
        }

        string blockReason =
            BuildWallSlideBlockReason(
                inputX);

        if (verboseWallDebug &&
            blockReason !=
            _debugPreviousBlockReason)
        {
            Debug.Log(
                $"[WallDebug] Slide Check | {blockReason} | " +
                $"InputX={inputX:F2}, " +
                $"Velocity={rb.linearVelocity}, " +
                $"Left={IsTouchingWallLeft}, " +
                $"Right={IsTouchingWallRight}",
                this);

            _debugPreviousBlockReason =
                blockReason;
        }

        _debugPreviousTouchingWallLeft =
            IsTouchingWallLeft;

        _debugPreviousTouchingWallRight =
            IsTouchingWallRight;

        _debugPreviousWallSliding =
            IsWallSliding;
    }


    private string BuildWallSlideBlockReason(
        float inputX)
    {
        if (IsWallSliding)
            return "WallSliding";

        if (IsGrounded)
            return "Blocked: Grounded";

        if (!IsTouchingWall)
            return "Blocked: No Wall Contact";

        if (!IsPressingTowardWall(
                inputX))
        {
            return "Blocked: Not Pressing Toward Wall";
        }

        if (rb.linearVelocity.y >= 0f)
            return "Blocked: Not Falling";

        return "Blocked: Unknown";
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

    private void SetVelocity(
        Vector2 velocity)
    {
        rb.linearVelocity =
            velocity;
    }


    private void LockMovementControl(
        float duration)
    {
        if (duration <= 0f)
            return;

        float remaining =
            MovementControlLockTimer
                .RemainingTime(Runner) ??
            0f;

        if (duration > remaining)
        {
            MovementControlLockTimer =
                TickTimer.CreateFromSeconds(
                    Runner,
                    duration);
        }

        ClearControlDrivenStates();
    }


    private void ApplyKnockback(
        Vector2 velocity)
    {
        if (!HasStateAuthority)
            return;

        rb.linearVelocity =
            velocity;

        ClearControlDrivenStates();
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

        MovementControlLockTimer =
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

    public void SetFacing(
        bool facingRight)
    {
        // 벽타기 중에는 벽 방향 규칙이 우선.
        if (IsWallSliding)
            return;

        FacingRight =
            facingRight;
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
            Gizmos.color =
                Application.isPlaying &&
                IsTouchingWallLeft
                    ? Color.green
                    : Color.red;

            Gizmos.DrawWireCube(
                wallCheckLeft.position,
                wallCheckSize);
        }

        if (wallCheckRight != null)
        {
            Gizmos.color =
                Application.isPlaying &&
                IsTouchingWallRight
                    ? Color.green
                    : Color.red;

            Gizmos.DrawWireCube(
                wallCheckRight.position,
                wallCheckSize);
        }

        Gizmos.color =
            Color.white;
    }

#endif
}
