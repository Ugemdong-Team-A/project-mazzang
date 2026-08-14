using Fusion;
using UnityEngine;

public struct SkillSlotRuntimeState :
    INetworkStruct
{
    public byte Phase;

    public byte SpentCharges;

    public Vector2 Direction;

    public TickTimer PhaseTimer;

    public TickTimer RechargeTimer;
}