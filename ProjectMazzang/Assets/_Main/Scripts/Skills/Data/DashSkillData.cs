using UnityEngine;

[CreateAssetMenu(
    menuName = "Mazzang/Data/Skill/Dash",
    fileName = "DashSkillData")]
public class DashSkillData :
    SkillData
{
    [Header("Charges")]
    [Min(1)]
    [SerializeField]
    private int maxCharges = 2;

    [Min(0f)]
    [SerializeField]
    private float rechargeDuration = 2f;


    [Header("Timing")]
    [Min(0f)]
    [SerializeField]
    private float startupDuration = 0.08f;

    [Min(0f)]
    [SerializeField]
    private float recoveryDuration = 0.06f;

    [Header("Dash")]
    [SerializeField]
    private DashData dash;


    public int MaxCharges =>
        maxCharges;

    public float RechargeDuration =>
        rechargeDuration;

    public float StartupDuration =>
        startupDuration;

    public float RecoveryDuration =>
        recoveryDuration;

    public DashData Dash =>
        dash;


    public override Skill CreateSkill()
    {
        return new DashSkill();
    }
}
