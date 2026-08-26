using System;

/// <summary>
/// 플레이어 입력으로 새 조작을 시작할 수 없는 영역입니다.
/// 이미 진행 중인 행동의 취소와 강제 Command 적용은 별도 규칙입니다.
/// 요청한 영역의 담당 PlayerTickModule은 해당 플레이어에 반드시 존재해야 합니다.
/// </summary>
[Flags]
public enum PlayerControlLock : byte
{
    None = 0,

    /// <summary>
    /// 이동, 점프 등 PlayerMovement가 처리하는 입력을 잠급니다.
    /// Knockback과 강제 속도 Command는 계속 적용됩니다.
    /// </summary>
    Movement = 1 << 0,

    /// <summary>
    /// PlayerCombat이 처리하는 새 기본 공격 입력을 잠급니다.
    /// 진행 중인 공격 취소는 CancelAttack Command가 담당합니다.
    /// 무기 버리기, 보조 무기, 패링은 포함하지 않습니다.
    /// </summary>
    Attack = 1 << 1,

    /// <summary>
    /// PlayerSkillController를 통한 새 스킬 사용을 잠급니다.
    /// 이미 실행 중인 스킬은 취소하지 않습니다.
    /// </summary>
    Skill = 1 << 2
}
