using UnityEngine;

[CreateAssetMenu(
    fileName = "ParryData",
    menuName = "Mazzang/Data/Combat/Parry")]
public sealed class ParryData : ScriptableObject
{
    [Min(0.01f)] [SerializeField] private float activeDuration = 0.18f;
    [Min(0f)] [SerializeField] private float cooldown = 1.1f;
    [Min(0.1f)] [SerializeField] private float radius = 1f;
    [Range(10f, 180f)] [SerializeField] private float arcAngle = 110f;
    [Range(0f, 1f)] [SerializeField] private float aimInfluence = 0.85f;
    [Tooltip(
        "플레이어 몸 기준 오프셋입니다. " +
        "X 양수는 바라보는 쪽의 앞, X 음수는 뒤이며 " +
        "Y 양수는 위, Y 음수는 아래입니다. 조준 회전의 영향은 받지 않습니다.")]
    [SerializeField] private Vector2 anchorOffset = new(0.45f, 0.75f);
    [Header("Reflected Projectile")]
    [Min(0f)] [SerializeField] private float damageMultiplier = 1f;
    [Min(0f)] [SerializeField] private float speedMultiplier = 1.15f;
    [Min(0f)] [SerializeField] private float knockbackMultiplier = 1f;
    [Min(0.01f)] [SerializeField] private float scaleMultiplier = 1f;

    public float ActiveDuration => activeDuration;
    public float Cooldown => cooldown;
    public float Radius => radius;
    public float HalfAngle => arcAngle * 0.5f;
    public float AimInfluence => aimInfluence;
    public ParryProjectileModifiers ProjectileModifiers =>
        new(
            damageMultiplier,
            speedMultiplier,
            knockbackMultiplier,
            scaleMultiplier);
    public Vector2 AnchorOffset => anchorOffset;
}
