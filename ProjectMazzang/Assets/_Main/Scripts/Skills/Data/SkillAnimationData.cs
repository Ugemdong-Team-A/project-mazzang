using UnityEngine;

[CreateAssetMenu(
    menuName = "Mazzang/Data/Skill/Animation",
    fileName = "SkillAnimation")]
public sealed class SkillAnimationData :
    ScriptableObject
{
    [Tooltip("시전 시간이 진행되는 동안 재생할 클립입니다.")]
    [SerializeField]
    private AnimationClip cast;

    [Tooltip("스킬 효과가 실제로 발동하는 순간 재생할 클립입니다.")]
    [SerializeField]
    private AnimationClip release;

    [Tooltip("지속 효과가 끝난 뒤 후딜레이에 재생할 선택형 클립입니다.")]
    [SerializeField]
    private AnimationClip recovery;

    public bool HasAnyClip =>
        cast != null ||
        release != null ||
        recovery != null;

    public AnimationClip GetClip(
        SkillAnimationPhase phase)
    {
        return phase switch
        {
            SkillAnimationPhase.Cast => cast,
            SkillAnimationPhase.Release => release,
            SkillAnimationPhase.Recovery => recovery,
            _ => null
        };
    }
}
