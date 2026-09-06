public enum SkillAnimationPhase : byte
{
    None = 0,
    Cast = 1,
    Release = 2,
    Recovery = 3
}


public enum SkillUsePhase : byte
{
    None = 0,
    Cast,
    Active,
    Recovery
}

/*
/// <summary>
/// 여러 번 저장해서 사용할 수 있는 스킬입니다.
/// 각 Charge는 순차적으로 회복됩니다.
/// </summary>
public interface IChargeSkill
{
    int MaxCharges { get; }

    float RechargeDuration { get; }
}


/// <summary>
/// 사용 후 실제 효과가 시작되기 전
/// 시전 시간이 존재하는 스킬입니다.
/// </summary>
public interface ICastTimeSkill
{
    float CastDuration { get; }
}


/// <summary>
/// 활성 상태가 일정 시간 유지되는 스킬입니다.
/// HUD에서 남은 활성 시간을 표시할 수도 있습니다.
/// </summary>
public interface IDurationSkill
{
    float Duration { get; }
}


/// <summary>
/// 효과 종료 후 다시 일반 행동으로 돌아가기까지
/// 후딜레이가 존재하는 스킬입니다.
/// </summary>
public interface IRecoverySkill
{
    float RecoveryDuration { get; }
}


/// <summary>
/// 사용 단계 동안 기본 공격과 무기 조작을 막는 스킬입니다.
/// 이동과 조준은 별도로 유지됩니다.
/// </summary>
public interface IActionLockSkill
{
    bool IsActionLocked(
        SkillUsePhase phase);
}

public interface IMeterSkill
{
    float MaxMeter { get; }

    float MeterCost { get; }

    float PassiveGainPerSecond { get; }

    float DamageGainPerDamage { get; }
}
*/
