using UnityEngine;

[CreateAssetMenu(
    menuName = "Mazzang/Data/Skill/Dash",
    fileName = "DashSkillData")]
public class DashSkillData :
    SkillData
{

    [Header("Dash")]
    [SerializeField]
    private DashData dash;


    public DashData Dash =>
        dash;


    public override Skill CreateSkill()
    {
        return new DashSkill();
    }
}
