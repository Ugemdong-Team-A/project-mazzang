using Fusion;

/// <summary>
/// 한 번의 발사 계획을 만들기 위한 발사자, 포즈, 능력치 스냅샷입니다.
/// </summary>
public readonly struct ProjectileShotContext
{
    public NetworkObject Source
    {
        get;
    }

    public ProjectileLaunchPose LaunchPose
    {
        get;
    }

    public ProjectileStatSnapshot Stats
    {
        get;
    }

    public int FireTick
    {
        get;
    }

    public int FireSequence
    {
        get;
    }

    // 현재 발사 코드가 ProjectileLaunchPose로 전환될 때까지 유지하는 호환 프로퍼티입니다.
    public WeaponFirePose FirePose =>
        new(
            LaunchPose.Origin,
            LaunchPose.Direction);

    public float AttackDamageMultiplier =>
        Stats.DamageMultiplier;


    public ProjectileShotContext(
        NetworkObject source,
        ProjectileLaunchPose launchPose,
        ProjectileStatSnapshot stats,
        int fireTick,
        int fireSequence)
    {
        Source = source;
        LaunchPose = launchPose;
        Stats = stats;
        FireTick = fireTick;
        FireSequence = fireSequence;
    }


    // 기존 총기·스킬 발사 경로를 위한 점진적 전환 생성자입니다.
    public ProjectileShotContext(
        NetworkObject source,
        WeaponFirePose firePose,
        float attackDamageMultiplier)
        : this(
            source,
            new ProjectileLaunchPose(
                firePose.Origin,
                firePose.Direction),
            ProjectileStatSnapshot
                .FromDamageMultiplier(
                    attackDamageMultiplier),
            0,
            0)
    {
    }
}
