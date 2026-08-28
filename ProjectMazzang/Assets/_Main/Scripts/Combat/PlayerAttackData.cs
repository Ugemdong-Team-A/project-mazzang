using UnityEngine;

public enum PlayerAttackMovementMode : byte
{
    Free = 0,
    Locked
}

[CreateAssetMenu(
    menuName = "Game/Combat/Player Attack Data",
    fileName = "PlayerAttackData")]
public sealed class PlayerAttackData :
    ScriptableObject
{
    [SerializeField]
    private AttackData attack;

    [Header("Timing")]
    [SerializeField]
    [Min(0f)]
    private float startupDuration;

    [SerializeField]
    [Min(0f)]
    private float activeDuration;

    [SerializeField]
    [Min(0f)]
    private float recoveryDuration;

    [SerializeField]
    [Min(0f)]
    private float cooldown;

    [Header("Player Rules")]
    [SerializeField]
    private PlayerAttackAimData aim;

    [SerializeField]
    private PlayerAttackMovementMode movementMode;


    public AttackData Attack =>
        attack;

    public float StartupDuration =>
        startupDuration;

    public float ActiveDuration =>
        activeDuration;

    public float RecoveryDuration =>
        recoveryDuration;

    public float Cooldown =>
        cooldown;

    public PlayerAttackAimData Aim =>
        aim;

    public PlayerAttackMovementMode MovementMode =>
        movementMode;


    public bool IsValid =>
        attack != null;
}
