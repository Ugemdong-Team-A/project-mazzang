using Fusion;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Game/Skills/Fireball",
    fileName = "FireballSkill")]
public sealed class FireballSkillData : SkillData
{
    [Header("Timing")]
    [Min(0f)] [SerializeField] private float castDuration = 0.65f;
    [Min(0f)] [SerializeField] private float recoveryDuration = 0.2f;

    [Header("Projectile")]
    [SerializeField] private NetworkObject projectilePrefab;
    [Min(0.01f)] [SerializeField] private float projectileSpeed = 16f;
    [Min(0.01f)] [SerializeField] private float projectileLifetime = 2.5f;
    [Min(0)] [SerializeField] private int damage = 18;
    [SerializeField] private Vector2 knockback = new(6f, 1.5f);
    [Min(0f)] [SerializeField] private float knockbackControlLock = 0.1f;

    [Header("Spawn")]
    [Tooltip("플레이어 루트에서 조준 방향으로 떨어진 생성 거리입니다.")]
    [Min(0f)] [SerializeField] private float spawnForward = 0.65f;
    [Tooltip("플레이어 루트에서 위쪽으로 떨어진 생성 높이입니다.")]
    [SerializeField] private float spawnUp = 0.9f;

    [Header("Presentation")]
    [Tooltip("비워두면 코드 기반 임시 시전 연출을 사용합니다.")]
    [SerializeField] private GameObject castVfxPrefab;

    public float CastDuration => castDuration;
    public float RecoveryDuration => recoveryDuration;
    public NetworkObject ProjectilePrefab => projectilePrefab;
    public float ProjectileSpeed => projectileSpeed;
    public float ProjectileLifetime => projectileLifetime;
    public int Damage => damage;
    public Vector2 Knockback => knockback;
    public float KnockbackControlLock => knockbackControlLock;
    public float SpawnForward => spawnForward;
    public float SpawnUp => spawnUp;
    public GameObject CastVfxPrefab => castVfxPrefab;

    public override Skill CreateSkill()
    {
        return new FireballSkill();
    }
}
