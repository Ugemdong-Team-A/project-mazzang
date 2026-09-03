using UnityEngine;

public class ActionAnimationData : ScriptableObject
{
    // [Header("")]
    [SerializeField] AnimationClip actionAnimationClip;
    [SerializeField] ActionAnimationPlayback actionAnimationPlayback;
    [SerializeField] ActionBodyMask actionBodyMask;
    [SerializeField] ActionAimComposition actionAimComposition;
    [SerializeField] ActionHandIkPolicy actionHandIkPolicy;
    [SerializeField] ActionAnimationSpeedMode actionAnimationSpeedMode;

}

public enum ActionAnimationPlayback
{
    PhaseClips,
    ContinuousClip
}

public enum ActionBodyMask
{
    FullBody,
    UpperBody,
    ArmsOnly
}


// 이미 존재하는 enum PlayerAttackPoseMode과 똑같지만
// 공통 애니메이션 데이타이며 공격에 따라 액션도 참고할 수 있는 값이란 의미
public enum ActionAimComposition
{
    ProceduralOverride,
    AnimationOnly,
    AnimationWithBodyAim
}

// Inherit: 현재 장착 상태의 기본 정책
// AnimatedTargets: 클립이 키로 저장한 손 IK Target 사용
// WeaponGrips: Grip이 있는 손은 무기 Grip, 나머지는 애니메이션 Target 사용
public enum ActionHandIkPolicy
{
    Inherit,
    AnimatedTargets,
    WeaponGrips
}

// NaturalSpeed: 클립 원래 속도
// MatchGameplayPhase: 게임플레이 단계 시간에 맞춤
// Multiplier: 지정 배율 적용
public enum ActionAnimationSpeedMode
{
    NaturalSpeed,
    MatchGameplayPhase,
    Multiplier
}
