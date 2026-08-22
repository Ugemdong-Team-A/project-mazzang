using UnityEngine;

public sealed class PlayerVisual :
    PlayerTickModule
{
    [Header("References")]
    [SerializeField]
    private GameObject characterVisualRoot;


    [Header("Hit")]
    [SerializeField]
    private Color hitColor =
        new Color(
            1f,
            0.35f,
            0.35f,
            1f);

    [Min(0f)]
    [SerializeField]
    private float hitColorDuration =
        0.1f;


    [Header("Invulnerability")]
    [Range(0f, 1f)]
    [SerializeField]
    private float invulnerableAlpha =
        0.35f;

    [Min(0.01f)]
    [SerializeField]
    private float invulnerableBlinkInterval =
        0.08f;


    private PlayerSkillController
        _skillController;

    PlayerTickState _boundTickState;

    PlayerTickCommands _boundTickCommands;


    private Vector3 _defaultScale;

    private SpriteRenderer[]
        _spriteRenderers;

    private Color[]
        _defaultColors;


    private int _previousHealth;

    private bool _healthPresentationInitialized;

    private bool _wasInvulnerable;

    private bool _invulnerableDimmed;

    private float _invulnerableBlinkTimer;

    private bool _hitColorActive;

    private float _hitColorTimer;

    private bool _facingInitialized;

    private bool _previousFacingRight;

    private float _previousStatScale = 1f;

    public override PlayerTickStage Stage => PlayerTickStage.Finalize;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        _skillController =
            GetComponent<PlayerSkillController>();

        if (characterVisualRoot == null)
            return;

        _defaultScale =
            characterVisualRoot
                .transform
                .localScale;

        CacheSpriteRenderers();
    }


    private void CacheSpriteRenderers()
    {
        _spriteRenderers =
            characterVisualRoot
                .GetComponentsInChildren<
                    SpriteRenderer>(
                    true);

        _defaultColors =
            new Color[
                _spriteRenderers.Length];

        for (int i = 0;
             i < _spriteRenderers.Length;
             i++)
        {
            SpriteRenderer spriteRenderer =
                _spriteRenderers[i];

            if (spriteRenderer == null)
                continue;

            _defaultColors[i] =
                spriteRenderer.color;
        }
    }

    // =========================================================
    // Fusion
    // =========================================================

    public override void Render()
    {
        if (characterVisualRoot == null)
            return;

        var state = _boundTickState;
        var commands = _boundTickCommands;

        UpdateVisibility(state);
        UpdateFacing(state, commands);
        UpdateHealthPresentation();
    }


    // =========================================================
    // Visibility
    // =========================================================

    private void UpdateVisibility(PlayerTickState state)
    {
        bool visible =
            state.IsAlive;

        if (characterVisualRoot.activeSelf ==
            visible)
        {
            return;
        }

        characterVisualRoot.SetActive(
            true);
    }


    // =========================================================
    // Facing
    // =========================================================

    private void UpdateFacing(PlayerTickState state, PlayerTickCommands commands)
    {
        bool facingRight =
            state.FacingRight;

        // Debug.Log("Visual FacingRight: " + facingRight);

        float statScale =
            _skillController != null
                ? _skillController
                    .GetActiveStatModifiers()
                    .VisualScale
                : 1f;

        if (_facingInitialized &&
            _previousFacingRight ==
            facingRight &&
            Mathf.Approximately(
                _previousStatScale,
                statScale))
        {
            return;
        }

        _facingInitialized =
            true;

        _previousFacingRight =
            facingRight;

        _previousStatScale =
            statScale;

        Vector3 scale =
            _defaultScale;

        scale.x *=
            facingRight
                ? 1f
                : -1f;

        scale.x *= statScale;
        scale.y *= statScale;

        characterVisualRoot
            .transform
            .localScale =
            scale;
    }


    // =========================================================
    // Health Presentation
    // =========================================================

    private void UpdateHealthPresentation()
    {
        /*if (_healthState == null)
            return;

        if (!_healthPresentationInitialized)
        {
            InitializeHealthPresentation();
            return;
        }

        DetectHit();
        UpdateHitColor();
        UpdateInvulnerability();

        _previousHealth =
            _healthState.Health;*/
    }


    private void InitializeHealthPresentation(int health, bool isInvulnerable)
    {
        _healthPresentationInitialized =
            true;

        _previousHealth =
            health;

        _wasInvulnerable =
            isInvulnerable;

        _invulnerableDimmed =
            false;

        _invulnerableBlinkTimer =
            invulnerableBlinkInterval;

        ApplyCurrentColor();
    }


    // =========================================================
    // Hit
    // =========================================================

    private void DetectHit(bool isAlive, int currentHeath)
    {
        if (!isAlive)
            return;

        if (currentHeath >= _previousHealth)
        {
            return;
        }

        _hitColorActive =
            true;

        _hitColorTimer =
            hitColorDuration;

        ApplyCurrentColor();
    }


    private void UpdateHitColor()
    {
        if (!_hitColorActive)
            return;

        _hitColorTimer -=
            Time.deltaTime;

        if (_hitColorTimer > 0f)
            return;

        _hitColorTimer =
            0f;

        _hitColorActive =
            false;

        ApplyCurrentColor();
    }


    // =========================================================
    // Invulnerability
    // =========================================================

    private void UpdateInvulnerability(bool isInvulnerable)
    {
        if (_wasInvulnerable !=
            isInvulnerable)
        {
            _wasInvulnerable =
                isInvulnerable;

            _invulnerableDimmed =
                false;

            _invulnerableBlinkTimer =
                invulnerableBlinkInterval;

            ApplyCurrentColor();
        }

        if (!isInvulnerable)
            return;

        if (_hitColorActive)
            return;

        _invulnerableBlinkTimer -=
            Time.deltaTime;

        if (_invulnerableBlinkTimer > 0f)
            return;

        _invulnerableBlinkTimer +=
            invulnerableBlinkInterval;

        _invulnerableDimmed =
            !_invulnerableDimmed;

        ApplyCurrentColor();
    }


    // =========================================================
    // Sprite Color
    // =========================================================

    private void ApplyCurrentColor()
    {
        if (_spriteRenderers == null)
            return;

        for (int i = 0;
             i < _spriteRenderers.Length;
             i++)
        {
            SpriteRenderer spriteRenderer =
                _spriteRenderers[i];

            if (spriteRenderer == null)
                continue;

            Color color =
                _defaultColors[i];

            if (_hitColorActive)
            {
                color.r *=
                    hitColor.r;

                color.g *=
                    hitColor.g;

                color.b *=
                    hitColor.b;
            }

            if (_wasInvulnerable &&
                _invulnerableDimmed)
            {
                color.a *=
                    invulnerableAlpha;
            }

            spriteRenderer.color =
                color;
        }
    }

    public override void Simulate(in PlayerTick tick)
    {       
        if (_boundTickState == null)
            _boundTickState = tick.State;

        if (_boundTickCommands == null)
            _boundTickCommands = tick.Commands;

        bool isAlive = _boundTickState.IsAlive;
        int  health = _boundTickState.Health;
        bool isInvulnerable = _boundTickState.IsInvulnerable;

        if (!_healthPresentationInitialized)
            InitializeHealthPresentation(health, isInvulnerable);

        DetectHit(isAlive, health);

        UpdateHitColor();

        UpdateInvulnerability(isInvulnerable);
    }
}
