using UnityEngine;

public abstract class SkillData :
    ScriptableObject
{
    [Header("Common")]
    [Min(0f)]
    [SerializeField]
    private float cooldown;

    public float Cooldown =>
        cooldown;

    public abstract Skill CreateSkill();
}