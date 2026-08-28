using UnityEngine;

[CreateAssetMenu(
    menuName = "Game/Skills/Dash",
    fileName = "DashSkill")]
public sealed class DashSkillData :
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
    private float dashDuration = 0.12f;

    [Min(0f)]
    [SerializeField]
    private float recoveryDuration = 0.06f;


    [Header("Movement")]
    [Min(0f)]
    [SerializeField]
    private float dashSpeed = 18f;


    [Header("Player Collision")]
    [SerializeField]
    private LayerMask playerHurtboxLayer;

    [SerializeField]
    private AttackData collisionAttack;


    public int MaxCharges =>
        maxCharges;

    public float RechargeDuration =>
        rechargeDuration;

    public float StartupDuration =>
        startupDuration;

    public float DashDuration =>
        dashDuration;

    public float RecoveryDuration =>
        recoveryDuration;

    public float DashSpeed =>
        dashSpeed;

    public LayerMask PlayerHurtboxLayer =>
        playerHurtboxLayer;

    public AttackData CollisionAttack =>
        collisionAttack;


    public override Skill CreateSkill()
    {
        return new DashSkill();
    }
}