using UnityEngine;

[CreateAssetMenu(
    menuName = "Game/Skills/Dash",
    fileName = "DashSkill")]
public sealed class DashSkillData :
    SkillData
{
    [Header("Dash")]
    [SerializeField]
    private float dashSpeed = 14f;

    [SerializeField]
    private float controlLockDuration = 0.12f;

    public float DashSpeed =>
        dashSpeed;

    public float ControlLockDuration =>
        controlLockDuration;


    public override Skill CreateSkill()
    {
        return new DashSkill();
    }
}