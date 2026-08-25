using UnityEngine;

/// <summary>
/// 자동 생성된 IK 오브젝트만 안전하게 지우기 위한 내부 마커.
/// Inspector에는 숨긴다.
/// </summary>
[AddComponentMenu("")]
public sealed class Standard2DGeneratedIKMarker : MonoBehaviour
{
    public enum GeneratedKind
    {
        Solver,
        Target,
        Effector
    }

    [SerializeField, HideInInspector]
    private Transform _ownerRoot;

    [SerializeField, HideInInspector]
    private GeneratedKind _kind;

    public Transform OwnerRoot =>
        _ownerRoot;

    public GeneratedKind Kind =>
        _kind;

    public void Initialize(
        Transform ownerRoot,
        GeneratedKind kind)
    {
        _ownerRoot = ownerRoot;
        _kind = kind;

        hideFlags =
            HideFlags.HideInInspector;
    }
}
