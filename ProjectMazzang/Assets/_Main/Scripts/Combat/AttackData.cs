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


    [Header("Damage")]
    [SerializeField]
    [Min(0)]
    private int damage = 10;


    [Header("Knockback")]
    [SerializeField]
    private float knockbackForward = 6f;

    [SerializeField]
    private float knockbackUp = 4f;


    [Header("Crowd Control")]
    [SerializeField]
    private CrowdControlDefinition crowdControl =
        new(
            CrowdControlType.HitStun,
            0.12f,
            0f,
            false);


    public int AttackId =>
        attackId;

    public string DisplayName =>
        displayName;

    public int Damage =>
        damage;

    public float KnockbackForward =>
        knockbackForward;

    public float KnockbackUp =>
        knockbackUp;

    public CrowdControlDefinition CrowdControl =>
        crowdControl;
}
