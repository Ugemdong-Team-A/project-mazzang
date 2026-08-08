using Fusion;
using UnityEngine;

public sealed class PlayerMovement : NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private Rigidbody2D rb;

    [SerializeField]
    private Transform groundCheck;

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
    private float groundCheckRadius = 0.15f;

    [SerializeField]
    private byte maxAirJumps = 1;

    [Networked]
    private byte RemainingAirJumps { get; set; }

    [Networked]
    private NetworkBool WasGrounded { get; set; }

    [Networked]
    private NetworkButtons PreviousButtons { get; set; }

    [Networked]
    public NetworkBool FacingRight { get; private set; }

    public bool IsGrounded { get; private set; }

    public Vector2 Velocity => rb.linearVelocity;

    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
    }

    public override void FixedUpdateNetwork()
    {
        UpdateGrounded();

        if (!GetInput(out PlayerInputData input))
            return;

        MoveHorizontal(input.MoveX);

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

    private void MoveHorizontal(float inputX)
    {
        inputX =
            Mathf.Clamp(inputX, -1f, 1f);

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
                acceleration * Runner.DeltaTime);

        rb.linearVelocity =
            velocity;
    }

    private void UpdateFacing(float inputX)
    {
        if (inputX > 0.01f)
            FacingRight = true;
        else if (inputX < -0.01f)
            FacingRight = false;
    }

    private void TryJump()
    {
        if (IsGrounded)
        {
            ApplyJump();
            return;
        }

        if (RemainingAirJumps == 0)
            return;

        RemainingAirJumps--;

        ApplyJump();
    }

    private void ApplyJump()
    {
        Vector2 velocity =
            rb.linearVelocity;

        velocity.y = jumpSpeed;

        rb.linearVelocity = velocity;
    }

    private void UpdateGrounded()
    {
        bool grounded =
            Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundLayer) != null;

        if (grounded && !WasGrounded)
        {
            RemainingAirJumps = maxAirJumps;
        }

        IsGrounded = grounded;
        WasGrounded = grounded;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius);
    }
#endif
}