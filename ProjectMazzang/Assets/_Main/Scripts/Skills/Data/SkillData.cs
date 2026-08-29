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

    public float Cooldown =>
        cooldown;

    public Sprite Icon
        => icon;

    public SkillAnimationData Animation =>
        animation;

    public abstract Skill CreateSkill();
}
