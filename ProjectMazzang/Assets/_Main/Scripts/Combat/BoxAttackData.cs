using UnityEngine;

[CreateAssetMenu(
    menuName = "Mazzang/Data/Combat/Attack/Box",
    fileName = "BoxAttackData")]
public sealed class BoxAttackData :
    AttackData
{
    [Header("Hitbox")]
    [SerializeField]
    private Vector2 hitboxOffset =
        new Vector2(
            1f,
            0f);

    [SerializeField]
    private Vector2 hitboxSize =
        new Vector2(
            1.5f,
            1f);


    public Vector2 HitboxOffset =>
        hitboxOffset;

    public Vector2 HitboxSize =>
        hitboxSize;
}
