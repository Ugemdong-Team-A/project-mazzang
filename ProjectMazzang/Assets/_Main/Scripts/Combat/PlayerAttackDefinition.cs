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

    [SerializeField]
    private PlayerAttackAimDefinition aim;

    [SerializeField]
    private PlayerAttackMovementMode movementMode;


    public AttackData Attack =>
        attack;

    public PlayerAttackAimDefinition Aim =>
        aim;

    public PlayerAttackMovementMode MovementMode =>
        movementMode;

    public bool IsValid =>
        attack != null;
}