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
}
