using UnityEngine;

[CreateAssetMenu(
    menuName = "Mazzang/Data/Skill/Projectile/Fireball",
    fileName = "FireballSkillData")]
public sealed class FireballSkillData : ProjectileSkillData
{
    public override Skill CreateSkill()
    {
        return new FireballSkill();
    }
}
