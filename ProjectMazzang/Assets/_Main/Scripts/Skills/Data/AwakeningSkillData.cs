using UnityEngine;

[CreateAssetMenu(
    menuName = "Mazzang/Data/Skill/Awakening",
    fileName = "AwakeningSkillData")]
public sealed class AwakeningSkillData : SkillData
{
    public override Skill CreateSkill()
    {
        return new AwakeningSkill();
    }
}
