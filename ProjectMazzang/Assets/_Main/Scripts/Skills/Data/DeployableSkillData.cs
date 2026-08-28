using Fusion;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Game/Skills/Deployable",
    fileName = "DeployableSkill")]
public sealed class DeployableSkillData :
    SkillData
{
    [Header("Timing")]
    [Min(0f)]
    [SerializeField]
    private float castDuration = 0.35f;

    [Min(0f)]
    [SerializeField]
    private float recoveryDuration = 0.15f;


    [Header("Deployable")]
    [SerializeField]
    private NetworkObject deployablePrefab;


    [Header("Placement")]

    [SerializeField]
    private bool requiresGrounded = true;

    [Tooltip("플레이어가 바라보는 방향으로 떨어진 설치 거리입니다.")]
    [Min(0f)]
    [SerializeField]
    private float spawnForward = 0.45f;

    [Tooltip("플레이어 루트에서 위쪽으로 떨어진 설치 높이입니다.")]
    [SerializeField]
    private float spawnUp;


    public float CastDuration =>
        castDuration;

    public float RecoveryDuration =>
        recoveryDuration;

    public NetworkObject DeployablePrefab =>
        deployablePrefab;

    public bool RequiresGrounded =>
        requiresGrounded;

    public float SpawnForward =>
        spawnForward;

    public float SpawnUp =>
        spawnUp;


    public override Skill CreateSkill()
    {
        return new DeployableSkill();
    }
}
