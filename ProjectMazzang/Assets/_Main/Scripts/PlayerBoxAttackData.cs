using UnityEngine;

[CreateAssetMenu(
    menuName = "Game/Combat/Box Attack Data",
    fileName = "BoxAttackData")]
public sealed class PlayerBoxAttackData :
    PlayerAttackData
{
    [Header("Damage")]
    [SerializeField]
    private int damage = 10;


    [Header("Hitbox")]
    [SerializeField]
    private Vector2 hitboxOffset =
        new Vector2(1f, 0f);

    [SerializeField]
    private Vector2 hitboxSize =
        new Vector2(1.5f, 1f);


    [Header("Knockback")]
    [SerializeField]
    private float knockbackForward = 6f;

    [SerializeField]
    private float knockbackUp = 4f;

    [SerializeField]
    [Min(0f)]
    private float knockbackControlLock = 0.12f;


    public int Damage =>
        damage;

    public Vector2 HitboxOffset =>
        hitboxOffset;

    public Vector2 HitboxSize =>
        hitboxSize;

    public float KnockbackForward =>
        knockbackForward;

    public float KnockbackUp =>
        knockbackUp;

    public float KnockbackControlLock =>
        knockbackControlLock;
}