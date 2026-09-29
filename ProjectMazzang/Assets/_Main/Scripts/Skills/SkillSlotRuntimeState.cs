using Fusion;
using UnityEngine;

public struct SkillSlotRuntimeState :
    INetworkStruct
{
    public SkillUsePhase Phase;

    public byte Charges;

    public float Meter;

    public Vector2 AimDirection;

    public TickTimer CooldownTimer;

    public TickTimer PhaseTimer;

    public TickTimer RechargeTimer;

    // 개별 Cast/Active/Recovery와 독립적인 재사용 허용 구간.
    public TickTimer ChargeWindowTimer;
}
