using UnityEngine;

[CreateAssetMenu(
    fileName = "ParryData",
    menuName = "Mazzang/Data/Combat/Parry")]
public sealed class ParryData : ScriptableObject
{
    [Min(0.01f)] [SerializeField] private float activeDuration = 0.18f;
    [Min(0.1f)] [SerializeField] private float radius = 1f;
    [Range(10f, 360f)] [SerializeField] private float arcAngle = 110f;
    [Range(0f, 1f)] [SerializeField] private float aimInfluence = 0.85f;
    [Header("Reflected Projectile")]
    [Min(0f)] [SerializeField] private float damageMultiplier = 1f;
    [Min(0f)] [SerializeField] private float speedMultiplier = 1.15f;
    [Min(0f)] [SerializeField] private float knockbackMultiplier = 1f;
    [Min(0.01f)] [SerializeField] private float scaleMultiplier = 1f;

    public float ActiveDuration => activeDuration;
    public float Radius => radius;
    public float HalfAngle => arcAngle * 0.5f;
    public float AimInfluence => aimInfluence;
    public ParryProjectileModifiers ProjectileModifiers =>
        new(
            damageMultiplier,
            speedMultiplier,
            knockbackMultiplier,
            scaleMultiplier);
}
