using System.Collections.Generic;

/// <summary>
/// 프로젝트에서 사용하는 표준 2D Skeleton / IK 규격.
/// 아티스트용 컴포넌트에는 규격 데이터가 노출되지 않도록 별도 클래스로 분리한다.
/// </summary>
public static class Standard2DRigDefinition
{
    public enum EffectorReachGroup
    {
        Arm,
        Leg,
        Head
    }

    public readonly struct BoneLink
    {
        public readonly string Child;
        public readonly string Parent;

        public BoneLink(
            string child,
            string parent)
        {
            Child = child;
            Parent = parent;
        }
    }

    public readonly struct LimbSpec
    {
        public readonly string Prefix;

        /// <summary>
        /// IK Chain의 가장 위 본.
        /// arm_l / thigh_l / foot_01_l / neck 등.
        /// </summary>
        public readonly string ChainRoot;

        /// <summary>
        /// Effector가 실제로 자식으로 생성되는 본.
        /// 팔 = forearm
        /// 다리 = calf
        /// 발 = foot_02
        /// 머리 = head
        /// </summary>
        public readonly string EffectorParent;

        /// <summary>
        /// EffectorParent의 길이를 계산할 때 참고할 다음 본.
        /// 팔 = hand
        /// 다리 = foot_01
        ///
        /// terminal 본인 foot_02 / head에는 다음 본이 없으므로 null.
        /// </summary>
        public readonly string TipReference;

        /// <summary>
        /// TipReference가 없을 때 길이 추정에 사용할 이전 본.
        /// 발 = foot_01
        /// 머리 = neck
        /// </summary>
        public readonly string PreviousBone;

        public readonly bool Flip;

        public readonly EffectorReachGroup ReachGroup;

        public LimbSpec(
            string prefix,
            string chainRoot,
            string effectorParent,
            string tipReference,
            string previousBone,
            bool flip,
            EffectorReachGroup reachGroup)
        {
            Prefix = prefix;
            ChainRoot = chainRoot;
            EffectorParent = effectorParent;
            TipReference = tipReference;
            PreviousBone = previousBone;
            Flip = flip;
            ReachGroup = reachGroup;
        }

        public string SolverName =>
            Prefix + "_solver";

        public string TargetName =>
            Prefix + "_solver_Target";

        public string EffectorName =>
            Prefix + "_effector";
    }

    public static IReadOnlyList<BoneLink> RequiredHierarchy { get; } =
        new BoneLink[]
        {
            new("pelvis", "root"),

            new("abdomen", "pelvis"),
            new("chest", "abdomen"),
            new("neck", "chest"),
            new("head", "neck"),

            new("hip", "pelvis"),

            new("shoulder_l", "chest"),
            new("arm_l", "shoulder_l"),
            new("forearm_l", "arm_l"),
            new("hand_l", "forearm_l"),
            new("weapon_slot_l", "hand_l"),

            new("shoulder_r", "chest"),
            new("arm_r", "shoulder_r"),
            new("forearm_r", "arm_r"),
            new("hand_r", "forearm_r"),
            new("weapon_slot_r", "hand_r"),

            new("thigh_l", "hip"),
            new("calf_l", "thigh_l"),
            new("foot_01_l", "calf_l"),
            new("foot_02_l", "foot_01_l"),

            new("thigh_r", "hip"),
            new("calf_r", "thigh_r"),
            new("foot_01_r", "calf_r"),
            new("foot_02_r", "foot_01_r")
        };

    /// <summary>
    /// Arm 2 + Leg 2 + Foot 2 + Head 1 = 7 Limb Solver 구성.
    ///
    /// Arm:
    /// arm -> forearm -> effector
    ///
    /// Leg:
    /// thigh -> calf -> effector
    ///
    /// Foot:
    /// foot_01 -> foot_02 -> effector
    ///
    /// Head:
    /// neck -> head -> effector
    ///
    /// 모두 transformCount = 3으로 구성한다.
    /// </summary>
    public static IReadOnlyList<LimbSpec> LimbSpecs { get; } =
        new LimbSpec[]
        {
            new(
                "arm_l",
                "arm_l",
                "forearm_l",
                "hand_l",
                null,
                true,
                EffectorReachGroup.Arm),

            new(
                "arm_r",
                "arm_r",
                "forearm_r",
                "hand_r",
                null,
                true,
                EffectorReachGroup.Arm),

            new(
                "leg_l",
                "thigh_l",
                "calf_l",
                "foot_01_l",
                null,
                false,
                EffectorReachGroup.Leg),

            new(
                "leg_r",
                "thigh_r",
                "calf_r",
                "foot_01_r",
                null,
                false,
                EffectorReachGroup.Leg),

            new(
                "foot_l",
                "foot_01_l",
                "foot_02_l",
                null,
                "foot_01_l",
                true,
                EffectorReachGroup.Leg),

            new(
                "foot_r",
                "foot_01_r",
                "foot_02_r",
                null,
                "foot_01_r",
                true,
                EffectorReachGroup.Leg),

            new(
                "head",
                "neck",
                "head",
                null,
                "neck",
                false,
                EffectorReachGroup.Head)
        };
}
