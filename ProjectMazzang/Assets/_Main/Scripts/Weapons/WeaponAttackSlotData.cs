using System;
using UnityEngine;

[Serializable]
public sealed class WeaponAttackSlotData :
    WeaponActionData
{
    [InspectorName("공격 데이터")]
    [SerializeField]
    private AttackData attack;

    [InspectorName("판정 크기")]
    [SerializeField]
    private Vector2 hitboxSize =
        new(1.8f, 0.8f);

    [InspectorName("판정 앞 거리")]
    [Min(0f)]
    [SerializeField]
    private float hitboxForwardOffset = 0.9f;

    [InspectorName("돌진 데이터")]
    [SerializeField]
    private DashData dash;

    [InspectorName("재사용 대기시간")]
    [Min(0f)]
    [SerializeField]
    private float cooldown = 0.5f;

    [InspectorName("판정 지연시간")]
    [Min(0f)]
    [SerializeField]
    private float hitDelay = 0.5f;

    public AttackData Attack => attack;

    public Vector2 HitboxSize => hitboxSize;

    public float HitboxForwardOffset =>
        hitboxForwardOffset;

    public DashData Dash => dash;

    public float Cooldown => cooldown;

    public float HitDelay => hitDelay;

    public float Duration =>
        hitDelay +
        (dash != null
            ? dash.Duration
            : 0f);

    public WeaponAttackSlotData()
    {
    }

    public WeaponAttackSlotData(
        bool enabled) :
        base(enabled)
    {
    }
}
