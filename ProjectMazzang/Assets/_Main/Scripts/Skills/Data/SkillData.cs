using UnityEngine;

public abstract class SkillData :
    ScriptableObject
{
    [Header("Common")]
    [Min(0f)]
    [SerializeField]
    private float cooldown;

    [SerializeField] 
    Sprite icon;

    [Header("Presentation")]
    [SerializeField]
    private SkillAnimationData animation;

    [Space]
    [SerializeField]
    private SkillPatternSettings patterns = new();

    /*[Tooltip("켜면 기존 스킬별 패턴 필드 대신 공통 Patterns를 사용합니다. 기존 값을 먼저 옮기세요.")]
    [SerializeField] private bool useCommonPatterns;
    public bool UseCommonPatterns => useCommonPatterns;*/

    public SkillPatternSettings Patterns => patterns;

    public bool ValidatePatterns(out string error)
    {
        if (patterns == null)
        {
            error = "패턴 설정이 없습니다.";
            return false;
        }

        return patterns.Validate(out error);
    }

    public float Cooldown =>
        cooldown;

    public Sprite Icon
        => icon;

    public SkillAnimationData Animation =>
        animation;

    public abstract Skill CreateSkill();
}
