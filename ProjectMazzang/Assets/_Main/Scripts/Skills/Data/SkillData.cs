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

    public float Cooldown =>
        cooldown;

    public Sprite Icon
        => icon;

    public abstract Skill CreateSkill();
}