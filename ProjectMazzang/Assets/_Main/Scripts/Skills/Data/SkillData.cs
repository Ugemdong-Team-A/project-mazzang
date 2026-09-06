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

    [SerializeField]
    private SkillPatternSettings patterns = new();

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
