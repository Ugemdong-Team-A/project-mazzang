using UnityEngine;

public enum PlayerAttackMovementMode : byte
{
    Free = 0,
    Locked
}


public abstract class PlayerAttackData :
    ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    [Min(1)]
    private int attackId = 1;

    [SerializeField]
    private string displayName;


    [Header("Aim")]
    [SerializeField]
    private PlayerAttackAimDefinition aim;


    [Header("Movement")]
    [SerializeField]
    private PlayerAttackMovementMode movementMode;


    [Header("Timing")]
    [SerializeField]
    [Min(0f)]
    private float startupDuration = 0.08f;

    [SerializeField]
    [Min(0f)]
    private float activeDuration = 0.06f;

    [SerializeField]
    [Min(0f)]
    private float recoveryDuration = 0.2f;

    [SerializeField]
    [Min(0f)]
    private float cooldown = 0.45f;


    public int AttackId =>
        attackId;

    public string DisplayName =>
        displayName;

    public PlayerAttackAimDefinition Aim =>
        aim;

    public PlayerAttackMovementMode MovementMode =>
        movementMode;

    public float StartupDuration =>
        startupDuration;

    public float ActiveDuration =>
        activeDuration;

    public float RecoveryDuration =>
        recoveryDuration;

    public float Cooldown =>
        cooldown;
}