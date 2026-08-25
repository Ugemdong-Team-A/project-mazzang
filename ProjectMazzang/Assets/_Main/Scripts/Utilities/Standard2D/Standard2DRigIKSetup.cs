using UnityEngine;
using UnityEngine.U2D.IK;

/// <summary>
/// 아티스트가 캐릭터 Root에 붙여 버튼 한 번으로
/// 표준 2D IK를 생성하기 위한 진입점 컴포넌트.
///
/// 실제 Rig 탐색 / 검증 / IK 생성 로직은 별도 클래스로 분리되어 있다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(IKManager2D))]
public sealed class Standard2DRigIKSetup : MonoBehaviour
{
    public const string ToolVersion = "5.0-Head-PerPartReach";

    [Header("Rig Search")]
    [Tooltip(
        "보통 비워두세요.\n" +
        "이 컴포넌트가 붙은 Player Root 아래에서 실제 Skeleton root를 자동 탐색합니다.\n" +
        "자동 탐색이 실패할 때만 Skeleton root 또는 그것을 포함하는 부모를 지정하세요.")]
    [SerializeField]
    private Transform _rigSearchRoot;

    [Header("Effector Reach")]

    [Tooltip(
        "팔 Effector Reach.\n" +
        "forearm의 Local +X(right) 방향으로 손 위치보다 얼마나 더 뻗을지 결정합니다.\n" +
        "손은 다리보다 조금 더 여유를 주는 편이 작업하기 편해서 기본값을 1.20으로 둡니다.")]
    [Min(1f)]
    [SerializeField]
    private float _armEffectorReachScale = 1.20f;

    [Tooltip(
        "다리/발 Effector Reach.\n" +
        "다리는 작은 증가만으로도 길쭉해 보이기 쉬워 기본값을 1.05로 둡니다.\n" +
        "Leg Solver와 Foot Solver가 이 값을 함께 사용합니다.")]
    [Min(1f)]
    [SerializeField]
    private float _legEffectorReachScale = 1.05f;

    [Tooltip(
        "머리 Effector Reach.\n" +
        "neck -> head 길이를 기준으로 head의 Local +X(right) 방향에 생성합니다.\n" +
        "기본값은 보수적으로 1.00입니다.")]
    [Min(0.1f)]
    [SerializeField]
    private float _headEffectorReachScale = 1.00f;

    [Header("IK Manager")]
    [Range(0f, 1f)]
    [SerializeField]
    private float _managerWeight = 1f;

    [SerializeField]
    private bool _alwaysUpdate = true;

    [Header("Limb Solver")]
    [SerializeField]
    private bool _constrainRotation = true;

    [SerializeField]
    private bool _solveFromDefaultPose = true;

    public Transform RigSearchRoot =>
        _rigSearchRoot != null
            ? _rigSearchRoot
            : transform;

    public Transform PlayerRoot =>
        transform;

    public float ArmEffectorReachScale =>
        Mathf.Max(
            1f,
            _armEffectorReachScale);

    public float LegEffectorReachScale =>
        Mathf.Max(
            1f,
            _legEffectorReachScale);

    public float HeadEffectorReachScale =>
        Mathf.Max(
            0.1f,
            _headEffectorReachScale);

    public float ManagerWeight =>
        Mathf.Clamp01(
            _managerWeight);

    public bool AlwaysUpdate =>
        _alwaysUpdate;

    public bool ConstrainRotation =>
        _constrainRotation;

    public bool SolveFromDefaultPose =>
        _solveFromDefaultPose;

    public float GetEffectorReachScale(
        Standard2DRigDefinition.EffectorReachGroup group)
    {
        return group switch
        {
            Standard2DRigDefinition.EffectorReachGroup.Arm =>
                ArmEffectorReachScale,

            Standard2DRigDefinition.EffectorReachGroup.Leg =>
                LegEffectorReachScale,

            Standard2DRigDefinition.EffectorReachGroup.Head =>
                HeadEffectorReachScale,

            _ =>
                1f
        };
    }
}
