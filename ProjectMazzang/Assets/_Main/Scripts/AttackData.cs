using UnityEngine;

[CreateAssetMenu(
    menuName = "Game/Combat/Attack Data",
    fileName = "AttackData")]
public class AttackData :
    ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    [Min(1)]
    private int attackId = 1;

    [SerializeField]
    private string displayName;


    [Header("Timing")]
    [SerializeField]
    [Min(0f)]
    private float startupDuration = 0.08f;

    [SerializeField]
    [Min(0f)]
    private float activeDuration = 0.06f;

    [SerializeField]
    [Min(0f)]
    private float recoveryDuration = 0.2f;

    [SerializeField]
    [Min(0f)]
    private float cooldown = 0.45f;


    [Header("Damage")]
    [SerializeField]
    [Min(0)]
    private int damage = 10;


    [Header("Knockback")]
    [SerializeField]
    private float knockbackForward = 6f;

    [SerializeField]
    private float knockbackUp = 4f;

    [SerializeField]
    [Min(0f)]
    private float knockbackControlLock = 0.12f;


    public int AttackId =>
        attackId;

    public string DisplayName =>
        displayName;

    public float StartupDuration =>
        startupDuration;

    public float ActiveDuration =>
        activeDuration;

    public float RecoveryDuration =>
        recoveryDuration;

    public float Cooldown =>
        cooldown;

    public int Damage =>
        damage;

    public float KnockbackForward =>
        knockbackForward;

    public float KnockbackUp =>
        knockbackUp;

    public float KnockbackControlLock =>
        knockbackControlLock;
}