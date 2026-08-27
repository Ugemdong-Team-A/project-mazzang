using Fusion;
using System;
using UnityEngine;

public enum CrowdControlType : byte
{
    None = 0,
    HitStun = 1,
    Root = 2,
    Stun = 3,
    Silence = 4,
    Disarm = 5
}


[Serializable]
public struct CrowdControlDefinition
{
    [SerializeField]
    private CrowdControlType type;

    [Min(0f)]
    [SerializeField]
    private float duration;

    [Tooltip("0이면 공격 적중과 동시에 발동합니다.")]
    [Min(0f)]
    [SerializeField]
    private float activationDelay;

    [Tooltip("CC 발동 시점과 별개로, 공격 적중 즉시 현재 이동 속도를 0으로 만듭니다.")]
    [SerializeField]
    private bool stopMovementOnApply;


    public CrowdControlType Type =>
        type;

    public float Duration =>
        duration;

    public float ActivationDelay =>
        activationDelay;

    public bool IsImmediate =>
        activationDelay <= 0f;

    public bool StopMovementOnApply =>
        stopMovementOnApply;


    public CrowdControlDefinition(
        CrowdControlType type,
        float duration,
        float activationDelay,
        bool stopMovementOnApply)
    {
        this.type =
            type;

        this.duration =
            Mathf.Max(
                0f,
                duration);

        this.activationDelay =
            Mathf.Max(
                0f,
                activationDelay);

        this.stopMovementOnApply =
            stopMovementOnApply;
    }
}


public static class CrowdControlRules
{
    public static PlayerControlLock ResolveLocks(
        CrowdControlType type)
    {
        // 이후 외부 데이터 표를 연결하더라도 이 진입점을 유지하고,
        // 값을 찾지 못했을 때 아래 기본 규칙으로 돌아오면 됩니다.
        return ResolveDefaultLocks(
            type);
    }


    public static PlayerControlLock ResolveDefaultLocks(
        CrowdControlType type)
    {
        return type switch
        {
            CrowdControlType.HitStun =>
                PlayerControlLock.Movement |
                PlayerControlLock.Attack,

            CrowdControlType.Root =>
                PlayerControlLock.Movement,

            CrowdControlType.Stun =>
                PlayerControlLock.Movement |
                PlayerControlLock.Attack |
                PlayerControlLock.Skill,

            CrowdControlType.Silence =>
                PlayerControlLock.Skill,

            CrowdControlType.Disarm =>
                PlayerControlLock.Attack,

            _ =>
                PlayerControlLock.None
        };
    }
}


public struct PendingCrowdControlState :
    INetworkStruct
{
    public NetworkBool IsActive;
    public CrowdControlType Type;
    public float Duration;
    public TickTimer ActivationTimer;
}
