using System;
using UnityEngine;

public enum PlayerAttackMovementMode : byte
{
    Free = 0,
    Locked
}

/// <summary>
/// 공용 AttackData에 플레이어 전용 사용 규칙을 덧씌우는 정의입니다.
/// AttackData 자체는 플레이어를 모르며,
/// PlayerCombat이 공격별 Aim / Movement 규칙을 해석합니다.
/// </summary>
[Serializable]
public struct PlayerAttackDefinition
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
    private PlayerAttackAimDefinition aim;

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

    public PlayerAttackAimDefinition Aim =>
        aim;

    public PlayerAttackMovementMode MovementMode =>
        movementMode;

    public bool IsValid =>
        attack != null;
}
