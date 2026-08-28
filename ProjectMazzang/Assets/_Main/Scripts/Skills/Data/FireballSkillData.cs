using UnityEngine;

[CreateAssetMenu(
    menuName = "Game/Skills/Fireball",
    fileName = "FireballSkill")]
public sealed class FireballSkillData : ProjectileSkillData
{
    public override Skill CreateSkill()
    {
        return new FireballSkill();
    }
}
