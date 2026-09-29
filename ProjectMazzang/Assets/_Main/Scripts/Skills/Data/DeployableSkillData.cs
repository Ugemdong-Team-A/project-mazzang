using Fusion;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Mazzang/Data/Skill/Deployable",
    fileName = "DeployableSkillData")]
public sealed class DeployableSkillData :
    SkillData
{
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
