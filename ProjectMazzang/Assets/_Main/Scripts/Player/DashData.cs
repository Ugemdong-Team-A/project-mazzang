using UnityEngine;

[CreateAssetMenu(
    menuName = "Mazzang/Data/Movement/Dash",
    fileName = "DashData")]
public sealed class DashData :
    ScriptableObject
{
    [Header("Movement")]
    [Min(0f)]
    [SerializeField]
    private float duration = 0.12f;

    [Min(0f)]
    [SerializeField]
    private float speed = 18f;

    [Header("Player Collision")]
    [SerializeField]
    private LayerMask playerHurtboxLayer;

    [Tooltip(
        "대시 중 플레이어와 충돌했을 때 적용할 공격입니다. " +
        "비워두면 대시 자체는 충돌 피해를 주지 않습니다.")]
    [SerializeField]
    private AttackData collisionAttack;

    public float Duration =>
        duration;

    public float Speed =>
        speed;

    public LayerMask PlayerHurtboxLayer =>
        playerHurtboxLayer;

    public AttackData CollisionAttack =>
        collisionAttack;
}
