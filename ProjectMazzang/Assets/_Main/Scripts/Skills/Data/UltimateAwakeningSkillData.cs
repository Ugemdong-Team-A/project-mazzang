using UnityEngine;

[CreateAssetMenu(
    menuName = "Mazzang/Data/Skill/Ultimate Awakening",
    fileName = "UltimateAwakeningSkillData")]
public sealed class UltimateAwakeningSkillData :
    SkillData
{
    public override Skill CreateSkill()
    {
        return new UltimateAwakeningSkill();
    }
}
